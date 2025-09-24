using System.Text;

namespace DiscordEmotify.Cli.Tests.Utils.Extensions;

internal static class StringExtensions
{
    public static string ReplaceWhiteSpace(this string str, string replacement = " ")
    {
        var buffer = new StringBuilder(str.Length);

        foreach (var ch in str)
            buffer.Append(char.IsWhiteSpace(ch) ? replacement : ch);

        return buffer.ToString();
    }
}
