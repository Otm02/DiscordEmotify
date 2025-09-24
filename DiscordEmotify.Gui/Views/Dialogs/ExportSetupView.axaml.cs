using Avalonia.Interactivity;
using DiscordEmotify.Gui.Framework;
using DiscordEmotify.Gui.ViewModels.Dialogs;

namespace DiscordEmotify.Gui.Views.Dialogs;

public partial class ExportSetupView : UserControl<ExportSetupViewModel>
{
    public ExportSetupView() => InitializeComponent();

    private void UserControl_OnLoaded(object? sender, RoutedEventArgs args) =>
        DataContext.InitializeCommand.Execute(null);
}
