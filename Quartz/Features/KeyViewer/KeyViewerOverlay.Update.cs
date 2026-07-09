using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Features.Status;
using Quartz.Resource;
using Quartz.UI;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Quartz.Features.KeyViewer;

// MonoBehaviour hook driven by the canvas this overlay owns. Two phases:
//   Update — visibility / drag-writeback / mode-specific per-frame state
//   LateUpdate — CSS gradient / image-download rebuild signals
// Visibility + drag-writeback happen in Update so they precede TMP mesh rebuild.
public static partial class KeyViewerOverlay {
    private sealed class Updater : MonoBehaviour {
        // CSS animation runs in LateUpdate so it samples the press state set in
        // Update and recolours after TMP has regenerated its mesh this frame. A
        // finished font/image download (background thread) triggers one rebuild here.
        private void LateUpdate() {
            // A finished font/image download (background thread) → one rebuild.
            if(cssDownloadArrived) {
                cssDownloadArrived = false;
                if(Conf != null && Conf.IsDmNoteMode) {
                    Rebuild();
                    return;
                }
            }
            if(cssFx.Count > 0 && root != null && root.gameObject.activeSelf) CssTick(Time.unscaledTime);
        }

        private void Update() {
            if(root == null) return;

            bool isReorganizing = UICore.IsReorganizing;
            bool overlayVisible = (Panels.PanelsOverlay.IsEnabled && Conf.Enabled && (Conf.ShowOutsideGame || GameStats.InGame)) || isReorganizing;
            bool show = (Conf.IsSimpleMode || Conf.IsDmNoteMode) && overlayVisible;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);

            if(dragObj != null && dragObj.activeSelf != isReorganizing) dragObj.SetActive(isReorganizing);

            // Foot element: shown only in simple mode with foot keys configured,
            // and draggable on its own in Reorganize mode.
            bool footShow = show && Conf.IsSimpleMode && Conf.FootKeyCount() > 0;
            if(footRoot != null && footRoot.gameObject.activeSelf != footShow) footRoot.gameObject.SetActive(footShow);
            if(footDragObj != null) {
                bool footDragActive = isReorganizing && footShow;
                if(footDragObj.activeSelf != footDragActive) footDragObj.SetActive(footDragActive);
            }

            float now = Time.unscaledTime;
            if(!show || !Application.isFocused) {
                MarkInputInactive(now, clearTransientStats: !show);
                return;
            }

            if(Conf.IsDmNoteMode) {
                // Position only moves while dragging in Reorganize mode; gate the
                // writeback so it isn't a per-frame no-op round-trip otherwise.
                if(isReorganizing) {
                    Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                    Conf.DmOffsetX = stored.x;
                    Conf.DmOffsetY = stored.y;
                }
                if(!InputReady(now)) return;
                UpdateDmNote(now);
                return;
            }

            // Drag writes the position; mirror it back into the settings so it
            // persists. Only the drag (Reorganize mode) can move root. The foot
            // element is dragged independently and writes its own position.
            if(isReorganizing && footRoot != null) {
                Vector2 footStored = OverlayCalibration.Unscale(footRoot.anchoredPosition);
                Conf.FootOffsetX = footStored.x;
                Conf.FootOffsetY = footStored.y;
            }
            if(isReorganizing) {
                Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                Conf.OffsetX = stored.x;
                Conf.OffsetY = stored.y;
            }

            if(!InputReady(now)) return;

            // KPS window: drop presses older than one second.
            while(pressLog.Count > 0 && now - pressLog.Peek() > 1f) pressLog.Dequeue();

            TMP_FontAsset font = FontManager.Current;

            foreach(Box box in boxes) {
                if(box.Label != null && box.Label.font != font) box.Label.font = font;
                if(box.Value != null && box.Value.font != font) box.Value.font = font;

                if(box.IsStat) {
                    // Streamer mode hides the KPS and Total boxes entirely.
                    bool statVisible = !Conf.StreamerMode;
                    if(box.Fill.gameObject.activeSelf != statVisible) box.Fill.gameObject.SetActive(statVisible);
                    if(!statVisible) continue;

                    // Per-box LastShown (not persistent updater fields), so the
                    // cache dies with the box on Rebuild/ResetCounts and a fresh
                    // box never gets stuck on "0" when its restored value happens
                    // to equal the pre-rebuild value.
                    int value = box.IsKps ? pressLog.Count : totalCount;
                    if(box.Value != null && box.LastShown != value) {
                        // Together mode renders the caption inline with the value;
                        // apart mode is the bare number. Both written alloc-free.
                        if(box.StatTogether) {
                            SetPrefixedCount(box.Value, box.StatCaptionChars, value);
                        } else {
                            SetCount(box.Value, value);
                        }
                        box.LastShown = value;
                    }
                    continue;
                }

                bool pressed = KeyHeld(box.Key);
                if(pressed && !box.Pressed) {
                    // Foot keys light up but never add to the counters.
                    if(!box.IsFoot) {
                        box.Count++;
                        totalCount++;
                        pressLog.Enqueue(now);
                        // Only the per-key KPS readout drains box.KpsLog; when it's
                        // off (the default) nothing ever dequeues, so an unconditional
                        // enqueue grows the queue unbounded for the whole session.
                        if(Conf.PerKeyKps) box.KpsLog.Enqueue(now);
                        countsDirty = true;
                    }

                    if(Conf.RainEnabled && box.RainGroup != 0 && rainManager != null) box.LastRain = SpawnRain(box, now);
                } else if(!pressed && box.Pressed && box.LastRain != null) {
                    // Release: freeze the drop's trailing edge.
                    box.LastRain.EndTime = now;
                    box.LastRain = null;
                }

                // Ghost rain: a separate streak from the slot's secondary key,
                // ghost-coloured, with no effect on the press counters. Active
                // whenever the slot has a ghost key set (no separate enable).
                if(Conf.RainEnabled && box.RainGroup != 0
                    && rainManager != null && box.GhostKey != KeyCode.None) {
                    bool ghostPressed = KeyHeld(box.GhostKey);
                    if(ghostPressed && !box.GhostPressed) {
                        box.LastGhostRain = SpawnRain(box, now, ghost: true);
                    } else if(!ghostPressed && box.GhostPressed && box.LastGhostRain != null) {
                        box.LastGhostRain.EndTime = now;
                        box.LastGhostRain = null;
                    }
                    box.GhostPressed = ghostPressed;
                } else if(box.GhostPressed) {
                    // Ghost disabled mid-hold: close any open streak.
                    if(box.LastGhostRain != null) {
                        box.LastGhostRain.EndTime = now;
                        box.LastGhostRain = null;
                    }
                    box.GhostPressed = false;
                }

                if(pressed != box.Pressed) {
                    box.Pressed = pressed;
                    ApplyBoxColors(box);
                    RaisePressChanged(box);
                }

                if(box.Value == null) continue;

                // Hide the per-key counter entirely if requested.
                bool countVisible = !Conf.HideMainKeyCount;
                if(box.Value.gameObject.activeSelf != countVisible) box.Value.gameObject.SetActive(countVisible);
                if(!countVisible) continue;

                if(Conf.PerKeyKps) {
                    // Per-key KPS: this key's presses in the last second. The
                    // window slides every frame, so recompute unconditionally.
                    while(box.KpsLog.Count > 0 && now - box.KpsLog.Peek() > 1f) box.KpsLog.Dequeue();
                    int kps = box.KpsLog.Count;
                    if(box.LastShown != kps) {
                        box.LastShown = kps;
                        SetCount(box.Value, kps);
                    }
                } else if(box.Count != box.LastShown) {
                    box.LastShown = box.Count;
                    SetCount(box.Value, box.Count);
                }
            }

            // Counts persist with the config; batch the writes so a press
            // burst doesn't spam the debounced save.
            if(countsDirty && now >= nextCountsSave) {
                nextCountsSave = now + 2f;
                FlushCounts();
            }
        }
    }
}