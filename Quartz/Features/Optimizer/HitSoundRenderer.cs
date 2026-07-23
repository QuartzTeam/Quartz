using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Quartz.Compat.Game;
namespace Quartz.Features.Optimizer;
internal static class HitSoundRenderer {
    private static int SampleRate = 48000;
    private const int Channels = 2;
    private const double SegmentSeconds = 0.5;
    private const double AheadSeconds = 12.0;
    private const double ClipTailSeconds = 0.03;
    private const double LateMarginSeconds = 0.05;
    private static int SegmentSamples = (int)Math.Ceiling(SegmentSeconds * 48000);
    private static int SegmentFloats = SegmentSamples * Channels;
    private const int MaxVoices = 32;
    private const int MaxQueuePerFrame = 2;
    private const int MaxApplyPerFrame = 2;
    private const int MaxPooledBuffers = 12;
    private sealed class HitSoundEvent {
        public double Time;
        public float Volume;
        public ClipData Data;
    }
    private sealed class ClipData {
        public float[] Data;
        public int Samples;
        public int ClipChannels;
        public int Frequency;
    }
    private sealed class SegmentJob {
        public double Start;
        public double End;
        public readonly List<HitSoundEvent> Events = new();
    }
    private sealed class RenderResult {
        public int Generation;
        public SegmentJob Job;
        public float[] Buffer;
    }
    private sealed class Voice {
        public GameObject Go;
        public AudioSource Source;
        public AudioClip Clip;
        public double BusyUntil;
    }
    private static readonly FieldInfo HitSoundsDataField = AccessTools.Field(typeof(scrConductor), "hitSoundsData");
    private static readonly FieldInfo NextHitSoundField = AccessTools.Field(typeof(scrConductor), "nextHitSoundToSchedule");
    private static readonly Type HitSoundsDataType = AccessTools.Inner(typeof(scrConductor), "HitSoundsData");
    private static readonly FieldInfo HitSoundField = HitSoundsDataType != null ? AccessTools.Field(HitSoundsDataType, "hitSound") : null;
    private static readonly FieldInfo TimeField = HitSoundsDataType != null ? AccessTools.Field(HitSoundsDataType, "time") : null;
    private static readonly FieldInfo VolumeField = HitSoundsDataType != null ? AccessTools.Field(HitSoundsDataType, "volume") : null;
    private static readonly Dictionary<int, ClipData> ClipCache = new();
    private static readonly Dictionary<int, ClipData> HitSoundClipCache = new();
    private static Func<object, int> hitSoundIdGetter;
    private static Func<object, double> timeGetter;
    private static Func<object, float> volumeGetter;
    private static bool captureGettersBuilt;
    private static bool EnsureCaptureGetters() {
        if(captureGettersBuilt) return hitSoundIdGetter != null;
        captureGettersBuilt = true;
        try {
            System.Linq.Expressions.ParameterExpression item =
                System.Linq.Expressions.Expression.Parameter(typeof(object), "item");
            System.Linq.Expressions.Expression typed =
                System.Linq.Expressions.Expression.Convert(item, HitSoundsDataType);
            Func<object, T> Build<T>(FieldInfo field) =>
                System.Linq.Expressions.Expression.Lambda<Func<object, T>>(
                    System.Linq.Expressions.Expression.Convert(
                        System.Linq.Expressions.Expression.Field(typed, field), typeof(T)),
                    item).Compile();
            hitSoundIdGetter = Build<int>(HitSoundField);
            timeGetter = Build<double>(TimeField);
            volumeGetter = Build<float>(VolumeField);
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn("[Optimizer] hit-sound capture getters unavailable, using reflection: " + e.Message);
            hitSoundIdGetter = null;
            timeGetter = null;
            volumeGetter = null;
            return false;
        }
    }
    private static readonly List<SegmentJob> Segments = new();
    private static int nextSegmentIndex;
    private static AudioMixerGroup mixerGroup;
    private static bool sceneHookInstalled;
    private static int generation;
    private static readonly ConcurrentQueue<RenderResult> pendingJobs = new();
    private static readonly ConcurrentQueue<RenderResult> completedJobs = new();
    private static readonly AutoResetEvent jobSignal = new(false);
    private static Thread renderThread;
    private static readonly object bufferPoolLock = new();
    private static readonly Stack<float[]> bufferPool = new();
    private static bool loggedRenderError;
    private static GameObject poolRoot;
    private static readonly List<Voice> voices = new();
    internal static bool Active {
        get {
            Optimizer.EnsureConf();
            return MainCore.IsModEnabled && Optimizer.Conf != null && Optimizer.Conf.RenderAllHitSounds;
        }
    }
    private static bool ReflectionReady =>
        HitSoundsDataField != null && NextHitSoundField != null &&
        HitSoundField != null && TimeField != null && VolumeField != null;
    internal static void EnsureSceneHook() {
        if(sceneHookInstalled) return;
        sceneHookInstalled = true;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    private static void EnsureAudioFormat() {
        int rate = AudioSettings.outputSampleRate;
        if(rate <= 0) rate = 48000;
        if(rate == SampleRate && SegmentSamples > 0) return;
        SampleRate = rate;
        SegmentSamples = (int)Math.Ceiling(SegmentSeconds * SampleRate);
        SegmentFloats = SegmentSamples * Channels;
        lock(bufferPoolLock) bufferPool.Clear();
        DestroyPool();
    }
    private static void OnSceneUnloaded(Scene scene) => StopAll("scene unloaded", destroyPool: true);
    internal static void Capture(scrConductor conductor) {
        if(conductor == null || !ReflectionReady) return;
        try {
            EnsureAudioFormat();
            if(HitSoundsDataField.GetValue(conductor) is not System.Collections.IList list) return;
            if(list.Count == 0) { StopAll("no hit sounds"); return; }
            bool fast = EnsureCaptureGetters();
            List<HitSoundEvent> events = new(list.Count);
            for(int i = 0; i < list.Count; i++) {
                object item = list[i];
                if(item == null) continue;
                ClipData data;
                if(fast) {
                    int hs = hitSoundIdGetter(item);
                    if(!HitSoundClipCache.TryGetValue(hs, out data)) {
                        if(!ResolveClipData(item, out data)) {
                            StopAll("clip unreadable");
                            return;
                        }
                        HitSoundClipCache[hs] = data;
                    }
                    if(data == null) continue;
                    events.Add(new HitSoundEvent {
                        Time = timeGetter(item),
                        Volume = volumeGetter(item),
                        Data = data,
                    });
                } else {
                    if(!ResolveClipData(item, out data)) {
                        StopAll("clip unreadable");
                        return;
                    }
                    if(data == null) continue;
                    events.Add(new HitSoundEvent {
                        Time = Convert.ToDouble(TimeField.GetValue(item)),
                        Volume = Convert.ToSingle(VolumeField.GetValue(item)),
                        Data = data,
                    });
                }
            }
            if(events.Count == 0) { StopAll("no readable hit sounds"); return; }
            events.Sort((a, b) => a.Time.CompareTo(b.Time));
            StopAll("recapture");
            mixerGroup = conductor.hitSoundGroup;
            BuildSegments(events);
            list.Clear();
            NextHitSoundField.SetValue(conductor, 0);
        } catch(Exception e) {
            MainCore.Log.Wrn("[Optimizer] Render All Hit Sounds capture failed: " + e.Message);
            StopAll("capture error");
        }
    }
    internal static void Pump() {
        if(!Active) return;
        try {
            double now = AudioSettings.dspTime;
            bool queued = false;
            int queuedCount = 0;
            while(queuedCount < MaxQueuePerFrame && nextSegmentIndex < Segments.Count
                  && Segments[nextSegmentIndex].Start <= now + AheadSeconds) {
                SegmentJob job = Segments[nextSegmentIndex];
                nextSegmentIndex++;
                if(job.End + ClipTailSeconds < now - 0.05) continue;
                pendingJobs.Enqueue(new RenderResult { Generation = generation, Job = job });
                queued = true;
                queuedCount++;
            }
            if(queued) {
                EnsureRenderThread();
                jobSignal.Set();
            }
            int applied = 0;
            while(applied < MaxApplyPerFrame && completedJobs.TryDequeue(out RenderResult result)) {
                float[] buffer = result.Buffer;
                if(result.Generation == generation) {
                    ScheduleSegment(result.Job, buffer, AudioSettings.dspTime);
                    applied++;
                }
                ReturnBuffer(buffer);
            }
        } catch(Exception e) {
            MainCore.Log.Wrn("[Optimizer] Render All Hit Sounds pump failed: " + e.Message);
            StopAll("pump error");
        }
    }
    internal static void StopAll(string reason) => StopAll(reason, destroyPool: false);
    private static void StopAll(string reason, bool destroyPool) {
        generation++;
        while(completedJobs.TryDequeue(out RenderResult stale)) ReturnBuffer(stale.Buffer);
        Segments.Clear();
        nextSegmentIndex = 0;
        for(int i = 0; i < voices.Count; i++) {
            Voice v = voices[i];
            if(v.Source != null) v.Source.Stop();
            v.BusyUntil = 0.0;
        }
        if(destroyPool) DestroyPool();
    }
    private static void DestroyPool() {
        for(int i = 0; i < voices.Count; i++) {
            Voice v = voices[i];
            if(v.Clip != null) UnityEngine.Object.Destroy(v.Clip);
            if(v.Go != null) UnityEngine.Object.Destroy(v.Go);
        }
        voices.Clear();
        if(poolRoot != null) UnityEngine.Object.Destroy(poolRoot);
        poolRoot = null;
    }
    private static void BuildSegments(List<HitSoundEvent> events) {
        Segments.Clear();
        nextSegmentIndex = 0;
        double first = events[0].Time;
        double last = events[events.Count - 1].Time;
        long wanted = (long)Math.Floor((last - first) / SegmentSeconds) + 1;
        int count = (int)Math.Min(200000L, Math.Max(1L, wanted));
        for(int i = 0; i < count; i++) {
            double start = first + i * SegmentSeconds;
            Segments.Add(new SegmentJob { Start = start, End = start + SegmentSeconds });
        }
        for(int i = 0; i < events.Count; i++) {
            HitSoundEvent ev = events[i];
            int startIdx = (int)Math.Floor((ev.Time - first) / SegmentSeconds);
            if(startIdx < 0) startIdx = 0;
            if(startIdx >= count) continue;
            double ringOut = ev.Data != null && ev.Data.Frequency > 0
                ? (double)ev.Data.Samples / ev.Data.Frequency
                : 0.0;
            int endIdx = (int)Math.Floor((ev.Time + ringOut - first) / SegmentSeconds);
            if(endIdx >= count) endIdx = count - 1;
            if(endIdx < startIdx) endIdx = startIdx;
            for(int s = startIdx; s <= endIdx; s++) Segments[s].Events.Add(ev);
        }
    }
    private static void EnsureRenderThread() {
        if(renderThread is { IsAlive: true }) return;
        renderThread = new Thread(RenderLoop) {
            IsBackground = true,
            Name = "Quartz HitSoundMixer",
            Priority = System.Threading.ThreadPriority.BelowNormal,
        };
        renderThread.Start();
    }
    private static void RenderLoop() {
        while(true) {
            jobSignal.WaitOne();
            while(pendingJobs.TryDequeue(out RenderResult job)) {
                if(job.Generation != Volatile.Read(ref generation)) continue;
                try {
                    float[] output = RentBuffer();
                    Array.Clear(output, 0, output.Length);
                    List<HitSoundEvent> events = job.Job.Events;
                    for(int i = 0; i < events.Count; i++) MixEvent(output, job.Job.Start, events[i]);
                    job.Buffer = output;
                    completedJobs.Enqueue(job);
                } catch(Exception e) {
                    if(!loggedRenderError) {
                        loggedRenderError = true;
                        MainCore.Log.Wrn("[Optimizer] Render All Hit Sounds mix failed: " + e.Message);
                    }
                }
            }
        }
    }
    private static float[] RentBuffer() {
        lock(bufferPoolLock) {
            if(bufferPool.Count > 0) return bufferPool.Pop();
        }
        return new float[SegmentFloats];
    }
    private static void ReturnBuffer(float[] buffer) {
        if(buffer == null || buffer.Length != SegmentFloats) return;
        lock(bufferPoolLock) {
            if(bufferPool.Count < MaxPooledBuffers) bufferPool.Push(buffer);
        }
    }
    private static void ScheduleSegment(SegmentJob job, float[] buffer, double now) {
        Voice voice = AcquireVoice(now);
        if(voice == null) return;
        double scheduleTime = job.Start;
        double skipped = 0.0;
        double minTime = now + LateMarginSeconds;
        if(scheduleTime < minTime) {
            skipped = minTime - scheduleTime;
            scheduleTime = minTime;
        }
        float clipLength = (float)((double)SegmentSamples / SampleRate);
        if(skipped >= clipLength - 0.001) return;
        voice.Clip.SetData(buffer, 0);
        AudioSource source = voice.Source;
        source.clip = voice.Clip;
        if(mixerGroup != null) source.outputAudioMixerGroup = mixerGroup;
        int timeSamples = skipped > 0.0 ? (int)Math.Round(skipped * SampleRate) : 0;
        if(timeSamples < 0) timeSamples = 0;
        if(timeSamples >= SegmentSamples) timeSamples = SegmentSamples - 1;
        source.timeSamples = timeSamples;
        source.PlayScheduled(scheduleTime);
        voice.BusyUntil = scheduleTime + clipLength - skipped + 0.1;
    }
    private static Voice AcquireVoice(double now) {
        EnsurePoolRoot();
        if(poolRoot == null) return null;
        Voice best = null;
        for(int i = 0; i < voices.Count; i++) {
            Voice v = voices[i];
            if(v.Source == null || v.Clip == null) continue;
            if(v.BusyUntil <= now) return v;
            if(best == null || v.BusyUntil < best.BusyUntil) best = v;
        }
        if(voices.Count < MaxVoices) {
            Voice v = CreateVoice();
            if(v != null) { voices.Add(v); return v; }
        }
        return best;
    }
    private static void EnsurePoolRoot() {
        if(poolRoot != null) return;
        poolRoot = new GameObject("Quartz HitSound Pool");
        UnityEngine.Object.DontDestroyOnLoad(poolRoot);
    }
    private static Voice CreateVoice() {
        if(poolRoot == null) return null;
        GameObject go = new("Voice");
        go.transform.SetParent(poolRoot.transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.volume = 1f;
        source.pitch = 1f;
        source.priority = 128;
        AudioClip clip = AudioClip.Create("QuartzHitSoundSegment", SegmentSamples, Channels, SampleRate, false);
        return new Voice { Go = go, Source = source, Clip = clip };
    }
    private static void MixEvent(float[] output, double segmentStart, HitSoundEvent hit) {
        ClipData data = hit.Data;
        if(data == null) return;
        int maxOutSamples = output.Length / Channels;
        int startSample = (int)Math.Round((hit.Time - segmentStart) * SampleRate);
        if(startSample >= maxOutSamples) return;
        int copySamples = Math.Min(maxOutSamples - startSample,
            (int)Math.Ceiling((double)data.Samples * SampleRate / data.Frequency));
        if(copySamples <= 0) return;
        for(int outSample = 0; outSample < copySamples; outSample++) {
            int dstSample = startSample + outSample;
            if(dstSample < 0) continue;
            double srcPos = (double)outSample * data.Frequency / SampleRate;
            int src0 = (int)srcPos;
            if(src0 < 0 || src0 >= data.Samples) continue;
            int src1 = Math.Min(data.Samples - 1, src0 + 1);
            float frac = (float)(srcPos - src0);
            for(int ch = 0; ch < Channels; ch++) {
                int srcCh = Math.Min(ch, data.ClipChannels - 1);
                float a = data.Data[src0 * data.ClipChannels + srcCh];
                float b = data.Data[src1 * data.ClipChannels + srcCh];
                output[dstSample * Channels + ch] += (a + (b - a) * frac) * hit.Volume;
            }
        }
    }
    private static bool ResolveClipData(object item, out ClipData data) {
        data = null;
        object hitSoundObj = HitSoundField.GetValue(item);
        if(hitSoundObj == null) return true;
        string name = hitSoundObj.ToString();
        if(string.Equals(name, "None", StringComparison.OrdinalIgnoreCase)) return true;
        AudioClip clip = LoadClip("snd" + name);
        if(clip == null) return true;
        return TryGetClipData(clip, out data);
    }
    private static AudioClip LoadClip(string clipName) {
        try {
            return AudioManager.Instance != null ? GameApi.FindAudioClip(clipName) : null;
        } catch(Exception e) {
            MainCore.Log.Wrn("[Optimizer] could not load " + clipName + ": " + e.Message);
            return null;
        }
    }
    private static bool TryGetClipData(AudioClip clip, out ClipData data) {
        data = null;
        if(clip == null) return false;
        int id = clip.GetInstanceID();
        if(ClipCache.TryGetValue(id, out data)) return true;
        try {
            if(clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int channels = Math.Max(1, clip.channels);
            int samples = Math.Max(0, clip.samples);
            if(samples <= 0) return false;
            float[] raw = new float[samples * channels];
            if(!clip.GetData(raw, 0)) return false;
            data = new ClipData {
                Data = raw,
                Samples = samples,
                ClipChannels = channels,
                Frequency = Math.Max(1, clip.frequency),
            };
            ClipCache[id] = data;
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn("[Optimizer] could not read clip " + clip.name + ": " + e.Message);
            data = null;
            return false;
        }
    }
}
