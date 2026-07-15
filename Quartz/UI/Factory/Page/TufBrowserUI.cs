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
    // Space between the id, difficulty and Installed badge on a card's top row.
    private const float MetaGap = 12f;
    private readonly Dictionary<int, TMP_Text> cardLabels = [];
    private readonly Dictionary<int, TMP_Text> deleteLabels = [];
    private readonly Dictionary<int, Image> deleteChips = [];
    private Image installedChip;
    private TMP_Text installedLabel;
    private string listSignature;
    private bool built;
    private bool pendingRebuild;
    // The card whose Delete is armed, and when it disarms. Confirmation lives here
    // rather than in a modal: one card can be armed at a time, and walking away
    // (or touching anything else) cancels it.
    private int armedDeleteId;
    private float armedUntil;

    // Hidden pages are deactivated; downloads still tick service.Changed. Defer the
    // list rebuild (forced layout passes + scroll restore need an active hierarchy)
    // until the page is shown again.
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
        // 64 tall expanded: the slider handles rise ~5px above the gradient track, so the
        // track band (bottom 28px) needs extra clearance from the toggle button up top.
        quantumRow = Rect("Quantum Range", parent, new(0f, 1f), new(1f, 1f), new(0f, -256f), new(0f, -192f));
        quantumRange = TufDifficultyRangeBar.CreateQuantum(quantumRow, sortRow, service.QuantumEnabled,
            service.QuantumMinIndex, service.QuantumMaxIndex, service.SetQuantumRange, service.ClearQuantum);
    }

    // Compact horizontal flyout in the sort row. Options use the flexible middle
    // space to the trigger's left, staying clear of the adjacent Quantum button.
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

        // Anchored to the host's left edge with a right pivot and content-sized
        // width, so the checkbox pills always end exactly beside the Special button
        // no matter how wide the localized labels make them.
        specialChecks = Rect("Special Options", host, new(0f, 0f), new(0f, 1f), new(2f, 0f), new(2f, 0f));
        specialChecks.pivot = new(1f, 0.5f);
        specialChecks.localScale = new(0.82f, 1f, 1f);
        // The flyout overlays the sort chips to its left; an opaque backdrop keeps
        // them from bleeding through the translucent checkbox pills while open.
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
        // Open bounces (overshoot past full size and settle); close is a quick clean
        // fade-out — bounce on exit reads as lag, not playfulness.
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

    // The quantum row gives back its entire height while the toggle is off — no
    // residual blank band, no stranded value label.
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

    // A checkbox toggle (box + fill + label) in the style of TUFHelper's special
    // difficulty list. The stored fill image is what RefreshControls tints on/off.
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
        text.textWrappingMode = TextWrappingModes.NoWrap;
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
        // The viewport must be a rect that holds ONLY the text (padding carved out
        // here, not on the text itself). With the old whole-background viewport,
        // TMP's caret-scrolling shifted the text component relative to the padded
        // background and could strand it at a stale offset — which looked like
        // un-erasable blank space at the start of the field.
        RectTransform textArea = Rect("Text Area", bg, Vector2.zero, Vector2.one, new(16f, 0f), new(-40f, 0f));
        textArea.gameObject.AddComponent<RectMask2D>();
        TMP_Text value = Text(textArea, "", 17f, TextAlignmentOptions.Left);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_Text placeholder = Text(textArea, "Search levels…", 17f, TextAlignmentOptions.Left);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
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

    // Infinite scroll: fetch the next page once the view is within ~a page of the
    // bottom. LoadingMore flips synchronously inside LoadMore, so this fires once
    // per page even though it polls every frame. Also fires when the first page is
    // shorter than the viewport, filling until the list scrolls.
    private void Update() {
        if(!built || service == null || content == null || viewport == null) return;
        if(armedDeleteId != 0 && Time.unscaledTime >= armedUntil) DisarmDelete();
        // The Installed view is the whole local library at once — nothing to page.
        if(!service.HasMore || service.LoadingMore || service.State != TufListState.Ready) return;
        float max = content.rect.height - viewport.rect.height;
        if(max <= 0f || content.anchoredPosition.y >= max - 400f) service.LoadMore();
    }

    // Snap the text back to the viewport origin. TMP only ever re-applies its own
    // scroll while the caret sits past the right edge, so this is stable at rest and
    // clears any leftover scroll offset from typing, IME composition, or clearing.
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
            // Same level list + item states: only progress/labels moved. Update the
            // affected labels in place instead of tearing down and re-laying out the
            // whole list — a download pushes ~20 progress ticks, and a full rebuild
            // (ClearChildren + two forced layout passes) on each is a visible hitch.
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
        GenerateUI.ClearChildren(content);
        cardLabels.Clear();
        deleteLabels.Clear();
        deleteChips.Clear();
        // A fresh (non-append) fetch — sort/filter/query change — always shows the
        // spinner instead of the stale list; appends keep the list and spin at the end.
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
            // Paging is automatic (see Update); the only interactive bottom row left
            // is a Retry after a failed append.
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

    // Identity of the currently-rendered list. Progress ticks keep it stable (they
    // change only level.Progress); anything structural — the level set, per-item
    // state, paging, or busy flag — changes it and forces a full rebuild.
    private string BuildSignature() {
        StringBuilder sb = new();
        sb.Append((int)service.State).Append('|')
            .Append(service.HasMore ? '1' : '0')
            .Append(service.LoadingMore ? '1' : '0')
            .Append(service.IsBusy ? '1' : '0')
            .Append(service.ShowInstalled ? '1' : '0').Append('|');
        foreach(TufLevel level in service.Levels)
            sb.Append(level.Id).Append(':').Append((int)level.State)
                // A card gains a badge and a Delete button (and loses text width) the
                // moment it becomes installed, so the install state is structural.
                .Append(level.InstallFolder == null ? '-' : '+')
                // So is having an error: the action tooltip only exists when there is
                // one, and it is attached during the rebuild.
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
        RectTransform rail = Rect("Difficulty Rail", card, new(0f, 0f), new(0f, 1f), new(5f, 8f), new(11f, -8f));
        Image railImage = rail.gameObject.AddComponent<Image>();
        railImage.sprite = MainCore.Spr.GetFilled(2f);
        railImage.type = Image.Type.Sliced;
        railImage.color = ColorUtility.TryParseHtmlString(level.DifficultyColor, out Color color) ? color : Color.white;

        // Fixed columns sized for the longest possible value left short ones (#3042,
        // P13) marooned in whitespace, so each label is measured and the next starts
        // just past it.
        float x = 22f;
        TMP_Text id = MetaLabel(card, "Id", $"#{level.Id}", ref x, 90f);
        id.color = new(1f, 1f, 1f, 0.48f);
        x += MetaGap;
        // TUF allows a 40-character difficulty name; capped so a wordy one ellipsizes
        // instead of pushing the badge under the buttons.
        TMP_Text diff = MetaLabel(card, "Difficulty", level.Difficulty, ref x, 150f);
        diff.color = railImage.color;

        bool installed = IsInstalled(level);
        if(installed) AddInstalledBadge(card, x + MetaGap);
        // Make room for the Delete button beside the action when there is one.
        float textRight = installed ? -244f : -150f;

        RectTransform songRect = Rect("Song", card, new(0f, 1f), new(1f, 1f), new(22f, -66f), new(textRight, -34f));
        // An adopted install (downloaded before the index existed, or by another mod)
        // has no metadata until it turns up in a search again; show the id instead of
        // an empty row.
        string song = string.IsNullOrEmpty(level.Song) ? Tr("TUF_UNKNOWN_LEVEL", "Level") + " #" + level.Id : level.Song;
        TMP_Text songText = Text(songRect, song, 23f, TextAlignmentOptions.Left);
        songText.fontStyle = FontStyles.Bold;
        songText.overflowMode = TextOverflowModes.Ellipsis;
        songText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform metaRect = Rect("Metadata", card, new(0f, 0f), new(1f, 0f), new(22f, 8f), new(textRight, 34f));
        TMP_Text meta = Text(metaRect, CardMeta(level), 15f, TextAlignmentOptions.Left);
        meta.color = new(1f, 1f, 1f, 0.46f);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        meta.textWrappingMode = TextWrappingModes.NoWrap;
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

    // A label sized to its own text, starting at x. Advances x to its right edge.
    private static TMP_Text MetaLabel(RectTransform card, string name, string value, ref float x, float maxWidth) {
        RectTransform rect = Rect(name, card, new(0f, 1f), new(0f, 1f), new(x, -35f), new(x, -8f));
        TMP_Text text = Text(rect, value, 16f, TextAlignmentOptions.Left);
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        // The string overload measures against unbounded space, so the rect being
        // zero-wide until it is sized here does not fold the text.
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

    // Two-step: the first click arms this one card, the second removes it. Arming
    // any card disarms every other, and the arm lapses on its own after a few
    // seconds so a stray click never leaves a live delete sitting on the screen.
    private void AddDelete(RectTransform card, TufLevel level) {
        RectTransform button = Rect("Delete", card, new(1f, 0.5f), new(1f, 0.5f), new(-232f, -23f), new(-146f, 23f));
        Image image = button.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        bool armed = armedDeleteId == level.Id;
        bool enabled = !service.IsBusy;
        image.color = armed
            ? new Color(0.86f, 0.31f, 0.33f, 0.92f)
            : Color.Lerp(UIColors.ObjectBG, new Color(0.86f, 0.31f, 0.33f, 1f), enabled ? 0.22f : 0.08f);
        TMP_Text label = Text(button, DeleteLabel(level), 15f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, enabled ? 0.95f : 0.45f);
        deleteLabels[level.Id] = label;
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
        button.AddToolTip("DESC_TUF_DELETE", "Delete this level from your library. It can be downloaded again.");
    }

    private string DeleteLabel(TufLevel level) =>
        armedDeleteId == level.Id ? Tr("TUF_DELETE_CONFIRM", "Sure?") : Tr("TUF_DELETE", "Delete");

    private void DisarmDelete() {
        if(armedDeleteId == 0) return;
        armedDeleteId = 0;
        RefreshDeleteChips();
    }

    // Repaints the delete buttons in place. Arming is view-only state, so it must not
    // reach the service or force a list rebuild.
    private void RefreshDeleteChips() {
        foreach(TufLevel level in service.Levels) {
            if(deleteLabels.TryGetValue(level.Id, out TMP_Text label) && label != null) {
                string text = DeleteLabel(level);
                if(label.text != text) label.text = text;
            }
            if(deleteChips.TryGetValue(level.Id, out Image image) && image != null)
                image.color = armedDeleteId == level.Id
                    ? new Color(0.86f, 0.31f, 0.33f, 0.92f)
                    : Color.Lerp(UIColors.ObjectBG, new Color(0.86f, 0.31f, 0.33f, 1f), 0.22f);
        }
    }

    private void AddAction(RectTransform card, TufLevel level) {
        RectTransform action = Rect("Action", card, new(1f, 0.5f), new(1f, 0.5f), new(-138f, -23f), new(-10f, 23f));
        Image image = action.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        bool enabled = level.State is not TufItemState.Unavailable and not TufItemState.Downloading
            and not TufItemState.Extracting and not TufItemState.Loading && !service.IsBusy;
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
        TufItemState.Unavailable => Tr("TUF_UNAVAILABLE", "Unavailable"),
        TufItemState.ChooseChart => Tr("TUF_CANCEL", "Cancel"),
        _ => Tr("TUF_DOWNLOAD", "Download")
    };

    // Rendered directly below a card whose level has multiple playable charts.
    // Choices fade in with a small stagger; the card action reads Cancel while open.
    private void AddChartChooser(TufLevel level) {
        // Charts is only populated while the level sits in ChooseChart. The caller
        // checks, but nothing stops a future one from forgetting.
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
            label.textWrappingMode = TextWrappingModes.NoWrap;
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

    // A status row with a rotating ring arc beside the message.
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
        filterLayoutSeq?.Kill();
        specialArrowSeq?.Kill();
        chartChooserSeq?.Kill();
    }
}
