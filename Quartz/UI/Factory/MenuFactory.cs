using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Transition;
using Quartz.UI.Utility;
using Quartz.Update;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GTweens.Tweens;
using Quartz.Tween;

using GTweens.Builders;
using GTweens.Easings;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;

using TMPro;

namespace Quartz.UI.Factory;

public static class MenuFactory {
    public static Action<int> OnStateChanged;

    public sealed class MenuItem {
        public int state;
        public GameObject obj;
        public Image bg;
        public GTween hoverSeq;
        public TMP_Text label;
        public Image arrow; // non-null only for the expandable Overlay item
    }

    // One row nested under the expanded Overlay item — jumps straight to that
    // feature's section on the (unchanged) Overlay page instead of being its
    // own tab/page.
    private sealed class OverlaySubItem {
        public string Title;
        public GameObject obj;
        public Image bg;
        public GTween hoverSeq;
        public TMP_Text label;
    }

    private static readonly List<MenuItem> items = [];
    private static readonly List<OverlaySubItem> overlayChildren = [];

    // Sub-rows mirror the Collapsible section titles inside PageOverlay
    // (PageProgressBar/Combo/Judgement/KeyViewer/SongTitle .AppendTo) — keep
    // these in sync with those exact title strings.
    private static readonly string[] OverlaySectionTitles = [
        "Progress Bar", "Combo", "Judgement", "Key Viewer", "Song Title"
    ];
    private const string OverlayExpandKey = "Menu/Overlay";

    private static RectTransform menuContentRect;
    private static GameObject overlayChildrenContainer;
    private static RectTransform overlayChildrenRect;
    private static VerticalLayoutGroup overlayChildrenLayout;
    private static ContentSizeFitter overlayChildrenFitter;
    private static LayoutElement overlayChildrenLE;
    private static CanvasGroup overlayChildrenCg;
    private static bool overlayExpanded;
    private static GTween overlayHeightSeq;
    private static GTween overlayArrowSeq;
    private static string activeOverlaySection;

    // Small dot on the Settings item while an update is available, so the
    // background startup check is visible without opening the Settings page.
    private static GameObject updateBadge;
    private static bool updateHooked;

    public static void CreateMenu(Transform parent) {
        items.Clear();
        overlayChildren.Clear();
        menuContentRect = parent as RectTransform;
        overlayExpanded = MainCore.Conf.GetCollapsibleExpanded(OverlayExpandKey);

        // Sized icon variants: 128px sources drawn at 28 units were ~4x
        // minified through the mip chain and visibly mushy. The panel canvas
        // multiplies px/unit by the user's UI scale, so bake for that too.
        float iconUnits = 28f * MainCore.Conf.UIScale;

        MenuItem overlayItem = CreateItem(parent, "Overlay", MainCore.Spr.Get(UISprite.Monitor128, iconUnits), (int)OriginalMenuState.Overlay, ToggleOverlayExpanded);
        CreateOverlayChildren(parent, overlayItem);
        CreateItem(parent, "Gameplay", MainCore.Spr.Get(UISprite.Gamepad128, iconUnits), (int)OriginalMenuState.Gameplay);
        CreateItem(parent, "Visuals", MainCore.Spr.Get(UISprite.Image128, iconUnits), (int)OriginalMenuState.Visuals);
        CreateItem(parent, "Tweaks", MainCore.Spr.Get(UISprite.AdjustmentsHorizontal128, iconUnits), (int)OriginalMenuState.Tweaks);
        CreateItem(parent, "Editor", MainCore.Spr.Get(UISprite.Wrench128, iconUnits), (int)OriginalMenuState.Editor);
        CreateItem(parent, "Search", MainCore.Spr.Get(UISprite.MagnifyingGlass128, iconUnits), (int)OriginalMenuState.Search);
        CreateItem(parent, "Profiles", MainCore.Spr.Get(UISprite.Users128, iconUnits), (int)OriginalMenuState.Profiles);
        CreateItem(parent, "Import", MainCore.Spr.Get(UISprite.Book128, iconUnits), (int)OriginalMenuState.Import);
        var settings = CreateItem(parent, "Settings", MainCore.Spr.Get(UISprite.Gear128, iconUnits), (int)OriginalMenuState.Settings);
        CreateItem(parent, "Credits", MainCore.Spr.Get(UISprite.Star128, iconUnits), (int)OriginalMenuState.Credits);

        // Developer tab — only present in "dev" builds.
        if(Info.IsDev) {
            CreateItem(parent, "Developer", MainCore.Spr.Get(UISprite.Wrench128, iconUnits), (int)OriginalMenuState.Developer);
        }

        CreateUpdateBadge(settings.obj.transform);
        if(!updateHooked) {
            UpdateService.OnChanged += RefreshUpdateBadge;
            updateHooked = true;
        }
        RefreshUpdateBadge();

        ApplyState(UICore.CurrentMenuState, true);
    }

    private static void CreateUpdateBadge(Transform parent) {
        updateBadge = new GameObject("UpdateBadge");
        updateBadge.transform.SetParent(parent, false);

        RectTransform rect = updateBadge.AddComponent<RectTransform>();
        rect.anchorMin = new(1f, 0.5f);
        rect.anchorMax = new(1f, 0.5f);
        rect.pivot = new(0.5f, 0.5f);
        rect.anchoredPosition = new(-22f, 0f);
        rect.sizeDelta = new(10f, 10f);

        Image img = updateBadge.AddComponent<Image>();
        // Sized variant: the 256px circle drawn at 10 units is a ~24x
        // minification — far past what the mip chain renders cleanly.
        img.sprite = MainCore.Spr.Get(UISprite.Circle256, 10f * MainCore.Conf.UIScale);
        img.color = UIColors.SoftRed;
        img.raycastTarget = false;

        updateBadge.SetActive(false);
    }

    private static void RefreshUpdateBadge() {
        if(updateBadge == null) return;

        updateBadge.SetActive(UpdateService.Status == UpdateStatus.Available);
    }

    // Re-applies menu selection colors after the accent palette changes.
    public static void RefreshTheme() {
        ApplyState(UICore.CurrentMenuState, true);
        ApplyOverlayExpand(false);
        RefreshOverlayChildHighlight();
    }

    public static MenuItem CreateItem(Transform parent, string name, Sprite icon, int state, Action onExpandToggle = null) {
        bool expandable = onExpandToggle != null;

        GameObject item = new(name);
        item.transform.SetParent(parent, false);

        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = new(0, 1);
        rect.anchorMax = new(1, 1);
        rect.pivot = new(0.5f, 1);
        rect.sizeDelta = new(0, 54);

        Image bg = item.AddComponent<Image>();
        bg.color = UIColors.MenuNormal;

        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(item.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new(0, 0.5f);
        iconRect.anchorMax = new(0, 0.5f);
        iconRect.pivot = new(0, 0.5f);
        iconRect.anchoredPosition = new(24, 0);
        iconRect.sizeDelta = new(28, 28);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.raycastTarget = false;

        GameObject textObj = new("Text");
        textObj.transform.SetParent(item.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(70, 0);
        // Expandable items reserve room on the right for the arrow so the
        // label doesn't run under it.
        textRect.offsetMax = expandable ? new(-34f, 0) : Vector2.zero;

        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = name;
        label.font = FontManager.Current;
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;

        // Every item's locale key is its name uppercased (OVERLAY, TWEAKS, ...).
        label.gameObject.AddComponent<TextLocalization>().Init(name.ToUpperInvariant(), name);

        // Expand/collapse arrow — same visual language as GenerateUI.Collapsible
        // (Triangle128, rotates 180 open/closed). The correct initial pose is
        // applied once by ApplyOverlayExpand(false) right after the children
        // list is built, so it starts neutral here.
        Image arrowImg = null;
        if(expandable) {
            GameObject arrowObj = new("Arrow");
            arrowObj.transform.SetParent(item.transform, false);

            RectTransform arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.anchorMin = new(1f, 0.5f);
            arrowRect.anchorMax = new(1f, 0.5f);
            arrowRect.pivot = new(0.5f, 0.5f);
            arrowRect.anchoredPosition = new(-22f, 0f);
            arrowRect.sizeDelta = new(20f, 20f);

            arrowImg = arrowObj.AddComponent<Image>();
            arrowImg.sprite = MainCore.Spr.Get(UISprite.Triangle128);
            arrowImg.color = UIColors.ObjectInactive;
            arrowImg.raycastTarget = false;
        }

        MenuItem menuItem = new() {
            obj = item,
            bg = bg,
            state = state,
            label = label,
            arrow = arrowImg
        };

        items.Add(menuItem);

        var trigger = item.AddComponent<EventTrigger>();

        void Add(EventTriggerType type, Action cb) {
            var e = new EventTrigger.Entry { eventID = type };

            e.callback.AddListener(_ => cb());
            trigger.triggers.Add(e);
        }

        // Enter/exit are the same fade toward a different palette color; the
        // color is resolved at hover time (Func) so accent changes apply live.
        void HoverFade(EventTriggerType type, Func<Color> color, float duration) => Add(type, () => {
            if(UICore.CurrentMenuState == state) return;

            menuItem.hoverSeq?.Kill();
            menuItem.hoverSeq = GTweenSequenceBuilder.New()
                .Append(bg.GTColor(color(), duration).SetEasing(Easing.OutSine))
                .Build();
            MainCore.TC.Play(menuItem.hoverSeq);
        });

        HoverFade(EventTriggerType.PointerEnter, static () => UIColors.MenuHover, 0.2f);
        HoverFade(EventTriggerType.PointerExit, static () => UIColors.MenuNormal, 0.25f);

        UnityUtils.AddClickEvent(trigger, _ => {
            SetState(state);
            onExpandToggle?.Invoke();
        });

        return menuItem;
    }

    public static void SetState(int to) {
        int from = UICore.CurrentMenuState;

        if(from == to) return;

        UICore.CurrentMenuState = to;

        PageSwicher.SwitchPage(from, to);
        ApplyState(to);

        OnStateChanged?.Invoke(to);
    }

    private static void ApplyState(int id, bool noAnimate = false) {
        for(int i = 0; i < items.Count; i++) {
            var it = items[i];

            it.hoverSeq?.Kill();

            bool selected = it.state == id;

            if(selected) {
                if(noAnimate) {
                    it.bg.color = UIColors.MenuSelected;
                } else {
                    it.bg.color = UIColors.MenuHighlight;

                    it.hoverSeq = it.bg.GTColor(UIColors.MenuSelected, 0.3f).SetEasing(Easing.OutSine);
                    MainCore.TC.Play(it.hoverSeq);
                }
            } else {
                it.bg.color = UIColors.MenuNormal;
            }
        }
    }

    // ===== Overlay item — expandable in place instead of navigating straight
    // into the Overlay page's own accordion. =====

    private static void CreateOverlayChildren(Transform parent, MenuItem overlayItem) {
        overlayChildrenContainer = new GameObject("OverlayChildren");
        overlayChildrenContainer.transform.SetParent(parent, false);
        overlayChildrenContainer.transform.SetSiblingIndex(overlayItem.obj.transform.GetSiblingIndex() + 1);

        overlayChildrenRect = overlayChildrenContainer.AddComponent<RectTransform>();
        overlayChildrenRect.anchorMin = new(0, 1);
        overlayChildrenRect.anchorMax = new(1, 1);
        overlayChildrenRect.pivot = new(0.5f, 1);

        overlayChildrenLayout = GenerateUI.FitVertical(overlayChildrenContainer, 0f);
        overlayChildrenFitter = overlayChildrenContainer.GetComponent<ContentSizeFitter>();
        overlayChildrenLE = overlayChildrenContainer.AddComponent<LayoutElement>();
        overlayChildrenContainer.AddComponent<RectMask2D>();
        overlayChildrenCg = overlayChildrenContainer.AddComponent<CanvasGroup>();

        foreach(string title in OverlaySectionTitles) CreateSubItem(overlayChildrenContainer.transform, title);

        // Snap to the persisted state — no slide on (re)build.
        ApplyOverlayExpand(false);
        RefreshOverlayChildHighlight();
    }

    private static void CreateSubItem(Transform parent, string title) {
        RectTransform rect = GenerateUI.Row(parent, 40f);
        rect.gameObject.name = title;

        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = UIColors.MenuNormal;

        GameObject textObj = new("Text");
        textObj.transform.SetParent(rect, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(96, 0);
        textRect.offsetMax = Vector2.zero;

        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = title;
        label.font = FontManager.Current;
        label.fontSize = 16;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;

        // Reuses the same "SECTION_..." keys as the Collapsible headers inside
        // PageOverlay (GenerateUI.Collapsible) — same title strings, so no new
        // localization entries are needed.
        label.gameObject.AddComponent<TextLocalization>().Init(GenerateUI.LocaleKeyFromText("SECTION", title), title);

        OverlaySubItem sub = new() { Title = title, obj = rect.gameObject, bg = bg, label = label };
        overlayChildren.Add(sub);

        var trigger = rect.gameObject.AddComponent<EventTrigger>();

        void Add(EventTriggerType type, Action cb) {
            var e = new EventTrigger.Entry { eventID = type };

            e.callback.AddListener(_ => cb());
            trigger.triggers.Add(e);
        }

        void HoverFade(EventTriggerType type, Func<Color> color, float duration) => Add(type, () => {
            if(activeOverlaySection == title) return;

            sub.hoverSeq?.Kill();
            sub.hoverSeq = GTweenSequenceBuilder.New()
                .Append(bg.GTColor(color(), duration).SetEasing(Easing.OutSine))
                .Build();
            MainCore.TC.Play(sub.hoverSeq);
        });

        HoverFade(EventTriggerType.PointerEnter, static () => UIColors.MenuHover, 0.2f);
        HoverFade(EventTriggerType.PointerExit, static () => UIColors.MenuNormal, 0.25f);

        UnityUtils.AddClickEvent(trigger, _ => FocusOverlaySection(title));
    }

    private static void ToggleOverlayExpanded() {
        overlayExpanded = !overlayExpanded;
        MainCore.Conf.SetCollapsibleExpanded(OverlayExpandKey, overlayExpanded);
        MainCore.ConfMgr.RequestSave();
        ApplyOverlayExpand(true);
    }

    // Slides the sidebar's Key Viewer/Song Title/... rows open or closed and
    // spins the Overlay item's arrow — same tween idiom as
    // GenerateUI.Collapsible.Apply(), applied to the sidebar instead of a
    // page body.
    private static void ApplyOverlayExpand(bool animate) {
        if(overlayChildrenContainer == null) return;

        bool exp = overlayExpanded;
        Image arrow = items.Find(i => i.state == (int)OriginalMenuState.Overlay)?.arrow;
        Vector3 targetRot = exp ? new Vector3(0f, 0f, 180f) : Vector3.zero;
        Color targetCol = exp ? UIColors.ObjectActive : UIColors.ObjectInactive;

        overlayChildrenCg.blocksRaycasts = exp;
        overlayChildrenCg.interactable = exp;

        overlayHeightSeq?.Kill();
        overlayArrowSeq?.Kill();

        if(!animate) {
            overlayChildrenContainer.SetActive(exp);
            overlayChildrenLayout.enabled = exp;
            overlayChildrenFitter.enabled = exp;
            overlayChildrenLE.preferredHeight = exp ? -1f : 0f;
            overlayChildrenCg.alpha = exp ? 1f : 0f;
            if(arrow != null) {
                arrow.rectTransform.localRotation = Quaternion.Euler(targetRot);
                arrow.color = targetCol;
            }
            if(menuContentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(menuContentRect);
            return;
        }

        overlayChildrenContainer.SetActive(true);
        overlayChildrenLayout.enabled = true;
        overlayChildrenFitter.enabled = true;
        overlayChildrenLE.preferredHeight = -1f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(menuContentRect);
        float content = overlayChildrenRect.rect.height;

        overlayChildrenLayout.enabled = false;
        overlayChildrenFitter.enabled = false;

        float to = exp ? content : 0f;
        overlayChildrenLE.preferredHeight = exp ? 0f : content;

        overlayHeightSeq = GTweenSequenceBuilder.New()
            .Join(GTweenExtensions.Tween(
                () => overlayChildrenLE.preferredHeight,
                x => {
                    overlayChildrenLE.preferredHeight = Mathf.Max(0f, x);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(menuContentRect);
                },
                to,
                0.16f
            ).SetEasing(exp ? Easing.OutBack : Easing.OutSine))
            .Join(GTweenExtensions.Tween(
                () => overlayChildrenCg.alpha,
                x => overlayChildrenCg.alpha = x,
                exp ? 1f : 0f,
                0.16f
            ).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(overlayExpanded) {
                    overlayChildrenLayout.enabled = true;
                    overlayChildrenFitter.enabled = true;
                    overlayChildrenLE.preferredHeight = -1f;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(menuContentRect);
                } else {
                    overlayChildrenContainer.SetActive(false);
                    overlayChildrenLE.preferredHeight = 0f;
                }
            })
            .Build();
        MainCore.TC.Play(overlayHeightSeq);

        if(arrow != null) {
            overlayArrowSeq = GTweenSequenceBuilder.New()
                .Join(arrow.rectTransform.GTRotate(targetRot, 0.4f).SetEasing(Easing.OutBack))
                .Join(arrow.GTColor(targetCol, 0.2f).SetEasing(Easing.OutSine))
                .Build();
            MainCore.TC.Play(overlayArrowSeq);
        }
    }

    // Jumps to the Overlay page and focuses one feature's section: collapses
    // its sibling sections (Progress Bar/Combo/Judgement/Key Viewer/Song
    // Title/Panels/Layout — whatever shares its parent) so only the picked
    // one shows, then scrolls it into view. Mirrors PageSearch.Navigate's
    // instant-expand-then-scroll approach (animating would leave stale
    // heights for the scroll math).
    private static void FocusOverlaySection(string sectionTitle) {
        SetState((int)OriginalMenuState.Overlay);

        activeOverlaySection = sectionTitle;
        RefreshOverlayChildHighlight();

        RectTransform page = UICore.Pages[(int)OriginalMenuState.Overlay];

        GenerateUI.CollapsibleSection target = null;
        foreach(GenerateUI.CollapsibleSection s in GenerateUI.Sections) {
            if(s.Section != null && s.Title == sectionTitle && s.Section.IsChildOf(page)) {
                target = s;
                break;
            }
        }
        if(target == null) return;

        foreach(GenerateUI.CollapsibleSection s in GenerateUI.Sections) {
            if(s.Section == null || s.Section.parent != target.Section.parent) continue;
            s.SetExpanded(s == target, false, true);
        }

        UIScrollController scroller = page.GetComponentInChildren<UIScrollController>(true);
        if(scroller == null || scroller.content == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(scroller.content);

        Vector3 worldCenter = target.Section.TransformPoint(target.Section.rect.center);
        float localY = scroller.content.InverseTransformPoint(worldCenter).y;
        float top = -localY - (target.Section.rect.height * 0.5f);
        scroller.ScrollTo(top - 8f);
    }

    private static void RefreshOverlayChildHighlight() {
        foreach(OverlaySubItem sub in overlayChildren) {
            sub.hoverSeq?.Kill();

            if(sub.Title == activeOverlaySection) {
                sub.bg.color = UIColors.MenuHighlight;
                sub.hoverSeq = sub.bg.GTColor(UIColors.MenuSelected, 0.3f).SetEasing(Easing.OutSine);
                MainCore.TC.Play(sub.hoverSeq);
            } else {
                sub.bg.color = UIColors.MenuNormal;
            }
        }
    }
}