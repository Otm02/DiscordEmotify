using System;
using System.Collections.Generic;
using DiscordChatExporter.Core.Discord.Data;
using DiscordEmotify.Gui.ViewModels;
using DiscordEmotify.Gui.ViewModels.Components;
using DiscordEmotify.Gui.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordEmotify.Gui.Framework;

public class ViewModelManager(IServiceProvider services)
{
    public MainViewModel CreateMainViewModel() => services.GetRequiredService<MainViewModel>();

    public DashboardViewModel CreateDashboardViewModel() =>
        services.GetRequiredService<DashboardViewModel>();

    // Export setup removed per DiscordEmotify requirements

    public ReactSetupViewModel CreateReactSetupViewModel(
        Guild guild,
        IReadOnlyList<Channel> channels
    )
    {
        var viewModel = services.GetRequiredService<ReactSetupViewModel>();
        viewModel.Guild = guild;
        viewModel.Channels = channels;
        return viewModel;
    }

    public MessageBoxViewModel CreateMessageBoxViewModel(
        string title,
        string message,
        string? okButtonText,
        string? cancelButtonText
    )
    {
        var viewModel = services.GetRequiredService<MessageBoxViewModel>();

        viewModel.Title = title;
        viewModel.Message = message;
        viewModel.DefaultButtonText = okButtonText;
        viewModel.CancelButtonText = cancelButtonText;

        return viewModel;
    }

    public MessageBoxViewModel CreateMessageBoxViewModel(string title, string message) =>
        CreateMessageBoxViewModel(title, message, "CLOSE", null);

    public SettingsViewModel CreateSettingsViewModel() =>
        services.GetRequiredService<SettingsViewModel>();
}
