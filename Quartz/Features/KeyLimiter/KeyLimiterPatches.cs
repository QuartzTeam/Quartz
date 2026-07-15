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
}
