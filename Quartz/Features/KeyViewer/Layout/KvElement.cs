using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Layout;
internal enum KvElementKind {
    Key,
    Stat,
    Graph,
    Knob,
}
internal sealed partial class KvElement {
    internal JObject Raw { get; }
    internal JObject Container =>
        Raw.Parent is JProperty { Name: "position" } prop && prop.Parent is JObject outer ? outer : Raw;
    internal KvElementKind Kind { get; }
    internal string GlobalKey { get; set; } = "";
    private KvElement(JObject raw, KvElementKind kind) {
        Raw = raw;
        Kind = kind;
    }
    internal static KvElement Wrap(JObject raw, KvElementKind kind, string globalKey = "") {
        EnsureRequired(raw, kind);
        return new KvElement(raw, kind) { GlobalKey = globalKey ?? "" };
    }
    private static void EnsureRequired(JObject raw, KvElementKind kind) {
        if(raw == null) return;
        if(raw["dx"] == null) raw["dx"] = 0f;
        if(raw["dy"] == null) raw["dy"] = 0f;
        if(raw["width"] == null) raw["width"] = 60f;
        if(raw["height"] == null) raw["height"] = 60f;
        if(raw["count"] == null) raw["count"] = 0;
        if(raw["noteColor"] == null) raw["noteColor"] = "#FFFFFF";
        if(raw["noteOpacity"] == null) raw["noteOpacity"] = 80;
        if(kind != KvElementKind.Graph) return;
        JObject outer = raw.Parent is JProperty { Name: "position" } prop && prop.Parent is JObject o ? o : raw;
        Fill("statType", "kps");
        Fill("graphType", "line");
        Fill("graphSpeed", 1000);
        Fill("graphColor", "#86EFAC");
        void Fill(string key, JToken value) {
            if(raw[key] == null) raw[key] = value;
            if(outer[key] == null) outer[key] = value.DeepClone();
        }
    }
    private float Num(string key, float fallback) {
        JToken t = Raw[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try { return t.ToObject<float>(); } catch { return fallback; }
    }
    private bool Flag(string key, bool fallback) {
        JToken t = Raw[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try { return t.ToObject<bool>(); } catch { return fallback; }
    }
    private string Str(string key, string fallback) {
        JToken t = Raw[key];
        return t == null || t.Type == JTokenType.Null ? fallback : t.ToString();
    }
    internal float X {
        get => Num("dx", 0f);
        set => Raw["dx"] = Clamp(value);
    }
    internal float Y {
        get => Num("dy", 0f);
        set => Raw["dy"] = Clamp(value);
    }
    internal float W {
        get => Num("width", DefaultW);
        set => Raw["width"] = Math.Max(MinSize, value);
    }
    internal float H {
        get => Num("height", DefaultH);
        set => Raw["height"] = Math.Max(MinSize, value);
    }
    internal float Z {
        get => Num("zIndex", 0f);
        set => Raw["zIndex"] = (int)Math.Round(value);
    }
    internal bool Hidden {
        get => Flag("hidden", false);
        set => Raw["hidden"] = value;
    }
    internal int Count {
        get => Math.Max(0, (int)Math.Round(Num("count", 0f)));
        set => Raw["count"] = Math.Max(0, value);
    }
    internal string DisplayText {
        get => Str("displayText", "");
        set {
            if(string.IsNullOrEmpty(value)) Raw.Remove("displayText");
            else Raw["displayText"] = value;
        }
    }
    internal string StatType {
        get {
            JObject outer = Container;
            if(!ReferenceEquals(outer, Raw)) {
                JToken t = outer["statType"];
                if(t != null && t.Type != JTokenType.Null && t.ToString().Length > 0) return t.ToString();
            }
            return Str("statType", "");
        }
        set {
            Raw["statType"] = value;
            JObject outer = Container;
            if(!ReferenceEquals(outer, Raw)) outer["statType"] = value;
        }
    }
    internal bool CountInTotal {
        get => Flag("quartzCountInTotal", true);
        set {
            if(value) Raw.Remove("quartzCountInTotal");
            else Raw["quartzCountInTotal"] = false;
        }
    }
    internal bool PerKeyKps {
        get => Flag("quartzPerKeyKps", false);
        set {
            if(value) Raw["quartzPerKeyKps"] = true;
            else Raw.Remove("quartzPerKeyKps");
        }
    }
    internal bool LabelEnabled {
        get => Flag("quartzLabelEnabled", true);
        set {
            if(value) Raw.Remove("quartzLabelEnabled");
            else Raw["quartzLabelEnabled"] = false;
        }
    }
    internal bool CounterShowWhilePressed {
        get => Flag("quartzCounterShowWhilePressed", true);
        set {
            if(value) Raw.Remove("quartzCounterShowWhilePressed");
            else Raw["quartzCounterShowWhilePressed"] = false;
        }
    }
    internal string PressedText {
        get => Str("quartzPressedText", "");
        set {
            if(string.IsNullOrEmpty(value)) Raw.Remove("quartzPressedText");
            else Raw["quartzPressedText"] = value;
        }
    }
    internal bool Foot {
        get => Flag("quartzFoot", false);
        set {
            if(value) Raw["quartzFoot"] = true;
            else Raw.Remove("quartzFoot");
        }
    }
    internal string GhostKey {
        get => Str("ghostKey", "");
        set {
            if(string.IsNullOrEmpty(value)) Raw.Remove("ghostKey");
            else Raw["ghostKey"] = value;
        }
    }
    internal const float MinSize = 10f;
    internal const float MinPosition = -8000f;
    internal const float MaxPosition = 8000f;
    private float DefaultW => Kind == KvElementKind.Graph ? 200f : 60f;
    private float DefaultH => Kind == KvElementKind.Graph ? 100f : 60f;
    private static float Clamp(float v) => Math.Clamp(v, MinPosition, MaxPosition);
    internal void MoveTo(float x, float y) {
        X = x;
        Y = y;
    }
    internal void Resize(float w, float h) {
        W = w;
        H = h;
    }
    internal KvElement Clone() => new((JObject)Raw.DeepClone(), Kind) { GlobalKey = GlobalKey };
}
