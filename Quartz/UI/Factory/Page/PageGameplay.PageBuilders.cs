using Quartz.Core;
using Quartz.Features.ChatterBlocker;
using Quartz.Features.Interop;
using Quartz.Features.KeyLimiter;
using Quartz.Features.KeyViewer;
using Quartz.Features.Restriction;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageGameplay {
    private static void CreateProfileControls(Transform body) {
        int count = KeyLimiter.Profiles.Count;
        int active = KeyLimiter.ActiveProfileIndex;
        int[] indices = new int[count];
        for(int i = 0; i < count; i++) indices[i] = i;
        GenerateUI.DropDown(
            GenerateUI.Row(body),
            0,
            active,
            indices,
            ProfileName,
            v => {
                KeyLimiter.SwitchProfile(v);
                UICore.Rebuild();
            },
            "kl_profile",
            260f,
            "Profile"
        );
        var nameInput = GenerateUI.Input(
            GenerateUI.Row(body),
            "",
            ProfileName(active),
            v => KeyLimiter.RenameActiveProfile(v),
            "Profile Name",
            MainCore.Spr.Get(UISprite.Text128),
            "kl_profile_name"
        );
        nameInput.Rect.AddToolTip(
            "DESC_KL_PROFILE_NAME",
            "Rename the current profile, e.g. \"12 Keys\"."
        );
        GenerateUI.Button(
            GenerateUI.Row(body),
            () => {
                KeyLimiter.AddProfile();
                UICore.Rebuild();
            },
            "Add Profile",
            "kl_add_profile"
        ).SetSecondary();
        UIButton removeBtn = GenerateUI.Button(
            GenerateUI.Row(body),
            () => {
                KeyLimiter.RemoveActiveProfile();
                UICore.Rebuild();
            },
            "Remove Profile",
            "kl_remove_profile"
        ).SetSecondary();
        removeBtn.SetBlocked(count <= 1, true);
    }
    private static string ProfileName(int index) {
        var profiles = KeyLimiter.Profiles;
        if(index < 0 || index >= profiles.Count) return "Profile " + (index + 1);
        string name = profiles[index].Name;
        return string.IsNullOrEmpty(name) ? "Profile " + (index + 1) : name;
    }
    private static void CreateKeyLimiter(Transform content) {
        KeyLimiter.EnsureConf();
        KeyLimiterSettings conf = KeyLimiter.Conf;
        KeyLimiterSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Key Limiter",
            v => {
                conf.Enabled = v;
                KeyLimiter.Save();
            },
            conf.Enabled,
            "Enable Key Limiter", "keylimiter_enable"
        );
        CreateProfileControls(sec.Body);
        UIButton captureBtn = null;
        captureBtn = GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => {
                if(KeyLimiter.IsCapturing) {
                    KeyLimiter.CancelCapture();
                    return;
                }
                if(captureBtn?.Label != null) captureBtn.Label.text = MainCore.Tr.Get("PRESS_A_KEY", "Press a key...");
                KeyLimiter.StartCapture(
                    key => KeyLimiter.ToggleAllowedKey(key),
                    () => {
                        if(captureBtn?.Label != null) captureBtn.Label.text = MainCore.Tr.Get("KL_CAPTURE", "Add / Remove Key");
                    }
                );
            },
            "Add / Remove Key",
            "kl_capture"
        );
        captureBtn.Rect.AddToolTip(
            "DESC_KL_CAPTURE",
            "Press any key to add/remove it from the allowed list. Escape cancels."
        );
        UIButton clearBtn = GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => KeyLimiter.ClearAllowedKeys(),
            "Clear All",
            "kl_clear"
        ).SetSecondary();
        var syncNote = GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(sec.Body, 30f),
            "KL_SYNC_LOCKED",
            "Keys are managed by the Key Viewer (Sync Keys to Key Limiter is on)."
        );
        GameObject list = new("AllowedKeysList");
        list.transform.SetParent(sec.Body, false);
        list.AddComponent<RectTransform>();
        GenerateUI.FitVertical(list, 6f);
        void RebuildKeysList() {
            if(list == null) return;
            GenerateUI.ClearChildren(list.transform);
            int[] keys = KeyLimiter.Conf?.AllowedKeys ?? [];
            if(keys.Length == 0) {
                GenerateUI.AddLocalizedMutedText(
                    GenerateUI.Row(list.transform), "KL_NO_ALLOWED_KEYS", "No allowed keys.", 19f);
                return;
            }
            bool locked = KeyViewerOverlay.IsSyncingToKeyLimiter;
            GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(list.transform)), "KL_ALLOWED_KEYS", "Allowed Keys");
            for(int i = 0; i < keys.Length; i++) {
                CreateKeyRow(list.transform, KeyLimiter.NormalizeKey((KeyCode)keys[i]), locked);
            }
        }
        void ApplySyncLock() {
            bool locked = KeyViewerOverlay.IsSyncingToKeyLimiter;
            if(locked && KeyLimiter.IsCapturing) KeyLimiter.CancelCapture();
            captureBtn.SetBlocked(locked, true);
            clearBtn.SetBlocked(locked, true);
            syncNote.gameObject.SetActive(locked);
            RebuildKeysList();
        }
        if(keysChangedHandler != null) KeyLimiter.Changed -= keysChangedHandler;
        keysChangedHandler = RebuildKeysList;
        KeyLimiter.Changed += keysChangedHandler;
        if(syncLockChangedHandler != null) KeyViewerOverlay.SyncSettingChanged -= syncLockChangedHandler;
        syncLockChangedHandler = ApplySyncLock;
        KeyViewerOverlay.SyncSettingChanged += syncLockChangedHandler;
        ApplySyncLock();
    }
    private static KeyCode setCaptureKey = KeyCode.None;
    private static void CreateKeyRow(Transform parent, KeyCode key, bool locked) {
        RectTransform row = GenerateUI.Row(parent);
        RectTransform bg = GenerateUI.BackGround();
        bg.SetParent(row, false);
        var label = GenerateUI.AddText(bg);
        label.text = Keybind.KeyName(key);
        if(locked) return;
        bool settingThis = setCaptureKey == key && KeyLimiter.IsCapturing;
        GenerateUI.MiniButton(bg, settingThis ? "..." : "Set", settingThis ? null : "SET", -106f, 90f, () => {
            if(KeyLimiter.IsCapturing) {
                KeyLimiter.CancelCapture();
                return;
            }
            setCaptureKey = key;
            KeyLimiter.StartCapture(
                newKey => KeyLimiter.ReplaceAllowedKey(key, newKey),
                () => setCaptureKey = KeyCode.None
            );
        });
        GenerateUI.MiniButton(bg, "Remove", "REMOVE", -8f, 90f, () => KeyLimiter.ToggleAllowedKey(key));
    }
    private static void CreateChatterBlocker(Transform content) {
        ChatterBlocker.EnsureConf();
        ChatterBlockerSettings conf = ChatterBlocker.Conf;
        ChatterBlockerSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Keyboard Chatter Blocker",
            v => {
                conf.Enabled = v;
                ChatterBlocker.Save();
            },
            conf.Enabled,
            "Enable Keyboard Chatter Blocker", "chatterblocker_enable"
        );
        UISlider threshold = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.ThresholdMs, 0f, 100f, conf.ThresholdMs,
            v => Mathf.Round(v), null, null,
            "Threshold (ms)",
            "kcb_ms"
        );
        threshold.Format = "0 ms";
        threshold.OnChanged = v => conf.ThresholdMs = v;
        threshold.OnComplete = v => {
            conf.ThresholdMs = v;
            ChatterBlocker.Save();
        };
    }
    private static void CreateJudgementRestriction(Transform content) {
        Restriction.EnsureConf();
        RestrictionSettings conf = Restriction.Conf;
        RestrictionSettings def = new();
        if(conf.JRestrictMode == 2 && !XPerfectBridge.Installed) conf.JRestrictMode = 1;
        var sec = GenerateUI.FlatSection(
            content, "Judgement Restriction",
            v => {
                conf.JRestrictEnabled = v;
                Restriction.Save();
            },
            conf.JRestrictEnabled,
            "Enable Judgement Restriction", "judgementrestriction_enable"
        );
        RectTransform accuracyRow = null;
        RectTransform[] maskRows = null;
        void RefreshConditionalRows() {
            accuracyRow?.gameObject.SetActive(conf.JRestrictMode == 0);
            if(maskRows == null) return;
            foreach(RectTransform row in maskRows) row?.gameObject.SetActive(conf.JRestrictMode == 3);
        }
        int[] modes = XPerfectBridge.Installed ? [0, 1, 2, 3, 4] : [0, 1, 3, 4];
        GenerateUI.DropDown(
            GenerateUI.Row(sec.Body),
            def.JRestrictMode,
            conf.JRestrictMode,
            modes,
            ModeName,
            v => {
                conf.JRestrictMode = v;
                RefreshConditionalRows();
                Restriction.Save();
            },
            "jr_mode"
        );
        accuracyRow = GenerateUI.Row(sec.Body);
        UISlider accuracy = GenerateUI.Slider(
            accuracyRow,
            def.JRestrictAccuracy, 0f, 100f, conf.JRestrictAccuracy,
            null, null, null,
            "Min Accuracy (%)",
            "jr_acc"
        );
        accuracy.Format = "0.0";
        accuracy.OnChanged = v => conf.JRestrictAccuracy = v;
        accuracy.OnComplete = v => {
            conf.JRestrictAccuracy = v;
            Restriction.Save();
        };
        (HitMargin Margin, string Label, string Id)[] entries = [
            (HitMargin.TooEarly, "Too Early", "jr_allow_tooearly"),
            (HitMargin.VeryEarly, "Very Early", "jr_allow_veryearly"),
            (HitMargin.EarlyPerfect, "Early Perfect", "jr_allow_earlyperfect"),
            (HitMargin.Perfect, "Perfect", "jr_allow_perfect"),
            (HitMargin.LatePerfect, "Late Perfect", "jr_allow_lateperfect"),
            (HitMargin.VeryLate, "Very Late", "jr_allow_verylate"),
            (HitMargin.TooLate, "Too Late", "jr_allow_toolate"),
            (HitMargin.Multipress, "Multipress", "jr_allow_multipress"),
            (HitMargin.FailMiss, "Miss", "jr_allow_miss"),
            (HitMargin.FailOverload, "Overload (No Fail)", "jr_allow_overload_nofail"),
            (HitMargin.OverPress, "Overload (Fail)", "jr_allow_overload_fail"),
        ];
        maskRows = new RectTransform[entries.Length];
        for(int i = 0; i < entries.Length; i++) {
            int bit = 1 << (int)entries[i].Margin;
            maskRows[i] = GenerateUI.Row(sec.Body);
            GenerateUI.Toggle(
                maskRows[i],
                (def.JRestrictAllowedMask & bit) != 0,
                (conf.JRestrictAllowedMask & bit) != 0,
                v => {
                    if(v) conf.JRestrictAllowedMask |= bit;
                    else conf.JRestrictAllowedMask &= ~bit;
                    Restriction.Save();
                },
                entries[i].Label,
                entries[i].Id
            );
        }
        var message = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.JRestrictMessage,
            conf.JRestrictMessage,
            v => {
                conf.JRestrictMessage = v;
                Restriction.Save();
            },
            "Restriction broken message",
            MainCore.Spr.Get(UISprite.Text128),
            "jr_message"
        );
        message.Rect.AddToolTip(
            "DESC_JR_MESSAGE",
            "Shown on the fail screen when the restriction kills the run."
        );
        var hintRow = GenerateUI.Row(sec.Body, 30f);
        var hint = GenerateUI.AddMutedText(hintRow, 16f, 0.45f);
        GenerateUI.Localize(hint, "JR_MESSAGE_HINT", "Use {judgement} for the judgement you broke.");
        RefreshConditionalRows();
    }
    private static string ModeName(int mode) => mode switch {
        0 => MainCore.Tr.Get("JR_MODE_MIN_ACCURACY", "Minimum Accuracy"),
        1 => MainCore.Tr.Get("JR_MODE_PURE_PERFECT", "Pure Perfect Only"),
        2 => MainCore.Tr.Get("JR_MODE_XPURE_PERFECT", "X-Perfect Only"),
        3 => MainCore.Tr.Get("JR_MODE_CUSTOM", "Custom Judgements"),
        4 => MainCore.Tr.Get("JR_MODE_NO_TOO_EARLY", "No Too Early"),
        _ => mode.ToString(),
    };
    private static void CreateDeathLimit(Transform content) {
        Restriction.EnsureConf();
        RestrictionSettings conf = Restriction.Conf;
        RestrictionSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Death Limit",
            v => {
                conf.DeathLimitEnabled = v;
                Restriction.Save();
            },
            conf.DeathLimitEnabled,
            "Enable Death Limit", "deathlimit_enable"
        );
        void LimitPair(string toggleLabel, string sliderLabel, string id,
            bool defOn, bool on, Action<bool> setOn,
            int defMax, int max, Action<int> setMax, float sliderMax) {
            RectTransform sliderRow = null;
            GenerateUI.Toggle(
                GenerateUI.Row(sec.Body),
                defOn,
                on,
                v => {
                    setOn(v);
                    sliderRow?.gameObject.SetActive(v);
                    Restriction.Save();
                },
                toggleLabel,
                id + "_on"
            );
            sliderRow = GenerateUI.Row(sec.Body);
            UISlider slider = GenerateUI.Slider(
                sliderRow,
                defMax, 0f, sliderMax, max,
                v => Mathf.Round(v), null, null,
                sliderLabel,
                id + "_max"
            );
            slider.Format = "0";
            slider.OnChanged = v => setMax((int)v);
            slider.OnComplete = v => {
                setMax((int)v);
                Restriction.Save();
            };
            sliderRow.gameObject.SetActive(on);
        }
        LimitPair("Limit Deaths (Miss + Overload)", "Max Deaths", "dl_deaths",
            def.MaxDeathsOn, conf.MaxDeathsOn, v => conf.MaxDeathsOn = v,
            def.MaxDeaths, conf.MaxDeaths, v => conf.MaxDeaths = v, 100f);
        LimitPair("Limit Misses", "Max Misses", "dl_misses",
            def.MaxMissesOn, conf.MaxMissesOn, v => conf.MaxMissesOn = v,
            def.MaxMisses, conf.MaxMisses, v => conf.MaxMisses = v, 50f);
        LimitPair("Limit Overloads", "Max Overloads", "dl_overloads",
            def.MaxOverloadsOn, conf.MaxOverloadsOn, v => conf.MaxOverloadsOn = v,
            def.MaxOverloads, conf.MaxOverloads, v => conf.MaxOverloads = v, 50f);
        var message = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.DeathLimitMessage,
            conf.DeathLimitMessage,
            v => {
                conf.DeathLimitMessage = v;
                Restriction.Save();
            },
            "Limit reached message",
            MainCore.Spr.Get(UISprite.Text128),
            "dl_message"
        );
        message.Rect.AddToolTip(
            "DESC_DL_MESSAGE",
            "Shown on the fail screen when a limit kills the run."
        );
    }
}
