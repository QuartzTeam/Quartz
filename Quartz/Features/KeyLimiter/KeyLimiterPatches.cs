using HarmonyLib;
namespace Quartz.Features.KeyLimiter;
internal static partial class KeyLimiter {
    // Ported from Bismuth's "Block game inputs while menu is open". Gameplay hit-counting
    // is blocked separately (see ChatterBlocker.CountValidKeysPressed, which already owns
    // that chokepoint); these three cover everything else the game reads through RDInput —
    // restart/pause/confirm/menu-nav actions and raw shortcut-key reads. Neither Quartz's
    // own menu (toggle hotkey, key-capture, text fields) nor anything else in this codebase
    // calls these RDInput members, so blocking them carries no risk of blocking the Quartz
    // menu itself.
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetState))]
    private static class MenuBlockGetStatePatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.WentDown))]
    private static class MenuBlockWentDownPatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.IsDown))]
    private static class MenuBlockIsDownPatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    // WentDown/IsDown/WentUp are `this KeyCode` extension wrappers over Input.GetKey* that the
    // game calls all over (menus, the level editor's shortcuts); WentUp completes the trio so a
    // key can't be "released" into a game that never saw it held.
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.WentUp))]
    private static class MenuBlockWentUpPatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    // The main-press path the first three patches never covered: mainPress/mainPressCount (title
    // and level-select "press any key", gameplay taps, mouse clicks as hits) read GetMain, not
    // GetState — which is why keys still reached the game outside a run.
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetMain))]
    private static class MenuBlockGetMainPatch {
        private static void Postfix(ref int __result) {
            if(IsMenuBlockActive()) __result = 0;
        }
    }
    // GetStateKeys backs GetMainPressKeys/GetMainHeldKeys — the per-key listing of the same
    // presses GetMain counts. The list is freshly built per call, so clearing it is safe.
    // Quartz's own reader (ChatterBlocker.CountValidKeysPressed) already returns before calling
    // it while the menu block is active, so this cannot starve the mod's own counting.
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetStateKeys))]
    private static class MenuBlockGetStateKeysPatch {
        private static void Postfix(System.Collections.Generic.List<AnyKeyCode> __result) {
            if(IsMenuBlockActive()) __result.Clear();
        }
    }
}
