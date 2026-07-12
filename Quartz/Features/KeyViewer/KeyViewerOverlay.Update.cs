using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Features.Status;
using Quartz.Resource;
using Quartz.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private sealed class Updater : MonoBehaviour {
        private void LateUpdate() {
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
            float now = Time.unscaledTime;
            bool inGame = GameStats.InGame;
            if(gameStateKnown && wasInGame && !inGame) FlushCounts();
            wasInGame = inGame;
            gameStateKnown = true;
            bool isReorganizing = UICore.IsReorganizing;
            bool overlayVisible = (Panels.PanelsOverlay.IsEnabled && Conf.Enabled && (Conf.ShowOutsideGame || inGame)) || isReorganizing;
            bool show = (Conf.IsSimpleMode || Conf.IsDmNoteMode) && overlayVisible;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if(dragObj != null && dragObj.activeSelf != isReorganizing) dragObj.SetActive(isReorganizing);
            bool footShow = show && Conf.IsSimpleMode && Conf.FootKeyCount() > 0;
            if(footRoot != null && footRoot.gameObject.activeSelf != footShow) footRoot.gameObject.SetActive(footShow);
            if(footDragObj != null) {
                bool footDragActive = isReorganizing && footShow;
                if(footDragObj.activeSelf != footDragActive) footDragObj.SetActive(footDragActive);
            }
            if(!show || !Application.isFocused) {
                MarkInputInactive(now, clearTransientStats: !show);
                TryFlushCounts(now, inGame);
                return;
            }
            if(Conf.IsDmNoteMode) {
                if(isReorganizing) {
                    Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                    Conf.DmOffsetX = stored.x;
                    Conf.DmOffsetY = stored.y;
                }
                if(!InputReady(now)) {
                    TryFlushCounts(now, inGame);
                    return;
                }
                UpdateDmNote(now);
                TryFlushCounts(now, inGame);
                return;
            }
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
            if(!InputReady(now)) {
                TryFlushCounts(now, inGame);
                return;
            }
            while(pressLog.Count > 0 && now - pressLog.Peek() > 1f) pressLog.Dequeue();
            TMP_FontAsset font = FontManager.Current;
            foreach(Box box in boxes) {
                if(box.Label != null && box.Label.font != font) box.Label.font = font;
                if(box.Value != null && box.Value.font != font) box.Value.font = font;
                if(box.IsStat) {
                    bool statVisible = !Conf.StreamerMode;
                    if(box.Fill.gameObject.activeSelf != statVisible) box.Fill.gameObject.SetActive(statVisible);
                    if(!statVisible) continue;
                    int value = box.IsKps ? pressLog.Count : totalCount;
                    if(box.Value != null && box.LastShown != value) {
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
                    if(!box.IsFoot) {
                        box.Count++;
                        totalCount++;
                        pressLog.Enqueue(now);
                        if(Conf.PerKeyKps) box.KpsLog.Enqueue(now);
                        MarkCountsDirty(now);
                    }
                    if(Conf.RainEnabled && box.RainGroup != 0 && rainManager != null) box.LastRain = SpawnRain(box, now);
                } else if(!pressed && box.Pressed && box.LastRain != null) {
                    box.LastRain.EndTime = now;
                    box.LastRain = null;
                }
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
                bool countVisible = !Conf.HideMainKeyCount;
                if(box.Value.gameObject.activeSelf != countVisible) box.Value.gameObject.SetActive(countVisible);
                if(!countVisible) continue;
                if(Conf.PerKeyKps) {
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
            TryFlushCounts(now, inGame);
        }
    }
}
