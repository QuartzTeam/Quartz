using System.Globalization;
using System.Text;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
namespace Quartz.Features.Panels;
public static partial class PanelsOverlay {
    private sealed class LivePanel {
        public PanelConfig Config;
        public RectTransform Rect;
        public Image Background;
        public GameObject DragObj;
        public TextMeshProUGUI Text;
        public string LastBody;
        public bool Dirty = true;
        public string SeparatorSource;
        public string Separator;
        public Func<string, string> ResolveToken;
    }
    private sealed class Updater : MonoBehaviour {
        private const float TextRefreshInterval = 0.05f;
        private readonly StringBuilder sb = new();
        private float nextTextRefresh;
        private bool lastShow;
        private bool lastReorganizing;
        private void Update() {
            bool isReorganizing = UICore.IsReorganizing;
            bool show = (IsEnabled && GameStats.InGame) || isReorganizing;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            float now = Time.unscaledTime;
            bool stateChanged = show != lastShow || isReorganizing != lastReorganizing;
            bool refreshText = stateChanged || now >= nextTextRefresh;
            if(refreshText) nextTextRefresh = now + TextRefreshInterval;
            lastShow = show;
            lastReorganizing = isReorganizing;
            foreach(LivePanel p in panels)
                UpdatePanel(p, show, isReorganizing, refreshText || p.Dirty);
        }
        private void UpdatePanel(LivePanel p, bool show, bool isReorganizing, bool refreshText) {
            if(p?.Text == null || p.Rect == null) return;
            if(p.DragObj != null && p.DragObj.activeSelf != isReorganizing) p.DragObj.SetActive(isReorganizing);
            if(!show) {
                if(p.Rect.gameObject.activeSelf) p.Rect.gameObject.SetActive(false);
                return;
            }
            if(!refreshText) {
                if(isReorganizing) SyncPosition(p);
                return;
            }
            sb.Clear();
            if(show) {
                PanelConfig c = p.Config;
                if(p.Separator == null || p.SeparatorSource != c.LabelSeparator) {
                    p.SeparatorSource = c.LabelSeparator;
                    p.Separator = EffectiveSeparator(c.LabelSeparator);
                }
                string separator = p.Separator;
                if(!string.IsNullOrEmpty(c.Prefix)) sb.AppendLine(c.Prefix);
                for(int i = 0; i < c.Stats.Count; i++) {
                    StatEntry entry = c.Stats[i];
                    if(!entry.Enabled) continue;
                    StatDef stat = FindStat(entry.Id);
                    if(stat == null) continue;
                    string value;
                    if(entry.Id == "text") {
                        if(string.IsNullOrEmpty(entry.Text)) continue;
                        value = Quartz.Addons.AddonTags.Interpolate(entry.Text, p.ResolveToken ??= MakeResolver(c));
                        if(string.IsNullOrEmpty(value)) continue;
                    } else {
                        try { value = stat.Value(c); }
                        catch { continue; }
                        if(value == null) continue;
                    }
                    if(entry.ShowLabel) {
                        string label = c.LocalizeStatLabels
                            ? LocalizedStatLabel(stat)
                            : stat.Label;
                        sb.Append(label).Append(separator);
                    }
                    StatColor color = entry.Color;
                    if(color is { Enabled: true }) {
                        Color tint = color.Evaluate(
                            ColorRatio(entry.Id, color),
                            // Percent stats round at the panel's decimals; the perfect colour has
                            // to agree with the number the user actually reads.
                            IsPercentStat(entry.Id) ? Mathf.Clamp(c.Decimals, 0, 6) : -1
                        );
                        sb.Append("<color=#");
                        AppendHex(sb, tint);
                        sb.Append('>').Append(value).AppendLine("</color>");
                    } else {
                        sb.AppendLine(value);
                    }
                }
            }
            int bodyLength = TrimmedBodyLength(sb);
            string body = BuilderEquals(sb, bodyLength, p.LastBody)
                ? p.LastBody
                : bodyLength == 0 ? "" : sb.ToString(0, bodyLength);
            if(isReorganizing && body.Length == 0) body = p.Config.Name;
            bool active = body.Length > 0 || isReorganizing;
            if(p.Rect.gameObject.activeSelf != active) {
                p.Rect.gameObject.SetActive(active);
                if(active) p.Dirty = true;
            }
            if(!active) return;
            TMP_FontAsset font = FontManager.Current;
            bool fontChanged = p.Text.font != font;
            if(fontChanged) p.Text.font = font;
            if(fontChanged || p.Dirty || body != p.LastBody) {
                p.Text.text = body;
                Vector2 pref = p.Text.GetPreferredValues(body);
                p.Rect.sizeDelta = new Vector2(pref.x + PadX * 2f, pref.y + PadY * 2f);
                TMPTextShadow.Apply(
                    p.Text,
                    p.Config.TextShadowEnabled,
                    p.Config.TextShadowX,
                    p.Config.TextShadowY,
                    p.Config.TextShadowSoftness,
                    p.Config.GetTextShadowColor()
                );
                p.LastBody = body;
                p.Dirty = false;
            }
            if(isReorganizing) SyncPosition(p);
        }
        private static StatDef FindStat(string id) =>
            id != null && CatalogById.TryGetValue(id, out StatDef stat) ? stat : null;
        // Built once per panel: an inline lambda here would capture a local and
        // allocate a display class on every 20Hz refresh of every panel.
        private static Func<string, string> MakeResolver(PanelConfig config) =>
            name => ResolveStatToken(name, config);
        private static string ResolveStatToken(string name, PanelConfig config) {
            if(name == "text" || !CatalogById.TryGetValue(name, out StatDef stat)) return null;
            try {
                return stat.Value(config) ?? "";
            } catch {
                return "";
            }
        }
        private static void SyncPosition(LivePanel p) {
            Vector2 stored = OverlayCalibration.Unscale(p.Rect.anchoredPosition);
            p.Config.PosX = stored.x;
            p.Config.PosY = stored.y;
        }
        private static void AppendHex(StringBuilder sb, Color tint) {
            Color32 c = tint;
            AppendHexByte(sb, c.r);
            AppendHexByte(sb, c.g);
            AppendHexByte(sb, c.b);
            AppendHexByte(sb, c.a);
        }
        private static void AppendHexByte(StringBuilder sb, byte b) {
            const string hex = "0123456789ABCDEF";
            sb.Append(hex[b >> 4]).Append(hex[b & 0xF]);
        }
        private static int TrimmedBodyLength(StringBuilder sb) {
            int len = sb.Length;
            while(len > 0 && char.IsWhiteSpace(sb[len - 1])) len--;
            return len;
        }
        private static bool BuilderEquals(StringBuilder sb, int length, string value) {
            if(value == null || value.Length != length) return false;
            for(int i = 0; i < length; i++)
                if(sb[i] != value[i]) return false;
            return true;
        }
        /// <summary>The stats Pct() renders — the ones whose colour gate must match the rounded
        /// display. See PanelsOverlay's stat catalog.</summary>
        private static bool IsPercentStat(string id) =>
            id is "progress" or "accuracy" or "xaccuracy" or "maxaccuracy" or "best";
        private static float ColorRatio(string id, StatColor color) {
            try {
                switch(id) {
                    case "progress": return GameStats.Progress;
                    case "accuracy": return GameStats.Accuracy;
                    case "xaccuracy": return GameStats.XAccuracy;
                    case "maxaccuracy": return GameStats.MaxXAccuracy;
                    case "musictime": return GameStats.MusicTimeRatio;
                    case "maptime": return GameStats.MapTimeRatio;
                    case "best": return GameStats.Best;
                    case "tbpm": {
                        GameStats.GetBpm(out float tbpm, out _);
                        return color.MaxBpm <= 0f ? 0f : tbpm / color.MaxBpm;
                    }
                    case "cbpm":
                    case "kps":
                    case "autokps": {
                        GameStats.GetBpm(out _, out float cbpm);
                        return color.MaxBpm <= 0f ? 0f : cbpm / color.MaxBpm;
                    }
                    default: return 1f;
                }
            } catch {
                return 1f;
            }
        }
    }
}
