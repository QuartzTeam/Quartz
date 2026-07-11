using Quartz.Core;
using Quartz.Features.AutoDeafen;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PageGameplay {
    private static void CreateAutoDeafen(Transform content) {
        AutoDeafen.EnsureConf();
        AutoDeafenSettings conf = AutoDeafen.Conf;
        AutoDeafenSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Auto Deafen (Discord)",
            v => {
                conf.Enabled = v;
                AutoDeafen.Save();
            },
            conf.Enabled,
            "Enable Auto Deafen (Discord)", "autodeafen_enable"
        );
        if(AutoDeafen.ShortcutSupported) {
            GenerateUI.DropDown(
                GenerateUI.Row(sec.Body),
                AutoDeafenSettings.ModeShortcut,
                conf.IsShortcut ? AutoDeafenSettings.ModeShortcut : AutoDeafenSettings.ModeBot,
                new[] { AutoDeafenSettings.ModeShortcut, AutoDeafenSettings.ModeBot },
                m => MainCore.Tr.Get(
                    m == AutoDeafenSettings.ModeShortcut ? "AD_MODE_SHORTCUT" : "AD_MODE_BOT",
                    m == AutoDeafenSettings.ModeShortcut ? "Shortcut" : "Bot"),
                v => {
                    conf.Mode = v;
                    AutoDeafen.Save();
                    UICore.Rebuild();
                },
                "ad_mode",
                260f,
                "Mode"
            );
        } else {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(sec.Body, 30f), "AD_SHORTCUT_WINDOWS_ONLY",
                "Shortcut mode is Windows-only — using Bot mode.");
        }
        UISlider pct = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.DeafenAtPercent, 0f, 100f, conf.DeafenAtPercent,
            v => Mathf.Round(v), null, null,
            "Deafen At %",
            "ad_pct"
        );
        pct.Format = "0";
        pct.OnChanged = v => conf.DeafenAtPercent = v;
        pct.OnComplete = v => {
            conf.DeafenAtPercent = v;
            AutoDeafen.Save();
        };
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.OnlyFromStart,
            conf.OnlyFromStart,
            v => {
                conf.OnlyFromStart = v;
                AutoDeafen.Save();
            },
            "Only When Starting From 0%",
            "ad_start"
        );
        if(AutoDeafen.EffectiveMode == AutoDeafenSettings.ModeShortcut) {
            CreateAutoDeafenShortcut(sec.Body, conf);
        } else {
            CreateAutoDeafenBot(sec.Body, conf, def);
        }
        var statusRow = GenerateUI.Row(sec.Body);
        var statusText = GenerateUI.AddMutedText(statusRow, 19f, 0.6f);
        statusRow.gameObject.AddComponent<AutoDeafenStatusLabel>().Label = statusText;
    }
    private static void CreateAutoDeafenShortcut(Transform body, AutoDeafenSettings conf) {
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(body, 30f), "AD_SHORTCUT_HINT",
            "Set this to match your Discord 'Toggle Deafen' keybind.", 16f);
        GenerateUI.Toggle(
            GenerateUI.Row(body), true, conf.ShortcutCtrl,
            v => { conf.ShortcutCtrl = v; AutoDeafen.Save(); }, "Keybind Ctrl", "ad_kb_ctrl");
        GenerateUI.Toggle(
            GenerateUI.Row(body), true, conf.ShortcutShift,
            v => { conf.ShortcutShift = v; AutoDeafen.Save(); }, "Keybind Shift", "ad_kb_shift");
        GenerateUI.Toggle(
            GenerateUI.Row(body), false, conf.ShortcutAlt,
            v => { conf.ShortcutAlt = v; AutoDeafen.Save(); }, "Keybind Alt", "ad_kb_alt");
        GenerateUI.Toggle(
            GenerateUI.Row(body), false, conf.ShortcutMeta,
            v => { conf.ShortcutMeta = v; AutoDeafen.Save(); }, "Keybind Win", "ad_kb_meta");
        DeafenKeyRow(body, conf);
    }
    private static void CreateAutoDeafenBot(Transform body, AutoDeafenSettings conf, AutoDeafenSettings def) {
        GenerateUI.Input(
            GenerateUI.Row(body),
            def.DiscordClientId,
            conf.DiscordClientId,
            v => {
                conf.DiscordClientId = v;
                AutoDeafen.Save();
            },
            "Discord Client ID",
            MainCore.Spr.Get(UISprite.Text128),
            "ad_client_id"
        );
        GenerateUI.Button(
            GenerateUI.Row(body),
            () => AutoDeafen.OpenAuthorizeUrl(),
            "Authorize (Open Discord)",
            "ad_authorize"
        );
        GenerateUI.Button(
            GenerateUI.Row(body),
            () => AutoDeafen.CopyAuthorizeUrl(),
            "Copy Authorize URL",
            "ad_copy_url"
        ).SetSecondary();
        GenerateUI.Button(
            GenerateUI.Row(body),
            () => AutoDeafen.OpenTutorial(),
            "Watch Tutorial",
            "ad_tutorial"
        ).SetSecondary();
        GenerateUI.Button(
            GenerateUI.Row(body),
            () => AutoDeafen.Unlink(),
            "Unlink",
            "ad_unlink"
        ).SetSecondary();
    }
    private static void DeafenKeyRow(Transform parent, AutoDeafenSettings conf) {
        RectTransform rect = GenerateUI.BackGround();
        rect.SetParent(parent, false);
        rect.name = "DeafenKey";
        var label = GenerateUI.AddText(rect);
        GenerateUI.Localize(label, "ad_key", "Discord Key");
        GameObject box = new("DeafenKeyBox");
        box.transform.SetParent(rect, false);
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(1f, 0.5f);
        boxRect.anchorMax = new Vector2(1f, 0.5f);
        boxRect.pivot = new Vector2(1f, 0.5f);
        boxRect.anchoredPosition = new Vector2(-16f, 0f);
        boxRect.sizeDelta = new Vector2(220f, 36f);
        Image boxImg = box.AddComponent<Image>();
        boxImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        boxImg.type = Image.Type.Sliced;
        boxImg.color = UIColors.ObjectButton;
        boxImg.raycastTarget = false;
        var display = GenerateUI.AddText(box.transform, true);
        display.alignment = TextAlignmentOptions.Center;
        display.raycastTarget = false;
        display.text = Keybind.KeyName((KeyCode)conf.ShortcutKey);
        var capture = rect.gameObject.AddComponent<DeafenKeyCapture>();
        capture.Display = display;
        capture.Conf = conf;
        GenerateUI.AddButton(rect.gameObject, btn => {
            if(btn == InputButton.Left) capture.Begin();
        });
    }
    private sealed class DeafenKeyCapture : MonoBehaviour {
        public TMP_Text Display;
        public AutoDeafenSettings Conf;
        private bool listening;
        private static readonly KeyCode[] AllKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));
        public void Begin() {
            if(listening) return;
            listening = true;
            Keybind.Capturing = true;
            Display.text = MainCore.Tr.Get("PRESS_A_KEY", "Press a key...");
        }
        private void Refresh() => Display.text = Keybind.KeyName((KeyCode)Conf.ShortcutKey);
        private void Cancel() {
            listening = false;
            Keybind.Capturing = false;
            Refresh();
        }
        private void OnDisable() {
            if(listening) Cancel();
        }
        private void Update() {
            if(!listening) return;
            if(Input.GetKeyDown(KeyCode.Escape)) {
                Cancel();
                return;
            }
            for(int i = 0; i < AllKeys.Length; i++) {
                KeyCode kc = AllKeys[i];
                if(kc == KeyCode.None || (int)kc >= (int)KeyCode.Mouse0 || Keybind.IsModifier(kc)) continue;
                if(!Input.GetKeyDown(kc)) continue;
                Conf.ShortcutKey = (int)kc;
                listening = false;
                Keybind.Capturing = false;
                Refresh();
                AutoDeafen.Save();
                return;
            }
        }
    }
    private sealed class AutoDeafenStatusLabel : MonoBehaviour {
        public TMP_Text Label;
        private float nextPoll;
        private void Update() {
            if(Label == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.25f;
            string text = MainCore.Tr.Get("STATUS_PREFIX", "Status: ") + AutoDeafen.Status;
            if(Label.text != text) Label.text = text;
        }
    }
}
