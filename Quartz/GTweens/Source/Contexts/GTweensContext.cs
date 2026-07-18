using GTweens.Tweens;
using System.Diagnostics;
namespace GTweens.Contexts;
public sealed class GTweensContext {
    public float TimeScale { get; set; } = 1f;
    public float TickDurationMs { get; private set; }
    readonly List<GTween> _aliveTweens = [];
    readonly List<GTween> _tweensToAdd = [];
    readonly List<GTween> _tweensToRemove = [];
    readonly Stopwatch _updateStopwatch = new();
    public void Play(GTween gTween) {
        if(gTween.IsNested) return;
        if(gTween.IsAlive) {
            TryStartTween(gTween);
            return;
        }
        gTween.IsAlive = true;
        _tweensToAdd.Add(gTween);
        TryStartTween(gTween);
    }
    public void Tick(float deltaTime) {
        // Quartz: ticked every frame for the mod's lifetime, but tweens only exist
        // around menu interactions — skip the stopwatch + list sweeps when idle.
        if(_aliveTweens.Count == 0 && _tweensToAdd.Count == 0) {
            TickDurationMs = 0f;
            return;
        }
        float scaledDeltaTime = deltaTime * TimeScale;
        _updateStopwatch.Restart();
        foreach(GTween tween in _tweensToAdd) _aliveTweens.Add(tween);
        _tweensToAdd.Clear();
        foreach(GTween tween in _aliveTweens) {
            if(tween.IsPlaying) {
                try {
                    tween.Tick(scaledDeltaTime);
                } catch(System.Exception e) {
                    Quartz.Core.MainCore.Log.Err($"[GTween] tween threw, dropping it: {e}");
                    _tweensToRemove.Add(tween);
                }
            } else {
                _tweensToRemove.Add(tween);
            }
        }
        // Quartz: IsAlive only flips false right here, so the RemoveAll scan is
        // pure overhead on frames where nothing finished.
        if(_tweensToRemove.Count > 0) {
            foreach(GTween tween in _tweensToRemove) {
                tween.IsAlive = false;
                _tweensToAdd.Remove(tween);
            }
            _aliveTweens.RemoveAll(static tween => !tween.IsAlive);
            _tweensToRemove.Clear();
        }
        _updateStopwatch.Stop();
        TickDurationMs = _updateStopwatch.ElapsedMilliseconds;
    }
    public void Clear() {
        _aliveTweens.Clear();
        _tweensToAdd.Clear();
        _tweensToRemove.Clear();
    }
    void TryStartTween(GTween gTween) {
        if(!gTween.IsPlaying) gTween.Start();
    }
}
