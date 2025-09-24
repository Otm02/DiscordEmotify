using System;
using System.Threading.Tasks;
using CliFx.Attributes;
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

[Command(
    "reactdm",
    Description = "Adds a reaction to every message in all direct message channels."
)]
public class ReactDirectMessagesCommand : DiscordCommandBase
{
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

        await console.Output.WriteLineAsync("Fetching DM channels...");
        var channels = await Discord.GetGuildChannelsAsync(
            Guild.DirectMessages.Id,
            cancellationToken
        );

        await console.Output.WriteLineAsync($"Processing {channels.Count} DM channel(s)...");

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
                                        // Small pacing to avoid hammering the rate limit bucket
                                        if (DelayMs > 0)
                                            await Task.Delay(DelayMs, innerCancellationToken);
                                    }
                                    catch (DiscordChatExporterException ex) when (!ex.IsFatal)
                                    {
                                        // Ignore not found when clearing; otherwise swallow to continue processing
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
                                            // Optionally write to error stream; keeping silent to match DM command behavior
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
