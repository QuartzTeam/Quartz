using System.Globalization;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    public static void Initialize(GameObject rootObject) {
        if(canvasObj != null) return;
        EnsureConf();
        canvasObj = new GameObject("QuartzKeyViewerCanvas");
        canvasObj.transform.SetParent(rootObject.transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32758;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;
        GameObject gridObj = new("KeyViewerGrid");
        gridObj.transform.SetParent(canvasObj.transform, false);
        root = gridObj.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        GameObject footObj = new("KeyViewerFoot");
        footObj.transform.SetParent(canvasObj.transform, false);
        footRoot = footObj.AddComponent<RectTransform>();
        footRoot.anchorMin = new Vector2(0.5f, 0f);
        footRoot.anchorMax = new Vector2(0.5f, 0f);
        footRoot.pivot = new Vector2(0.5f, 0f);
        rainManager = canvasObj.AddComponent<RainManager>();
        canvasObj.AddComponent<Updater>();
        Rebuild();
    }
    public static void Rebuild() {
        if(root == null) return;
        CaptureCounts();
        rainManager?.Clear();
        Quartz.UI.Generator.GenerateUI.ClearChildren(root);
        if(footRoot != null) {
            Quartz.UI.Generator.GenerateUI.ClearChildren(footRoot);
            footRoot.sizeDelta = Vector2.zero;
        }
        boxes.Clear();
        cssFx.Clear();
        cssGlowLayer = null;
        dragObj = null;
        footDragObj = null;
        kpsMax = 0;
        kpsSum = 0;
        kpsSamples = 0;
        nextKpsSample = 0f;
        inputWasActive = false;
        inputPrimed = false;
        if(Conf.IsDmNoteMode) {
            BuildDmNote();
            return;
        }
        if(!Conf.IsSimpleMode) {
            builtMode = null;
            builtStyle = -1;
            root.sizeDelta = Vector2.zero;
            Apply();
            return;
        }
        int style = Mathf.Clamp(Conf.Style, 0, KeyViewerSettings.MaxStyle);
        builtMode = KeyViewerSettings.ModeSimple;
        builtStyle = style;
        int[] keys = Conf.KeysForStyle(style);
        GameObject rainObj = new("RainLayer");
        rainObj.transform.SetParent(root, false);
        RectTransform rainLayer = rainObj.AddComponent<RectTransform>();
        rainLayer.anchorMin = Vector2.zero;
        rainLayer.anchorMax = Vector2.one;
        rainLayer.offsetMin = Vector2.zero;
        rainLayer.offsetMax = Vector2.zero;
        rainObj.AddComponent<Canvas>().overrideSorting = false;
        rainManager?.SetLayer(rainLayer);
        List<KeySlot> keySlots = [];
        List<StatSlot> statSlots = [];
        BuildLayout(style, keySlots, statSlots);
        foreach(KeySlot slot in keySlots) AddKey(keys, slot.Slot, slot.X, slot.Y, slot.W, slot.H);
        foreach(StatSlot slot in statSlots) AddStat(slot.Total, slot.X, slot.Y, slot.W, slot.H);
        root.sizeDelta = GridSize(style);
        BuildFoot();
        totalCount = 0;
        foreach(Box box in boxes)
            if(!box.IsStat) totalCount += box.Count;
        AddReorganizeHandle();
        Apply();
        SyncKeysToKeyLimiter();
    }
    public static event Action SyncSettingChanged;
    public static void RaiseSyncSettingChanged() => SyncSettingChanged?.Invoke();
    public static bool IsSyncingToKeyLimiter {
        get {
            EnsureConf();
            return Conf is { SyncToKeyLimiter: true } && Conf.IsSimpleMode;
        }
    }
    public static void SyncKeysToKeyLimiter() {
        EnsureConf();
        if(!Conf.IsSimpleMode || !Conf.SyncToKeyLimiter) return;
        Features.KeyLimiter.KeyLimiter.EnsureConf();
        int[] keys = Conf.KeysForStyle(Mathf.Clamp(Conf.Style, 0, KeyViewerSettings.MaxStyle));
        List<int> result = [];
        HashSet<int> seen = [];
        void AddKeys(int[] codes, int count) {
            for(int i = 0; i < count && i < codes.Length; i++) {
                int normalized = (int)Features.KeyLimiter.KeyLimiter.NormalizeKey((KeyCode)codes[i]);
                if(normalized != 0 && seen.Add(normalized)) result.Add(normalized);
            }
        }
        AddKeys(keys, keys.Length);
        AddKeys(Conf.FootKeysForStyle(Conf.FootStyle), Conf.FootKeyCount());
        if(result.Count == 0) return;
        int[] current = Features.KeyLimiter.KeyLimiter.Conf.AllowedKeys;
        if(current != null && current.Length == result.Count) {
            bool same = true;
            for(int i = 0; i < current.Length; i++) {
                if(current[i] != result[i]) {
                    same = false;
                    break;
                }
            }
            if(same) return;
        }
        Features.KeyLimiter.KeyLimiter.SetAllowedKeys([.. result]);
    }
    public static void Apply() {
        if(root == null) return;
        if(Conf.IsDmNoteMode) {
            if(builtMode != KeyViewerSettings.ModeDmNote) {
                Rebuild();
                return;
            }
            ApplyDmRuntimeSettings();
            root.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.DmOffsetX, Conf.DmOffsetY));
            float dmScale = Mathf.Clamp(Conf.DmScale, 0.2f, 4f);
            root.localScale = new Vector3(dmScale, dmScale, 1f);
            if(!Conf.DmNoteEffect) rainManager?.Clear();
            return;
        }
        if(!Conf.IsSimpleMode) {
            rainManager?.Clear();
            if(root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }
        if(builtMode != KeyViewerSettings.ModeSimple || builtStyle != Mathf.Clamp(Conf.Style, 0, KeyViewerSettings.MaxStyle)) {
            Rebuild();
            return;
        }
        root.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.OffsetX, Conf.OffsetY));
        float size = Mathf.Clamp(Conf.Size, 0.2f, 4f);
        root.localScale = new Vector3(size, size, 1f);
        if(footRoot != null) {
            footRoot.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.FootOffsetX, Conf.FootOffsetY));
            footRoot.localScale = new Vector3(size, size, 1f);
        }
        if(!Conf.RainEnabled) rainManager?.Clear();
        foreach(Box box in boxes) ApplyBoxColors(box);
    }
    public static void ResetPosition() {
        KeyViewerSettings def = new();
        if(Conf.IsDmNoteMode) {
            Conf.DmOffsetX = def.DmOffsetX;
            Conf.DmOffsetY = def.DmOffsetY;
        } else {
            Conf.OffsetX = def.OffsetX;
            Conf.OffsetY = def.OffsetY;
            Conf.FootOffsetX = def.FootOffsetX;
            Conf.FootOffsetY = def.FootOffsetY;
        }
        Apply();
        Save();
    }
    public static bool ImportDmNotePreset(out string error) =>
        ImportDmNoteFile(out error, "JSON Preset", "json", "Select DM Note preset", "preset",
            (text, _) => {
                Conf.DmPresetJson = KeyViewerPersistence.SanitizeDmPreset(text);
            });
    public static bool ImportDmNoteCss(out string error) =>
        ImportDmNoteFile(out error, "CSS", "css", "Select DM Note custom CSS", "CSS",
            (text, picked) => {
                Conf.DmCssText = text;
                Conf.DmCssPath = picked;
                Conf.DmCssEnabled = true;
            });
    private static bool ImportDmNoteFile(out string error, string filterName, string ext,
        string title, string what, Action<string, string> store) {
        error = null;
        string picked;
        try {
            picked = UnityFileDialog.FileBrowser.PickFile(
                "", filterName, new[] { ext }, title);
        } catch(Exception ex) {
            error = "Picker failed: " + ex.Message;
            MainCore.Log.Msg("[KeyViewer] " + error);
            return false;
        }
        if(string.IsNullOrEmpty(picked)) return false;
        try {
            string text = File.ReadAllText(picked);
            store(text, picked);
            Rebuild();
            Save();
            MainCore.Log.Msg("[KeyViewer] Imported DM Note " + what + " from " + picked);
            return true;
        } catch(Exception ex) {
            error = "Import failed: " + ex.Message;
            MainCore.Log.Msg("[KeyViewer] " + error);
            return false;
        }
    }
    public static void ResetCounts() {
        countsDirty = false;
        nextCountsSave = 0f;
        Conf.Counts.Clear();
        pressLog.Clear();
        kpsMax = 0;
        kpsSum = 0;
        kpsSamples = 0;
        totalCount = 0;
        foreach(Box box in boxes) {
            box.Count = 0;
            box.LastShown = int.MinValue;
        }
        Save();
    }
    private static void MarkCountsDirty(float now) {
        countsDirty = true;
        nextCountsSave = KeyViewerPersistence.CountSaveDeadline(now);
    }
    private static void TryFlushCounts(float now, bool inGame) {
        if(KeyViewerPersistence.ShouldFlushCounts(countsDirty, inGame, now, nextCountsSave)) FlushCounts();
    }
    private static void CaptureCounts() {
        if(!countsDirty) return;
        foreach(Box box in boxes)
            if(KeyViewerPersistence.ShouldPersistBoxCount(box.IsStat, box.IsFoot)) Conf.SetCount(box.Name, box.Count);
    }
    private static bool FlushCounts() {
        if(!countsDirty) return false;
        CaptureCounts();
        countsDirty = false;
        ConfMgr?.Save();
        return true;
    }
    public static void Dispose() {
        if(canvasObj == null) return;
        if(!FlushCounts()) ConfMgr?.Save();
        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        root = null;
        dragObj = null;
        footRoot = null;
        footDragObj = null;
        rainManager = null;
        boxes.Clear();
        pressLog.Clear();
        kpsMax = 0;
        kpsSum = 0;
        kpsSamples = 0;
        countsDirty = false;
        nextCountsSave = 0f;
        gameStateKnown = false;
        wasInGame = false;
        builtStyle = -1;
        DisposeCssRenderCaches();
        DisposeCssImageCache();
        SyncSettingChanged = null;
        OnKeyPressChanged = null;
    }
}
