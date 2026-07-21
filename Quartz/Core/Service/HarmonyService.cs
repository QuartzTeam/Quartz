using System;
using HarmonyLib;
using Quartz.Compat.Interface;
namespace Quartz.Core.Service;
public sealed class HarmonyService : IRuntimeService, IRuntimeTick {
    public HarmonyLib.Harmony Harmony { get; private set; }
    private bool patchesApplied;
    public void Initialize() => Harmony = new HarmonyLib.Harmony(Info.Name);
    public void Tick() {
        if(patchesApplied) return;
        patchesApplied = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        PatchAllResilient();
        MainCore.Log.Msg($"[Harmony] applied patches (deferred to first tick) in {sw.ElapsedMilliseconds} ms");
    }
    private void PatchAllResilient() {
        foreach(Type type in AccessTools.GetTypesFromAssembly(MainCore.Asm)) {
            try {
                Harmony.CreateClassProcessor(type).Patch();
            } catch(Exception e) {
                MainCore.Log.Wrn($"[Harmony] skipped patch class {type.FullName}: {e.Message}");
            }
        }
    }
    public void Dispose() {
        Harmony?.UnpatchAll(Harmony.Id);
        Harmony = null;
    }
}
