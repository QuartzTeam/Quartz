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
using Quartz.Compat.Game;
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
        rainManager = canvasObj.AddComponent<RainManager>();
        canvasObj.AddComponent<Updater>();
        Rebuild();
    }
    public static void Rebuild() {
        if(root == null) return;
        CaptureCounts();
        rainManager?.Clear();
        Quartz.UI.Generator.GenerateUI.ClearChildren(root);
        boxes.Clear();
        counterBounces.Clear();
        cssFx.Clear();
        cssGlowLayer = null;
        dragObj = null;
        kpsMax = 0;
        kpsSum = 0;
        kpsSamples = 0;
        nextKpsSample = 0f;
        inputWasActive = false;
        inputPrimed = false;
        layoutRebuildPending = false;
        BuildDmNote();
        SyncKeysToKeyLimiter();
    }
    public static void RequestLayoutRebuild() {
        if(root == null || Conf == null) return;
        layoutRebuildPending = true;
        layoutRebuildAt = KvClock.Now + LayoutRebuildDebounceSeconds;
    }
    public static event Action SyncSettingChanged;
    public static void RaiseSyncSettingChanged() => SyncSettingChanged?.Invoke();
    public static bool IsSyncingToKeyLimiter {
        get {
            EnsureConf();
            return Conf is { SyncToKeyLimiter: true };
        }
    }
    public static void SetSyncToKeyLimiter(bool value) {
        EnsureConf();
        if(Conf == null) return;
        Conf.SyncToKeyLimiter = value;
        Save();
        if(value) SyncKeysToKeyLimiter();
        RaiseSyncSettingChanged();
    }
    public static void SyncKeysToKeyLimiter() {
        EnsureConf();
        if(Conf is not { SyncToKeyLimiter: true }) return;
        Features.KeyLimiter.KeyLimiter.EnsureConf();
        List<int> result = [];
        HashSet<int> seen = [];
        void Add(KeyCode key) {
            int normalized = (int)Features.KeyLimiter.KeyLimiter.NormalizeKey(key);
            if(normalized != 0 && seen.Add(normalized)) result.Add(normalized);
        }
        AddLayoutKeys(Add);
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
    private static void AddLayoutKeys(Action<KeyCode> add) {
        Layout.KvDocument doc = Layout.KvStore.Current;
        if(doc == null) return;
        foreach(Layout.KvElement el in doc.BoundKeyElements(doc.SelectedTab)) add(el.KeyCodeValue);
    }
    public static void Apply() {
        if(root == null) return;
        if(!built) {
            Rebuild();
            return;
        }
        ApplyDmRuntimeSettings();
        root.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.DmOffsetX, Conf.DmOffsetY));
        float dmScale = Mathf.Clamp(Conf.DmScale, 0.2f, 4f);
        root.localScale = new Vector3(dmScale, dmScale, 1f);
        ApplyBorderScale(dmScale);
        if(!Conf.DmNoteEffect) rainManager?.Clear();
    }
    private const float MinBorderScreenUnits = 1.4f;
    internal static float ScaledBorderStroke(float radiusUnits, float strokeUnits, float scale) {
        if(strokeUnits <= 0.01f || scale >= 1f) return strokeUnits;
        float floor = MinBorderScreenUnits / Mathf.Max(0.05f, scale);
        if(floor <= strokeUnits) return strokeUnits;
        return Mathf.Min(floor, Mathf.Max(strokeUnits, radiusUnits * 0.85f));
    }
    private static void ApplyBorderScale(float scale) {
        foreach(Box box in boxes) {
            if(box?.Border == null || box.Dm is { IsGraph: true }) continue;
            float stroke = box.Dm?.BoxBorderWidth ?? BorderWidth;
            if(stroke <= 0.01f) continue;
            float radius = box.Dm?.BorderRadius ?? KeyRadius;
            float eff = Mathf.Round(ScaledBorderStroke(radius, stroke, scale) * 4f) / 4f;
            if(Mathf.Approximately(eff, box.AppliedBorderStroke)) continue;
            box.AppliedBorderStroke = eff;
            box.Border.sprite = MainCore.Spr.GetRing(Mathf.Max(0.5f, radius), Mathf.Max(0.1f, eff));
        }
    }
    public static void ResetPosition() {
        KeyViewerSettings def = new();
        Conf.DmOffsetX = def.DmOffsetX;
        Conf.DmOffsetY = def.DmOffsetY;
        Apply();
        Save();
    }
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
            picked = FileDialog.PickFile(
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
        bool layout = false;
        foreach(Box box in boxes) {
            box.Count = 0;
            box.LastShown = int.MinValue;
            if(box.Source != null && box.Source.Count != 0) {
                box.Source.Count = 0;
                layout = true;
            }
        }
        if(layout) Layout.KvStore.RequestSave();
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
        foreach(Box box in boxes) {
            if(!KeyViewerPersistence.ShouldPersistBoxCount(box.IsStat, box.IsFoot)) continue;
            if(box.Source != null) box.Source.Count = box.Count;
            else Conf.SetCount(box.Name, box.Count);
        }
    }
    private static bool FlushCounts(bool immediate = false) {
        if(!countsDirty) return false;
        CaptureCounts();
        countsDirty = false;
        if(built) {
            if(immediate) Layout.KvStore.Save();
            else Layout.KvStore.RequestSave();
        }
        ConfMgr?.Save();
        return true;
    }
    public static void Dispose() {
        if(canvasObj == null) return;
        KvInputQueue.Shutdown();
        keyMap.Clear();
        pollBoxes.Clear();
        drainBuffer.Clear();
        uncoveredBindings = 0;
        hookWasActive = false;
        resyncRequested = false;
        if(!FlushCounts(immediate: true)) ConfMgr?.Save();
        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        root = null;
        dragObj = null;
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
        built = false;
        DisposeCssRenderCaches();
        DisposeCssImageCache();
        SyncSettingChanged = null;
        OnKeyPressChanged = null;
    }
}
