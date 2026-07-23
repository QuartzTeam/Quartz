using System.Text;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
public static class TufPacksUI {
    public static void Create(RectTransform parent) {
        TufPacksView view = parent.gameObject.AddComponent<TufPacksView>();
        view.Build(parent);
    }
}
internal sealed class TufPacksView : MonoBehaviour {
    private TufPackService service;
    private RectTransform content;
    private RectTransform viewport;
    private UIScrollController scroll;
    private TMP_InputField search;
    private readonly List<(TufPackSort Sort, Image Image)> sortChips = [];
    private Image directionChip;
    private TMP_Text directionLabel;
    private readonly Dictionary<int, TMP_Text> cardLabels = [];
    private readonly HashSet<long> expandedFolders = [];
    private TufPreviewGroup previews;
    private static bool ShowPreviews => TufService.Instance?.ShowPreviews ?? true;
    private string expandedPackId;
    private GTween chartChooserSeq;
    private GTween viewSwitchSeq;
    private CanvasGroup contentCg;
    private bool lastDetailView;
    private float listScrollY;
    private string listSignature;
    private bool built;
    private bool pendingRebuild;
    private void OnEnable() {
        if(!pendingRebuild) return;
        pendingRebuild = false;
        Rebuild();
    }
    public void Build(RectTransform parent) {
        service = TufPackService.Instance;
        if(service == null) return;
        RectTransform pad = Rect("TUF Packs", parent, Vector2.zero, Vector2.one, new(18f, 18f), new(-18f, -18f));
        BuildHeader(pad);
        viewport = Rect("Pack Viewport", pad, Vector2.zero, Vector2.one, Vector2.zero, new(0f, -138f));
        viewport.gameObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Pack Cards", viewport, new(0f, 1f), new(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new(0.5f, 1f);
        contentCg = content.gameObject.AddComponent<CanvasGroup>();
        GenerateUI.FitVertical(content.gameObject, 8f);
        scroll = pad.gameObject.AddComponent<UIScrollController>();
        scroll.SetContent(content, viewport);
        built = true;
        previews = new TufPreviewGroup();
        service.Changed += Rebuild;
        service.EnsureLoaded();
        Rebuild();
    }
    private void BuildHeader(RectTransform parent) {
        RectTransform titleRect = Rect("Title", parent, new(0f, 1f), new(1f, 1f), new(0f, -30f), Vector2.zero);
        TMP_Text title = Text(titleRect, "Packs", 28f, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<TextLocalization>().Init("TUF_PACKS", "Packs");
        RectTransform taglineRect = Rect("Tagline", titleRect, new(0f, 0f), new(1f, 1f), new(110f, 4f), new(0f, 0f));
        TMP_Text tagline = Text(taglineRect, "Browse level packs, open one, then load its levels.", 14f, TextAlignmentOptions.Left);
        tagline.color = new(1f, 1f, 1f, 0.42f);
        tagline.gameObject.AddComponent<TextLocalization>().Init("TUF_PACKS_TAGLINE", tagline.text);
        RectTransform searchRow = Rect("Search Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -78f), new(0f, -42f));
        AddHorizontal(searchRow);
        BuildSearch(searchRow);
        (Image refresh, TMP_Text refreshLabel) = Chip(searchRow, "Refresh", 92f, service.RefreshPacks);
        refreshLabel.gameObject.AddComponent<TextLocalization>().Init("TUF_REFRESH", "Refresh");
        RectTransform sortRow = Rect("Sort Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -126f), new(0f, -90f));
        AddHorizontal(sortRow);
        AddSortChip(sortRow, TufPackSort.Recent, "TUF_SORT_RECENT", "Recent", 76f);
        AddSortChip(sortRow, TufPackSort.Name, "TUF_PACK_SORT_NAME", "Name", 64f);
        AddSortChip(sortRow, TufPackSort.Levels, "TUF_PACK_SORT_LEVELS", "Levels", 72f);
        (directionChip, directionLabel) = Chip(sortRow, "↓", 48f, service.ToggleAscending);
    }
    private void BuildSearch(Transform parent) {
        RectTransform bg = Rect("Search", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = bg.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 170f;
        size.flexibleWidth = 1f;
        Image image = bg.gameObject.AddComponent<Image>();
        image.color = UIColors.ObjectBG;
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        search = bg.gameObject.AddComponent<TMP_InputField>();
        RectTransform textArea = Rect("Text Area", bg, Vector2.zero, Vector2.one, new(16f, 0f), new(-40f, 0f));
        textArea.gameObject.AddComponent<RectMask2D>();
        TMP_Text value = Text(textArea, "", 17f, TextAlignmentOptions.Left);
        TextCompat.NoWrap(value);
        TMP_Text placeholder = Text(textArea, "Search packs…", 17f, TextAlignmentOptions.Left);
        TextCompat.NoWrap(placeholder);
        placeholder.color = new(1f, 1f, 1f, 0.28f);
        placeholder.gameObject.AddComponent<TextLocalization>().Init("TUF_PACK_SEARCH_PLACEHOLDER", "Search packs…");
        GameObject iconObj = new("Search Icon");
        iconObj.transform.SetParent(bg, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new(1f, 0.5f);
        iconRect.sizeDelta = new(22f, 22f);
        iconRect.anchoredPosition = new(-17f, 0f);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = MainCore.Spr.Get(UISprite.MagnifyingGlass128);
        icon.color = new(1f, 1f, 1f, 0.25f);
        search.textViewport = textArea;
        search.textComponent = value as TextMeshProUGUI;
        search.placeholder = placeholder as TextMeshProUGUI;
        search.lineType = TMP_InputField.LineType.SingleLine;
        search.richText = false;
        search.characterLimit = 128;
        search.SetTextWithoutNotify(service.Query);
        search.onValueChanged.AddListener(text => {
            service.SetQuery(text);
            if(string.IsNullOrEmpty(text)) ResetSearchScroll();
        });
        search.onDeselect.AddListener(_ => ResetSearchScroll());
    }
    private void Update() {
        if(!built || service == null || content == null || viewport == null) return;
        previews?.Tick();
        if(service.SelectedPack != null) return;
        if(!service.HasMore || service.LoadingMore || service.ListState != TufPackListState.Ready) return;
        float max = content.rect.height - viewport.rect.height;
        if(max <= 0f || content.anchoredPosition.y >= max - 400f) service.LoadMore();
    }
    private void ResetSearchScroll() {
        if(search == null || search.textComponent == null) return;
        search.textComponent.rectTransform.anchoredPosition =
            new(0f, search.textComponent.rectTransform.anchoredPosition.y);
    }
    private void AddSortChip(Transform parent, TufPackSort sort, string key, string label, float width) {
        (Image image, TMP_Text text) = Chip(parent, label, width, () => service.SetSort(sort));
        text.gameObject.AddComponent<TextLocalization>().Init(key, label);
        sortChips.Add((sort, image));
    }
    private void Rebuild() {
        if(!built || content == null || service == null) return;
        if(!gameObject.activeInHierarchy) {
            pendingRebuild = true;
            return;
        }
        string signature = BuildSignature();
        if(signature == listSignature && cardLabels.Count > 0) {
            foreach(TufLevel level in service.PackLevels) {
                if(!cardLabels.TryGetValue(level.Id, out TMP_Text label) || label == null) continue;
                string text = ActionLabel(level);
                if(label.text != text) label.text = text;
            }
            RefreshControls();
            return;
        }
        listSignature = signature;
        float oldY = content.anchoredPosition.y;
        RefreshControls();
        chartChooserSeq?.Kill();
        previews.ClearSlots();
        GenerateUI.ClearChildren(content);
        cardLabels.Clear();
        bool detail = service.SelectedPack != null;
        if(detail) RebuildDetail();
        else RebuildList();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if(detail != lastDetailView) {
            if(detail) {
                listScrollY = oldY;
                scroll.ScrollTo(0f);
            } else {
                scroll.ScrollTo(listScrollY);
            }
            lastDetailView = detail;
            PlayViewSwitch(detail);
        } else {
            scroll.ScrollTo(oldY);
        }
    }
    private void PlayViewSwitch(bool detail) {
        if(contentCg == null) return;
        viewSwitchSeq?.Kill();
        contentCg.alpha = 0f;
        float fromX = detail ? 48f : -48f;
        content.anchoredPosition = new(fromX, content.anchoredPosition.y);
        viewSwitchSeq = GTweenSequenceBuilder.New()
            .Join(contentCg.GTAlpha(1f, 0.2f).SetEasing(Easing.OutSine))
            .Join(GTweens.Extensions.GTweenExtensions.Tween(
                () => content.anchoredPosition.x,
                x => content.anchoredPosition = new(x, content.anchoredPosition.y),
                0f, 0.3f).SetEasing(Easing.OutCubic))
            .Build();
        MainCore.TC.Play(viewSwitchSeq);
    }
    private void RebuildList() {
        if(service.ListState == TufPackListState.Loading) {
            AddLoadingStatus(Tr("TUF_PACK_LOADING", "Loading packs…"));
        } else if(service.ListState == TufPackListState.Error && service.Packs.Count == 0) {
            if(service.OfflineError) AddOfflineStatus(service.ListError, service.RefreshPacks);
            else AddStatus(Tr("TUF_PACK_API_ERROR", "Could not load TUF packs.") + "\n" + service.ListError, true, service.RefreshPacks);
        } else if(service.ListState == TufPackListState.Empty) {
            AddStatus(Tr("TUF_PACK_EMPTY", "No packs matched your search."), false, null);
        } else {
            foreach(TufPack pack in service.Packs) AddPackCard(pack);
            if(service.HasMore) {
                if(service.LoadingMore) AddLoadingStatus(Tr("TUF_PACK_LOADING", "Loading packs…"));
                else if(service.ListState == TufPackListState.Error) AddStatus(Tr("TUF_RETRY", "Retry"), true, service.LoadMore);
            }
            else if(service.Packs.Count > 0) AddStatus(Tr("TUF_END", "End of results"), false, null, 38f);
        }
    }
    private void RebuildDetail() {
        TufPack pack = service.SelectedPack;
        if(expandedPackId != pack.Id) {
            expandedPackId = pack.Id;
            expandedFolders.Clear();
        }
        AddBackRow(pack);
        if(service.DetailState == TufPackListState.Loading) {
            AddLoadingStatus(Tr("TUF_PACK_LOADING_LEVELS", "Loading pack levels…"));
        } else if(service.DetailState == TufPackListState.Error && service.PackLevels.Count == 0) {
            if(service.OfflineError) AddOfflineStatus(service.DetailError, service.RetryPackLevels);
            else AddStatus(Tr("TUF_PACK_LEVELS_ERROR", "Could not load this pack.") + "\n" + service.DetailError, true, service.RetryPackLevels);
        } else if(service.DetailState == TufPackListState.Empty) {
            AddStatus(Tr("TUF_PACK_NO_LEVELS", "This pack has no playable levels."), false, null);
        } else {
            AddLevelSortRow();
            RenderItems(service.PackItems, 0);
        }
    }
    private IReadOnlyList<TufPackItem> SortItems(IReadOnlyList<TufPackItem> items) {
        if(service.LevelSort == TufPackLevelSort.PackOrder) return items;
        List<TufPackItem> result = [.. items];
        List<int> slots = [];
        List<TufPackItem> levels = [];
        for(int i = 0; i < result.Count; i++) {
            if(result[i].IsFolder) continue;
            slots.Add(i);
            levels.Add(result[i]);
        }
        IEnumerable<TufPackItem> sorted = (service.LevelSort, service.LevelAscending) switch {
            (TufPackLevelSort.Difficulty, true) => levels.OrderBy(RankOf),
            (TufPackLevelSort.Difficulty, false) => levels.OrderByDescending(RankOf),
            (TufPackLevelSort.Clears, true) => levels.OrderBy(ClearsOf),
            _ => levels.OrderByDescending(ClearsOf),
        };
        int slot = 0;
        foreach(TufPackItem item in sorted) result[slots[slot++]] = item;
        return result;
    }
    private static int RankOf(TufPackItem item) => item.Level?.DifficultyRank ?? 0;
    private static int ClearsOf(TufPackItem item) => item.Level?.Clears ?? 0;
    private void RenderItems(IReadOnlyList<TufPackItem> items, int depth) {
        float indent = depth * 26f;
        foreach(TufPackItem item in SortItems(items)) {
            if(item.IsFolder) {
                AddFolderRow(item, indent);
                if(expandedFolders.Contains(item.Key)) RenderItems(item.Children, depth + 1);
            } else {
                RenderLevel(item.Level, indent);
            }
        }
    }
    private void RenderLevel(TufLevel level, float indent) {
        AddLevelCard(level, indent);
        if(level.State == TufItemState.ChooseChart && level.Charts != null) AddChartChooser(level, indent);
    }
    private void AddLevelSortRow() {
        RectTransform row = FixedRow("Level Sort", 36f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        AddLevelSortChip(row, TufPackLevelSort.PackOrder, "TUF_PACK_SORT_ORDER", "Pack Order", 108f);
        AddLevelSortChip(row, TufPackLevelSort.Difficulty, "TUF_SORT_DIFFICULTY", "Difficulty", 92f);
        AddLevelSortChip(row, TufPackLevelSort.Clears, "TUF_SORT_CLEARS", "Clears", 70f);
        (Image direction, TMP_Text directionText) = Chip(row, service.LevelAscending ? "↑" : "↓", 48f, service.ToggleLevelAscending);
        direction.color = service.LevelAscending ? UIColors.ObjectBG : UIColors.ObjectActive;
        directionText.color = new(1f, 1f, 1f, service.LevelSort == TufPackLevelSort.PackOrder ? 0.35f : 1f);
    }
    private void AddLevelSortChip(Transform parent, TufPackLevelSort sort, string key, string label, float width) {
        (Image image, TMP_Text text) = Chip(parent, label, width, () => service.SetLevelSort(sort));
        text.gameObject.AddComponent<TextLocalization>().Init(key, label);
        image.color = sort == service.LevelSort ? UIColors.ObjectActive : UIColors.ObjectBG;
    }
    private void AddFolderRow(TufPackItem folder, float indent) {
        bool expanded = expandedFolders.Contains(folder.Key);
        RectTransform row = IndentedRow("Folder " + folder.Name, 52f, indent);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.ObjectBG;
        RectTransform arrowRect = Rect("Arrow", row, new(0f, 0.5f), new(0f, 0.5f), Vector2.zero, Vector2.zero);
        arrowRect.sizeDelta = new(16f, 16f);
        arrowRect.anchoredPosition = new(22f, 0f);
        arrowRect.localEulerAngles = new(0f, 0f, expanded ? 0f : 90f);
        Image arrow = arrowRect.gameObject.AddComponent<Image>();
        arrow.sprite = MainCore.Spr.Get(UISprite.Triangle128);
        arrow.color = expanded ? UIColors.ObjectActive : UIColors.ObjectInactive;
        arrow.raycastTarget = false;
        TMP_Text name = Text(row, folder.Name, 18f, TextAlignmentOptions.Left);
        name.rectTransform.offsetMin = new(46f, 0f);
        name.rectTransform.offsetMax = new(-140f, 0f);
        name.fontStyle = FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(name);
        TMP_Text count = Text(row, string.Format(Tr("TUF_PACK_LEVEL_COUNT", "{0} levels"), folder.LevelCount), 14f, TextAlignmentOptions.Right);
        count.rectTransform.offsetMax = new(-18f, 0f);
        count.color = new(1f, 1f, 1f, 0.46f);
        GenerateUI.AddButton(row.gameObject, input => {
            if(input != PointerEventData.InputButton.Left) return;
            if(!expandedFolders.Add(folder.Key)) expandedFolders.Remove(folder.Key);
            listSignature = null;
            Rebuild();
        });
    }
    private RectTransform IndentedRow(string name, float height, float indent) {
        RectTransform row = FixedRow(name, height);
        if(indent <= 0f) return row;
        return Rect(name + " Inner", row, Vector2.zero, Vector2.one, new(indent, 0f), Vector2.zero);
    }
    private string BuildSignature() {
        StringBuilder sb = new();
        sb.Append(ShowPreviews ? 'P' : 'p').Append(service.OfflineError ? 'O' : 'o');
        if(service.SelectedPack != null) {
            sb.Append("D:").Append(service.SelectedPack.Id).Append('|')
                .Append((int)service.DetailState).Append('|').Append(service.IsBusy ? '1' : '0').Append('|')
                .Append((int)service.LevelSort).Append(service.LevelAscending ? '1' : '0').Append('|');
            foreach(long key in expandedFolders) sb.Append(key).Append('^');
            sb.Append('|');
            foreach(TufLevel level in service.PackLevels)
                sb.Append(level.Id).Append(':').Append((int)level.State)
                    .Append('#').Append(level.Charts?.Count ?? 0).Append(',');
        } else {
            sb.Append("L:").Append((int)service.ListState).Append('|')
                .Append(service.HasMore ? '1' : '0').Append(service.LoadingMore ? '1' : '0').Append('|');
            foreach(TufPack pack in service.Packs) sb.Append(pack.Id).Append(',');
        }
        return sb.ToString();
    }
    private void RefreshControls() {
        foreach((TufPackSort sort, Image image) in sortChips)
            image.color = sort == service.Sort ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionChip.color = service.Ascending ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionLabel.text = service.Ascending ? "↑" : "↓";
    }
    private void AddBackRow(TufPack pack) {
        RectTransform row = FixedRow("Back", 52f);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.ObjectBG;
        TMP_Text back = Text(row, "←", 22f, TextAlignmentOptions.Left);
        back.rectTransform.offsetMin = new(20f, 0f);
        back.rectTransform.offsetMax = new(-20f, 0f);
        TMP_Text name = Text(row, pack.Name, 19f, TextAlignmentOptions.Left);
        name.rectTransform.offsetMin = new(52f, 0f);
        name.rectTransform.offsetMax = new(-180f, 0f);
        name.fontStyle = FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(name);
        TMP_Text count = Text(row, string.Format(Tr("TUF_PACK_LEVEL_COUNT", "{0} levels"), pack.LevelCount), 15f, TextAlignmentOptions.Right);
        count.rectTransform.offsetMin = new(0f, 0f);
        count.rectTransform.offsetMax = new(-20f, 0f);
        count.color = new(1f, 1f, 1f, 0.46f);
        GenerateUI.AddButton(row.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) service.ClosePack();
        });
    }
    private void AddPackCard(TufPack pack) {
        RectTransform card = FixedRow("Pack " + pack.Id, 88f);
        Image bg = card.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.12f);
        if(ShowPreviews) previews.Attach(card, "pack-" + pack.Id, TufPreviewSource.ForPack(pack.IconUrl, pack.FirstLevelId));
        RectTransform nameRect = Rect("Name", card, new(0f, 1f), new(1f, 1f), new(22f, -46f), new(-22f, -12f));
        TMP_Text name = Text(nameRect, pack.Name, 22f, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(name);
        string preview = pack.Preview.Count > 0 ? "  ·  " + string.Join(", ", pack.Preview) : "";
        RectTransform metaRect = Rect("Metadata", card, new(0f, 0f), new(1f, 0f), new(22f, 10f), new(-22f, 46f));
        TMP_Text meta = Text(metaRect,
            string.Format(Tr("TUF_PACK_LEVEL_COUNT", "{0} levels"), pack.LevelCount)
                + $"  ·  {pack.Owner}  ·  ♥ {pack.Favorites:N0}" + preview,
            15f, TextAlignmentOptions.Left);
        meta.color = new(1f, 1f, 1f, 0.46f);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(meta);
        GenerateUI.AddButton(card.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) service.OpenPack(pack);
        });
    }
    private void AddLevelCard(TufLevel level, float indent) {
        RectTransform card = IndentedRow("Level " + level.Id, 94f, indent);
        Image bg = card.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.12f);
        if(ShowPreviews) previews.Attach(card, level.Id.ToString(), TufPreviewSource.Video(level.VideoLink));
        RectTransform rail = Rect("Difficulty Rail", card, new(0f, 0f), new(0f, 1f), new(5f, 8f), new(11f, -8f));
        Image railImage = rail.gameObject.AddComponent<Image>();
        railImage.sprite = MainCore.Spr.GetFilled(2f);
        railImage.type = Image.Type.Sliced;
        railImage.color = ColorUtility.TryParseHtmlString(level.DifficultyColor, out Color color) ? color : Color.white;
        RectTransform idRect = Rect("Id", card, new(0f, 1f), new(0f, 1f), new(22f, -35f), new(108f, -8f));
        TMP_Text id = Text(idRect, $"#{level.Id}", 16f, TextAlignmentOptions.Left);
        id.color = new(1f, 1f, 1f, 0.48f);
        RectTransform diffRect = Rect("Difficulty", card, new(0f, 1f), new(0f, 1f), new(104f, -35f), new(235f, -8f));
        TMP_Text diff = Text(diffRect, level.Difficulty, 16f, TextAlignmentOptions.Left);
        diff.color = railImage.color;
        RectTransform songRect = Rect("Song", card, new(0f, 1f), new(1f, 1f), new(22f, -66f), new(-150f, -34f));
        TMP_Text song = Text(songRect, level.Song, 23f, TextAlignmentOptions.Left);
        song.fontStyle = FontStyles.Bold;
        song.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(song);
        RectTransform metaRect = Rect("Metadata", card, new(0f, 0f), new(1f, 0f), new(22f, 8f), new(-150f, 34f));
        TMP_Text meta = Text(metaRect, $"{level.Artist}  ·  {level.Creator}  ·  ✓ {level.Clears:N0}  ♥ {level.Likes:N0}", 15f, TextAlignmentOptions.Left);
        meta.color = new(1f, 1f, 1f, 0.46f);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(meta);
        AddAction(card, level);
    }
    private void AddAction(RectTransform card, TufLevel level) {
        RectTransform action = Rect("Action", card, new(1f, 0.5f), new(1f, 0.5f), new(-138f, -23f), new(-10f, 23f));
        Image image = action.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        bool actionable = level.State is not TufItemState.Unavailable and not TufItemState.Downloading
                and not TufItemState.Extracting and not TufItemState.Loading
            || (level.State == TufItemState.Unavailable && TufMainLevel.Resolve(level, out _) != TufMainLevel.TufMainAction.None);
        bool enabled = actionable && !service.IsBusy;
        image.color = enabled ? UIColors.ObjectButton : Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.25f);
        TMP_Text label = Text(action, ActionLabel(level), 15f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, enabled ? 1f : 0.5f);
        cardLabels[level.Id] = label;
        if(enabled) GenerateUI.AddButton(action.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) service.Act(level);
        });
        if(!string.IsNullOrWhiteSpace(level.Error)) action.AddToolTip(level.Error.Length > 900 ? level.Error[..900] + "…" : level.Error);
    }
    private string ActionLabel(TufLevel level) => level.State switch {
        TufItemState.Downloading => level.Progress < 0
            ? Tr("TUF_DOWNLOADING", "Downloading…")
            : string.Format(Tr("TUF_DOWNLOADING_PROGRESS", "Downloading {0}%"), Mathf.Clamp((int)(level.Progress * 100f), 0, 100)),
        TufItemState.Extracting => Tr("TUF_EXTRACTING", "Extracting…"),
        TufItemState.Loading => Tr("TUF_LOADING_LEVEL", "Loading…"),
        TufItemState.Load => Tr("TUF_LOAD", "Load"),
        TufItemState.Retry => Tr("TUF_RETRY", "Retry"),
        TufItemState.Unavailable => TufMainLevel.Resolve(level, out _) switch {
            TufMainLevel.TufMainAction.Play => Tr("TUF_PLAY", "Play"),
            TufMainLevel.TufMainAction.BuyDlc => Tr("TUF_BUY_DLC", "Buy DLC"),
            _ => Tr("TUF_UNAVAILABLE", "Unavailable"),
        },
        TufItemState.ChooseChart => Tr("TUF_CANCEL", "Cancel"),
        _ => Tr("TUF_DOWNLOAD", "Download")
    };
    private void AddChartChooser(TufLevel level, float indent = 0f) {
        if(level?.Charts == null) return;
        GTweenSequenceBuilder animation = GTweenSequenceBuilder.New();
        int index = 0;
        foreach(string chart in level.Charts) {
            string display = ChartDisplayName(level, chart);
            RectTransform row = IndentedRow("Chart " + display, 40f, indent);
            CanvasGroup fade = row.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            bg.type = Image.Type.Sliced;
            bg.color = UIColors.ObjectBG;
            TMP_Text label = Text(row, "▶  " + display, 15f, TextAlignmentOptions.Left);
            label.rectTransform.offsetMin = new(40f, 0f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            TextCompat.NoWrap(label);
            GenerateUI.AddButton(row.gameObject, input => {
                if(input == PointerEventData.InputButton.Left) service.LaunchChart(level, chart);
            });
            float delay = index++ * 0.035f;
            animation.JoinSequence(sequence => {
                if(delay > 0f) sequence.AppendTime(delay);
                sequence.Append(fade.GTAlpha(1f, 0.18f).SetEasing(Easing.OutSine));
            });
        }
        chartChooserSeq = animation.Build();
        MainCore.TC.Play(chartChooserSeq);
    }
    private static string ChartDisplayName(TufLevel level, string chart) {
        try {
            return string.IsNullOrEmpty(level.ChartsRoot)
                ? Path.GetFileName(chart)
                : Path.GetRelativePath(level.ChartsRoot, chart);
        } catch { return Path.GetFileName(chart); }
    }
    private void AddLoadingStatus(string message, float height = 70f) {
        RectTransform row = FixedRow("Loading", height);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.35f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
        UISpinner spinner = UISpinner.Attach(row, 26f, new Color(1f, 1f, 1f, 0.55f));
        LayoutElement spinnerSize = spinner.gameObject.AddComponent<LayoutElement>();
        spinnerSize.minWidth = spinnerSize.preferredWidth = 26f;
        spinnerSize.minHeight = spinnerSize.preferredHeight = 26f;
        TMP_Text label = Text(row, message, 18f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, 0.48f);
    }
    private void AddOfflineStatus(string detail, Action retry) {
        int installed = TufService.Instance?.InstalledCount ?? 0;
        AddStatus(Tr("TUF_OFFLINE", "TUF could not be reached — you may be offline.") + "\n" + detail, false, null, 78f);
        RectTransform row = FixedRow("Offline Actions", 58f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = new RectOffset(0, 0, 6, 6);
        (Image switchChip, TMP_Text switchLabel) = Chip(row, "", 264f, () => {
            TufService.Instance?.ShowInstalledLevels();
            MenuFactory.SetState((int)OriginalMenuState.NostalgiaTuf);
        });
        switchChip.color = installed > 0 ? UIColors.ObjectActive : UIColors.ObjectBG;
        switchLabel.text = installed > 0
            ? string.Format(Tr("TUF_OFFLINE_SWITCH", "Switch to Installed ({0})"), installed)
            : Tr("TUF_OFFLINE_SWITCH_EMPTY", "Switch to Installed");
        (Image _, TMP_Text retryLabel) = Chip(row, Tr("TUF_RETRY", "Retry"), 96f, retry);
        retryLabel.color = new(1f, 1f, 1f, 0.82f);
    }
    private void AddStatus(string message, bool button, Action action, float height = 70f) {
        RectTransform row = FixedRow("Status", height);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = button ? UIColors.ObjectBG : Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.35f);
        TMP_Text label = Text(row, message, 18f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, button ? 0.9f : 0.48f);
        if(action != null) GenerateUI.AddButton(row.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) action();
        });
    }
    private RectTransform FixedRow(string name, float height) {
        RectTransform row = Rect(name, content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
        size.minHeight = height;
        size.preferredHeight = height;
        return row;
    }
    private static (Image, TMP_Text) Chip(Transform parent, string value, float width, Action action) {
        RectTransform rect = Rect("Chip " + value, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        image.color = UIColors.ObjectBG;
        TMP_Text label = Text(rect, value, 17f, TextAlignmentOptions.Center);
        GenerateUI.AddButton(rect.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) action?.Invoke();
        });
        return (image, label);
    }
    private static void AddHorizontal(Transform row, float spacing = 8f) {
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
    }
    private static TMP_Text Text(Transform parent, string value, float size, TextAlignmentOptions align) {
        TextMeshProUGUI text = GenerateUI.AddText(parent, true);
        text.text = value;
        text.font = FontManager.Current;
        text.fontSize = size;
        text.alignment = align;
        text.richText = false;
        SetFull(text.rectTransform, 0f, 0f);
        return text;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }
    private static void SetFull(RectTransform rect, float padX, float padY) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new(padX, padY);
        rect.offsetMax = new(-padX, -padY);
    }
    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
    private void OnDestroy() {
        viewSwitchSeq?.Kill();
        chartChooserSeq?.Kill();
        previews?.Dispose();
        if(service != null) service.Changed -= Rebuild;
    }
}
