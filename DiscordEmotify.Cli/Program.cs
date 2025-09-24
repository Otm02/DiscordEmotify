using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CliFx;
using DiscordEmotify.Cli.Commands;
using DiscordEmotify.Cli.Commands.Converters;

namespace DiscordEmotify.Cli;

public static class Program
{
    // Explicit references because CliFx relies on reflection and we're publishing with trimming enabled
    // Export commands removed for DiscordEmotify
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GetChannelsCommand))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GetDirectChannelsCommand))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GetGuildsCommand))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GuideCommand))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReactCommand))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReactDirectMessagesCommand))]
    // Removed DynamicDependency attributes for export-related types
    public static async Task<int> Main(string[] args) =>
        await new CliApplicationBuilder()
            // Export commands removed for DiscordEmotify
            .AddCommand<GetChannelsCommand>()
            .AddCommand<GetDirectChannelsCommand>()
            .AddCommand<GetGuildsCommand>()
            .AddCommand<GuideCommand>()
            .AddCommand<ReactCommand>()
            .AddCommand<ReactDirectMessagesCommand>()
            .Build()
            .RunAsync(args);
}
