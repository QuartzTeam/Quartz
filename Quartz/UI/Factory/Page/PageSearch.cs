using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using Quartz.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static class PageSearch {
    private sealed class Entry {
        public int State;
        public string Text;
        public string NormText;
        public string Section;
        public RectTransform Target;
        public bool IsCategory;
    }
    private const int MAX_RESULTS = 40;
    // Walking every page's full hierarchy on each keystroke is the expensive part of
    // a search; cache the index (with pre-normalized text) for a typing burst. The
    // short TTL self-heals rows that appear/disappear at runtime, and destroyed
    // targets are already null-guarded in Navigate.
    private const float INDEX_TTL_SECONDS = 10f;
    private static List<Entry> cachedIndex;
    private static float cachedIndexTime;
    private static string cachedIndexLang;
    private static RectTransform resultsContainer;
    private static TextMeshProUGUI statusText;
    public static void Create(RectTransform parent) {
        cachedIndex = null;
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var inputRow = GenerateUI.Row(content.transform);
        var input = GenerateUI.Input(
            inputRow,
            "",
            "",
            RunSearch,
            "Search",
            MainCore.Spr.Get(UISprite.MagnifyingGlass128),
            "search_query"
        );
        input.Placeholder.gameObject.AddComponent<TextLocalization>().Init("SEARCH", "Search");
        input.InputField.characterLimit = 40;
        var statusRow = GenerateUI.Row(content.transform, 32f);
        statusText = GenerateUI.AddMutedText(statusRow, 18f);
        GameObject results = new("Results");
        results.transform.SetParent(content.transform, false);
        resultsContainer = results.AddComponent<RectTransform>();
        GenerateUI.FitVertical(results, 8f);
        RunSearch("");
    }
    private static string Norm(string input) {
        string normalized = StringUtils.Normalize(input);
        if(MainCore.Conf.Language == "ko-KR" && !string.IsNullOrEmpty(normalized))
            normalized = StringUtils.NormalizeToHangulChosung(normalized);
        return normalized;
    }
    private static string TabName(int state) {
        if(Quartz.Addons.AddonUI.IsAddonState(state)) {
            foreach(var def in Quartz.Addons.AddonUI.Pages)
                if(def.State == state) return GenerateUI.Tr(def.LocaleKey, def.Title);
            return "?";
        }
        return TabNameFixed(state);
    }
    private static string TabNameFixed(int state) => (OriginalMenuState)state switch {
        OriginalMenuState.OverlayGeneral => GenerateUI.Tr("OVERLAY_GENERAL", "General"),
        OriginalMenuState.KeyViewer => GenerateUI.Tr("SECTION_KEY_VIEWER", "Key Viewer"),
        OriginalMenuState.ProgressBar => GenerateUI.Tr("SECTION_PROGRESS_BAR", "Progress Bar"),
        OriginalMenuState.Combo => GenerateUI.Tr("SECTION_COMBO", "Combo"),
        OriginalMenuState.Judgement => GenerateUI.Tr("SECTION_JUDGEMENT", "Judgement"),
        OriginalMenuState.SongTitle => GenerateUI.Tr("SECTION_SONG_TITLE", "Song Title"),
        OriginalMenuState.Panels => GenerateUI.Tr("SECTION_PANELS", "Panels"),
        OriginalMenuState.GameplayKeyLimiter => GenerateUI.Tr("SECTION_KEY_LIMITER", "Key Limiter"),
        OriginalMenuState.GameplayChatter => GenerateUI.Tr("SECTION_KEYBOARD_CHATTER_BLOCKER", "Keyboard Chatter Blocker"),
        OriginalMenuState.GameplayJudgement => GenerateUI.Tr("SECTION_JUDGEMENT_RESTRICTION", "Judgement Restriction"),
        OriginalMenuState.GameplayDeath => GenerateUI.Tr("SECTION_DEATH_LIMIT", "Death Limit"),
        OriginalMenuState.GameplayAutoDeafen => GenerateUI.Tr("SECTION_AUTO_DEAFEN_DISCORD", "Auto Deafen (Discord)"),
        OriginalMenuState.VisualsEffectRemover => GenerateUI.Tr("SECTION_EFFECT_REMOVER", "Effect Remover"),
        OriginalMenuState.VisualsHideJudgements => GenerateUI.Tr("SECTION_HIDE_JUDGEMENTS", "Hide Judgements"),
        OriginalMenuState.VisualsVisualTweaks => GenerateUI.Tr("SECTION_VISUAL_TWEAKS", "Visual Tweaks"),
        OriginalMenuState.VisualsPlanetColors => GenerateUI.Tr("SECTION_PLANET_COLORS", "Planet Colors"),
        OriginalMenuState.VisualsOttoIcon => GenerateUI.Tr("SECTION_OTTO_ICON", "Otto Icon"),
        OriginalMenuState.VisualsUiHiding => GenerateUI.Tr("SECTION_UI_HIDING", "UI Hiding"),
        OriginalMenuState.TweaksGeneral => GenerateUI.Tr("TWEAKS_GENERAL", "General"),
        OriginalMenuState.TweaksOptimizer => GenerateUI.Tr("SECTION_OPTIMIZER", "Optimizer"),
        OriginalMenuState.TweaksMainMenu => GenerateUI.Tr("SECTION_MAIN_MENU", "Main Menu"),
        OriginalMenuState.TweaksResults => GenerateUI.Tr("SECTION_DETAILED_RESULTS", "Detailed Results"),
        OriginalMenuState.EditorInspector => GenerateUI.Tr("SECTION_INSPECTOR", "Inspector"),
        OriginalMenuState.EditorTileReadout => GenerateUI.Tr("SECTION_SELECTED_TILE_READOUT", "Selected Tile Readout"),
        OriginalMenuState.EditorBga => GenerateUI.Tr("SECTION_BGA_MOD", "BGA Mod"),
        OriginalMenuState.NostalgiaGameplay or OriginalMenuState.NostalgiaVisuals
            or OriginalMenuState.NostalgiaTweaks or OriginalMenuState.NostalgiaEditor
            => GenerateUI.Tr("NOSTALGIA", "Nostalgia"),
        OriginalMenuState.NostalgiaTuf => GenerateUI.Tr("TUF", "TUF"),
        OriginalMenuState.NostalgiaTufSettings => GenerateUI.Tr("TUF", "TUF") + " · " + GenerateUI.Tr("TUF_SETTINGS", "Settings"),
        OriginalMenuState.Profiles => GenerateUI.Tr("PROFILES", "Profiles"),
        OriginalMenuState.Import => GenerateUI.Tr("IMPORT", "Import"),
        OriginalMenuState.Addons => GenerateUI.Tr("ADDONS", "Addons"),
        OriginalMenuState.Settings => GenerateUI.Tr("SETTINGS", "Settings"),
        OriginalMenuState.Credits => GenerateUI.Tr("CREDITS", "Credits"),
        OriginalMenuState.Developer => GenerateUI.Tr("DEVELOPER", "Developer"),
        _ => "?",
    };
    private static void RunSearch(string query) {
        if(resultsContainer == null) return;
        GenerateUI.ClearChildren(resultsContainer);
        string q = Norm(query ?? "");
        if(string.IsNullOrWhiteSpace(q)) {
            statusText.text = GenerateUI.Tr("SEARCH_HINT", "Type to search every tab — categories, toggles, buttons, everything.");
            return;
        }
        List<Entry> matches = [];
        foreach(Entry e in Index())
            if(e.NormText.Contains(q)) matches.Add(e);
        matches.Sort((a, b) => {
            bool ap = a.NormText.StartsWith(q);
            bool bp = b.NormText.StartsWith(q);
            if(ap != bp) return ap ? -1 : 1;
            if(a.IsCategory != b.IsCategory) return a.IsCategory ? -1 : 1;
            return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
        });
        if(matches.Count == 0) {
            statusText.text = GenerateUI.Tr("SEARCH_NO_RESULTS", "No results.");
            return;
        }
        int shown = Mathf.Min(matches.Count, MAX_RESULTS);
        statusText.text = matches.Count > MAX_RESULTS
            ? string.Format(GenerateUI.Tr("SEARCH_RESULTS_CAPPED", "{0} results (showing first {1})"), matches.Count, MAX_RESULTS)
            : string.Format(GenerateUI.Tr("SEARCH_RESULTS", "{0} result(s)"), matches.Count);
        for(int i = 0; i < shown; i++)
            AddResultRow(matches[i]);
    }
    private static void AddResultRow(Entry e) {
        var row = GenerateUI.Row(resultsContainer);
        string text = e.Text.Length > 60 ? e.Text[..57] + "…" : e.Text;
        string path = string.IsNullOrEmpty(e.Section)
            ? TabName(e.State)
            : $"{TabName(e.State)} › {e.Section}";
        var btn = GenerateUI.Button(
            row,
            () => Navigate(e),
            $"<alpha=#77>{path} ›<alpha=#FF> {text}",
            "search_result"
        ).SetSecondary();
        btn.Label.overflowMode = TextOverflowModes.Ellipsis;
        btn.Label.fontSize = 19f;
    }
    private static List<Entry> Index() {
        if(cachedIndex != null
            && cachedIndexLang == MainCore.Conf.Language
            && Time.unscaledTime - cachedIndexTime < INDEX_TTL_SECONDS) {
            return cachedIndex;
        }
        cachedIndex = BuildIndex();
        foreach(Entry e in cachedIndex) e.NormText = Norm(e.Text);
        cachedIndexTime = Time.unscaledTime;
        cachedIndexLang = MainCore.Conf.Language;
        return cachedIndex;
    }
    private static List<Entry> BuildIndex() {
        List<Entry> list = [];
        foreach(KeyValuePair<int, RectTransform> page in UICore.Pages) {
            if(page.Key == (int)OriginalMenuState.Search || page.Value == null) continue;
            Walk(page.Value, page.Key, null, list);
        }
        return list;
    }
    private static void Walk(Transform t, int state, string section, List<Entry> list) {
        for(int i = 0; i < t.childCount; i++) {
            Transform child = t.GetChild(i);
            string name = child.name;
            if(name == "List") continue;
            if(name.StartsWith("Section_")) {
                TMP_Text headerLabel = child.Find("Header/Bar/Label")?.GetComponent<TMP_Text>();
                string title = headerLabel != null ? headerLabel.text : name["Section_".Length..];
                list.Add(new Entry {
                    State = state,
                    Text = title,
                    Section = section,
                    Target = (RectTransform)child,
                    IsCategory = true,
                });
                Transform body = child.Find("Body");
                if(body != null) Walk(body, state, title, list);
                continue;
            }
            if((name == "Text" || name == "Label") && child.TryGetComponent(out TMP_Text tmp)) {
                string text = tmp.text;
                if(!string.IsNullOrWhiteSpace(text) && child.GetComponent<TextLocalization>() != null) {
                    RectTransform target = RowTarget(child, state);
                    if(target != null) {
                        list.Add(new Entry {
                            State = state,
                            Text = text,
                            Section = section,
                            Target = target,
                        });
                    }
                }
            }
            Walk(child, state, section, list);
        }
    }
    private static RectTransform RowTarget(Transform label, int state) {
        RectTransform page = UICore.Pages[state];
        Transform cur = label;
        while(cur.parent != null && cur != page) {
            if(cur.parent.GetComponent<VerticalLayoutGroup>() != null) return cur as RectTransform;
            cur = cur.parent;
        }
        return null;
    }
    private static void Navigate(Entry e) {
        if(e.Target == null) return;
        foreach(GenerateUI.CollapsibleSection s in GenerateUI.Sections) {
            if(s.Body == null) continue;
            if(e.Target == s.Section || e.Target.IsChildOf(s.Body)) s.SetExpanded(true, false, false);
        }
        MenuFactory.SetState(e.State);
        RectTransform page = UICore.Pages[e.State];
        UIScrollController scroller = page.GetComponentInChildren<UIScrollController>(true);
        if(scroller == null || scroller.content == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroller.content);
        Vector3 worldCenter = e.Target.TransformPoint(e.Target.rect.center);
        float localY = scroller.content.InverseTransformPoint(worldCenter).y;
        float top = -localY - (e.Target.rect.height * 0.5f);
        scroller.ScrollTo(top - 8f);
        Flash(e.Target);
    }
    private static void Flash(RectTransform target) {
        GameObject flash = new("SearchFlash");
        flash.transform.SetParent(target, false);
        RectTransform rect = flash.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-250f, 0f);
        Image img = flash.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;
        Color accent = UIColors.ObjectActive;
        img.color = new Color(accent.r, accent.g, accent.b, 0.45f);
        img.raycastTarget = false;
        var seq = GTweenSequenceBuilder.New()
            .AppendTime(0.35f)
            .Append(GTweenExtensions.Tween(
                () => img.color.a,
                a => {
                    Color c = img.color;
                    c.a = a;
                    img.color = c;
                },
                0f,
                0.8f
            ).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(flash != null) Object.Destroy(flash);
            })
            .Build();
        MainCore.TC.Play(seq);
    }
}
