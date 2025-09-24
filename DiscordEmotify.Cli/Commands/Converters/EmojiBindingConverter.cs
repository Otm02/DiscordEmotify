using System;
using CliFx.Exceptions;
using CliFx.Extensibility;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils;

namespace DiscordEmotify.Cli.Commands.Converters;

// Converts input like ":smile:" or "🙂" or "name:123456789012345678" to Emoji
using EmojiData = DiscordChatExporter.Core.Discord.Data.Emoji;

internal class EmojiBindingConverter : BindingConverter<EmojiData>
{
    public override EmojiData Convert(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new CommandException("Emoji value cannot be empty.");

        var value = raw.Trim();

        // Custom emoji in form name:id
        if (value.Contains(':'))
        {
            var parts = value.Split(':', 2);
            if (parts.Length == 2)
            {
                var id = Snowflake.TryParse(parts[1]);
                if (id is { } parsed)
                    return new EmojiData(parsed, parts[0], false);
            }
        }

        // If it's enclosed in colons like :smile:, strip them and map to Unicode
        if (value.Length >= 2 && value[0] == ':' && value[^1] == ':')
        {
            var code = value.Substring(1, value.Length - 2);
            return EmojiData.FromCode(code);
        }

        // Otherwise assume the user provided the actual Unicode emoji character
        return new EmojiData(null, value, false);
    }
}
