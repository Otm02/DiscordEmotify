using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exceptions;
using DiscordChatExporter.Core.Utils.Extensions;
using DiscordEmotify.Gui.Framework;
using DiscordEmotify.Gui.Models;
using DiscordEmotify.Gui.Services;
using DiscordEmotify.Gui.Utils;
using DiscordEmotify.Gui.Utils.Extensions;
using Gress;
using Gress.Completable;

namespace DiscordEmotify.Gui.ViewModels.Components;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ViewModelManager _viewModelManager;
    private readonly SnackbarManager _snackbarManager;
    private readonly DialogManager _dialogManager;
    private readonly SettingsService _settingsService;

    private readonly DisposableCollector _eventRoot = new();
    private readonly AutoResetProgressMuxer _progressMuxer;

    private DiscordClient? _discord;
    private CancellationTokenSource? _reactCts;
    private Task? _reactTask;
    private int _totalReactions;

    public DashboardViewModel(
        ViewModelManager viewModelManager,
        DialogManager dialogManager,
        SnackbarManager snackbarManager,
        SettingsService settingsService
    )
    {
        _viewModelManager = viewModelManager;
        _dialogManager = dialogManager;
        _snackbarManager = snackbarManager;
        _settingsService = settingsService;

        _progressMuxer = Progress.CreateMuxer().WithAutoReset();

        _eventRoot.Add(
            Progress.WatchProperty(
                o => o.Current,
                () => OnPropertyChanged(nameof(IsProgressIndeterminate))
            )
        );

        _eventRoot.Add(
            SelectedChannels.WatchProperty(
                o => o.Count,
                () => ReactCommand.NotifyCanExecuteChanged()
            )
        );
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressIndeterminate))]
    [NotifyCanExecuteChangedFor(nameof(PullGuildsCommand))]
    [NotifyCanExecuteChangedFor(nameof(PullChannelsCommand))]
    public partial bool IsBusy { get; set; }

    public ProgressContainer<Percentage> Progress { get; } = new();

    public bool IsProgressIndeterminate => IsBusy && Progress.Current.Fraction is <= 0 or >= 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PullGuildsCommand))]
    public partial string? Token { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Guild>? AvailableGuilds { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PullChannelsCommand))]
    public partial Guild? SelectedGuild { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChannelConnection>? AvailableChannels { get; set; }

    public ObservableCollection<ChannelConnection> SelectedChannels { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReactCommand))]
    public partial bool IsReacting { get; set; }

    [ObservableProperty]
    public partial string? ReactStatus { get; set; }

    [RelayCommand]
    private void Initialize()
    {
        if (!string.IsNullOrWhiteSpace(_settingsService.LastToken))
            Token = _settingsService.LastToken;
    }

    [RelayCommand]
    private async Task ShowSettingsAsync() =>
        await _dialogManager.ShowDialogAsync(_viewModelManager.CreateSettingsViewModel());

    [RelayCommand]
    private void ShowHelp() => ProcessEx.StartShellExecute(Program.ProjectDocumentationUrl);

    private bool CanPullGuilds() => !IsBusy && !string.IsNullOrWhiteSpace(Token);

    [RelayCommand(CanExecute = nameof(CanPullGuilds))]
    private async Task PullGuildsAsync()
    {
        IsBusy = true;
        var progress = _progressMuxer.CreateInput();

        try
        {
            var token = Token?.Trim('"', ' ');
            if (string.IsNullOrWhiteSpace(token))
                return;

            AvailableGuilds = null;
            SelectedGuild = null;
            AvailableChannels = null;
            SelectedChannels.Clear();

            _discord = new DiscordClient(token, _settingsService.RateLimitPreference);
            _settingsService.LastToken = token;

            var guilds = await _discord.GetUserGuildsAsync();

            AvailableGuilds = guilds;
            SelectedGuild = guilds.FirstOrDefault();

            await PullChannelsAsync();
        }
        catch (DiscordChatExporterException ex) when (!ex.IsFatal)
        {
            _snackbarManager.Notify(ex.Message.TrimEnd('.'));
        }
        catch (Exception ex)
        {
            var dialog = _viewModelManager.CreateMessageBoxViewModel(
                "Error pulling servers",
                ex.ToString()
            );

            await _dialogManager.ShowDialogAsync(dialog);
        }
        finally
        {
            progress.ReportCompletion();
            IsBusy = false;
        }
    }

    private bool CanPullChannels() => !IsBusy && _discord is not null && SelectedGuild is not null;

    [RelayCommand(CanExecute = nameof(CanPullChannels))]
    private async Task PullChannelsAsync()
    {
        IsBusy = true;
        var progress = _progressMuxer.CreateInput();

        try
        {
            if (_discord is null || SelectedGuild is null)
                return;

            AvailableChannels = null;
            SelectedChannels.Clear();

            var channels = new List<Channel>();

            // Regular channels
            await foreach (var channel in _discord.GetGuildChannelsAsync(SelectedGuild.Id))
                channels.Add(channel);

            // Threads
            if (_settingsService.ThreadInclusionMode != ThreadInclusionMode.None)
            {
                await foreach (
                    var thread in _discord.GetGuildThreadsAsync(
                        SelectedGuild.Id,
                        _settingsService.ThreadInclusionMode == ThreadInclusionMode.All
                    )
                )
                {
                    channels.Add(thread);
                }
            }

            // Build a hierarchy of channels
            var channelTree = ChannelConnection.BuildTree(
                channels
                    .OrderByDescending(c => c.IsDirect ? c.LastMessageId : null)
                    .ThenBy(c => c.Position)
                    .ToArray()
            );

            AvailableChannels = channelTree;
            SelectedChannels.Clear();
        }
        catch (DiscordChatExporterException ex) when (!ex.IsFatal)
        {
            _snackbarManager.Notify(ex.Message.TrimEnd('.'));
        }
        catch (Exception ex)
        {
            var dialog = _viewModelManager.CreateMessageBoxViewModel(
                "Error pulling channels",
                ex.ToString()
            );

            await _dialogManager.ShowDialogAsync(dialog);
        }
        finally
        {
            progress.ReportCompletion();
            IsBusy = false;
        }
    }

    // Export functionality removed per DiscordEmotify requirements

    private bool CanReact() =>
        // Allow start when ready or allow stop when currently reacting
        (
            _discord is not null
            && SelectedGuild is not null
            && SelectedChannels.Any()
            && !IsReacting
        ) || IsReacting;

    [RelayCommand(CanExecute = nameof(CanReact))]
    private async Task ReactAsync()
    {
        // Toggle: if already reacting, stop
        if (IsReacting)
        {
            try
            {
                _reactCts?.Cancel();
                if (_reactTask is not null)
                    await _reactTask;
            }
            catch (OperationCanceledException)
            {
                // expected during stop
            }
            catch (AggregateException aex)
                when (aex.InnerExceptions.All(e => e is OperationCanceledException))
            {
                // all canceled
            }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ReactStatus = null;
                    IsReacting = false;
                });
            }

            return;
        }

        try
        {
            if (_discord is null || SelectedGuild is null || !SelectedChannels.Any())
                return;

            var dialog = _viewModelManager.CreateReactSetupViewModel(
                SelectedGuild,
                SelectedChannels.Select(c => c.Channel).ToArray()
            );

            if (await _dialogManager.ShowDialogAsync(dialog) != true)
                return;

            var emojiInput = dialog.EmojiInput?.Trim();
            if (string.IsNullOrWhiteSpace(emojiInput))
            {
                _snackbarManager.Notify("Please enter an emoji.");
                return;
            }

            var parsedEmoji = ParseEmoji(emojiInput);
            var delayMs = dialog.DelayMs;
            var descending = dialog.Order == Models.ReactionOrder.Desc;
            var clear = dialog.Clear;

            var pairs = dialog
                .Channels!.Select(c => new { Channel = c, Progress = _progressMuxer.CreateInput() })
                .ToArray();

            // Set up cancellation and live status
            _reactCts = new CancellationTokenSource();
            _totalReactions = 0;
            IsReacting = true;
            Dispatcher.UIThread.Post(() =>
                ReactStatus = (
                    clear ? "Clearing... 0 reactions processed" : "Reacting... 0 reactions sent"
                )
            );

            // Run in the background so the button can be used to stop
            _reactTask = Task.Run(async () =>
            {
                try
                {
                    await Parallel.ForEachAsync(
                        pairs,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Math.Max(1, _settingsService.ParallelLimit),
                            CancellationToken = _reactCts.Token,
                        },
                        async (pair, cancellationToken) =>
                        {
                            try
                            {
                                await foreach (
                                    var message in _discord.GetMessagesAsync(
                                        pair.Channel.Id,
                                        after: null,
                                        before: null,
                                        progress: null,
                                        cancellationToken: cancellationToken,
                                        descending: descending
                                    )
                                )
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    try
                                    {
                                        if (clear)
                                        {
                                            await _discord.RemoveReactionAsync(
                                                pair.Channel.Id,
                                                message.Id,
                                                parsedEmoji,
                                                cancellationToken
                                            );
                                        }
                                        else
                                        {
                                            await _discord.AddReactionAsync(
                                                pair.Channel.Id,
                                                message.Id,
                                                parsedEmoji,
                                                cancellationToken
                                            );
                                        }

                                        var total = Interlocked.Increment(ref _totalReactions);
                                        Dispatcher.UIThread.Post(() =>
                                            ReactStatus = (
                                                clear
                                                    ? $"Clearing... {total} reactions processed"
                                                    : $"Reacting... {total} reactions sent"
                                            )
                                        );

                                        if (delayMs > 0)
                                            await Task.Delay(delayMs, cancellationToken);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        // stopping
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        if (_reactCts?.IsCancellationRequested == true)
                                        {
                                            // treat as stop
                                        }
                                        else
                                        {
                                            // If clearing and the reaction wasn't present, ignore silently
                                            if (
                                                !(
                                                    clear
                                                    && ex.Message.Contains(
                                                        "not found",
                                                        StringComparison.OrdinalIgnoreCase
                                                    )
                                                )
                                            )
                                            {
                                                var dialog =
                                                    _viewModelManager.CreateMessageBoxViewModel(
                                                        "Error reacting to channel(s)",
                                                        ex.ToString()
                                                    );
                                                Dispatcher.UIThread.Post(() =>
                                                    _ = _dialogManager.ShowDialogAsync(dialog)
                                                );
                                            }
                                        }
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // stopping
                            }
                            catch (AggregateException aex)
                                when (aex.InnerExceptions.All(e => e is OperationCanceledException))
                            {
                                // stopping
                            }
                            catch (Exception ex)
                            {
                                if (_reactCts?.IsCancellationRequested == true)
                                {
                                    // treat as stop
                                }
                                else
                                {
                                    var dialog = _viewModelManager.CreateMessageBoxViewModel(
                                        "Error reacting to channel(s)",
                                        ex.ToString()
                                    );
                                    Dispatcher.UIThread.Post(() =>
                                        _ = _dialogManager.ShowDialogAsync(dialog)
                                    );
                                }
                            }
                        }
                    );
                }
                catch (OperationCanceledException)
                {
                    // stopping
                }
                catch (AggregateException aex)
                    when (aex.InnerExceptions.All(e => e is OperationCanceledException))
                {
                    // stopping
                }
                catch (Exception ex)
                {
                    var dialog = _viewModelManager.CreateMessageBoxViewModel(
                        "Error reacting to channel(s)",
                        ex.ToString()
                    );
                    Dispatcher.UIThread.Post(() => _ = _dialogManager.ShowDialogAsync(dialog));
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ReactStatus = null;
                        IsReacting = false;
                    });
                    _reactCts?.Dispose();
                    _reactCts = null;
                }
            });
        }
        catch (Exception ex)
        {
            var dialog = _viewModelManager.CreateMessageBoxViewModel(
                "Error reacting to channel(s)",
                ex.ToString()
            );
            await _dialogManager.ShowDialogAsync(dialog);
        }
    }

    private static Emoji ParseEmoji(string input)
    {
        // name:id
        if (input.Contains(':'))
        {
            var parts = input.Split(':');
            if (parts.Length == 2)
            {
                var id = Snowflake.TryParse(parts[1]);
                if (id is not null)
                    return new Emoji(id, parts[0], false);
            }
        }

        // :code:
        if (input.StartsWith(':') && input.EndsWith(':') && input.Length > 2)
            return Emoji.FromCode(input.Trim(':'));

        // unicode
        return new Emoji(null, input, false);
    }

    [RelayCommand]
    private void OpenDiscord() => ProcessEx.StartShellExecute("https://discord.com/app");

    [RelayCommand]
    private void OpenDiscordDeveloperPortal() =>
        ProcessEx.StartShellExecute("https://discord.com/developers/applications");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _reactCts?.Cancel();
            }
            catch
            {
                // ignore
            }
            finally
            {
                _reactCts?.Dispose();
                _reactCts = null;
            }
            _eventRoot.Dispose();
        }

        base.Dispose(disposing);
    }
}
