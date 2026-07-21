using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.Features.SongTitle;
public static class SongTitleOverlay {
    public static SettingsFile<SongTitleSettings> ConfMgr { get; private set; }
    public static SongTitleSettings Conf => ConfMgr.Data;
    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static RectTransform root;
    private static TextMeshProUGUI text;
    private static GameObject dragObj;
    private static Updater updater;
    private static string bbArtist;
    private static string bbTitle;
    private static string bbFmt;
    private static bool bbReorg;
    private static string bbResult;
    private static string bbRawSource;
    private static string bbRawResult;
    private static readonly Dictionary<int, Graphic> hiddenTitleGraphics = [];
    public static void EnsureConf() => ConfMgr ??= SettingsFile<SongTitleSettings>.Loaded("SongTitle.json");
    public static bool TakesOverTitle {
        get {
            if(!MainCore.IsModEnabled) return false;
            EnsureConf();
            return PanelsOverlay.IsEnabled && Conf.Enabled;
        }
    }
    public static void Initialize(GameObject rootObject) {
        if(canvasObj != null) return;
        EnsureConf();
        canvasObj = UnityUtils.CreateOverlayCanvas("QuartzSongTitleCanvas", rootObject.transform, 32756, out raycaster);
        GameObject titleObj = new("SongTitle");
        titleObj.transform.SetParent(canvasObj.transform, false);
        root = titleObj.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        GameObject labelObj = new("Label");
        labelObj.transform.SetParent(root, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        text = labelObj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.alignment = TextAlignmentOptions.Top;
        text.raycastTarget = false;
        TextCompat.NoWrap(text);
        text.text = "";
        dragObj = ReorganizeHandle.CreateDragSurface(root, () => MainCore.Tr.Get("SONG_TITLE", "Song Title"), Save);
        updater = canvasObj.AddComponent<Updater>();
        Apply();
    }
    public static void Apply() {
        if(root == null) return;
        root.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.OffsetX, Conf.OffsetY));
        root.localScale = Vector3.one * Mathf.Max(0.01f, Conf.MasterSize);
        if(text != null) {
            text.font = FontManager.Current;
            text.fontSize = Mathf.Clamp(Conf.FontSize, 4f, 400f);
            text.color = Conf.GetColor();
            ApplyShadow();
        }
    }
    public static void ApplyShadow() {
        if(text == null) return;
        TMPTextShadow.Apply(
            text,
            Conf.ShadowEnabled,
            Conf.ShadowX,
            Conf.ShadowY,
            Conf.ShadowSoftness,
            Conf.GetShadowColor(),
            isolateCanvas: true
        );
    }
    public static void Save() => ConfMgr?.Save();
    public static void ResetPosition() {
        SongTitleSettings def = new();
        Conf.OffsetX = def.OffsetX;
        Conf.OffsetY = def.OffsetY;
        Apply();
        Save();
    }
    public static void Dispose() {
        if(canvasObj == null) return;
        ConfMgr?.Save();
        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        root = null;
        text = null;
        dragObj = null;
        updater = null;
        hiddenTitleGraphics.Clear();
    }
    internal static string BuildBody(bool isReorganizing) {
        string artist = GameStats.SongArtist;
        string title = GameStats.SongTitle;
        if(string.IsNullOrEmpty(artist) && string.IsNullOrEmpty(title)) {
            if(isReorganizing) {
                artist = MainCore.Tr.Get("SONGTITLE_PLACEHOLDER_ARTIST", "Artist");
                title = MainCore.Tr.Get("SONGTITLE_PLACEHOLDER_TITLE", "Title");
            } else {
                string raw = GameStats.SongTitleRaw;
                if(bbRawResult == null || raw != bbRawSource) {
                    bbRawSource = raw;
                    bbRawResult = NormalizeColorTags(raw);
                }
                return bbRawResult;
            }
        }
        string fmt = string.IsNullOrEmpty(Conf.Format) ? "{artist} - {title}" : Conf.Format;
        if(bbResult != null && isReorganizing == bbReorg
            && artist == bbArtist && title == bbTitle && fmt == bbFmt) {
            return bbResult;
        }
        string result = NormalizeColorTags(fmt.Replace("{artist}", artist).Replace("{title}", title));
        bbArtist = artist;
        bbTitle = title;
        bbFmt = fmt;
        bbReorg = isReorganizing;
        bbResult = result;
        return result;
    }
    private static readonly Regex HexColorTagRegex =
        new(@"<color=#([0-9a-fA-F]+)>", RegexOptions.IgnoreCase);
    private static string NormalizeColorTags(string s) {
        if(string.IsNullOrEmpty(s) || s.IndexOf("<color=#", System.StringComparison.OrdinalIgnoreCase) < 0) return s;
        return HexColorTagRegex.Replace(s, m => {
            string hex = m.Groups[1].Value;
            int valid = hex.Length switch {
                >= 8 => 8,
                6 or 7 => 6,
                4 or 5 => 4,
                3 => 3,
                _ => 0,
            };
            if(valid == 0) return string.Empty;
            return valid == hex.Length ? m.Value : $"<color=#{hex.Substring(0, valid)}>";
        });
    }
    private sealed class Updater : MonoBehaviour {
        private string lastBody;
        private void Update() {
            if(root == null || text == null) return;
            bool isReorganizing = UICore.IsReorganizing;
            bool show = (PanelsOverlay.IsEnabled && Conf.Enabled && GameStats.InGame) || isReorganizing;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if(dragObj != null && dragObj.activeSelf != isReorganizing) dragObj.SetActive(isReorganizing);
            if(!show) return;
            if(isReorganizing) {
                Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                Conf.OffsetX = stored.x;
                Conf.OffsetY = stored.y;
            }
            TMP_FontAsset font = FontManager.Current;
            if(text.font != font) {
                text.font = font;
                ApplyShadow();
            }
            string body = BuildBody(isReorganizing);
            if(body != lastBody) {
                text.text = body;
                lastBody = body;
                ApplyShadow();
            }
        }
    }
    [HarmonyPatch(typeof(scrHUDText), "Update")]
    private static class HideGameTitlePatch {
        private static void Postfix(scrHUDText __instance) {
            try {
                if(__instance == null || !__instance.isTitle) return;
                if(!TakesOverTitle || !GameStats.InGame) return;
                int id = __instance.GetInstanceID();
                if(!hiddenTitleGraphics.TryGetValue(id, out Graphic g)) {
                    PruneDestroyedTitleGraphics();
                    g = __instance.GetComponent<Graphic>();
                    hiddenTitleGraphics[id] = g;
                } else if(g == null) {
                    g = __instance.GetComponent<Graphic>();
                    hiddenTitleGraphics[id] = g;
                }
                if(g != null && g.enabled) g.enabled = false;
            } catch {
            }
        }
        private static void PruneDestroyedTitleGraphics() {
            List<int> dead = null;
            foreach(KeyValuePair<int, Graphic> kv in hiddenTitleGraphics)
                if(kv.Value == null) (dead ??= []).Add(kv.Key);
            if(dead != null) foreach(int id in dead) hiddenTitleGraphics.Remove(id);
        }
    }
}
