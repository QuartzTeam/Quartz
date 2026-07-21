using Quartz.Addons;
using Quartz.Async;
using Quartz.Core;
using Quartz.Localization;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal static class PageAddons {
    public static void Create(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var headerRow = GenerateUI.Row(content.transform);
        var headerText = GenerateUI.AddTextH1(headerRow);
        headerText.gameObject.AddComponent<TextLocalization>().Init("ADDONS", "Addons");
        var hintRow = GenerateUI.Row(content.transform, 54f);
        var hintText = GenerateUI.AddMutedText(hintRow, 17f, 0.45f, true);
        hintText.gameObject.AddComponent<TextLocalization>().Init(
            "ADDONS_HINT",
            "Build a .qaddon against the QuartzAddon SDK and drop it into UserData/Quartz/Addons (or use Add Addon). Quartz loads addons at launch; Reload re-reads the folder from disk."
        );
        var actionsRow = GenerateUI.Row(content.transform);
        GenerateUI.ButtonRow(actionsRow);
        UIButton addBtn = GenerateUI.Button(
            actionsRow,
            AddAddon,
            "Add Addon",
            "addons_add"
        );
        GenerateUI.FixWidth(addBtn, 200f);
        addBtn.Rect.AddToolTip(
            "DESC_ADDONS_ADD",
            "Pick a .qaddon or .dll file to copy into the Addons folder, then reload."
        );
        UIButton reloadBtn = GenerateUI.Button(
            actionsRow,
            () => MainThread.Enqueue(AddonService.Reload),
            "Reload Addons",
            "addons_reload"
        );
        GenerateUI.FixWidth(reloadBtn, 200f);
        reloadBtn.Rect.AddToolTip(
            "DESC_ADDONS_RELOAD",
            "Unloads every addon, re-scans the Addons folder, and reloads and rebuilds this window."
        );
        UIButton folderBtn = GenerateUI.Button(
            actionsRow,
            AddonService.OpenAddonsFolder,
            "Open Folder",
            "addons_open_folder"
        );
        GenerateUI.FixWidth(folderBtn, 200f);
        folderBtn.Rect.AddToolTip(
            "DESC_ADDONS_OPEN_FOLDER",
            "Opens UserData/Quartz/Addons in your file browser."
        );
        if(AddonService.Addons.Count == 0) {
            var emptyRow = GenerateUI.Row(content.transform, 54f);
            var emptyText = GenerateUI.AddMutedText(emptyRow, 17f, 0.45f, true);
            emptyText.gameObject.AddComponent<TextLocalization>().Init(
                "ADDONS_EMPTY",
                "No addons installed yet."
            );
            return;
        }
        foreach(AddonService.Handle handle in AddonService.Addons) {
            AddonService.Handle h = handle;
            GenerateUI.Toggle(
                GenerateUI.Row(content.transform, 64f),
                true,
                h.Enabled,
                v => AddonService.SetAddonEnabled(h, v),
                h.Name,
                "addon_" + h.UnitId,
                52f
            );
            string error = h.Error;
            bool hasError = error != null;
            var statusRow = GenerateUI.Row(content.transform, hasError ? 96f : 34f);
            TextMeshProUGUI status = GenerateUI.AddMutedText(statusRow, 15f, 0.45f, true);
            TextCompat.SetWrap(status, hasError);
            status.overflowMode = TextOverflowModes.Ellipsis;
            status.verticalAlignment = hasError ? VerticalAlignmentOptions.Top : VerticalAlignmentOptions.Middle;
            if(error != null) {
                status.color = UIColors.SoftRed;
                status.text = error;
                statusRow.AddToolTip(error.Length > 900 ? error[..900] + "…" : error);
            } else if(!h.Enabled) {
                status.text = MainCore.Tr.Get("ADDONS_STATUS_DISABLED", "Disabled");
            } else if(h.Loaded) {
                string src = Path.GetFileName(h.SourcePath);
                string by = string.IsNullOrEmpty(h.Author) ? "" : $" · {h.Author}";
                status.text = $"v{h.Version}{by} · {src}";
            }
            var removeRow = GenerateUI.Row(content.transform, 44f);
            GenerateUI.ButtonRow(removeRow);
            bool armed = false;
            UIButton removeBtn = null;
            removeBtn = GenerateUI.Button(
                removeRow,
                () => {
                    if(removeBtn == null) return;
                    if(!armed) {
                        armed = true;
                        removeBtn.Label.text = MainCore.Tr.Get("ADDONS_REMOVE_CONFIRM", "Sure?");
                        removeBtn.RestColor = static () => UIColors.SoftRed;
                        removeBtn.Background.color = UIColors.SoftRed;
                        return;
                    }
                    AddonService.RemoveAddon(h);
                },
                "Remove",
                "addons_remove"
            ).SetSecondary();
            GenerateUI.FixWidth(removeBtn, 130f);
            removeBtn.Rect.AddToolTip(
                "DESC_ADDONS_REMOVE",
                "Deletes this addon's file and settings from disk. This can't be undone."
            );
        }
    }
    private static void AddAddon() {
        string path;
        try {
            path = FileDialog.PickFile(
                null,
                "Quartz Addon",
                AddonService.ImportExtensions,
                GenerateUI.Tr("ADDONS_ADD_TITLE", "Add Quartz Addon")
            );
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PageAddons)}] PickFile failed: {e}");
            return;
        }
        if(string.IsNullOrEmpty(path)) return;
        AddonService.ImportAddon(path);
    }
}
