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
public static class TufBrowserUI {
    public static void Create(RectTransform parent) {
        TufBrowserView view = parent.gameObject.AddComponent<TufBrowserView>();
        view.Build(parent);
    }
}
internal sealed class TufBrowserView : MonoBehaviour {
    private TufService service;
    private RectTransform content;
    private UIScrollController scroll;
    private TMP_InputField search;
    private readonly List<(TufSort Sort, Image Image)> sortChips = [];
    private readonly List<(string Name, Image Image)> difficultyChips = [];
    private Image directionChip;
    private TMP_Text directionLabel;
    private TufDifficultyRangeBar difficultyRange;
    private TufDifficultyRangeBar quantumRange;
    private RectTransform quantumRow;
    private RectTransform viewport;
    private RectTransform specialChecks;
    private CanvasGroup specialChecksCg;
    private RectTransform specialArrowRect;
    private Image specialArrow;
    private GTween specialArrowSeq;
    private GTween filterLayoutSeq;
    private GTween chartChooserSeq;
    private bool specialExpanded;
    private bool lastQuantumOn;
    private float quantumLayout;
    private float specialChecksScale = 0.82f;
    private const float ArmSeconds = 4f;
    private const float MetaGap = 12f;
    private readonly Dictionary<int, TMP_Text> cardLabels = [];
    private readonly Dictionary<int, Image> deleteChips = [];
    private TufPreviewGroup previews;
    private Image installedChip;
    private TMP_Text installedLabel;
    private string listSignature;
    private bool built;
    private bool pendingRebuild;
    private int armedDeleteId;
    private float armedUntil;
    private void OnEnable() {
        if(!pendingRebuild) return;
        pendingRebuild = false;
        Rebuild();
    }
    public void Build(RectTransform parent) {
        service = TufService.Instance;
        if(service == null) return;
        RectTransform pad = Rect("TUF Browser", parent, Vector2.zero, Vector2.one, new(18f, 18f), new(-18f, -18f));
        BuildHeader(pad);
        viewport = Rect("Level Viewport", pad, Vector2.zero, Vector2.one, Vector2.zero, new(0f, -266f));
        viewport.gameObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        lastQuantumOn = service.QuantumEnabled;
        quantumLayout = lastQuantumOn ? 1f : 0f;
        ApplyFilterLayout();
        content = Rect("Level Cards", viewport, new(0f, 1f), new(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new(0.5f, 1f);
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
        TMP_Text title = Text(titleRect, "TUF", 28f, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<TextLocalization>().Init("TUF", "TUF");
        RectTransform taglineRect = Rect("Tagline", titleRect, new(0f, 0f), new(1f, 1f), new(78f, 4f), new(0f, 0f));
        TMP_Text tagline = Text(taglineRect, "Browse community levels, download them, then load them in the editor.", 14f, TextAlignmentOptions.Left);
        tagline.color = new(1f, 1f, 1f, 0.42f);
        tagline.gameObject.AddComponent<TextLocalization>().Init("TUF_TAGLINE", tagline.text);
        RectTransform searchRow = Rect("Search Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -78f), new(0f, -42f));
        AddHorizontal(searchRow);
        BuildSearch(searchRow);
        (Image refresh, TMP_Text refreshLabel) = Chip(searchRow, "Refresh", 92f, service.Refresh);
        refreshLabel.gameObject.AddComponent<TextLocalization>().Init("TUF_REFRESH", "Refresh");
        RectTransform sortRow = Rect("Sort Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -126f), new(0f, -90f));
        AddHorizontal(sortRow);
        AddSortChip(sortRow, TufSort.Recent, "TUF_SORT_RECENT", "Recent", 76f);
        AddSortChip(sortRow, TufSort.Difficulty, "TUF_SORT_DIFFICULTY", "Difficulty", 92f);
        AddSortChip(sortRow, TufSort.Clears, "TUF_SORT_CLEARS", "Clears", 70f);
        AddSortChip(sortRow, TufSort.Likes, "TUF_SORT_LIKES", "Likes", 64f);
        (directionChip, directionLabel) = Chip(sortRow, "↓", 48f, service.ToggleAscending);
        (installedChip, installedLabel) = Chip(sortRow, "Installed", 96f, () => {
            DisarmDelete();
            service.ToggleInstalled();
        });
        installedLabel.gameObject.AddComponent<TextLocalization>().Init("TUF_INSTALLED", "Installed");
        installedChip.rectTransform.AddToolTip("DESC_TUF_INSTALLED",
            "Show only the levels you have downloaded, newest first. Works offline.");
        AddFlexibleSpacer(sortRow);
        BuildDifficultyChips(sortRow);
        RectTransform rangeRow = Rect("Difficulty Range", parent, new(0f, 1f), new(1f, 1f), new(0f, -186f), new(0f, -130f));
        difficultyRange = TufDifficultyRangeBar.Create(rangeRow, service.MinDifficultyIndex,
            service.MaxDifficultyIndex, service.SetDifficultyRange);
        quantumRow = Rect("Quantum Range", parent, new(0f, 1f), new(1f, 1f), new(0f, -256f), new(0f, -192f));
        quantumRange = TufDifficultyRangeBar.CreateQuantum(quantumRow, sortRow, service.QuantumEnabled,
            service.QuantumMinIndex, service.QuantumMaxIndex, service.SetQuantumRange, service.ClearQuantum);
    }
    private void BuildDifficultyChips(Transform parent) {
        RectTransform host = Rect("Special Dropleft", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement hostSize = host.gameObject.AddComponent<LayoutElement>();
        hostSize.minWidth = hostSize.preferredWidth = 94f;
        RectTransform button = Rect("Special Button", host, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image buttonBg = button.gameObject.AddComponent<Image>();
        buttonBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        buttonBg.type = Image.Type.Sliced;
        buttonBg.color = UIColors.ObjectBG;
        TMP_Text label = Text(button, "Special", 14f, TextAlignmentOptions.Left);
        label.rectTransform.offsetMin = new(38f, 0f);
        label.rectTransform.offsetMax = new(-12f, 0f);
        label.color = new(1f, 1f, 1f, 0.7f);
        label.raycastTarget = false;
        label.gameObject.AddComponent<TextLocalization>().Init("TUF_SPECIAL", "Special");
        specialArrowRect = Rect("Arrow", button, new(0f, 0.5f), new(0f, 0.5f), Vector2.zero, Vector2.zero);
        specialArrowRect.sizeDelta = new(18f, 18f);
        specialArrowRect.anchoredPosition = new(16f, 0f);
        specialArrowRect.localEulerAngles = new(0f, 0f, 90f);
        specialArrow = specialArrowRect.gameObject.AddComponent<Image>();
        specialArrow.sprite = MainCore.Spr.Get(UISprite.Triangle128);
        specialArrow.color = UIColors.ObjectInactive;
        specialArrow.raycastTarget = false;
        GenerateUI.AddButton(button.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) ToggleSpecialDropdown();
        });
        button.AddToolTip("TUF_SPECIAL", "Special difficulties");
        specialChecks = Rect("Special Options", host, new(0f, 0f), new(0f, 1f), new(2f, 0f), new(2f, 0f));
        specialChecks.pivot = new(1f, 0.5f);
        specialChecks.localScale = new(0.82f, 1f, 1f);
        Image checksBackdrop = specialChecks.gameObject.AddComponent<Image>();
        checksBackdrop.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        checksBackdrop.type = Image.Type.Sliced;
        checksBackdrop.color = UIColors.PanelBG;
        HorizontalLayoutGroup checksLayout = AddHorizontal(specialChecks, 6f);
        checksLayout.padding = new RectOffset(6, 6, 0, 0);
        ContentSizeFitter checksFit = specialChecks.gameObject.AddComponent<ContentSizeFitter>();
        checksFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        specialChecksCg = specialChecks.gameObject.AddComponent<CanvasGroup>();
        specialChecksCg.alpha = 0f;
        specialChecksCg.blocksRaycasts = false;
        specialChecksCg.interactable = false;
        AddDifficultyCheckbox(specialChecks, "Unranked", "TUF_SPECIAL_UNRANKED", "Unranked", 116f);
        AddDifficultyCheckbox(specialChecks, "Censored", "TUF_SPECIAL_CENSORED", "Censored", 114f);
        AddDifficultyCheckbox(specialChecks, "Impossible", "TUF_SPECIAL_IMPOSSIBLE", "Impossible", 122f);
    }
    private void ToggleSpecialDropdown() {
        specialExpanded = !specialExpanded;
        specialChecksCg.blocksRaycasts = specialExpanded;
        specialChecksCg.interactable = specialExpanded;
        specialArrowSeq?.Kill();
        specialArrowSeq = GTweenSequenceBuilder.New()
            .Join(specialArrowRect.GTRotate(new Vector3(0f, 0f, specialExpanded ? -90f : 90f), 0.45f)
                .SetEasing(specialExpanded ? Easing.OutBounce : Easing.OutBack))
            .Join(specialArrow.GTColor(specialExpanded ? UIColors.ObjectActive : UIColors.ObjectInactive, 0.2f)
                .SetEasing(Easing.OutSine))
            .Join(specialChecksCg.GTAlpha(specialExpanded ? 1f : 0f, specialExpanded ? 0.14f : 0.16f).SetEasing(Easing.OutSine))
            .Join(GTweens.Extensions.GTweenExtensions.Tween(
                () => specialChecksScale,
                value => {
                    specialChecksScale = value;
                    specialChecks.localScale = new Vector3(value, 1f, 1f);
                },
                specialExpanded ? 1f : 0.82f,
                specialExpanded ? 0.42f : 0.18f)
                .SetEasing(specialExpanded ? Easing.OutBack : Easing.OutSine))
            .Build();
        MainCore.TC.Play(specialArrowSeq);
    }
    private void ApplyFilterLayout() {
        if(viewport == null || quantumRow == null) return;
        float qShift = (1f - quantumLayout) * 64f;
        quantumRow.offsetMin = new(0f, -256f + qShift);
        viewport.offsetMax = new(0f, -266f + qShift);
    }
    private void AnimateFilterLayout() {
        filterLayoutSeq?.Kill();
        filterLayoutSeq = GTweenSequenceBuilder.New()
            .Join(GTweens.Extensions.GTweenExtensions.Tween(
                () => quantumLayout,
                x => { quantumLayout = x; ApplyFilterLayout(); },
                lastQuantumOn ? 1f : 0f,
                0.16f).SetEasing(Easing.OutSine))
            .Build();
        MainCore.TC.Play(filterLayoutSeq);
    }
    private void AddDifficultyCheckbox(Transform parent, string name, string key, string label, float width) {
        RectTransform cell = Rect("Check " + name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = cell.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        Image cellBg = cell.gameObject.AddComponent<Image>();
        cellBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        cellBg.type = Image.Type.Sliced;
        cellBg.color = new(1f, 1f, 1f, 0.05f);
        RectTransform box = Rect("Box", cell, new(0f, 0.5f), new(0f, 0.5f), Vector2.zero, Vector2.zero);
        box.sizeDelta = new(18f, 18f);
        box.anchoredPosition = new(19f, 0f);
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        boxImage.type = Image.Type.Sliced;
        boxImage.color = new(1f, 1f, 1f, 0.5f);
        boxImage.raycastTarget = false;
        RectTransform fill = Rect("Fill", box, Vector2.zero, Vector2.one, new(4f, 4f), new(-4f, -4f));
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        fillImage.type = Image.Type.Sliced;
        fillImage.color = new(1f, 1f, 1f, 0f);
        fillImage.raycastTarget = false;
        TMP_Text text = Text(cell, label, 14f, TextAlignmentOptions.Left);
        text.rectTransform.offsetMin = new(38f, 0f);
        text.rectTransform.offsetMax = new(-10f, 0f);
        text.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(text);
        text.gameObject.AddComponent<TextLocalization>().Init(key, label);
        GenerateUI.AddButton(cell.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) service.ToggleSpecialDifficulty(name);
        });
        cell.AddToolTip(name);
        difficultyChips.Add((name, fillImage));
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
        TMP_Text placeholder = Text(textArea, "Search levels…", 17f, TextAlignmentOptions.Left);
        TextCompat.NoWrap(placeholder);
        placeholder.color = new(1f, 1f, 1f, 0.28f);
        placeholder.gameObject.AddComponent<TextLocalization>().Init("TUF_SEARCH_PLACEHOLDER", "Search levels…");
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
        if(armedDeleteId != 0 && Time.unscaledTime >= armedUntil) DisarmDelete();
        if(!service.HasMore || service.LoadingMore || service.State != TufListState.Ready) return;
        float max = content.rect.height - viewport.rect.height;
        if(max <= 0f || content.anchoredPosition.y >= max - 400f) service.LoadMore();
    }
    private void ResetSearchScroll() {
        if(search == null || search.textComponent == null) return;
        search.textComponent.rectTransform.anchoredPosition =
            new(0f, search.textComponent.rectTransform.anchoredPosition.y);
    }
    private void AddSortChip(Transform parent, TufSort sort, string key, string label, float width) {
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
            foreach(TufLevel level in service.Levels) {
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
        deleteChips.Clear();
        if(service.State == TufListState.Loading) {
            AddLoadingStatus(Tr("TUF_LOADING", "Loading levels…"));
        } else if(service.State == TufListState.Error && service.Levels.Count == 0) {
            AddStatus(Tr("TUF_API_ERROR", "Could not load TUF levels.") + "\n" + service.Error, true, service.Refresh);
        } else if(service.State == TufListState.Empty) {
            AddStatus(EmptyMessage(), false, null);
        } else {
            foreach(TufLevel level in service.Levels) {
                AddCard(level);
                if(level.State == TufItemState.ChooseChart && level.Charts != null) AddChartChooser(level);
            }
            if(service.HasMore) {
                if(service.LoadingMore) AddLoadingStatus(Tr("TUF_LOADING", "Loading levels…"));
                else if(service.State == TufListState.Error) AddStatus(Tr("TUF_RETRY", "Retry"), true, service.LoadMore);
            }
            else if(service.Levels.Count > 0)
                AddStatus(service.ShowInstalled
                    ? string.Format(Tr("TUF_INSTALLED_COUNT", "{0} level(s) in your library"), service.Levels.Count)
                    : Tr("TUF_END", "End of results"), false, null, 38f);
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.ScrollTo(oldY);
    }
    private string BuildSignature() {
        StringBuilder sb = new();
        sb.Append((int)service.State).Append('|')
            .Append(service.HasMore ? '1' : '0')
            .Append(service.LoadingMore ? '1' : '0')
            .Append(service.IsBusy ? '1' : '0')
            .Append(service.ShowInstalled ? '1' : '0')
            .Append(service.ShowPreviews ? '1' : '0').Append('|');
        foreach(TufLevel level in service.Levels)
            sb.Append(level.Id).Append(':').Append((int)level.State)
                .Append(level.InstallFolder == null ? '-' : '+')
                .Append(string.IsNullOrEmpty(level.Error) ? '-' : '!')
                .Append('#').Append(level.Charts?.Count ?? 0).Append(',');
        return sb.ToString();
    }
    private string EmptyMessage() {
        if(!service.ShowInstalled) return Tr("TUF_EMPTY", "No levels matched your search.");
        return string.IsNullOrEmpty(service.Query)
            ? Tr("TUF_INSTALLED_EMPTY", "You have not downloaded any levels yet.")
            : Tr("TUF_INSTALLED_NO_MATCH", "No downloaded level matched your search.");
    }
    private void RefreshControls() {
        foreach((TufSort sort, Image image) in sortChips)
            image.color = sort == service.Sort ? UIColors.ObjectActive : UIColors.ObjectBG;
        if(installedChip != null)
            installedChip.color = service.ShowInstalled ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionChip.color = service.Ascending ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionLabel.text = service.Ascending ? "↑" : "↓";
        difficultyRange?.SetRange(service.MinDifficultyIndex, service.MaxDifficultyIndex);
        quantumRange?.SetQuantum(service.QuantumEnabled, service.QuantumMinIndex, service.QuantumMaxIndex);
        if(service.QuantumEnabled != lastQuantumOn) {
            lastQuantumOn = service.QuantumEnabled;
            AnimateFilterLayout();
        }
        foreach((string name, Image fill) in difficultyChips)
            fill.color = service.DifficultyFilter.IsSelected(name)
                ? UIColors.ObjectActive : new Color(1f, 1f, 1f, 0f);
    }
    private void AddCard(TufLevel level) {
        RectTransform card = FixedRow("Level " + level.Id, 94f);
        Image bg = card.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.12f);
        if(service.ShowPreviews) previews.Attach(card, level.Id.ToString(), TufPreviewSource.Video(level.VideoLink));
        RectTransform rail = Rect("Difficulty Rail", card, new(0f, 0f), new(0f, 1f), new(5f, 8f), new(11f, -8f));
        Image railImage = rail.gameObject.AddComponent<Image>();
        railImage.sprite = MainCore.Spr.GetFilled(2f);
        railImage.type = Image.Type.Sliced;
        railImage.color = ColorUtility.TryParseHtmlString(level.DifficultyColor, out Color color) ? color : Color.white;
        float x = 22f;
        TMP_Text id = MetaLabel(card, "Id", $"#{level.Id}", ref x, 90f);
        id.color = new(1f, 1f, 1f, 0.48f);
        x += MetaGap;
        TMP_Text diff = MetaLabel(card, "Difficulty", level.Difficulty, ref x, 150f);
        diff.color = railImage.color;
        bool installed = IsInstalled(level);
        if(installed) AddInstalledBadge(card, x + MetaGap);
        float textRight = installed ? -204f : -150f;
        RectTransform songRect = Rect("Song", card, new(0f, 1f), new(1f, 1f), new(22f, -66f), new(textRight, -34f));
        string song = string.IsNullOrEmpty(level.Song) ? Tr("TUF_UNKNOWN_LEVEL", "Level") + " #" + level.Id : level.Song;
        TMP_Text songText = Text(songRect, song, 23f, TextAlignmentOptions.Left);
        songText.fontStyle = FontStyles.Bold;
        songText.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(songText);
        RectTransform metaRect = Rect("Metadata", card, new(0f, 0f), new(1f, 0f), new(22f, 8f), new(textRight, 34f));
        TMP_Text meta = Text(metaRect, CardMeta(level), 15f, TextAlignmentOptions.Left);
        meta.color = new(1f, 1f, 1f, 0.46f);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(meta);
        AddAction(card, level);
        if(installed) AddDelete(card, level);
    }
    private string CardMeta(TufLevel level) {
        if(string.IsNullOrEmpty(level.Artist) && string.IsNullOrEmpty(level.Creator))
            return Tr("TUF_INSTALLED_UNKNOWN", "Downloaded before Quartz tracked level details.");
        return $"{level.Artist}  ·  {level.Creator}  ·  ✓ {level.Clears:N0}  ♥ {level.Likes:N0}";
    }
    private bool IsInstalled(TufLevel level) =>
        level.InstallFolder != null
        && level.State is not TufItemState.Downloading and not TufItemState.Extracting
            and not TufItemState.Loading;
    private static TMP_Text MetaLabel(RectTransform card, string name, string value, ref float x, float maxWidth) {
        RectTransform rect = Rect(name, card, new(0f, 1f), new(0f, 1f), new(x, -35f), new(x, -8f));
        TMP_Text text = Text(rect, value, 16f, TextAlignmentOptions.Left);
        text.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(text);
        float width = Mathf.Min(Mathf.Ceil(text.GetPreferredValues(value).x), maxWidth);
        rect.offsetMax = new(x + width, -8f);
        x += width;
        return text;
    }
    private void AddInstalledBadge(RectTransform card, float x) {
        string value = Tr("TUF_INSTALLED", "Installed");
        RectTransform badge = Rect("Installed Badge", card, new(0f, 1f), new(0f, 1f), new(x, -33f), new(x, -10f));
        Image image = badge.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        image.color = new(0.38f, 0.78f, 0.52f, 0.22f);
        image.raycastTarget = false;
        TMP_Text label = Text(badge, value, 13f, TextAlignmentOptions.Center);
        label.color = new(0.62f, 0.92f, 0.72f, 0.95f);
        label.raycastTarget = false;
        badge.offsetMax = new(x + Mathf.Ceil(label.GetPreferredValues(value).x) + 22f, -10f);
    }
    private void AddDelete(RectTransform card, TufLevel level) {
        RectTransform button = Rect("Delete", card, new(1f, 0.5f), new(1f, 0.5f), new(-192f, -23f), new(-146f, 23f));
        Image image = button.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        bool enabled = !service.IsBusy;
        image.color = DeleteColor(armedDeleteId == level.Id, enabled);
        RectTransform iconRect = Rect("Icon", button, new(0.5f, 0.5f), new(0.5f, 0.5f), new(-11f, -11f), new(11f, 11f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = MainCore.Spr.Get(UISprite.Trash128, 22f);
        icon.color = new(1f, 1f, 1f, enabled ? 0.95f : 0.45f);
        icon.raycastTarget = false;
        deleteChips[level.Id] = image;
        if(!enabled) return;
        GenerateUI.AddButton(button.gameObject, input => {
            if(input != PointerEventData.InputButton.Left) return;
            if(armedDeleteId == level.Id) {
                DisarmDelete();
                service.DeleteInstalled(level);
                return;
            }
            armedDeleteId = level.Id;
            armedUntil = Time.unscaledTime + ArmSeconds;
            RefreshDeleteChips();
        });
        button.AddToolTip("DESC_TUF_DELETE", "Delete this level from your library. Click it twice to confirm; the level can be downloaded again.");
    }
    private static Color DeleteColor(bool armed, bool enabled) => armed
        ? new Color(0.86f, 0.31f, 0.33f, 0.92f)
        : Color.Lerp(UIColors.ObjectBG, new Color(0.86f, 0.31f, 0.33f, 1f), enabled ? 0.22f : 0.08f);
    private void DisarmDelete() {
        if(armedDeleteId == 0) return;
        armedDeleteId = 0;
        RefreshDeleteChips();
    }
    private void RefreshDeleteChips() {
        foreach(TufLevel level in service.Levels)
            if(deleteChips.TryGetValue(level.Id, out Image image) && image != null)
                image.color = DeleteColor(armedDeleteId == level.Id, !service.IsBusy);
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
    private void AddChartChooser(TufLevel level) {
        if(level?.Charts == null) return;
        GTweenSequenceBuilder animation = GTweenSequenceBuilder.New();
        int index = 0;
        foreach(string chart in level.Charts) {
            string display = ChartDisplayName(level, chart);
            RectTransform row = FixedRow("Chart " + display, 40f);
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
        HorizontalLayoutGroup layout = AddHorizontal(row, 12f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = false;
        UISpinner spinner = UISpinner.Attach(row, 26f, new Color(1f, 1f, 1f, 0.55f));
        LayoutElement spinnerSize = spinner.gameObject.AddComponent<LayoutElement>();
        spinnerSize.minWidth = spinnerSize.preferredWidth = 26f;
        spinnerSize.minHeight = spinnerSize.preferredHeight = 26f;
        TMP_Text label = Text(row, message, 18f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, 0.48f);
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
    private static HorizontalLayoutGroup AddHorizontal(Transform row, float spacing = 8f) {
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        return layout;
    }
    private static void AddFlexibleSpacer(Transform row) {
        RectTransform spacer = Rect("Spacer", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
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
    private static void SetFull(RectTransform rect, float left, float right) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new(left, 0f);
        rect.offsetMax = new(-right, 0f);
    }
    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
    private void OnDestroy() {
        if(service != null) service.Changed -= Rebuild;
        previews?.Dispose();
        filterLayoutSeq?.Kill();
        specialArrowSeq?.Kill();
        chartChooserSeq?.Kill();
    }
}
