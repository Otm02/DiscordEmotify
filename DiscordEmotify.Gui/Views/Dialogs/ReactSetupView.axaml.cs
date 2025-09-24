using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DiscordEmotify.Gui.Views.Dialogs;

public partial class ReactSetupView : UserControl
{
    public ReactSetupView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
