using System.Collections;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Quartz.Features.Tuf;

public sealed class TufLevelLauncher : MonoBehaviour {
    private string levelsRoot;
    private Coroutine pending;
    private Action<bool, string> completion;
    private GameObject loadingCover;

    public void Initialize(string root) => levelsRoot = Path.GetFullPath(root);

    public bool Launch(string chartPath, Action<bool, string> completed) {
        if(pending != null || completion != null) Cancel();
        completion = completed;
        try {
            if(!TufArchive.IsChartUnderRoot(chartPath, levelsRoot) || !File.Exists(chartPath))
                throw new InvalidDataException(Tr("TUF_LAUNCH_INVALID_PATH", "Playable chart path is invalid."));
            string expected = Path.GetFullPath(chartPath);
            if(!ClearTufHelperLaunchState())
                throw new InvalidOperationException(Tr("TUF_LAUNCH_STATE_ERROR",
                    "Could not clear conflicting TUFHelper launch state."));
            DiscordController.shouldUpdatePresence = true;

            scnEditor active = scnEditor.instance;
            if(SceneManager.GetActiveScene().name == "scnEditor"
               && active != null && active.initialized && !active.playMode) {
                if(HasUnsavedChanges(active)) {
                    Complete(false, Tr("TUF_UNSAVED_EDITOR",
                        "Save or discard your editor changes, then try again."));
                    return false;
                }
                MainCore.Log.Msg("[TUF] opening chart in current editor: " + expected);
                ShowLoadingCover();
                pending = StartCoroutine(Guarded(OpenAndLoad(active, expected)));
                return true;
            }

            MainCore.Log.Msg("[TUF] opening editor for chart: " + expected);
            ShowLoadingCover();
            GCS.sceneToLoad = "scnEditor";
            GCS.worldEntrance = null;
            scnEditor.levelToOpenOnLoad = null;
            SceneManager.LoadScene("scnEditor");
            pending = StartCoroutine(Guarded(WaitAndLoad(expected)));
            return true;
        } catch(Exception e) {
            Complete(false, Tr("TUF_LAUNCH_FAILED", "Could not launch the TUF level: {0}", e.Message));
            return false;
        }
    }

    private IEnumerator WaitAndLoad(string expected) {
        float initDeadline = Time.realtimeSinceStartup + 15f;
        scnEditor editor = null;
        while(Time.realtimeSinceStartup < initDeadline) {
            editor = scnEditor.instance;
            if(editor != null && editor.initialized) break;
            yield return null;
        }
        if(editor == null || !editor.initialized) {
            MainCore.Log.Wrn("[TUF] editor initialization did not complete");
            Complete(false, Tr("TUF_EDITOR_INIT_FAILED",
                "Editor initialization was interrupted; check other mods."));
            yield break;
        }
        IEnumerator load = OpenAndLoad(editor, expected);
        while(load.MoveNext()) yield return load.Current;
    }

    private IEnumerator Guarded(IEnumerator operation) {
        while(true) {
            bool moved = false;
            object current = null;
            Exception failure = null;
            try {
                moved = operation.MoveNext();
                if(moved) current = operation.Current;
            } catch(Exception e) {
                failure = e;
            }
            if(failure != null) {
                MainCore.Log.Wrn("[TUF] unexpected level-load failure: " + failure);
                Complete(false, Tr("TUF_LAUNCH_FAILED",
                    "Could not launch the TUF level: {0}", failure.Message));
                yield break;
            }
            if(!moved) yield break;
            yield return current;
        }
    }

    private IEnumerator OpenAndLoad(scnEditor editor, string expected) {
        yield return null;
        if(editor == null) {
            Complete(false, Tr("TUF_EDITOR_CLOSED", "Editor closed before the TUF level could load."));
            yield break;
        }
        // OpenLevelCo reports load failures (mod-required events, corrupt json, …)
        // only through this notification popup; snapshot its state so a popup that
        // APPEARS after OpenLevel means our load failed and we must report it.
        GameObject failurePopup = editor.notificationPopupContainer;
        bool popupWasActive = failurePopup != null && failurePopup.activeInHierarchy;
        try {
            MainCore.Log.Msg("[TUF] invoking scnEditor.OpenLevel for: " + expected);
            editor.OpenLevel(expected);
        } catch(Exception e) {
            Complete(false, Tr("TUF_CHART_OPEN_FAILED",
                "Could not open the downloaded chart: {0}", e.Message));
            yield break;
        }

        float loadDeadline = Time.realtimeSinceStartup + 30f;
        while(Time.realtimeSinceStartup < loadDeadline) {
            if(editor == null) break;
            if(!popupWasActive && failurePopup != null && failurePopup.activeInHierarchy) {
                string reason = PopupMessage(failurePopup);
                MainCore.Log.Wrn("[TUF] the game rejected the chart: " + (reason ?? "<no message>"));
                Complete(false, string.IsNullOrWhiteSpace(reason)
                    ? Tr("TUF_CHART_LOAD_REJECTED", "The game could not load this level — it may require another mod.")
                    : reason);
                yield break;
            }
            if(!editor.isLoading && SamePath(ADOBase.levelPath, expected) && editor.floors?.Count > 1) {
                // Let the editor finish one rendered frame behind the cover before
                // revealing the fully-loaded chart.
                yield return null;
                MainCore.Log.Msg("[TUF] chart loaded, ready to play: " + expected);
                Complete(true, "");
                yield break;
            }
            yield return null;
        }
        string loadedPath = ADOBase.levelPath ?? "<none>";
        int floorCount = editor?.floors?.Count ?? 0;
        MainCore.Log.Wrn($"[TUF] chart load failed; expected='{expected}', loaded='{loadedPath}', floors={floorCount}");
        Complete(false, SamePath(loadedPath, expected) && floorCount <= 1
            ? Tr("TUF_CHART_UNPLAYABLE", "The downloaded chart could not be loaded or is not playable.")
            : Tr("TUF_CHART_LOAD_TIMEOUT", "The downloaded chart did not finish loading in the editor."));
    }

    private void ShowLoadingCover() {
        HideLoadingCover();
        loadingCover = UnityUtils.CreateOverlayCanvas(
            "TUF Loading Cover", MainCore.Root.transform, 32766, out GraphicRaycaster raycaster);
        raycaster.enabled = true;
        GameObject background = new("Background");
        background.transform.SetParent(loadingCover.transform, false);
        RectTransform rect = background.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = background.AddComponent<Image>();
        image.color = Color.Lerp(UIColors.PanelBG, Color.black, 0.3f);
        image.raycastTarget = true;
    }

    private void HideLoadingCover() {
        if(loadingCover != null) Destroy(loadingCover);
        loadingCover = null;
    }

    private void Complete(bool success, string error) {
        pending = null;
        HideLoadingCover();
        if(success) UICore.Close(true);
        Action<bool, string> callback = completion;
        completion = null;
        callback?.Invoke(success, error ?? "");
    }

    // scnEditor.unsavedChanges is a private property; read its backing field so a
    // direct OpenLevel cannot silently discard the user's unsaved editor work.
    // Fail open (false) if the game renames the field — behavior then matches vanilla.
    private static readonly FieldInfo UnsavedChangesField =
        AccessTools.Field(typeof(scnEditor), "_unsavedChanges");
    private static bool HasUnsavedChanges(scnEditor editor) {
        try { return UnsavedChangesField?.GetValue(editor) is true; }
        catch { return false; }
    }

    // The game's load-failure popup text ("This level requires a mod …"), so the
    // user sees the real reason in the TUF card instead of a generic timeout.
    private static string PopupMessage(GameObject popup) {
        try {
            TMPro.TMP_Text text = popup.GetComponentInChildren<TMPro.TMP_Text>(true);
            string value = text != null ? text.text?.Trim() : null;
            if(string.IsNullOrWhiteSpace(value)) return null;
            return value.Length <= 300 ? value : value[..300] + "…";
        } catch { return null; }
    }

    private static bool SamePath(string a, string b) {
        if(string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try {
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), comparison);
        } catch { return false; }
    }

    private static bool ClearTufHelperLaunchState() {
        bool mainCleared = TrySetStatic("TUFHelper.Main", "isInTUFHelper", false, false);
        bool sourceCleared = TrySetStatic(
            "TUFHelper.Utils.ADOFAIGameplayHandler", "IsFromTUFHelper", false, true);
        bool infoCleared = TrySetStatic(
            "TUFHelper.Utils.ADOFAIGameplayHandler+EditorPlayPatch", "CurrentLevelInfo", null, true);
        if(!mainCleared || !sourceCleared || !infoCleared)
            MainCore.Log.Wrn("[TUF] found TUFHelper state but could not fully clear its editor handoff");
        return mainCleared && sourceCleared && infoCleared;
    }

    private static bool TrySetStatic(string typeName, string memberName, object value, bool property) {
        Type type;
        try { type = AccessTools.TypeByName(typeName); }
        catch { return true; }
        if(type == null) return true;
        try {
            if(property) {
                PropertyInfo member = AccessTools.Property(type, memberName);
                if(member == null) return false;
                member.SetValue(null, value, null);
            } else {
                FieldInfo member = AccessTools.Field(type, memberName);
                if(member == null) return false;
                member.SetValue(null, value);
            }
            return true;
        } catch { return false; }
    }

    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
    private static string Tr(string key, string fallback, object value) =>
        string.Format(MainCore.Tr.Get(key, fallback), value);

    public void Cancel() {
        if(pending != null) StopCoroutine(pending);
        if(completion != null) Complete(false, "");
        else {
            pending = null;
            HideLoadingCover();
        }
    }

    private void OnDestroy() => Cancel();
}
