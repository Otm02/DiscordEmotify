using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exceptions;
using DiscordChatExporter.Core.Exporting.Filtering;
using DiscordChatExporter.Core.Utils.Extensions;
using DiscordEmotify.Cli.Commands.Base;
using DiscordEmotify.Cli.Commands.Converters;
using DiscordEmotify.Cli.Utils.Extensions;
using Spectre.Console;
using EmojiData = DiscordChatExporter.Core.Discord.Data.Emoji;

namespace DiscordEmotify.Cli.Commands;

[Command("react", Description = "Adds a reaction to every message in the given channel(s).")]
public class ReactCommand : DiscordCommandBase
{
    [CommandOption(
        "channel",
        'c',
        Description = "Channel ID(s). If a category is specified, all channels inside will be processed."
    )]
    public required IReadOnlyList<Snowflake> ChannelIds { get; init; }

    [CommandOption(
        "emoji",
        'e',
        Description = "Emoji to react with. Supports standard (e.g. 🙂 or :smile:) and custom (name:id).",
        Converter = typeof(EmojiBindingConverter)
    )]
    public required EmojiData Emoji { get; init; }

    [CommandOption(
        "after",
        Description = "Only include messages sent after this date or message ID."
    )]
    public Snowflake? After { get; init; }

    [CommandOption(
        "before",
        Description = "Only include messages sent before this date or message ID."
    )]
    public Snowflake? Before { get; init; }

    [CommandOption("filter", Description = "Only react to messages that satisfy this filter.")]
    public MessageFilter MessageFilter { get; init; } = MessageFilter.Null;

    [CommandOption(
        "parallel",
        Description = "Limits how many channels can be processed in parallel."
    )]
    public int ParallelLimit { get; init; } = 1;

    [CommandOption(
        "delay",
        Description = "Delay in milliseconds between reactions to reduce rate limits (default: 200)."
    )]
    public int DelayMs { get; init; } = 200;

    [CommandOption(
        "order",
        Description = "Order to process messages: 'asc' (oldest first) or 'desc' (newest first)."
    )]
    public string Order { get; init; } = "asc";

    [CommandOption(
        "clear",
        Description = "If set, removes the specified emoji reaction instead of adding it."
    )]
    public bool Clear { get; init; }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);

        var cancellationToken = console.RegisterCancellationHandler();

        await console.Output.WriteLineAsync("Resolving channel(s)...");

        var channels = new List<Channel>();
        var channelsByGuild = new Dictionary<Snowflake, IReadOnlyList<Channel>>();

        foreach (var channelId in ChannelIds)
        {
            var channel = await Discord.GetChannelAsync(channelId, cancellationToken);

            if (channel.IsCategory)
            {
                var guildChannels =
                    channelsByGuild.GetValueOrDefault(channel.GuildId)
                    ?? await Discord.GetGuildChannelsAsync(channel.GuildId, cancellationToken);

                foreach (var guildChannel in guildChannels)
                {
                    if (guildChannel.Parent?.Id == channel.Id)
                        channels.Add(guildChannel);
                }

                channelsByGuild[channel.GuildId] = guildChannels;
            }
            else
            {
                channels.Add(channel);
            }
        }

        await console.Output.WriteLineAsync($"Processing {channels.Count} channel(s)...");

        await console
            .CreateProgressTicker()
            .HideCompleted(ParallelLimit > 1)
            .StartAsync(async ctx =>
            {
                await Parallel.ForEachAsync(
                    channels,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, ParallelLimit),
                        CancellationToken = cancellationToken,
                    },
                    async (channel, innerCancellationToken) =>
                    {
                        await ctx.StartTaskAsync(
                            Markup.Escape(channel.GetHierarchicalName()),
                            async progress =>
                            {
                                var messageCount = 0;

                                // Configure sort order
                                var descending = string.Equals(
                                    Order,
                                    "desc",
                                    StringComparison.OrdinalIgnoreCase
                                );

                                await foreach (
                                    var message in Discord.GetMessagesAsync(
                                        channel.Id,
                                        After,
                                        Before,
                                        null,
                                        innerCancellationToken,
                                        descending
                                    )
                                )
                                {
                                    // Apply filter if provided
                                    if (!MessageFilter.IsMatch(message))
                                        continue;

                                    try
                                    {
                                        if (Clear)
                                        {
                                            await Discord.RemoveReactionAsync(
                                                channel.Id,
                                                message.Id,
                                                Emoji,
                                                innerCancellationToken
                                            );
                                        }
                                        else
                                        {
                                            await Discord.AddReactionAsync(
                                                channel.Id,
                                                message.Id,
                                                Emoji,
                                                innerCancellationToken
                                            );
                                        }
                                        if (DelayMs > 0)
                                            await Task.Delay(DelayMs, innerCancellationToken);
                                    }
                                    catch (DiscordChatExporterException ex) when (!ex.IsFatal)
                                    {
                                        // Non-fatal; if removing and not found, ignore quietly
                                        if (
                                            !(
                                                Clear
                                                && ex.Message.Contains(
                                                    "not found",
                                                    StringComparison.OrdinalIgnoreCase
                                                )
                                            )
                                        )
                                        {
                                            await console.Error.WriteLineAsync(
                                                $"Failed to {(Clear ? "remove" : "add")} reaction in '{channel.GetHierarchicalName()}' on message {message.Id}: {ex.Message}"
                                            );
                                        }
                                    }

                                    messageCount++;
                                }

                                progress.Description =
                                    $"{Markup.Escape(channel.GetHierarchicalName())} — {(Clear ? "cleared" : "reacted to")} {messageCount} message(s)";
                            }
                        );
                    }
                );
            });
    }
}
