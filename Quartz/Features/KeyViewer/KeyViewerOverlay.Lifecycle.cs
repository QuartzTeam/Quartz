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

// Lifecycle: Initialize/Rebuild/Apply + KeyLimiter sync + import/reset/dispose.
public static partial class KeyViewerOverlay {
    public static void Initialize(GameObject rootObject) {
        if(canvasObj != null) return;

        EnsureConf();

        canvasObj = new GameObject("QuartzKeyViewerCanvas");
        canvasObj.transform.SetParent(rootObject.transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the combo counter (32757).
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

        // Separate element for the foot keys, dragged on its own.
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

    // Rebuilds the key grid from the current style/keys. Cheap enough to run
    // on any structural settings change.
    public static void Rebuild() {
        if(root == null) return;

        // Drops live under the root being torn down; reset before destroying.
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

        // Rain layer first so drops render behind the key boxes.
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

    // Fires when the sync-to-limiter arrangement may have changed (option
    // toggled, viewer mode switched) so the Key Limiter page can lock or
    // unlock its key-editing UI.
    public static event Action SyncSettingChanged;

    public static void RaiseSyncSettingChanged() => SyncSettingChanged?.Invoke();

    // True while the Key Limiter's allowed keys are owned by the key viewer —
    // sync only runs in simple mode.
    public static bool IsSyncingToKeyLimiter {
        get {
            EnsureConf();
            return Conf is { SyncToKeyLimiter: true } && Conf.IsSimpleMode;
        }
    }

    // v1 SettingsGui.SyncSimpleKeysToKeyLimiter: while the option is on, the
    // Key Limiter's allowed list is overwritten with exactly the keys shown
    // on the viewer (normalized, deduped). Runs after every rebuild — style
    // change, rebind, startup — and when the option itself is switched on.
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
        // Foot keys are shown on the viewer too (FootKeys[0..FootKeyCount]), so
        // they must join the allowed set or the limiter blocks them in-game.
        AddKeys(Conf.FootKeys, Conf.FootKeyCount());

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

    // Re-applies position, scale and colors (no structural change).
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

        // Foot element rides its own position at the same scale.
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
            // The foot element is a separate piece — reset it too.
            Conf.FootOffsetX = def.FootOffsetX;
            Conf.FootOffsetY = def.FootOffsetY;
        }
        Apply();
        Save();
    }

    public static bool ImportDmNotePreset(out string error) =>
        ImportDmNoteFile(out error, "JSON Preset", "json", "Select DM Note preset", "preset",
            (text, _) => {
                JObject.Parse(text);
                Conf.DmPresetJson = text;
            });

    // Picks a DM Note custom-CSS file and stores its text on the config (like
    // the preset, the CSS travels with the config so it survives a file move).
    // Enables the CSS layer on a successful import.
    public static bool ImportDmNoteCss(out string error) =>
        ImportDmNoteFile(out error, "CSS", "css", "Select DM Note custom CSS", "CSS",
            (text, picked) => {
                Conf.DmCssText = text;
                Conf.DmCssPath = picked;
                Conf.DmCssEnabled = true;
            });

    // Shared pick-file/read/store/rebuild/log scaffold for the two imports above.
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

    private static void FlushCounts() {
        if(!countsDirty) return;

        countsDirty = false;
        foreach(Box box in boxes)
            if(!box.IsStat) Conf.SetCount(box.Name, box.Count);
        Save();
    }

    public static void Dispose() {
        if(canvasObj == null) return;

        FlushCounts();
        ConfMgr?.Save();

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
        builtStyle = -1;

        // UMM reloads the mod in-process, so statics holding runtime-created
        // Unity assets would leak a full set per reload — destroy them here.
        DisposeCssRenderCaches();
        DisposeCssImageCache();
        SyncSettingChanged = null;
        OnKeyPressChanged = null;
    }
}