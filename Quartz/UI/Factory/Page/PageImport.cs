using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal static class PageImport {
    private static RectTransform listContainer;
    private static TextMeshProUGUI statusText;
    private static readonly Dictionary<string, SettingsImportReplaceMode> modes = [];
    private static readonly Dictionary<string, SettingsImportKeyViewerPart> parts = [];
    private static readonly (SettingsImportKeyViewerPart Flag, string Id, string Default)[] PartDefs = [
        (SettingsImportKeyViewerPart.KeysLayout, "import_part_keys", "Keys / layout"),
        (SettingsImportKeyViewerPart.Labels, "import_part_labels", "Labels"),
        (SettingsImportKeyViewerPart.Colors, "import_part_colors", "Colors"),
        (SettingsImportKeyViewerPart.Rain, "import_part_rain", "Rain"),
        (SettingsImportKeyViewerPart.PositionSize, "import_part_position", "Position / size"),
    ];
    public static void Create(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        TextMeshProUGUI headerText = GenerateUI.AddTextH1(GenerateUI.Row(content.transform));
        GenerateUI.Localize(headerText, "IMPORT_HEADER", "Import from other mods");
        var hintRow = GenerateUI.Row(content.transform, 96f);
        var hintText = GenerateUI.AddMutedText(hintRow, 17f, 0.45f, true);
        TextCompat.Wrap(hintText);
        hintText.rectTransform.offsetMax = new Vector2(-250f, 0f);
        GenerateUI.Localize(
            hintText,
            "IMPORT_HINT",
            "Pull your settings in from another ADOFAI mod. Quartz reads each supported mod " +
            "loaded through Unity Mod Manager and copies over what it has a home for. Your other settings are left untouched."
        );
        var topRow = GenerateUI.Row(content.transform);
        UIButton rescanBtn = GenerateUI.Button(topRow, RebuildList, "Rescan", "import_rescan").SetSecondary();
        {
            var br = rescanBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(160f, 50f);
            br.offsetMax = Vector2.zero;
        }
        rescanBtn.Rect.AddToolTip("DESC_IMPORT_RESCAN", "Re-scan for supported mods loaded through Unity Mod Manager.");
        var statusRow = GenerateUI.Row(content.transform, 32f);
        statusText = GenerateUI.AddMutedText(statusRow, 18f, 0.45f, true);
        statusText.text = "";
        GameObject list = new("Mods");
        list.transform.SetParent(content.transform, false);
        listContainer = list.AddComponent<RectTransform>();
        GenerateUI.FitVertical(list, 16f);
        RebuildList();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
    private static void RebuildList() {
        if(listContainer == null) return;
        GenerateUI.ClearChildren(listContainer);
        List<SettingsImportOption> options = SettingsImporter.GetAvailableOptions();
        List<InstalledModInfo> installed = SettingsImporter.GetAllInstalledMods();
        HashSet<string> compatIds = new(StringComparer.OrdinalIgnoreCase);
        foreach(SettingsImportOption opt in options) compatIds.Add(opt.Id);
        List<InstalledModInfo> incompatible = [];
        foreach(InstalledModInfo mod in installed)
            if(!compatIds.Contains(mod.Id)) incompatible.Add(mod);
        if(options.Count == 0 && incompatible.Count == 0) {
            var emptyRow = GenerateUI.Row(listContainer, 96f);
            var emptyText = GenerateUI.AddMutedText(emptyRow, 18f, 0.6f, true);
            TextCompat.Wrap(emptyText);
            emptyText.rectTransform.offsetMax = new Vector2(-250f, 0f);
            GenerateUI.Localize(
                emptyText,
                "IMPORT_NONE",
                "No supported mods detected. Load one through Unity Mod Manager — KorenResourcePack (v1), " +
                "JipperResourcePack, JipperKeyViewer, ADOFAI Tweaks, KeyboardChatterBlocker, or Enhanced Effect Remover — then press Rescan."
            );
            return;
        }
        options.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        incompatible.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        foreach(SettingsImportOption option in options) CreateOptionCard(option);
        foreach(InstalledModInfo mod in incompatible) CreateIncompatibleCard(mod);
    }
    private static void CreateIncompatibleCard(InstalledModInfo mod) {
        var row = GenerateUI.Row(listContainer, 50f);
        GenerateUI.ButtonRow(row);
        var label = GenerateUI.AddText(row, noPad: true);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.color = new Color(1f, 1f, 1f, 0.55f);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        label.text = mod.Label;
        var tag = GenerateUI.AddMutedText(row, 17f, 0.4f, true);
        tag.alignment = TextAlignmentOptions.MidlineRight;
        LayoutElement tagLe = tag.gameObject.AddComponent<LayoutElement>();
        tagLe.preferredWidth = 170f;
        tagLe.minWidth = 170f;
        tagLe.flexibleWidth = 0f;
        GenerateUI.Localize(tag, "IMPORT_NOT_COMPATIBLE", "Not Compatible");
    }
    private static void CreateOptionCard(SettingsImportOption option) {
        var row = GenerateUI.Row(listContainer, 50f);
        GenerateUI.ButtonRow(row);
        var label = GenerateUI.AddText(row, noPad: true);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        label.text = option.Label;
        UIButton importBtn = GenerateUI.Button(row, () => RunImport(option, false), "Import", "import_do");
        GenerateUI.FixWidth(importBtn, 140f);
        importBtn.Rect.AddToolTip(
            "DESC_IMPORT_DO",
            "Copy this mod's settings into Quartz. Settings it doesn't cover are left as they are."
        );
        UIButton profileBtn = GenerateUI.Button(row, () => RunImport(option, true), "Import Profile", "import_profile").SetSecondary();
        GenerateUI.FixWidth(profileBtn, 190f);
        profileBtn.Rect.AddToolTip(
            "DESC_IMPORT_PROFILE",
            "Copy this mod's settings into a new Quartz profile, leaving the current profile selected."
        );
        if(!SettingsImporter.HasKeyViewerPayload(option.Source)) return;
        SettingsImportReplaceMode mode = modes.TryGetValue(option.OptionId, out var m) ? m : SettingsImportReplaceMode.ReplaceAll;
        var modeHeaderRow = GenerateUI.Row(listContainer, 30f);
        var modeHeader = GenerateUI.AddMutedText(modeHeaderRow, 16f, 0.55f, true);
        GenerateUI.Localize(modeHeader, "IMPORT_KV_MODE", "KeyViewer import");
        IReadOnlyList<SettingsImportReplaceMode> modeValues = new[] {
            SettingsImportReplaceMode.ReplaceAll,
            SettingsImportReplaceMode.ReplaceCertain,
            SettingsImportReplaceMode.KeepOld,
        };
        var modeRow = GenerateUI.Row(listContainer);
        GenerateUI.DropDown(
            modeRow,
            SettingsImportReplaceMode.ReplaceAll,
            mode,
            modeValues,
            ModeLabel,
            chosen => {
                modes[option.OptionId] = chosen;
                RebuildList();
            },
            "import_mode_" + option.OptionId
        );
        if(mode != SettingsImportReplaceMode.ReplaceCertain) return;
        SettingsImportKeyViewerPart selected = parts.TryGetValue(option.OptionId, out var p) ? p : SettingsImportKeyViewerPart.All;
        foreach((SettingsImportKeyViewerPart flag, string id, string def) in PartDefs) {
            GenerateUI.Toggle(
                listContainer,
                true,
                (selected & flag) != 0,
                on => {
                    SettingsImportKeyViewerPart cur = parts.TryGetValue(option.OptionId, out var cp)
                        ? cp
                        : SettingsImportKeyViewerPart.All;
                    cur = on ? cur | flag : cur & ~flag;
                    parts[option.OptionId] = cur;
                },
                def,
                id
            );
        }
    }
    private static string ModeLabel(SettingsImportReplaceMode mode) => mode switch {
        SettingsImportReplaceMode.ReplaceAll => GenerateUI.Tr("IMPORT_MODE_REPLACE_ALL", "Replace all"),
        SettingsImportReplaceMode.ReplaceCertain => GenerateUI.Tr("IMPORT_MODE_REPLACE_CERTAIN", "Replace certain"),
        _ => GenerateUI.Tr("IMPORT_MODE_KEEP_OLD", "Keep old"),
    };
    private static void RunImport(SettingsImportOption option, bool separateProfile) {
        SettingsImportReplaceMode mode = modes.TryGetValue(option.OptionId, out var m) ? m : SettingsImportReplaceMode.ReplaceAll;
        SettingsImportKeyViewerPart p = parts.TryGetValue(option.OptionId, out var pp) ? pp : SettingsImportKeyViewerPart.All;
        SettingsImportResult result = separateProfile
            ? SettingsImporter.ImportToProfile(option, mode, p)
            : SettingsImporter.Import(option, mode, p);
        if(!result.Success) {
            statusText.text = string.Format(GenerateUI.Tr("IMPORT_FAIL", "Import failed: {0}"), result.Message);
            return;
        }
        if(separateProfile && result.ImportedCount > 0) {
            statusText.text = string.Format(
                GenerateUI.Tr("IMPORT_PROFILE_OK", "Imported {0} settings from {1} into profile {2}."),
                result.ImportedCount,
                option.Label,
                result.ProfileName
            );
            return;
        }
        statusText.text = result.ImportedCount > 0
            ? string.Format(GenerateUI.Tr("IMPORT_OK", "Imported {0} settings from {1}."), result.ImportedCount, option.Label)
            : string.Format(GenerateUI.Tr("IMPORT_OK_NONE", "Nothing to import from {0}."), option.Label);
    }
}
