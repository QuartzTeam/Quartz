using ADOFAI;
using ADOFAI.Editor.Actions;
using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Quartz.Features.EffectRemover;
public static partial class EffectRemover {
    public static SettingsFile<EffectRemoverSettings> ConfMgr { get; private set; }
    public static EffectRemoverSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<EffectRemoverSettings>.Loaded("EffectRemover.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf.On;
        }
    }
    public static bool EditorSaveEnabled => !EnhancedActive || Conf.EnableSave;
    private static bool EnhancedActive => Enabled && Conf.IsEnhanced;
    internal static bool SimpleActive => Enabled && Conf.IsSimple;
    private static readonly string[] ConditionalTagKeys = [
        "perfectTag",
        "hitTag",
        "earlyPerfectTag",
        "latePerfectTag",
        "barelyTag",
        "veryEarlyTag",
        "veryLateTag",
        "missTag",
        "tooEarlyTag",
        "tooLateTag",
        "lossTag",
    ];
    private static LevelEventType Event(int value) => (LevelEventType)value;
    public static void RefreshEditorSaveButtons() => SetEditorSaveButtons(scnEditor.instance, EditorSaveEnabled);
    public static void RestoreEditorSaveButtons() => SetEditorSaveButtons(scnEditor.instance, true);
    private static void SetEditorSaveButtons(scnEditor editor, bool enabled) {
        if(editor == null || SceneManager.GetActiveScene().name != "scnEditor") return;
        if(editor.popupUnsavedChangesSave != null) editor.popupUnsavedChangesSave.interactable = enabled;
        if(editor.buttonSave != null) editor.buttonSave.interactable = enabled;
    }
    internal static void Remove(LevelData levelData) {
        if(!Enabled || levelData == null) return;
        EffectRemoverSettings conf = Conf;
        List<LevelEventType> events = [];
        if(conf.Decorations) RemoveDecorations(levelData, conf);
        if(conf.Filters) AddFilterEvents(events);
        if(conf.AdvancedFilters) events.Add(Event(25));
        if(conf.Backgrounds) RemoveBackgrounds(events, levelData, conf);
        if(conf.Cameras) RemoveCameras(events, levelData, conf);
        if(conf.PlanetOrbit) events.Add(Event(26));
        if(conf.PlanetScale) events.Add(Event(56));
        if(conf.PlanetRadius) events.Add(Event(52));
        if(conf.RepeatEvents) events.Add(Event(31));
        if(conf.FrameRate) events.Add(Event(61));
        if(conf.HitSounds) {
            events.Add(Event(42));
            events.Add(Event(23));
        }
        if(conf.HoldSounds) events.Add(Event(34));
        if(conf.TrackAnimations) RemoveTrackAnimations(events, levelData, conf);
        if(conf.TrackPositions) events.Add(Event(30));
        if(conf.TrackMoves) events.Add(Event(18));
        if(conf.TrackColors) RemoveTrackColors(events, levelData, conf);
        if(conf.HideIcons) events.Add(Event(50));
        if(conf.LimitTrackOpacity) LimitTrackOpacityValues(levelData);
        if(events.Count == 0) return;
        HashSet<LevelEventType> eventSet = [.. events];
        levelData.levelEvents.RemoveAll(data => data != null && eventSet.Contains(data.eventType));
    }
    private static void AddFilterEvents(List<LevelEventType> events) {
        events.Add(Event(22));
        events.Add(Event(24));
        events.Add(Event(27));
        events.Add(Event(28));
        events.Add(Event(32));
        events.Add(Event(36));
        events.Add(Event(37));
    }
    private static void RemoveBackgrounds(List<LevelEventType> events, LevelData levelData, EffectRemoverSettings conf) {
        events.Add(Event(13));
        levelData.backgroundSettings = new LevelEvent(0, Event(7), GCS.settingsInfo["BackgroundSettings"]);
        levelData.miscSettings["bgVideo"] = "";
        levelData.backgroundSettings["defaultBGShapeType"] = BGShapeType.Disabled;
        if(conf.RemoveTutorialPatterns) levelData.backgroundSettings["showDefaultBGTile"] = false;
    }
    private static void RemoveCameras(List<LevelEventType> events, LevelData levelData, EffectRemoverSettings conf) {
        events.Add(Event(12));
        if(!conf.SetCameraZoom) return;
        float zoom = Mathf.Clamp(conf.CameraZoomScale, 100f, 1000f);
        conf.CameraZoomScale = zoom;
        levelData.cameraSettings = new LevelEvent(0, Event(8), GCS.settingsInfo["CameraSettings"]);
        levelData.cameraSettings["zoom"] = zoom;
    }
    private static void RemoveDecorations(LevelData levelData, EffectRemoverSettings conf) {
        if(!conf.DecoPlanet && !conf.DecoTiles && !conf.DecoImage && !conf.DecoText && !conf.Particles && !conf.DecoFailHitbox) return;
        // DecorationSettings/MoveDecorations aren't type-specific (no planet/tile/image/text of their own), so they
        // only get swept up once every real type is selected - matching the old all-or-nothing removal for them.
        bool allCoreTypes = conf.DecoPlanet && conf.DecoTiles && conf.DecoImage && conf.DecoText;
        if(allCoreTypes && conf.RemoveAllDecorations) {
            levelData.decorationSettings = new LevelEvent(0, Event(11), GCS.settingsInfo["DecorationSettings"]);
        }
        HashSet<string> conditionalEventTags = GetConditionalEventTags(levelData);
        HashSet<string> preservedDecorationTags = GetPreservedDecorationTags(levelData, conditionalEventTags);
        bool ShouldRemove(LevelEvent data) =>
            IsDecorationData(data)
            && MatchesDecorationCategory(data, conf, allCoreTypes)
            && (conf.RemoveAllDecorations || !ShouldPreserve(data, conditionalEventTags, preservedDecorationTags));
        levelData.decorations.RemoveAll(ShouldRemove);
        levelData.levelEvents.RemoveAll(ShouldRemove);
    }
    private static bool MatchesDecorationCategory(LevelEvent data, EffectRemoverSettings conf, bool allCoreTypes) {
        switch(data.eventType) {
            case LevelEventType.DecorationSettings:
            case LevelEventType.MoveDecorations:
                return allCoreTypes;
            case LevelEventType.AddDecoration:
                return conf.DecoImage
                    || (conf.DecoFailHitbox && data.ContainsKey("hitbox") && data.Get<HitboxType>("hitbox") == HitboxType.Kill);
            case LevelEventType.AddText:
            case LevelEventType.SetText:
            case LevelEventType.SetDefaultText:
                return conf.DecoText;
            case LevelEventType.AddParticle:
            case LevelEventType.SetParticle:
            case LevelEventType.EmitParticle:
                return conf.Particles;
            case LevelEventType.AddObject:
            case LevelEventType.SetObject:
                bool isFloor = data.ContainsKey("objectType") && data.Get<ObjectDecorationType>("objectType") == ObjectDecorationType.Floor;
                return isFloor ? conf.DecoTiles : conf.DecoPlanet;
            default:
                return false;
        }
    }
    private static HashSet<string> GetConditionalEventTags(LevelData levelData) {
        HashSet<string> tags = [];
        foreach(LevelEvent eventData in levelData.levelEvents) {
            if(eventData == null || eventData.eventType != Event(35)) continue;
            foreach(string key in ConditionalTagKeys) {
                if(!eventData.ContainsKey(key)) continue;
                string tag = eventData.GetString(key);
                if(!string.IsNullOrWhiteSpace(tag) && tag != "None" && tag != "없음") tags.Add(tag);
            }
        }
        return tags;
    }
    private static HashSet<string> GetPreservedDecorationTags(LevelData levelData, HashSet<string> conditionalEventTags) {
        HashSet<string> tags = [];
        foreach(LevelEvent eventData in levelData.levelEvents) {
            if(!IsDecorationData(eventData) || !HasAnyEventTag(eventData, conditionalEventTags)) continue;
            foreach(string tag in GetTags(eventData, "tag")) tags.Add(tag);
        }
        return tags;
    }
    private static bool ShouldPreserve(LevelEvent eventData, HashSet<string> conditionalEventTags, HashSet<string> preservedDecorationTags)
        => HasAnyEventTag(eventData, conditionalEventTags) || HasAnyTag(eventData, preservedDecorationTags);
    private static bool IsDecorationData(LevelEvent eventData) {
        if(eventData == null) return false;
        LevelEventType type = eventData.eventType;
        return type == LevelEventType.DecorationSettings
            || type == LevelEventType.AddDecoration
            || type == LevelEventType.AddText
            || type == LevelEventType.SetText
            || type == LevelEventType.SetDefaultText
            || type == LevelEventType.MoveDecorations
            || type == LevelEventType.AddObject
            || type == LevelEventType.SetObject
            || type == LevelEventType.AddParticle
            || type == LevelEventType.SetParticle
            || type == LevelEventType.EmitParticle;
    }
    private static bool HasAnyEventTag(LevelEvent eventData, HashSet<string> tags) {
        foreach(string eventTag in GetTags(eventData, "eventTag")) {
            if(tags.Contains(eventTag)) return true;
        }
        return false;
    }
    private static bool HasAnyTag(LevelEvent eventData, HashSet<string> tags) {
        foreach(string tag in GetTags(eventData, "tag")) {
            if(tags.Contains(tag)) return true;
        }
        return false;
    }
    private static IEnumerable<string> GetTags(LevelEvent eventData, string key) {
        if(eventData == null || !eventData.ContainsKey(key)) yield break;
        string tags = eventData.GetString(key);
        if(string.IsNullOrWhiteSpace(tags)) yield break;
        foreach(string tag in tags.Split(' ')) {
            if(!string.IsNullOrWhiteSpace(tag)) yield return tag;
        }
    }
    private static void RemoveTrackAnimations(List<LevelEventType> events, LevelData levelData, EffectRemoverSettings conf) {
        events.Add(Event(16));
        if(conf.ResetTrackAnimation) {
            levelData.trackSettings["trackAppearAnimation"] = TrackAnimationType.Fade;
            levelData.trackSettings["trackDisappearAnimation"] = TrackAnimationType.Fade;
            levelData.trackSettings["beatsAhead"] = 8.0f;
            levelData.trackSettings["beatsBehind"] = 0.0f;
        }
    }
    private static void RemoveTrackColors(List<LevelEventType> events, LevelData levelData, EffectRemoverSettings conf) {
        events.Add(Event(15));
        events.Add(Event(17));
        if(conf.ResetTrackColor) {
            levelData.trackSettings["trackStyle"] = TrackStyle.Standard;
            levelData.trackSettings["trackColor"] = "debb7bff";
            levelData.trackSettings["trackColorType"] = TrackColorType.Single;
        }
    }
    private static void LimitTrackOpacityValues(LevelData levelData) {
        foreach(LevelEvent eventData in levelData.levelEvents) {
            if(eventData == null || (eventData.eventType != Event(18) && eventData.eventType != Event(30))) continue;
            if(eventData.ContainsKey("opacity") && eventData.GetFloat("opacity") > 100.0f) eventData["opacity"] = 100.0f;
        }
    }
    [HarmonyPatch(typeof(LevelData), "Decode")]
    private static class LevelDataDecodePatch {
        private static void Postfix(LevelData __instance) {
            if(EnhancedActive) Remove(__instance);
        }
    }
    [HarmonyPatch(typeof(SaveLevelEditorAction), "Execute")]
    private static class SaveLevelEditorActionPatch {
        private static bool Prefix() => EditorSaveEnabled;
    }
    [HarmonyPatch(typeof(scnEditor), "LoadGameScene")]
    private static class EditorLoadGameScenePatch {
        private static void Postfix(scnEditor __instance) => SetEditorSaveButtons(__instance, EditorSaveEnabled);
    }
}
