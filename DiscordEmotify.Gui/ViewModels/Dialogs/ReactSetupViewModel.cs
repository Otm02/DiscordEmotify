using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordChatExporter.Core.Discord.Data;
using DiscordEmotify.Gui.Framework;
using DiscordEmotify.Gui.Models;
using DiscordEmotify.Gui.Services;

namespace DiscordEmotify.Gui.ViewModels.Dialogs;

public partial class ReactSetupViewModel : DialogViewModelBase
{
    [ObservableProperty]
    public partial Guild? Guild { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Channel>? Channels { get; set; }

    [ObservableProperty]
    public partial string EmojiInput { get; set; } = "";

    [ObservableProperty]
    public partial int DelayMs { get; set; }

    [ObservableProperty]
    public partial ReactionOrder Order { get; set; } = ReactionOrder.Asc;

    [ObservableProperty]
    public partial bool Clear { get; set; }

    public bool IsSingleChannel => Channels?.Count == 1;

    public ReactSetupViewModel(SettingsService settings)
    {
        DelayMs = settings.ReactionDelayMs;
        Order = settings.ReactionOrder;
        Clear = false;
    }

    [RelayCommand]
    private void Confirm() => Close(true);
}
