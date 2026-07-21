using HarmonyLib;
using Quartz.Core;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Quartz.Compat.Game;
namespace Quartz.Features.InGameOverlay;
public static class InGameOverlayFont {
    public enum Category { SongTitle, Countdown, Judgement }
    private sealed class Capture {
        public Category Cat;
        public TMP_Text Tmp;
        public TMP_FontAsset Original;
        public float OriginalSize;
        public object OriginalWrap;
    }
    private static readonly Dictionary<int, Capture> tmpCaptures = [];
    private static bool hooked;
    private static bool TitleActive => MainCore.IsModEnabled && MainCore.Conf.FontSongTitle && FontManager.Current != null;
    private static bool CountdownActive => MainCore.IsModEnabled && MainCore.Conf.FontCountdown && FontManager.Current != null;
    private static bool JudgementActive => MainCore.IsModEnabled && MainCore.Conf.FontJudgement && FontManager.Current != null;
    public static void Initialize() {
        if(hooked) return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    public static void Unhook() {
        if(hooked) {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            hooked = false;
        }
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Async.MainThread.Enqueue(Refresh);
    }
    public static void Refresh() {
        PruneDeadCaptures();
        RefreshTitle();
        RefreshCountdown();
        RefreshJudgement();
    }
    private static void PruneDeadCaptures() {
        List<int> dead = null;
        foreach(var kv in tmpCaptures)
            if(kv.Value.Tmp == null) (dead ??= []).Add(kv.Key);
        if(dead != null) foreach(int id in dead) tmpCaptures.Remove(id);
    }
    private static void RefreshTitle() {
        if(!TitleActive) { RestoreCategory(Category.SongTitle); return; }
        foreach(scrHUDText hud in UnityEngine.Object.FindObjectsByType<scrHUDText>(FindObjectsSortMode.None)) {
            if(!hud.isTitle) continue;
            ApplyToHudObject(hud.gameObject, Category.SongTitle);
        }
    }
    private static void RefreshCountdown() {
        if(!CountdownActive) { RestoreCategory(Category.Countdown); return; }
        foreach(scrCountdown cd in UnityEngine.Object.FindObjectsByType<scrCountdown>(FindObjectsSortMode.None)) {
            ApplyToHudObject(cd.gameObject, Category.Countdown);
        }
    }
    private static void RefreshJudgement() {
        if(!JudgementActive) RestoreCategory(Category.Judgement);
    }
    private static void ApplyToHudObject(GameObject go, Category cat) {
        var tmp = go.GetComponent<TMP_Text>();
        if(tmp != null) { OverrideTmp(tmp, cat); return; }
        var text = go.GetComponent<Text>();
        if(text != null) GameFontMirror.Ensure()?.Track(text, cat);
    }
    private static void RestoreCategory(Category cat) {
        GameFontMirror.Instance?.RestoreCategory(cat);
        List<int> dead = null;
        foreach(var kv in tmpCaptures) {
            if(kv.Value.Cat != cat) continue;
            if(kv.Value.Tmp != null) {
                kv.Value.Tmp.font = kv.Value.Original;
                TextCompat.RestoreWrap(kv.Value.Tmp, kv.Value.OriginalWrap);
                kv.Value.Tmp.fontSize = kv.Value.OriginalSize;
            }
            (dead ??= []).Add(kv.Key);
        }
        if(dead != null) foreach(int id in dead) tmpCaptures.Remove(id);
    }
    public static void RestoreAll() {
        GameFontMirror.DisposeInstance();
        foreach(Capture cap in tmpCaptures.Values) {
            if(cap.Tmp == null) continue;
            cap.Tmp.font = cap.Original;
            TextCompat.RestoreWrap(cap.Tmp, cap.OriginalWrap);
            cap.Tmp.fontSize = cap.OriginalSize;
        }
        tmpCaptures.Clear();
    }
    private static void OverrideTmp(TMP_Text tmp, Category cat) {
        TMP_FontAsset want = FontManager.Current;
        if(tmp == null || want == null) return;
        int id = tmp.GetInstanceID();
        if(!tmpCaptures.ContainsKey(id)) {
            float gameSize = tmp.fontSize;
            if(gameSize <= 0f) return;
            tmpCaptures[id] = new Capture {
                Cat = cat,
                Tmp = tmp,
                Original = tmp.font,
                OriginalSize = gameSize,
                OriginalWrap = TextCompat.CaptureWrap(tmp),
            };
            tmp.font = want;
            tmp.fontSharedMaterial = GameApi.FontMaterial(want);
            ApplySize(tmp, gameSize, cat);
        } else if(tmp.font != want) {
            tmp.font = want;
            tmp.fontSharedMaterial = GameApi.FontMaterial(want);
        }
    }
    private static void ApplySize(TMP_Text tmp, float gameSize, Category cat) {
        TextCompat.NoWrap(tmp);
        float boxW = tmp.rectTransform.rect.width;
        float wantW = tmp.GetPreferredValues(tmp.text).x;
        float fit = (boxW > 0f && wantW > boxW) ? gameSize * (boxW / wantW) * 0.98f : gameSize;
        tmp.fontSize = fit * SizeMultiplier(cat);
    }
    public static void RefreshSizeOnly(Category cat) {
        foreach(Capture cap in tmpCaptures.Values) {
            if(cap.Cat == cat && cap.Tmp != null) ApplySize(cap.Tmp, cap.OriginalSize, cat);
        }
    }
    internal static float SizeMultiplier(Category cat) => cat switch {
        Category.SongTitle => MainCore.Conf.FontSongTitleSize,
        Category.Countdown => MainCore.Conf.FontCountdownSize,
        Category.Judgement => MainCore.Conf.FontJudgementSize,
        _ => 1f,
    };
    [HarmonyPatch(typeof(RDString), "SetLocalizedFont", new[] { typeof(TMP_Text) })]
    private static class TmpFontPatch {
        private static void Postfix(TMP_Text text) {
            try {
                if(text == null) return;
                if(TitleActive && text.GetComponent<scrHUDText>() is { isTitle: true }) OverrideTmp(text, Category.SongTitle);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[InGameOverlayFont] TmpFontPatch: {e}");
            }
        }
    }
    [HarmonyPatch(typeof(RDString), "SetLocalizedFont", new[] { typeof(Text) })]
    private static class TextFontPatch {
        private static void Postfix(Text text) {
            try {
                if(text == null) return;
                if(TitleActive && text.GetComponent<scrHUDText>() is { isTitle: true }) {
                    GameFontMirror.Ensure()?.Track(text, Category.SongTitle);
                } else if(CountdownActive && text.GetComponent<scrCountdown>() != null) {
                    GameFontMirror.Ensure()?.Track(text, Category.Countdown);
                }
            } catch(Exception e) {
                MainCore.Log.Wrn($"[InGameOverlayFont] TextFontPatch: {e}");
            }
        }
    }
    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    private static class JudgementFontPatch {
        private static void Postfix(scrHitTextMesh __instance) {
            if(!JudgementActive) return;
            try {
                TMP_Text label = GameApi.HitTextLabel(__instance);
                if(label != null) OverrideTmp(label, Category.Judgement);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[InGameOverlayFont] JudgementFontPatch: {e}");
            }
        }
    }
}
internal sealed class GameFontMirror : MonoBehaviour {
    private sealed class Pair {
        public InGameOverlayFont.Category Cat;
        public Text Source;
        public TextMeshProUGUI Twin;
        public string LastRaw;
    }
    private const string TwinName = "QuartzInGameFontTwin";
    private static GameFontMirror instance;
    private static readonly HashSet<int> twinIds = [];
    private readonly List<Pair> pairs = [];
    private readonly HashSet<int> trackedSources = [];
    public static GameFontMirror Instance => instance;
    public static GameFontMirror Ensure() {
        if(instance == null && MainCore.Root != null) instance = MainCore.Root.AddComponent<GameFontMirror>();
        return instance;
    }
    public static void DisposeInstance() {
        if(instance != null) {
            instance.Clear();
            Destroy(instance);
            instance = null;
        }
        twinIds.Clear();
    }
    public void Track(Text source, InGameOverlayFont.Category cat) {
        if(source == null || !trackedSources.Add(source.GetInstanceID())) return;
        var twinGo = new GameObject(TwinName);
        twinGo.transform.SetParent(source.transform, false);
        var rt = twinGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        var twin = twinGo.AddComponent<TextMeshProUGUI>();
        twin.font = FontManager.Current;
        twin.raycastTarget = false;
        twinIds.Add(twin.GetInstanceID());
        var pair = new Pair { Cat = cat, Source = source, Twin = twin };
        pairs.Add(pair);
        Apply(pair);
    }
    public void RestoreCategory(InGameOverlayFont.Category cat) {
        for(int i = pairs.Count - 1; i >= 0; i--) {
            Pair pair = pairs[i];
            if(pair.Cat != cat) continue;
            if(pair.Twin != null) {
                twinIds.Remove(pair.Twin.GetInstanceID());
                Destroy(pair.Twin.gameObject);
            }
            if(pair.Source != null) {
                trackedSources.Remove(pair.Source.GetInstanceID());
                pair.Source.canvasRenderer.SetAlpha(1f);
            }
            pairs.RemoveAt(i);
        }
    }
    private int lastSyncFrame = -1;
    private void OnEnable() => Canvas.willRenderCanvases += SyncPairs;
    private void OnDisable() => Canvas.willRenderCanvases -= SyncPairs;
    private void SyncPairs() {
        int frame = Time.frameCount;
        if(lastSyncFrame == frame) return;
        lastSyncFrame = frame;
        for(int i = pairs.Count - 1; i >= 0; i--) {
            Pair pair = pairs[i];
            if(pair.Source == null || pair.Twin == null) {
                if(pair.Twin != null) {
                    twinIds.Remove(pair.Twin.GetInstanceID());
                    Destroy(pair.Twin.gameObject);
                }
                if(pair.Source != null) trackedSources.Remove(pair.Source.GetInstanceID());
                pairs.RemoveAt(i);
                continue;
            }
            Apply(pair);
        }
    }
    private static void Apply(Pair pair) {
        Text source = pair.Source;
        TextMeshProUGUI twin = pair.Twin;
        TMP_FontAsset want = FontManager.Current;
        if(want != null && twin.font != want) twin.font = want;
        if(pair.LastRaw != source.text) {
            pair.LastRaw = source.text;
            twin.text = source.text;
        }
        if(twin.color != source.color) twin.color = source.color;
        if(twin.richText != source.supportRichText) twin.richText = source.supportRichText;
        FontStyles style = MapStyle(source.fontStyle);
        if(twin.fontStyle != style) twin.fontStyle = style;
        TextAlignmentOptions alignment = MapAlignment(source.alignment);
        if(twin.alignment != alignment) twin.alignment = alignment;
        bool wrap = source.horizontalOverflow == HorizontalWrapMode.Wrap;
        if(TextCompat.GetWrap(twin) != wrap) TextCompat.SetWrap(twin, wrap);
        if(twin.overflowMode != TextOverflowModes.Overflow) twin.overflowMode = TextOverflowModes.Overflow;
        float mult = InGameOverlayFont.SizeMultiplier(pair.Cat);
        if(source.resizeTextForBestFit) {
            float maxPx = source.resizeTextMaxSize > 0 ? source.resizeTextMaxSize : source.fontSize;
            if(!twin.enableAutoSizing) twin.enableAutoSizing = true;
            if(twin.fontSizeMin != 1f) twin.fontSizeMin = 1f;
            float max = Mathf.Max(1f, maxPx) * mult;
            if(twin.fontSizeMax != max) twin.fontSizeMax = max;
        } else {
            if(twin.enableAutoSizing) twin.enableAutoSizing = false;
            float boxW = source.rectTransform.rect.width;
            float size = source.fontSize;
            if(boxW > 0f) {
                float wantW = twin.GetPreferredValues(twin.text).x;
                if(wantW > boxW) size = source.fontSize * (boxW / wantW) * 0.98f;
            }
            size *= mult;
            if(twin.fontSize != size) twin.fontSize = size;
        }
        if(twin.enabled != source.enabled) twin.enabled = source.enabled;
        float srcAlpha = source.canvasRenderer.GetAlpha();
        if(twin.canvasRenderer.GetAlpha() != srcAlpha) twin.canvasRenderer.SetAlpha(srcAlpha);
        source.canvasRenderer.SetAlpha(0f);
    }
    private static FontStyles MapStyle(FontStyle style) => style switch {
        FontStyle.Bold => FontStyles.Bold,
        FontStyle.Italic => FontStyles.Italic,
        FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
        _ => FontStyles.Normal,
    };
    private static TextAlignmentOptions MapAlignment(TextAnchor anchor) => anchor switch {
        TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
        TextAnchor.UpperCenter => TextAlignmentOptions.Top,
        TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
        TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
        TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
        TextAnchor.MiddleRight => TextAlignmentOptions.Right,
        TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
        TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
        TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
        _ => TextAlignmentOptions.Center,
    };
    private void Clear() {
        foreach(Pair pair in pairs) {
            if(pair.Twin != null) Destroy(pair.Twin.gameObject);
            if(pair.Source != null) pair.Source.canvasRenderer.SetAlpha(1f);
        }
        pairs.Clear();
        trackedSources.Clear();
    }
}
