using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using Quartz.Compat.Game;
namespace Quartz.Features.Status;
internal static class Bpm {
    private static readonly Queue<float> autoTileTimes = new();
    private static int autoKpsFrame = -1;
    private static int autoKpsValue;
    private static int bpmFrame = -1;
    private static float cachedTileBpm;
    private static float cachedActualBpm;
    internal static int GetAutoKps() {
        int frame = Time.frameCount;
        if(autoKpsFrame == frame) return autoKpsValue;
        autoKpsFrame = frame;
        float now = Time.time;
        while(autoTileTimes.Count > 0 && now - autoTileTimes.Peek() > 1f) autoTileTimes.Dequeue();
        return autoKpsValue = autoTileTimes.Count;
    }
    [HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
    private static class MoveToNextFloorPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            try {
                if(RDC.auto) {
                    float now = Time.time;
                    while(autoTileTimes.Count > 0 && now - autoTileTimes.Peek() > 1f) autoTileTimes.Dequeue();
                    autoTileTimes.Enqueue(now);
                    autoKpsFrame = -1;
                }
            } catch { }
        }
    }
    internal static void GetBpmValues(out float tileBpm, out float actualBpm) {
        int frame = Time.frameCount;
        if(bpmFrame == frame) {
            tileBpm = cachedTileBpm;
            actualBpm = cachedActualBpm;
            return;
        }
        bpmFrame = frame;
        tileBpm = 0f;
        actualBpm = 0f;
        try {
            scrController controller = scrController.instance;
            scrConductor conductor = scrConductor.instance;
            scrFloor floor = controller != null ? (controller.currFloor ?? controller.firstFloor) : null;
            if(controller == null || conductor == null || floor == null || conductor.song == null) return;
            double speed = GameApi.PlanetSpeed(controller);
            tileBpm = (float)(conductor.bpm * conductor.song.pitch * speed);
            double dt = floor.nextfloor ? floor.nextfloor.entryTime - floor.entryTime : 0.0;
            actualBpm = floor.nextfloor && dt > 1e-9
                ? (float)(60.0 / dt * conductor.song.pitch)
                : tileBpm;
        } catch {
            tileBpm = 0f;
            actualBpm = 0f;
        }
        cachedTileBpm = tileBpm;
        cachedActualBpm = actualBpm;
    }
}
