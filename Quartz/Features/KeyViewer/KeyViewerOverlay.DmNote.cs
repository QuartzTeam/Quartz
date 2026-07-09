using Quartz.Features;
using Quartz.Resource;
using UnityEngine;

using TMPro;

namespace Quartz.Features.KeyViewer;

// DM Note mode: per-frame UpdateDmNote, delayed-note scheduling, and stat-value
// helpers. Box visuals come from KeyViewerOverlay.DmNoteLayout, preset parsing
// from KeyViewerOverlay.DmNoteParsing, graph rendering from KeyViewerOverlay.Graph.
public static partial class KeyViewerOverlay {
    private static void BuildDmNote() {
        builtMode = KeyViewerSettings.ModeDmNote;
        builtStyle = -1;

        GameObject rainObj = new("RainLayer");
        rainObj.transform.SetParent(root, false);
        RectTransform rainLayer = rainObj.AddComponent<RectTransform>();
        rainLayer.anchorMin = Vector2.zero;
        rainLayer.anchorMax = Vector2.one;
        rainLayer.offsetMin = Vector2.zero;
        rainLayer.offsetMax = Vector2.zero;
        rainObj.AddComponent<Canvas>().overrideSorting = false;
        rainManager?.SetLayer(rainLayer);

        List<DmNoteSpec> specs = ParseDmNoteSpecs();
        root.sizeDelta = new Vector2(dmCanvasWidth, dmCanvasHeight);

        // Paint order replicates DmNote's CSS stacking: every element (key, stat,
        // graph) carries the preset's zIndex — default = its index within its own
        // array, as OverlayScene does with `pos.zIndex ?? index` — and a higher
        // zIndex draws on top. Ties fall back to DOM order (keys, then stats, then
        // graphs, in array order — the order ParseDmNoteSpecs already emits), where
        // the later element wins. Unity paints later siblings on top, so create in
        // ascending (zIndex, sequence) order. index is only the GameObject name /
        // graph id; box identity is spec.KeyCode, so reordering creation can't
        // disturb key->count.
        int[] order = new int[specs.Count];
        for(int i = 0; i < order.Length; i++) order[i] = i;
        System.Array.Sort(order, (a, b) => {
            int byZ = specs[a].ZIndex.CompareTo(specs[b].ZIndex); // lower zIndex first (behind)
            return byZ != 0 ? byZ : a.CompareTo(b);                // tie: later array index on top
        });
        foreach(int i in order) AddDmNoteBox(i, specs[i]);

        totalCount = 0;
        foreach(Box box in boxes)
            if(!box.IsStat) totalCount += box.Count;

        AddReorganizeHandle();

        Apply();
    }

    private static void RecordDmPress(Box box, float now) {
        box.Count++;
        totalCount++;
        pressLog.Enqueue(now);
        countsDirty = true;
    }

    private static void BeginDmNoteRain(Box box, float now) {
        DmNoteSpec spec = box.Dm;
        if(!Conf.DmNoteEffect || spec == null || !spec.NoteEnabled || rainManager == null) return;

        float delay = dmDelayedNoteEnabled ? dmShortNoteThresholdMs / 1000f : 0f;
        if(delay > 0.0001f) {
            box.DelayedNotePending = true;
            box.DelayedReleasedBeforeStart = false;
            box.DelayedDownTime = now;
            box.DelayedStartTime = now + delay;
            box.DelayedReleaseTime = -1f;
            return;
        }

        box.LastRain = SpawnDmRain(box, now, false);
    }

    private static void EndDmNoteRain(Box box, float now, bool forceMinLength = false) {
        if(box.DelayedNotePending) {
            box.DelayedReleasedBeforeStart = true;
            box.DelayedReleaseTime = now;
            return;
        }

        if(box.LastRain == null) return;

        float minLengthSeconds = dmNoteSpeed > 0f ? dmShortNoteMinLengthPx / dmNoteSpeed : 0f;
        float end = now;
        if(forceMinLength || minLengthSeconds > 0.0001f) end = Mathf.Max(end, box.LastRain.StartTime + Mathf.Max(0.001f, minLengthSeconds));

        box.LastRain.EndTime = end;
        box.LastRain = null;
    }

    private static void UpdateDelayedDmNote(Box box, float now) {
        if(!box.DelayedNotePending || now < box.DelayedStartTime) return;

        DmNoteSpec spec = box.Dm;
        box.DelayedNotePending = false;
        if(!Conf.DmNoteEffect || spec == null || !spec.NoteEnabled || rainManager == null) return;

        box.LastRain = SpawnDmRain(box, box.DelayedStartTime, false);
        if(box.DelayedReleasedBeforeStart) {
            EndDmNoteRain(box, box.DelayedReleaseTime >= 0f ? box.DelayedReleaseTime : now, forceMinLength: true);
            box.DelayedReleasedBeforeStart = false;
        }
    }

    private static int DmStatValue(Box box) {
        if(box.IsKps) return pressLog.Count;
        if(box.IsKpsAvg) return kpsSamples > 0 ? Mathf.RoundToInt(kpsSum / (float)kpsSamples) : 0;
        if(box.IsKpsMax) return kpsMax;
        return box.IsTotal ? totalCount : 0;
    }

    // Current value of a named stat, for the KPS graph to plot.
    internal static int GraphStatValue(string statType) {
        if(string.IsNullOrEmpty(statType)) return pressLog.Count;
        if(statType.Equals("kpsAvg", StringComparison.OrdinalIgnoreCase)) return kpsSamples > 0 ? Mathf.RoundToInt(kpsSum / (float)kpsSamples) : 0;
        if(statType.Equals("kpsMax", StringComparison.OrdinalIgnoreCase)) return kpsMax;
        if(statType.Equals("total", StringComparison.OrdinalIgnoreCase)) return totalCount;
        return pressLog.Count; // kps (default)
    }

    private static void UpdateDmNote(float now) {
        // dm* runtime caches are refreshed by Apply()/ParseDmNoteSpecs() on every
        // settings change, so re-deriving them (8 reads + 8 Mathf.Clamp) every
        // frame here was pure waste — removed.

        while(pressLog.Count > 0 && now - pressLog.Peek() > 1f) pressLog.Dequeue();

        if(now >= nextKpsSample) {
            int kps = pressLog.Count;
            if(kps > kpsMax) kpsMax = kps;
            if(kps > 0) {
                kpsSum += kps;
                kpsSamples++;
            }
            nextKpsSample = now + 0.05f;
        }

        TMP_FontAsset font = FontManager.Current;
        int limiterMode = Mathf.Clamp(Conf.DmOutOfLimiterMode, 0, 2);

        foreach(Box box in boxes) {
            // A font swap re-tessellates the mesh, wiping per-glyph gradient
            // colours — null the trackers so CssTick re-applies them.
            if(box.Label != null && box.Label.font != font) { box.Label.font = font; box.GradLabelText = null; }
            if(box.Value != null && box.Value.font != font) { box.Value.font = font; box.GradValueText = null; }

            DmNoteSpec spec = box.Dm;
            if(spec == null) continue;

            if(spec.IsStat) {
                int value = DmStatValue(box);
                if(box.Value != null && box.LastShown != value) {
                    // Nulling GradValueText tells the glyph-gradient pass to
                    // recolour (and re-mesh) the new digits; see TickBox.
                    SetCount(box.Value, value, thousands: false);
                    box.GradValueText = null;
                } else if(box.Value == null && box.Label != null && spec.InlineStatCounter && box.LastShown != value) {
                    SetPrefixedCount(box.Label, box.DmStatPrefix, value, thousands: false);
                    box.GradLabelText = null;
                }
                box.LastShown = value;
                continue;
            }

            bool rawPressed = KeyHeld(box.Key);
            bool blocked = box.Key != KeyCode.None && rawPressed && KeyLimiter.KeyLimiter.ShouldBlockKey(box.Key);
            bool hidden = blocked && limiterMode == 0;
            bool rainOnly = blocked && limiterMode == 1;
            bool physicalPressed = rawPressed && !hidden && !rainOnly;
            bool ghostPressed = (rainOnly || KeyHeld(spec.GhostKeyCode)) && !hidden;

            if(physicalPressed && !box.RawPressed) {
                RecordDmPress(box, now);
                BeginDmNoteRain(box, now);
            } else if(!physicalPressed && box.RawPressed) {
                EndDmNoteRain(box, now);
            }
            box.RawPressed = physicalPressed;
            UpdateDelayedDmNote(box, now);

            if(ghostPressed && !box.GhostPressed) {
                if(Conf.DmNoteEffect && spec.NoteEnabled && rainManager != null) box.LastGhostRain = SpawnDmRain(box, now, true);
                if(rainOnly) {
                    totalCount++;
                    pressLog.Enqueue(now);
                }
            } else if(!ghostPressed && box.GhostPressed && box.LastGhostRain != null) {
                float minLengthSeconds = dmNoteSpeed > 0f ? dmShortNoteMinLengthPx / dmNoteSpeed : 0f;
                box.LastGhostRain.EndTime = Mathf.Max(now, box.LastGhostRain.StartTime + Mathf.Max(0.001f, minLengthSeconds));
                box.LastGhostRain = null;
            }

            bool displayPressed;
            float displayDelay = dmKeyDisplayDelayMs / 1000f;
            if(displayDelay <= 0.0001f) {
                box.DisplayTargetPressed = physicalPressed;
                box.DisplayTargetTime = now;
                displayPressed = physicalPressed;
            } else {
                if(physicalPressed != box.DisplayTargetPressed) {
                    box.DisplayTargetPressed = physicalPressed;
                    box.DisplayTargetTime = now + displayDelay;
                }
                displayPressed = now >= box.DisplayTargetTime ? box.DisplayTargetPressed : box.Pressed;
            }

            if(displayPressed != box.Pressed) {
                box.Pressed = displayPressed;
                ApplyBoxColors(box);
                RaisePressChanged(box);
            }
            box.GhostPressed = ghostPressed;

            if(box.Value != null && box.Count != box.LastShown) {
                box.LastShown = box.Count;
                SetCount(box.Value, box.Count, thousands: false);
                box.GradValueText = null;
            }
        }

        if(countsDirty && now >= nextCountsSave) {
            nextCountsSave = now + 2f;
            FlushCounts();
        }
    }
}