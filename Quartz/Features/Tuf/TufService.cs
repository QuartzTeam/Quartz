using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.UI;

namespace Quartz.Features.Tuf;

public sealed class TufService : IRuntimeService {
    public static TufService Instance { get; private set; }
    public IReadOnlyList<TufLevel> Levels => levels;
    public TufListState State { get; private set; } = TufListState.Idle;
    public string Error { get; private set; } = "";
    public string Query { get; private set; } = "";
    public TufSort Sort { get; private set; } = TufSort.Recent;
    public bool Ascending { get; private set; }
    public bool HasMore { get; private set; }
    public bool LoadingMore { get; private set; }
    public bool IsBusy => actions?.IsBusy ?? false;
    // Shared with TufPackService so a level downloaded/launched from either browser
    // reuses one cache, one download semaphore, and one launcher instance.
    internal TufDownloadService Downloads => downloads;
    internal TufLevelLauncher Launcher => launcher;
    public TufDifficultyFilter DifficultyFilter { get; private set; } = TufDifficultyFilter.AllRanked;
    public int MinDifficultyIndex => DifficultyFilter.MinIndex;
    public int MaxDifficultyIndex => DifficultyFilter.MaxIndex;
    public bool QuantumEnabled => DifficultyFilter.HasQuantum;
    public int QuantumMinIndex => quantumMinIndex;
    public int QuantumMaxIndex => quantumMaxIndex;
    public IReadOnlyList<string> SelectedDifficulties => DifficultyFilter.SelectedDifficulties;
    public event Action Changed = delegate { };

    private readonly List<TufLevel> levels = [];
    private TufApiClient api;
    private TufDownloadService downloads;
    private TufLevelLauncher launcher;
    private TufLevelActionRunner actions;
    private CancellationTokenSource listRequest;
    private CancellationTokenSource debounce;
    private int listGeneration;
    private int nextOffset;
    private bool appendFailed;
    private bool disposed;
    private SettingsFile<TufSettings> settings;
    private int quantumMinIndex;
    private int quantumMaxIndex = TufDifficultyFilter.QuantumNames.Count - 1;

    public void Initialize() {
        Instance = this;
        settings = new SettingsFile<TufSettings>(Path.Combine(MainCore.Paths.TufPath, "Settings.json"));
        settings.Load();
        Sort = settings.Data.GetSort();
        Ascending = settings.Data.Ascending;
        DifficultyFilter = settings.Data.GetDifficultyFilter();
        quantumMinIndex = settings.Data.QuantumMinIndex;
        quantumMaxIndex = settings.Data.QuantumMaxIndex;
        api = new TufApiClient();
        downloads = new TufDownloadService(MainCore.Paths.TufLevelsPath);
        launcher = MainCore.Root.AddComponent<TufLevelLauncher>();
        launcher.Initialize(MainCore.Paths.TufLevelsPath);
        actions = new TufLevelActionRunner(levels, downloads, launcher, Notify);
    }

    public void EnsureLoaded() {
        if(State == TufListState.Idle) Refresh();
    }

    public void SetQuery(string value) {
        string query = TufInput.NormalizeQuery(value);
        if(query == Query) return;
        Query = query;
        InvalidateListRequest();
        CancelDebounce();
        levels.Clear();
        HasMore = false;
        LoadingMore = false;
        nextOffset = 0;
        appendFailed = false;
        State = TufListState.Loading;
        Error = "";
        Notify();
        debounce = new CancellationTokenSource();
        DebouncedRefresh(debounce.Token);
    }

    private async void DebouncedRefresh(CancellationToken token) {
        try {
            await Task.Delay(300, token);
            if(!token.IsCancellationRequested) {
                if(debounce != null && debounce.Token == token) {
                    debounce.Dispose();
                    debounce = null;
                }
                Fetch(false);
            }
        } catch(OperationCanceledException) { }
    }

    public void SetSort(TufSort value) {
        if(Sort == value) return;
        Sort = value;
        SaveSettings();
        Refresh();
    }

    public void ToggleAscending() {
        Ascending = !Ascending;
        SaveSettings();
        Refresh();
    }

    public void SetDifficultyRange(int minIndex, int maxIndex) =>
        SetDifficultyFilter(DifficultyFilter.WithRange(minIndex, maxIndex));
    public void ToggleSpecialDifficulty(string name) =>
        SetDifficultyFilter(DifficultyFilter.Toggle(name));
    public void SetQuantumRange(int minIndex, int maxIndex) {
        int last = TufDifficultyFilter.QuantumNames.Count - 1;
        quantumMinIndex = Math.Clamp(minIndex, 0, last);
        quantumMaxIndex = Math.Clamp(maxIndex, 0, last);
        if(quantumMinIndex > quantumMaxIndex) (quantumMinIndex, quantumMaxIndex) = (quantumMaxIndex, quantumMinIndex);
        SetDifficultyFilter(DifficultyFilter.WithQuantumRange(quantumMinIndex, quantumMaxIndex));
    }
    public void ClearQuantum() => SetDifficultyFilter(DifficultyFilter.WithoutQuantum());
    public void ResetDifficultyFilter() => SetDifficultyFilter(TufDifficultyFilter.AllRanked);

    private void SetDifficultyFilter(TufDifficultyFilter filter) {
        if(DifficultyFilter.Equals(filter)) return;
        DifficultyFilter = filter;
        SaveSettings();
        levels.Clear();
        HasMore = false;
        nextOffset = 0;
        Refresh();
    }

    private void SaveSettings() {
        if(settings == null) return;
        settings.Data.Sort = (int)Sort;
        settings.Data.Ascending = Ascending;
        settings.Data.SetDifficultyFilter(DifficultyFilter, quantumMinIndex, quantumMaxIndex);
        settings.RequestSave();
    }

    public void Refresh() {
        CancelDebounce();
        Fetch(false);
    }
    public void LoadMore() {
        if(HasMore && !LoadingMore
            && (State == TufListState.Ready || (State == TufListState.Error && appendFailed))) Fetch(true);
    }

    private async void Fetch(bool append) {
        listRequest?.Cancel();
        listRequest?.Dispose();
        listRequest = new CancellationTokenSource();
        CancellationToken token = listRequest.Token;
        int generation = ++listGeneration;
        string query = Query;
        TufSort sort = Sort;
        bool ascending = Ascending;
        TufDifficultyFilter filter = DifficultyFilter;
        if(append) {
            LoadingMore = true;
            appendFailed = false;
        }
        else {
            appendFailed = false;
            State = TufListState.Loading;
            Error = "";
        }
        Notify();
        try {
            TufPage page = await api.FetchAsync(query, sort, ascending, append ? nextOffset : 0, filter, token);
            MainThread.Enqueue(() => ApplyPage(page, append, token, generation, query, sort, ascending, filter));
        } catch(OperationCanceledException) { }
        catch(Exception e) {
            MainThread.Enqueue(() => {
                if(!RequestIsCurrent(token, generation, query, sort, ascending, filter)) return;
                LoadingMore = false;
                appendFailed = append;
                State = TufListState.Error;
                Error = e.Message;
                Notify();
            });
        }
    }

    private void ApplyPage(TufPage page, bool append, CancellationToken token, int generation,
        string query, TufSort sort, bool ascending, TufDifficultyFilter filter) {
        if(!RequestIsCurrent(token, generation, query, sort, ascending, filter)) return;
        if(!append) {
            levels.Clear();
            nextOffset = 0;
        }
        nextOffset += page.ConsumedCount;
        HashSet<int> existing = levels.Select(x => x.Id).ToHashSet();
        foreach(TufLevel level in page.Results) {
            if(existing.Add(level.Id)) {
                if(downloads.TryGetCachedChart(level.Id, out _)) level.State = TufItemState.Load;
                levels.Add(level);
            }
        }
        HasMore = page.HasMore && page.ConsumedCount > 0;
        LoadingMore = false;
        appendFailed = false;
        State = levels.Count == 0 ? TufListState.Empty : TufListState.Ready;
        Notify();
    }

    private bool RequestIsCurrent(CancellationToken token, int generation, string query,
        TufSort sort, bool ascending, TufDifficultyFilter filter) =>
        !token.IsCancellationRequested && !disposed && generation == listGeneration
        && query == Query && sort == Sort && ascending == Ascending && filter.Equals(DifficultyFilter);

    private void InvalidateListRequest() {
        listRequest?.Cancel();
        listGeneration++;
    }

    private void CancelDebounce() {
        debounce?.Cancel();
        debounce?.Dispose();
        debounce = null;
    }

    public void Act(TufLevel level) => actions?.Act(level);

    // Launch the chart the user picked from the ChooseChart list.
    public void LaunchChart(TufLevel level, string chart) => actions?.LaunchChart(level, chart);

    private void Notify() => Changed?.Invoke();
    public void Dispose() {
        disposed = true;
        settings?.Save();
        if(settings != null) SettingsRegistry.Unregister(settings);
        debounce?.Cancel();
        listRequest?.Cancel();
        actions?.Dispose();
        downloads?.Cancel();
        launcher?.Cancel();
        debounce?.Dispose();
        listRequest?.Dispose();
        downloads?.Dispose();
        api?.Dispose();
        Changed = delegate { };
        levels.Clear();
        if(ReferenceEquals(Instance, this)) Instance = null;
    }
}
