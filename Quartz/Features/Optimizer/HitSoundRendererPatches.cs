using HarmonyLib;
namespace Quartz.Features.Optimizer;
// Hooks for the "Render All Hit Sounds" optimizer toggle. Capture the built
// hit-sound timeline, then stop generated audio whenever the game stops its
// own sounds. Our segments play through independent AudioSources scheduled up
// to a few seconds ahead, so on death/restart/pause they MUST be stopped
// explicitly or they keep playing after the run ends.
public static class HitSoundRendererPatches {
    [HarmonyPatch(typeof(scrConductor), "PlayHitTimes")]
    private static class PlayHitTimesPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrConductor), "PlayHitTimes") != null;
        private static void Postfix(scrConductor __instance) {
            if(HitSoundRenderer.Active) HitSoundRenderer.Capture(__instance);
        }
    }
    // Primary stop signal. The game calls this on death (via FailAction),
    // restart, pause, quit, and every other point it silences its own sounds.
    // Our generated AudioSources are not owned by AudioManager, so this is where
    // we have to stop them too. Called inside PlayHitTimes before our capture
    // runs, so it can never clobber a freshly built track.
    [HarmonyPatch(typeof(AudioManager), "StopAllSounds")]
    private static class StopAllSoundsPatch {
        private static bool Prepare() => AccessTools.Method(typeof(AudioManager), "StopAllSounds") != null;
        private static void Postfix() => HitSoundRenderer.StopAll("sounds stopped");
    }
    [HarmonyPatch(typeof(scrConductor), "KillAllSounds")]
    private static class KillAllSoundsPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrConductor), "KillAllSounds") != null;
        private static void Prefix() => HitSoundRenderer.StopAll("sounds killed");
    }
    [HarmonyPatch(typeof(scrController), "Restart")]
    private static class RestartPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrController), "Restart") != null;
        private static void Prefix() => HitSoundRenderer.StopAll("restart");
    }
    // FailAction always ends the run (it stops the song) — it is not invoked
    // under No Fail — so stop unconditionally. Do NOT gate on currentState:
    // ChangeState(States.Fail) inside FailAction does not update currentState
    // synchronously, so the state still reads its pre-fail value here.
    [HarmonyPatch(typeof(scrController), "FailAction")]
    private static class FailActionPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrController), "FailAction") != null;
        private static void Postfix() => HitSoundRenderer.StopAll("game over");
    }
}
