using Avalonia.Interactivity;
using DiscordEmotify.Gui.Framework;
using DiscordEmotify.Gui.ViewModels;

namespace DiscordEmotify.Gui.Views;

public partial class MainView : Window<MainViewModel>
{
    public MainView() => InitializeComponent();

    private void DialogHost_OnLoaded(object? sender, RoutedEventArgs args) =>
        DataContext.InitializeCommand.Execute(null);
}
