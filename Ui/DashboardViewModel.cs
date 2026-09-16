using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace texAi.Ui;

internal enum DashboardSection
{
    Activity,
    Errors,
    Models,
    Settings,
}

/// <summary>
/// State for the dashboard. Hand-rolled INotifyPropertyChanged rather than a
/// source generator or a framework: there is one window, and a dependency to
/// service it would outweigh the boilerplate it removes.
/// </summary>
internal sealed class DashboardViewModel : INotifyPropertyChanged
{
    private CancellationTokenSource? _pullCancel;
    private CancellationTokenSource? _compareCancel;

    public DashboardViewModel()
    {
        // Seeded before any binding is created, so the settings ComboBox has
        // something to select from the first time it evaluates.
        ModelChoices.Add(Model);

        foreach (ErrorEntry entry in ErrorLog.Snapshot())
        {
            Errors.Add(entry);
        }

        ErrorLog.EntryAdded += OnErrorLogged;

        HistoryStore.Entries.CollectionChanged += (_, _) =>
        {
            Notify(nameof(LatestRewrite));
            Notify(nameof(LatestGrammar));
            Notify(nameof(HasHistory));
        };

        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
        PullCommand = new RelayCommand(name => _ = PullAsync(name as string ?? PullName), _ => !IsPulling);
        CancelPullCommand = new RelayCommand(_ => _pullCancel?.Cancel(), _ => IsPulling);
        DeleteCommand = new RelayCommand(model => _ = DeleteAsync(model as InstalledModel));
        ActivateCommand = new RelayCommand(model => Activate(model as InstalledModel));
        CompareCommand = new RelayCommand(_ => _ = CompareAsync(), _ => !IsComparing);
        ClearHistoryCommand = new RelayCommand(_ => HistoryStore.Clear());
        ClearErrorsCommand = new RelayCommand(_ => { ErrorLog.Clear(); Errors.Clear(); });

        _ = RefreshAsync();
    }

    // ---- navigation -------------------------------------------------------

    private DashboardSection _section = DashboardSection.Activity;

    public DashboardSection Section
    {
        get => _section;
        set
        {
            if (Set(ref _section, value))
            {
                Notify(nameof(OnActivity));
                Notify(nameof(OnErrors));
                Notify(nameof(OnModels));
                Notify(nameof(OnSettings));
            }
        }
    }

    // Settable so the rail's RadioButtons can bind IsChecked two-way; setting
    // one to false is the group deselecting it, which is not a navigation.
    public bool OnActivity
    {
        get => Section == DashboardSection.Activity;
        set { if (value) Section = DashboardSection.Activity; }
    }

    public bool OnErrors
    {
        get => Section == DashboardSection.Errors;
        set { if (value) Section = DashboardSection.Errors; }
    }

    public bool OnModels
    {
        get => Section == DashboardSection.Models;
        set { if (value) Section = DashboardSection.Models; }
    }

    public bool OnSettings
    {
        get => Section == DashboardSection.Settings;
        set { if (value) Section = DashboardSection.Settings; }
    }

    // ---- status -----------------------------------------------------------

    private bool _connected;
    private string _statusText = "Checking Ollama...";

    public bool Connected { get => _connected; private set => Set(ref _connected, value); }

    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    /// <summary>
    /// True when the user has no usable setup: either Ollama is down or the
    /// active model is not pulled. Drives the onboarding panel, which is
    /// therefore not a one-time wizard but a permanent "here is what is wrong".
    /// </summary>
    public bool NeedsOnboarding => !Connected || !Installed.Any(m => m.Name == Model);

    // ---- activity ---------------------------------------------------------

    public ObservableCollection<HistoryEntry> History => HistoryStore.Entries;

    public bool HasHistory => HistoryStore.Entries.Count > 0;

    public HistoryEntry? LatestRewrite => HistoryStore.Latest(HotkeyAction.Rewrite);

    public HistoryEntry? LatestGrammar => HistoryStore.Latest(HotkeyAction.Grammar);

    // ---- errors -----------------------------------------------------------

    public ObservableCollection<ErrorEntry> Errors { get; } = [];

    // ---- models -----------------------------------------------------------

    public ObservableCollection<InstalledModel> Installed { get; } = [];

    /// <summary>
    /// Installed names, plus the active model even when it is missing, so the
    /// settings dropdown still shows what texAi is trying to use rather than
    /// going blank and looking unset.
    /// </summary>
    public ObservableCollection<string> ModelChoices { get; } = [];

    public IReadOnlyList<RecommendedModel> Recommended => Config.RecommendedModels;

    private string _pullName = string.Empty;
    private string _pullStatus = string.Empty;
    private double _pullFraction;
    private bool _isPulling;

    public string PullName { get => _pullName; set => Set(ref _pullName, value); }

    public string PullStatus { get => _pullStatus; private set => Set(ref _pullStatus, value); }

    public double PullFraction { get => _pullFraction; private set => Set(ref _pullFraction, value); }

    public bool IsPulling
    {
        get => _isPulling;
        private set
        {
            if (Set(ref _isPulling, value))
            {
                PullCommand.RaiseCanExecuteChanged();
                CancelPullCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // ---- compare ----------------------------------------------------------

    private string _compareSample = "ami valo hoye jabo";
    private bool _isComparing;

    public string CompareSample { get => _compareSample; set => Set(ref _compareSample, value); }

    public ObservableCollection<CompareResult> CompareResults { get; } = [];

    public ObservableCollection<string> CompareSelection { get; } = [];

    public bool IsComparing
    {
        get => _isComparing;
        private set
        {
            if (Set(ref _isComparing, value))
            {
                CompareCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // ---- settings ---------------------------------------------------------

    /// <summary>
    /// Guarded against empty values, not out of caution but because a
    /// two-way-bound ComboBox writes null back the moment its ItemsSource is
    /// briefly empty. The models list is fetched asynchronously, so it always
    /// is at load, and without this the active model is wiped on every launch.
    /// </summary>
    public string Model
    {
        get => SettingsStore.Current.Model;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || SettingsStore.Current.Model == value)
            {
                return;
            }

            SettingsStore.Current.Model = value;
            SettingsStore.Save();
            Notify();
            Notify(nameof(NeedsOnboarding));
            _ = RefreshAsync();
            _ = OllamaClient.WarmAsync();
        }
    }

    public string Tone
    {
        get => SettingsStore.Current.Tone;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || SettingsStore.Current.Tone == value)
            {
                return;
            }

            SettingsStore.Current.Tone = value;
            SettingsStore.Save();
            Notify();
        }
    }

    public IReadOnlyList<string> Tones => Config.Tones;

    public string HotkeyGrammar => Display(HotkeyAction.Grammar);
    public string HotkeyTranslate => Display(HotkeyAction.Translate);
    public string HotkeyRewrite => Display(HotkeyAction.Rewrite);
    public string HotkeyTone => Display(HotkeyAction.Tone);

    public string SettingsPath => SettingsStore.FilePath;

    public void Rebind(HotkeyAction action, HotkeyBinding binding)
    {
        SettingsStore.Current.SetBinding(action, binding);
        SettingsStore.Save();

        Notify(nameof(HotkeyGrammar));
        Notify(nameof(HotkeyTranslate));
        Notify(nameof(HotkeyRewrite));
        Notify(nameof(HotkeyTone));
    }

    private static string Display(HotkeyAction action) => SettingsStore.Current.Bindings[action].ToString();

    // ---- commands ---------------------------------------------------------

    public RelayCommand RefreshCommand { get; }
    public RelayCommand PullCommand { get; }
    public RelayCommand CancelPullCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ActivateCommand { get; }
    public RelayCommand CompareCommand { get; }
    public RelayCommand ClearHistoryCommand { get; }
    public RelayCommand ClearErrorsCommand { get; }

    public async Task RefreshAsync()
    {
        IReadOnlyList<InstalledModel>? models = await ModelService.ListAsync();

        Installed.Clear();

        if (models is null)
        {
            Connected = false;
            StatusText = "Ollama is not running";
        }
        else
        {
            foreach (InstalledModel model in models)
            {
                Installed.Add(model);
            }

            Connected = true;
            StatusText = Installed.Any(m => m.Name == Model)
                ? $"Connected, using {Model}"
                : $"Connected, but {Model} is not installed";
        }

        RebuildModelChoices();
        Notify(nameof(NeedsOnboarding));
    }

    /// <summary>
    /// Updated by difference rather than Clear-then-refill. Clearing removes the
    /// entry the settings ComboBox has selected, which silently blanks it, and
    /// re-announcing Model afterwards does not bring the selection back. The
    /// active model is always in the desired set, so this never removes it.
    /// </summary>
    private void RebuildModelChoices()
    {
        var desired = Installed.Select(m => m.Name).ToList();

        if (!desired.Contains(Model))
        {
            desired.Insert(0, Model);
        }

        for (int i = ModelChoices.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(ModelChoices[i]))
            {
                ModelChoices.RemoveAt(i);
            }
        }

        for (int i = 0; i < desired.Count; i++)
        {
            if (!ModelChoices.Contains(desired[i]))
            {
                ModelChoices.Insert(Math.Min(i, ModelChoices.Count), desired[i]);
            }
        }
    }

    private async Task PullAsync(string name)
    {
        name = name.Trim();

        if (string.IsNullOrEmpty(name) || IsPulling)
        {
            return;
        }

        IsPulling = true;
        PullFraction = 0;
        PullStatus = $"Starting {name}";
        _pullCancel = new CancellationTokenSource();

        var progress = new Progress<PullProgress>(p =>
        {
            PullStatus = p.Fraction is { } fraction
                ? $"{p.Status} - {fraction:P0} of {p.Total / 1024d / 1024d / 1024d:0.0} GB"
                : p.Status;

            PullFraction = p.Fraction ?? 0;
        });

        string? failure = await ModelService.PullAsync(name, progress, _pullCancel.Token);

        PullStatus = _pullCancel.IsCancellationRequested
            ? "Cancelled"
            : failure ?? $"{name} is ready";

        if (failure is not null && !_pullCancel.IsCancellationRequested)
        {
            ErrorLog.Record(FailureKind.ModelNotInstalled, $"Could not pull {name}", failure);
        }

        _pullCancel.Dispose();
        _pullCancel = null;
        IsPulling = false;

        await RefreshAsync();
    }

    private async Task DeleteAsync(InstalledModel? model)
    {
        if (model is null)
        {
            return;
        }

        if (MessageBox.Show(
                $"Delete {model.Name}? It is {model.SizeGb:0.0} GB and pulling it again means downloading it again.",
                "texAi",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        string? failure = await ModelService.DeleteAsync(model.Name);

        if (failure is not null)
        {
            ErrorLog.Record(FailureKind.HttpError, $"Could not delete {model.Name}", failure);
        }

        await RefreshAsync();
    }

    private void Activate(InstalledModel? model)
    {
        if (model is not null)
        {
            Model = model.Name;
        }
    }

    private async Task CompareAsync()
    {
        if (IsComparing || CompareSelection.Count == 0 || string.IsNullOrWhiteSpace(CompareSample))
        {
            return;
        }

        IsComparing = true;
        CompareResults.Clear();
        _compareCancel = new CancellationTokenSource();

        try
        {
            IReadOnlyList<CompareResult> results = await ModelService.CompareAsync(
                CompareSelection.ToList(),
                HotkeyAction.Translate,
                CompareSample,
                _compareCancel.Token);

            foreach (CompareResult result in results)
            {
                CompareResults.Add(result);
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to show; the list simply stays as far as it got.
        }
        finally
        {
            _compareCancel.Dispose();
            _compareCancel = null;
            IsComparing = false;
        }
    }

    // Never unsubscribed, deliberately: one view model is created for the one
    // dashboard window, which is hidden rather than closed and lives as long as
    // the process does.
    private void OnErrorLogged(ErrorEntry entry) =>
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Errors.Insert(0, entry);

            // Otherwise the rail keeps saying "connected" next to an error that
            // says Ollama is not running, until the next 30s poll catches up.
            if (entry.Kind is FailureKind.OllamaUnreachable or FailureKind.ModelNotInstalled)
            {
                _ = RefreshAsync();
            }
        });

    // ---- INotifyPropertyChanged -------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Notify(property);
        return true;
    }
}
