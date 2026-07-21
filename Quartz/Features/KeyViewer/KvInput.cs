using System.Diagnostics;
using System.Threading;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
internal static class KvClock {
    private static readonly long Origin = Stopwatch.GetTimestamp();
    private static readonly double TicksToSeconds = 1.0 / Stopwatch.Frequency;
    public static float Now => (float)((Stopwatch.GetTimestamp() - Origin) * TicksToSeconds);
}
internal static class KvInputQueue {
    internal struct Ev {
        public int Seq;
        public int Key;
        public bool Down;
        public float Time;
    }
    private const int Capacity = 2048;
    private const int Mask = Capacity - 1;
    private static readonly Ev[] Ring = new Ev[Capacity];
    private static int writeIndex;
    private static int readIndex;
    private static int readShared;
    private static int overflowed;
    private static volatile bool focused = true;
    private static volatile bool wanted;
    private static volatile bool hookActive;
    private static float nextHookCheck;
    public static bool HookActive => hookActive;
    public static void SetWanted(bool value) => wanted = value;
    public static void SetFocused(bool value) => focused = value;
    public static void Push(KeyCode key, bool down) {
        if(!wanted || key == KeyCode.None) return;
        if(down && !focused) return;
        int w = writeIndex;
        if(w - Volatile.Read(ref readShared) >= Capacity) {
            Volatile.Write(ref overflowed, 1);
            return;
        }
        int slot = w & Mask;
        Ring[slot].Key = (int)key;
        Ring[slot].Down = down;
        Ring[slot].Time = KvClock.Now;
        Volatile.Write(ref Ring[slot].Seq, w + 1);
        writeIndex = w + 1;
    }
    public static void Drain(List<Ev> into) {
        int r = readIndex;
        while(true) {
            int slot = r & Mask;
            if(Volatile.Read(ref Ring[slot].Seq) != r + 1) break;
            into.Add(Ring[slot]);
            r++;
        }
        Commit(r);
    }
    public static void Discard() {
        int r = readIndex;
        while(Volatile.Read(ref Ring[r & Mask].Seq) == r + 1) r++;
        Commit(r);
        Volatile.Write(ref overflowed, 0);
    }
    private static void Commit(int r) {
        if(r == readIndex) return;
        readIndex = r;
        Volatile.Write(ref readShared, r);
    }
    public static bool TakeOverflow() => Interlocked.Exchange(ref overflowed, 0) != 0;
    public static void RecoverFromGap() {
        int highest = readIndex;
        for(int i = 0; i < Capacity; i++) {
            int seq = Volatile.Read(ref Ring[i].Seq);
            if(seq > highest) highest = seq;
        }
        Commit(highest);
    }
    public static void Pump(float now, bool enabled) => EnsureHook(now, enabled);
    private static void EnsureHook(float now, bool enabled) {
        if(!enabled) {
            hookActive = false;
            return;
        }
        if(now < nextHookCheck) return;
        nextHookCheck = now + HookCheckIntervalSeconds;
        try {
            hookActive = AsyncInputManager.isActive;
        } catch {
            hookActive = false;
        }
    }
    public static void Shutdown() {
        wanted = false;
        hookActive = false;
        Discard();
        nextHookCheck = 0f;
    }
    private const float HookCheckIntervalSeconds = 0.5f;
    public static void Reset() {
        Discard();
        nextHookCheck = 0f;
    }
}
