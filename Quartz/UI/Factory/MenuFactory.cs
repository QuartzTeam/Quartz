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
        public bool isCategory;
    }
    private static readonly List<MenuItem> items = [];
    private static readonly List<MenuItem> subItems = [];
    private static readonly Dictionary<int, (string Title, string Key, int State)[]> CategoryChildren = new() {
        [(int)OriginalMenuState.OverlayGeneral] = [
            ("General", "OVERLAY_GENERAL", (int)OriginalMenuState.OverlayGeneral),
            ("Key Viewer", "SECTION_KEY_VIEWER", (int)OriginalMenuState.KeyViewer),
            ("Progress Bar", "SECTION_PROGRESS_BAR", (int)OriginalMenuState.ProgressBar),
            ("Combo", "SECTION_COMBO", (int)OriginalMenuState.Combo),
            ("Judgement", "SECTION_JUDGEMENT", (int)OriginalMenuState.Judgement),
            ("Song Title", "SECTION_SONG_TITLE", (int)OriginalMenuState.SongTitle),
            ("Panels", "SECTION_PANELS", (int)OriginalMenuState.Panels),
            ("Calibration", "SECTION_CALIBRATION", (int)OriginalMenuState.Calibration),
        ],
        [(int)OriginalMenuState.GameplayKeyLimiter] = [
            ("Key Limiter", "SECTION_KEY_LIMITER", (int)OriginalMenuState.GameplayKeyLimiter),
            ("Keyboard Chatter Blocker", "SECTION_KEYBOARD_CHATTER_BLOCKER", (int)OriginalMenuState.GameplayChatter),
            ("Judgement Restriction", "SECTION_JUDGEMENT_RESTRICTION", (int)OriginalMenuState.GameplayJudgement),
            ("Death Limit", "SECTION_DEATH_LIMIT", (int)OriginalMenuState.GameplayDeath),
            ("Auto Deafen (Discord)", "SECTION_AUTO_DEAFEN_DISCORD", (int)OriginalMenuState.GameplayAutoDeafen),
        ],
        [(int)OriginalMenuState.VisualsEffectRemover] = [
            ("Effect Remover", "SECTION_EFFECT_REMOVER", (int)OriginalMenuState.VisualsEffectRemover),
            ("Hide Judgements", "SECTION_HIDE_JUDGEMENTS", (int)OriginalMenuState.VisualsHideJudgements),
            ("Visual Tweaks", "SECTION_VISUAL_TWEAKS", (int)OriginalMenuState.VisualsVisualTweaks),
            ("Planet Colors", "SECTION_PLANET_COLORS", (int)OriginalMenuState.VisualsPlanetColors),
            ("Otto Icon", "SECTION_OTTO_ICON", (int)OriginalMenuState.VisualsOttoIcon),
            ("UI Hiding", "SECTION_UI_HIDING", (int)OriginalMenuState.VisualsUiHiding),
        ],
        [(int)OriginalMenuState.TweaksGeneral] = [
            ("General", "TWEAKS_GENERAL", (int)OriginalMenuState.TweaksGeneral),
            ("Optimizer", "SECTION_OPTIMIZER", (int)OriginalMenuState.TweaksOptimizer),
            ("Main Menu", "SECTION_MAIN_MENU", (int)OriginalMenuState.TweaksMainMenu),
        ],
        [(int)OriginalMenuState.EditorTileReadout] = [
            ("Selected Tile Readout", "SECTION_SELECTED_TILE_READOUT", (int)OriginalMenuState.EditorTileReadout),
            ("BGA Mod", "SECTION_BGA_MOD", (int)OriginalMenuState.EditorBga),
            ("Flip & Rotate Tiles", "SECTION_FLIP_ROTATE_TILES", (int)OriginalMenuState.EditorFlipRotate),
        ],
        [(int)OriginalMenuState.NostalgiaGameplay] = [
            ("Gameplay", "GAMEPLAY", (int)OriginalMenuState.NostalgiaGameplay),
            ("Visuals", "VISUALS", (int)OriginalMenuState.NostalgiaVisuals),
            ("Tweaks", "TWEAKS", (int)OriginalMenuState.NostalgiaTweaks),
            ("Editor", "EDITOR", (int)OriginalMenuState.NostalgiaEditor),
        ],
        [(int)OriginalMenuState.NostalgiaTuf] = [
            ("Levels", "TUF_LEVELS", (int)OriginalMenuState.NostalgiaTuf),
            ("Packs", "TUF_PACKS", (int)OriginalMenuState.NostalgiaTufPacks),
            ("Settings", "TUF_SETTINGS", (int)OriginalMenuState.NostalgiaTufSettings),
        ],
    };
    public static int CategoryFor(int state) {
        foreach(var kvp in CategoryChildren)
            foreach(var c in kvp.Value)
                if(c.State == state) return kvp.Key;
        return state;
    }
    private static int activeCategoryKey = -1;
    private static readonly Dictionary<int, int> lastChildForCategory = [];
    private static GameObject updateBadge;
    private static bool updateHooked;
    public static void CreateMenu(Transform parent) {
        items.Clear();
        subItems.Clear();
        activeCategoryKey = -1;
        var addonPages = Quartz.Addons.AddonUI.Pages;
        if(addonPages.Count > 0) {
            var children = new (string Title, string Key, int State)[addonPages.Count + 1];
            children[0] = ("Addons", "ADDONS", (int)OriginalMenuState.Addons);
            for(int i = 0; i < addonPages.Count; i++)
                children[i + 1] = (addonPages[i].Title, addonPages[i].LocaleKey, addonPages[i].State);
            CategoryChildren[(int)OriginalMenuState.Addons] = children;
        } else {
            CategoryChildren.Remove((int)OriginalMenuState.Addons);
        }
        List<int> stale = [];
        foreach(var kvp in lastChildForCategory)
            if(Quartz.Addons.AddonUI.IsAddonState(kvp.Value)
               && !addonPages.Any(p => p.State == kvp.Value)) stale.Add(kvp.Key);
        foreach(int key in stale) lastChildForCategory.Remove(key);
        float iconUnits = 28f * MainCore.Conf.UIScale;
        CreateItem(parent, "Overlay", MainCore.Spr.Get(UISprite.Monitor128, iconUnits), (int)OriginalMenuState.OverlayGeneral);
        CreateItem(parent, "Gameplay", MainCore.Spr.Get(UISprite.Gamepad128, iconUnits), (int)OriginalMenuState.GameplayKeyLimiter);
        CreateItem(parent, "Visuals", MainCore.Spr.Get(UISprite.Image128, iconUnits), (int)OriginalMenuState.VisualsEffectRemover);
        CreateItem(parent, "Tweaks", MainCore.Spr.Get(UISprite.AdjustmentsHorizontal128, iconUnits), (int)OriginalMenuState.TweaksGeneral);
        CreateItem(parent, "Editor", MainCore.Spr.Get(UISprite.Wrench128, iconUnits), (int)OriginalMenuState.EditorTileReadout);
        CreateItem(parent, "Nostalgia", MainCore.Spr.Get(UISprite.ClockRewind128, iconUnits), (int)OriginalMenuState.NostalgiaGameplay);
        CreateItem(parent, "TUF", MainCore.Spr.Get(UISprite.TufLogo, iconUnits), (int)OriginalMenuState.NostalgiaTuf);
        CreateItem(parent, "Search", MainCore.Spr.Get(UISprite.MagnifyingGlass128, iconUnits), (int)OriginalMenuState.Search);
        CreateItem(parent, "Profiles", MainCore.Spr.Get(UISprite.Users128, iconUnits), (int)OriginalMenuState.Profiles);
        CreateItem(parent, "Import", MainCore.Spr.Get(UISprite.Book128, iconUnits), (int)OriginalMenuState.Import);
        CreateItem(parent, "Addons", MainCore.Spr.Get(UISprite.Wrench128, iconUnits), (int)OriginalMenuState.Addons);
        var settings = CreateItem(parent, "Settings", MainCore.Spr.Get(UISprite.Gear128, iconUnits), (int)OriginalMenuState.Settings);
        CreateItem(parent, "Credits", MainCore.Spr.Get(UISprite.Star128, iconUnits), (int)OriginalMenuState.Credits);
        if(Info.IsDev) {
            CreateItem(parent, "Developer", MainCore.Spr.Get(UISprite.Wrench128, iconUnits), (int)OriginalMenuState.Developer);
        }
        CreateUpdateBadge(settings.obj.transform);
        if(!updateHooked) {
            UpdateService.OnChanged += RefreshUpdateBadge;
            updateHooked = true;
        }
        RefreshUpdateBadge();
        RefreshSubMenu(CategoryFor(UICore.CurrentMenuState), animate: false);
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
        img.sprite = MainCore.Spr.Get(UISprite.Circle256, 10f * MainCore.Conf.UIScale);
        img.color = UIColors.SoftRed;
        img.raycastTarget = false;
        updateBadge.SetActive(false);
    }
    private static void RefreshUpdateBadge() {
        if(updateBadge == null) return;
        updateBadge.SetActive(UpdateService.Status == UpdateStatus.Available);
    }
    public static void RefreshTheme() {
        ApplyState(UICore.CurrentMenuState, true);
    }
    public static MenuItem CreateItem(Transform parent, string name, Sprite icon, int state) {
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
        textRect.offsetMax = Vector2.zero;
        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = name;
        label.font = FontManager.Current;
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.gameObject.AddComponent<TextLocalization>().Init(name.ToUpperInvariant(), name);
        MenuItem menuItem = new() {
            obj = item,
            bg = bg,
            state = state,
            label = label,
            isCategory = true
        };
        items.Add(menuItem);
        WireItemInteractions(menuItem, item, bg);
        return menuItem;
    }
    private static void CreateSubItem(Transform parent, string title, string key, int state) {
        RectTransform rect = GenerateUI.Row(parent, 40f);
        rect.gameObject.name = title;
        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = UIColors.MenuNormal;
        GameObject textObj = new("Text");
        textObj.transform.SetParent(rect, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(24, 0);
        textRect.offsetMax = Vector2.zero;
        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = title;
        label.font = FontManager.Current;
        label.fontSize = 16;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.gameObject.AddComponent<TextLocalization>().Init(key, title);
        MenuItem menuItem = new() { obj = rect.gameObject, bg = bg, state = state, label = label, isCategory = false };
        subItems.Add(menuItem);
        WireItemInteractions(menuItem, rect.gameObject, bg);
    }
    private static void RefreshSubMenu(int categoryKey, bool animate) {
        if(categoryKey == activeCategoryKey) return;
        activeCategoryKey = categoryKey;
        foreach(var it in subItems) it.hoverSeq?.Kill();
        subItems.Clear();
        GenerateUI.ClearChildren(UICore.SubMenuContent);
        bool has = CategoryChildren.TryGetValue(categoryKey, out var children);
        if(has) foreach((string title, string key, int state) in children) CreateSubItem(UICore.SubMenuContent, title, key, state);
        UICore.SetSubMenuVisible(has, animate);
    }
    private static void WireItemInteractions(MenuItem menuItem, GameObject item, Image bg) {
        var trigger = item.AddComponent<EventTrigger>();
        void Add(EventTriggerType type, Action cb) {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => cb());
            trigger.triggers.Add(e);
        }
        void HoverFade(EventTriggerType type, Func<Color> color, float duration) => Add(type, () => {
            if(IsSelected(menuItem, UICore.CurrentMenuState)) return;
            menuItem.hoverSeq?.Kill();
            menuItem.hoverSeq = GTweenSequenceBuilder.New()
                .Append(bg.GTColor(color(), duration).SetEasing(Easing.OutSine))
                .Build();
            MainCore.TC.Play(menuItem.hoverSeq);
        });
        HoverFade(EventTriggerType.PointerEnter, static () => UIColors.MenuHover, 0.2f);
        HoverFade(EventTriggerType.PointerExit, static () => UIColors.MenuNormal, 0.25f);
        UnityUtils.AddClickEvent(trigger, _ => {
            if(IsSelected(menuItem, UICore.CurrentMenuState)) return;
            SetState(menuItem.isCategory
                ? lastChildForCategory.GetValueOrDefault(menuItem.state, menuItem.state)
                : menuItem.state);
        });
    }
    public static void SetState(int to) {
        int from = UICore.CurrentMenuState;
        if(from == to) return;
        UICore.CurrentMenuState = to;
        int cat = CategoryFor(to);
        if(CategoryChildren.ContainsKey(cat)) lastChildForCategory[cat] = to;
        RefreshSubMenu(cat, animate: true);
        PageSwicher.SwitchPage(from, to);
        ApplyState(to);
        OnStateChanged?.Invoke(to);
    }
    private static bool IsSelected(MenuItem it, int currentState) =>
        it.isCategory ? CategoryFor(currentState) == it.state : it.state == currentState;
    private static void ApplyState(int id, bool noAnimate = false) {
        ApplyStateList(items, id, noAnimate);
        ApplyStateList(subItems, id, noAnimate);
    }
    private static void ApplyStateList(List<MenuItem> list, int id, bool noAnimate) {
        for(int i = 0; i < list.Count; i++) {
            var it = list[i];
            it.hoverSeq?.Kill();
            bool selected = IsSelected(it, id);
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
}
