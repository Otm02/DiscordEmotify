using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DiscordEmotify.Gui.ViewModels;
using DiscordEmotify.Gui.ViewModels.Components;
using DiscordEmotify.Gui.ViewModels.Dialogs;
using DiscordEmotify.Gui.Views;
using DiscordEmotify.Gui.Views.Components;
using DiscordEmotify.Gui.Views.Dialogs;

namespace DiscordEmotify.Gui.Framework;

public partial class ViewManager
{
    private Control? TryCreateView(ViewModelBase viewModel) =>
        viewModel switch
        {
            MainViewModel => new MainView(),
            DashboardViewModel => new DashboardView(),
            // Export view removed per DiscordEmotify requirements
            MessageBoxViewModel => new MessageBoxView(),
            SettingsViewModel => new SettingsView(),
            ReactSetupViewModel => new ReactSetupView(),
            _ => null,
        };

    public Control? TryBindView(ViewModelBase viewModel)
    {
        var view = TryCreateView(viewModel);
        if (view is null)
            return null;

        view.DataContext ??= viewModel;

        return view;
    }
}

public partial class ViewManager : IDataTemplate
{
    bool IDataTemplate.Match(object? data) => data is ViewModelBase;

    Control? ITemplate<object?, Control?>.Build(object? data) =>
        data is ViewModelBase viewModel ? TryBindView(viewModel) : null;
}
