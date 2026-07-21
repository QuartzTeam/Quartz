using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvInspector {
    private static readonly string[] Placements = ["inside", "outside"];
    private static readonly string[] CounterAligns = ["top", "bottom", "left", "right"];
    private static readonly string[] AlignModes = ["center", "between"];
    private void BuildCounterTab(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        if(batch.Length == 0) return;
        Header(root, "KVI_SEC_COUNTER", "Counter");
        Flag(root, tracked, "Show Counter", "kvi_counter_enabled", true,
            batch, el => KvProps.Bool(Read(el), "enabled", true),
            (el, v) => Counter(el)["enabled"] = v
        ).Rect.AddToolTip(
            "DESC_KVI_COUNTER_ENABLED",
            "Draw this element's press count. The Key Viewer's own Show Counter setting still has to be on."
        );
        KvElement[] keys = OfKind(batch, KvElementKind.Key);
        if(keys.Length > 0) {
            Flag(root, tracked, "Per-Key KPS", "kvi_counter_perkeykps", false,
                keys, el => el.PerKeyKps, (el, v) => el.PerKeyKps = v
            ).Rect.AddToolTip(
                "DESC_KVI_COUNTER_PERKEYKPS",
                "This key's counter shows its own presses-per-second instead of its running total. The total is still counted."
            );
            Flag(root, tracked, "Show Counter While Pressing", "kvi_counter_showwhilepressing", true,
                keys, el => el.CounterShowWhilePressed, (el, v) => el.CounterShowWhilePressed = v
            ).Rect.AddToolTip(
                "DESC_KVI_COUNTER_SHOWWHILEPRESSING",
                "Keep the counter on screen while the key is held. Off hides it for as long as the key is down and brings it back on release; its space is still reserved, so the label does not move."
            );
        }
        Header(root, "KVI_SEC_COUNTER_PLACE", "Placement");
        CounterSegments(root, batch, Placements, PlaceName, PlaceKey, "placement", "inside");
        CounterSegments(root, batch, CounterAligns, CounterAlignName, CounterAlignKey, "align", "top");
        CounterSegments(root, batch, AlignModes, ModeName, ModeKey, "alignMode", "center");
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(root, 44f), "KVI_COUNTER_MODE_HINT",
            "Center draws the label and count as one line; Between pushes them to opposite edges.",
            15f, 0.45f
        );
        Num(root, tracked, "Counter Gap", "kvi_counter_gap", 6f, 0f, 100f, "0 px", 1f,
            batch, el => KvProps.Float(Read(el), "gap", 6f),
            (el, v) => KvProps.SetInt(Counter(el), "gap", v));
        Num(root, tracked, "Counter Font Size", "kvi_counter_font", 16f, 1f, 200f, "0 px", 1f,
            batch, el => KvProps.Int(Read(el), "fontSize", 16),
            (el, v) => KvProps.SetInt(Counter(el), "fontSize", v));
        FontStyleRows(root, tracked, batch, "kvi_counter", Read, Counter);
        Header(root, "KVI_SEC_COUNTER_ANIM", "Animation");
        Flag(root, tracked, "Press Animation", "kvi_counter_anim", true,
            batch, el => KvProps.Bool(AnimOrNull(el), "enabled", true),
            (el, v) => Anim(el)["enabled"] = v);
        Num(root, tracked, "Animation Scale", "kvi_counter_anim_scale", 110f, 100f, 200f, "0' %'", 1f,
            batch, el => KvProps.Float(AnimOrNull(el), "scale", 1.1f) * 100f,
            (el, v) => Anim(el)["scale"] = Mathf.Round(v) / 100f);
        Num(root, tracked, "Animation Duration", "kvi_counter_anim_duration", 300f, 50f, 2000f, "0 ms", 10f,
            batch, el => KvProps.Float(AnimOrNull(el), "durationMs", 300f),
            (el, v) => KvProps.SetInt(Anim(el), "durationMs", v));
        Header(root, "KVI_SEC_COUNTER_COLORS", "Counter Colors");
        CounterColor(root, tracked, batch, "Counter Text", "kvi_counter_fill", "fill", "idle",
            el => KvProps.Str(el.Raw, "fontColor", DefFont), 1f);
        CounterColor(root, tracked, batch, "Counter Text (Pressed)", "kvi_counter_fill_active", "fill", "active",
            el => KvProps.Str(el.Raw, "activeFontColor", DefFontActive), 1f);
        CounterColor(root, tracked, batch, "Counter Outline", "kvi_counter_stroke", "stroke", "idle",
            _ => "transparent", 0f);
        CounterColor(root, tracked, batch, "Counter Outline (Pressed)", "kvi_counter_stroke_active", "stroke", "active",
            _ => "transparent", 0f);
    }
    private void CounterSegments(
        RectTransform root, KvElement[] batch, string[] values,
        Func<string, string> name, Func<string, string> key, string field, string def
    ) => Segments(root, values, name, key,
        MatchMulti(values, batch, el => KvProps.Str(Read(el), field, def), def),
        v => {
            Edit(() => {
                foreach(KvElement el in batch) Counter(el)[field] = v;
            });
            Push();
        });
    private void CounterColor(
        RectTransform root, List<UIObject> tracked, KvElement[] batch,
        string label, string id, string groupKey, string field,
        Func<KvElement, string> def, float defAlpha
    ) => Colour(
        root, tracked, label, id,
        KeyViewerOverlay.HexToColor(def(batch[0]), defAlpha),
        batch,
        el => KvProps.Color(KvProps.ChildOrNull(Read(el), groupKey), field, def(el), defAlpha),
        (el, c) => KvProps.SetColor(KvProps.Child(Counter(el), groupKey), field, c), true
    );
    private static JObject Read(KvElement el) => KvProps.ChildOrNull(el.Raw, "counter");
    private static JObject Counter(KvElement el) => KvProps.Child(el.Raw, "counter");
    private static JObject AnimOrNull(KvElement el) => KvProps.ChildOrNull(Read(el), "animation");
    private static JObject Anim(KvElement el) => KvProps.Child(Counter(el), "animation");
    private static string PlaceName(string s) => s == "outside"
        ? MainCore.Tr.Get("KVI_PLACE_OUTSIDE", "Outside")
        : MainCore.Tr.Get("KVI_PLACE_INSIDE", "Inside");
    private static string PlaceKey(string s) => s == "outside" ? "KVI_PLACE_OUTSIDE" : "KVI_PLACE_INSIDE";
    private static string CounterAlignName(string s) => s switch {
        "bottom" => MainCore.Tr.Get("KVI_CALIGN_BOTTOM", "Bottom"),
        "left" => MainCore.Tr.Get("KVI_CALIGN_LEFT", "Left"),
        "right" => MainCore.Tr.Get("KVI_CALIGN_RIGHT", "Right"),
        _ => MainCore.Tr.Get("KVI_CALIGN_TOP", "Top"),
    };
    private static string CounterAlignKey(string s) => s switch {
        "bottom" => "KVI_CALIGN_BOTTOM",
        "left" => "KVI_CALIGN_LEFT",
        "right" => "KVI_CALIGN_RIGHT",
        _ => "KVI_CALIGN_TOP",
    };
    private static string ModeName(string s) => s == "between"
        ? MainCore.Tr.Get("KVI_CMODE_BETWEEN", "Between")
        : MainCore.Tr.Get("KVI_CMODE_CENTER", "Center");
    private static string ModeKey(string s) => s == "between" ? "KVI_CMODE_BETWEEN" : "KVI_CMODE_CENTER";
}
