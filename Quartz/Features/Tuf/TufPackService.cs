using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
namespace Quartz.Features.Tuf;
public sealed class TufPackService : IRuntimeService {
    public static TufPackService Instance { get; private set; }
    private const int PageSize = 30;
    public IReadOnlyList<TufPack> Packs => packs;
    public IReadOnlyList<TufPackItem> PackItems => packItems;
    public IReadOnlyList<TufLevel> PackLevels => packLevels;
    public TufPackLevelSort LevelSort { get; private set; } = TufPackLevelSort.PackOrder;
    public bool LevelAscending { get; private set; } = true;
    public TufPackListState ListState { get; private set; } = TufPackListState.Idle;
    public TufPackListState DetailState { get; private set; } = TufPackListState.Idle;
    public string ListError { get; private set; } = "";
    public string DetailError { get; private set; } = "";
    public bool OfflineError { get; private set; }
    public string Query { get; private set; } = "";
    public TufPackSort Sort { get; private set; } = TufPackSort.Recent;
    public bool Ascending { get; private set; }
    public bool HasMore { get; private set; }
    public bool LoadingMore { get; private set; }
    public TufPack SelectedPack { get; private set; }
    public bool IsBusy => actions?.IsBusy ?? false;
    public event Action Changed = delegate { };
    private readonly List<TufPack> packs = [];
    private readonly List<TufPackItem> packItems = [];
    private readonly List<TufLevel> packLevels = [];
    private TufPackApiClient api;
    private TufLevelActionRunner actions;
    private TufDifficultyDictionary difficulties;
    private CancellationTokenSource listRequest;
    private CancellationTokenSource detailRequest;
    private CancellationTokenSource debounce;
    private int listGeneration;
    private int detailGeneration;
    private int nextOffset;
    private bool appendFailed;
    private bool disposed;
    public void Initialize() {
        Instance = this;
        api = new TufPackApiClient();
        TufService tuf = TufService.Instance;
        if(tuf != null) actions = new TufLevelActionRunner(packLevels, tuf.Downloads, tuf.Launcher,
            Notify, tuf.RecordInstalledLevel);
    }
    public void EnsureLoaded() {
        if(ListState == TufPackListState.Idle) RefreshPacks();
    }
    public void SetQuery(string value) {
        string query = TufInput.NormalizeQuery(value);
        if(query == Query) return;
        Query = query;
        InvalidateListRequest();
        CancelDebounce();
        packs.Clear();
        HasMore = false;
        LoadingMore = false;
        nextOffset = 0;
        appendFailed = false;
        ListState = TufPackListState.Loading;
        ListError = "";
        OfflineError = false;
        Notify();
        debounce = new CancellationTokenSource();
        DebouncedRefresh(debounce.Token);
    }
    private async void DebouncedRefresh(CancellationToken token) {
        try {
            await Task.Delay(300, token);
            if(token.IsCancellationRequested) return;
            if(debounce != null && debounce.Token == token) {
                debounce.Dispose();
                debounce = null;
            }
            FetchPacks(false);
        } catch(OperationCanceledException) { }
    }
    public void SetSort(TufPackSort value) {
        if(Sort == value) return;
        Sort = value;
        RefreshPacks();
    }
    public void ToggleAscending() {
        Ascending = !Ascending;
        RefreshPacks();
    }
    public void RefreshPacks() {
        CancelDebounce();
        FetchPacks(false);
    }
    public void LoadMore() {
        if(HasMore && !LoadingMore
            && (ListState == TufPackListState.Ready || (ListState == TufPackListState.Error && appendFailed)))
            FetchPacks(true);
    }
    private async void FetchPacks(bool append) {
        listRequest?.Cancel();
        listRequest?.Dispose();
        listRequest = new CancellationTokenSource();
        CancellationToken token = listRequest.Token;
        int generation = ++listGeneration;
        string query = Query;
        TufPackSort sort = Sort;
        bool ascending = Ascending;
        if(append) {
            LoadingMore = true;
            appendFailed = false;
        } else {
            appendFailed = false;
            ListState = TufPackListState.Loading;
            ListError = "";
        }
        OfflineError = false;
        Notify();
        try {
            TufPacksPage page = await api.FetchPacksAsync(query, sort, ascending, append ? nextOffset : 0, PageSize, token);
            MainThread.Enqueue(() => ApplyPacks(page, append, token, generation, query, sort, ascending));
        } catch(OperationCanceledException) when(token.IsCancellationRequested) { }
        catch(Exception e) {
            bool offline = e is OperationCanceledException || TufNetworkPolicy.IsOfflineError(e);
            string message = e is OperationCanceledException
                ? MainCore.Tr.Get("TUF_TIMEOUT", "The request to TUF timed out.")
                : e.Message;
            MainThread.Enqueue(() => {
                if(!ListRequestIsCurrent(token, generation, query, sort, ascending)) return;
                MainCore.Log.Wrn("[TUF] pack list could not be loaded: " + e);
                LoadingMore = false;
                appendFailed = append;
                ListState = TufPackListState.Error;
                ListError = message;
                OfflineError = offline;
                Notify();
            });
        }
    }
    private void ApplyPacks(TufPacksPage page, bool append, CancellationToken token, int generation,
        string query, TufPackSort sort, bool ascending) {
        if(!ListRequestIsCurrent(token, generation, query, sort, ascending)) return;
        if(!append) {
            packs.Clear();
            nextOffset = 0;
        }
        HashSet<string> existing = packs.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach(TufPack pack in page.Results)
            if(existing.Add(pack.Id)) packs.Add(pack);
        nextOffset = packs.Count;
        HasMore = packs.Count < page.Total && page.Results.Count > 0;
        LoadingMore = false;
        appendFailed = false;
        ListState = packs.Count == 0 ? TufPackListState.Empty : TufPackListState.Ready;
        Notify();
    }
    private bool ListRequestIsCurrent(CancellationToken token, int generation, string query,
        TufPackSort sort, bool ascending) =>
        !token.IsCancellationRequested && !disposed && generation == listGeneration
        && query == Query && sort == Sort && ascending == Ascending;
    public void OpenPack(TufPack pack) {
        if(pack == null) return;
        detailRequest?.Cancel();
        detailRequest?.Dispose();
        detailRequest = new CancellationTokenSource();
        CancellationToken token = detailRequest.Token;
        int generation = ++detailGeneration;
        SelectedPack = pack;
        packItems.Clear();
        packLevels.Clear();
        DetailState = TufPackListState.Loading;
        DetailError = "";
        OfflineError = false;
        Notify();
        LoadPackLevels(pack, token, generation);
    }
    private async void LoadPackLevels(TufPack pack, CancellationToken token, int generation) {
        try {
            difficulties ??= await api.FetchDifficultiesAsync(token);
            IReadOnlyList<TufPackItem> items = await api.FetchPackItemsAsync(pack.Id, difficulties, token);
            MainThread.Enqueue(() => ApplyPackLevels(pack, items, token, generation));
        } catch(OperationCanceledException) when(token.IsCancellationRequested) { }
        catch(Exception e) {
            bool offline = e is OperationCanceledException || TufNetworkPolicy.IsOfflineError(e);
            string message = e is OperationCanceledException
                ? MainCore.Tr.Get("TUF_TIMEOUT", "The request to TUF timed out.")
                : e.Message;
            MainThread.Enqueue(() => {
                if(!DetailRequestIsCurrent(token, generation, pack)) return;
                MainCore.Log.Wrn($"[TUF] pack {pack.Id} levels could not be loaded: {e}");
                DetailState = TufPackListState.Error;
                DetailError = message;
                OfflineError = offline;
                Notify();
            });
        }
    }
    private void ApplyPackLevels(TufPack pack, IReadOnlyList<TufPackItem> items, CancellationToken token, int generation) {
        if(!DetailRequestIsCurrent(token, generation, pack)) return;
        packItems.Clear();
        packItems.AddRange(items);
        packLevels.Clear();
        TufPackApiClient.FlattenLevels(items, packLevels);
        TufService tuf = TufService.Instance;
        if(tuf != null)
            foreach(TufLevel level in packLevels) tuf.MarkIfInstalled(level);
        DetailState = packLevels.Count == 0 ? TufPackListState.Empty : TufPackListState.Ready;
        Notify();
    }
    public void SetLevelSort(TufPackLevelSort value) {
        if(LevelSort == value) return;
        LevelSort = value;
        Notify();
    }
    public void ToggleLevelAscending() {
        LevelAscending = !LevelAscending;
        Notify();
    }
    private bool DetailRequestIsCurrent(CancellationToken token, int generation, TufPack pack) =>
        !token.IsCancellationRequested && !disposed && generation == detailGeneration
        && ReferenceEquals(pack, SelectedPack);
    public void ClosePack() {
        detailRequest?.Cancel();
        SelectedPack = null;
        packItems.Clear();
        packLevels.Clear();
        DetailState = TufPackListState.Idle;
        DetailError = "";
        OfflineError = false;
        Notify();
    }
    public void RetryPackLevels() {
        if(SelectedPack != null) OpenPack(SelectedPack);
    }
    public void Act(TufLevel level) => actions?.Act(level);
    public void LaunchChart(TufLevel level, string chart) => actions?.LaunchChart(level, chart);
    private void InvalidateListRequest() {
        listRequest?.Cancel();
        listGeneration++;
    }
    private void CancelDebounce() {
        debounce?.Cancel();
        debounce?.Dispose();
        debounce = null;
    }
    private void Notify() => Changed?.Invoke();
    public void Dispose() {
        disposed = true;
        debounce?.Cancel();
        listRequest?.Cancel();
        detailRequest?.Cancel();
        actions?.Dispose();
        debounce?.Dispose();
        listRequest?.Dispose();
        detailRequest?.Dispose();
        api?.Dispose();
        Changed = delegate { };
        packs.Clear();
        packItems.Clear();
        packLevels.Clear();
        if(ReferenceEquals(Instance, this)) Instance = null;
    }
}
