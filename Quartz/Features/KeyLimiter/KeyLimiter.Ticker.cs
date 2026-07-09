using Quartz.Core;
using Quartz.IO;
using MonsterLove.StateMachine;
using SkyHook;
using System.Threading;
using UnityEngine;

namespace Quartz.Features.KeyLimiter;

// Per-frame Ticker MonoBehaviour. Extracted from KeyLimiter.cs.
internal static partial class KeyLimiter {
    private sealed class Ticker : MonoBehaviour {
        private readonly HashSet<KeyCode> prevHeld = [];
        private bool wasCapturing;

        private void Update() {
            InPlayerControl();

            if(!IsCapturing) {
                wasCapturing = false;
                if(prevHeld.Count > 0) prevHeld.Clear();
                return;
            }

            // First frame of a capture: remember what's already held so a
            // key the user hadn't released yet isn't captured instantly.
            bool priming = !wasCapturing;
            wasCapturing = true;

            KeyCode[] candidates = CaptureCandidates;
            for(int i = 0; i < candidates.Length; i++) {
                KeyCode key = candidates[i];
                bool held;
                try { held = UnityEngine.Input.GetKey(key); }
                catch { continue; }

                // Unity's legacy Input is blind to the Korean Hangul/Hanja keys,
                // which surface as RightAlt/RightControl — on a Korean layout the
                // right-Ctrl/Alt position IS the Hanja/Hangul key. Without this the
                // capture loop never sees them, so they can't be added to the
                // allowed list. Fall back to the SkyHook-fed held state (the only
                // path that sees them, still forwarded during capture), mirroring
                // the KeyViewer's KeyHeld. Scoped to those modifiers so normal keys
                // and the NumLock-off numpad keep using Input alone.
                if(!held && IsHookOnlyModifier(key)) held = HookKeyHeld(key);

                if(held && !priming && !prevHeld.Contains(key)) {
                    prevHeld.Add(key);
                    EndCapture(key);
                    return;
                }

                if(held) prevHeld.Add(key);
                else prevHeld.Remove(key);
            }
        }
    }
}
