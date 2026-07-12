using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;

namespace Quartz.UI.Factory.Page;

internal static class TufSettingsUI {
    public static void Create(RectTransform parent) {
        RectTransform content = PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "TUF_SETTINGS", "Settings");
        TufService service = TufService.Instance;
        if(service == null) return;

        RectTransform warningRow = null;
        void RefreshWarning(bool linked) =>
            warningRow?.gameObject.SetActive(linked && !TufHelperLiteLink.Installed);

        UIButton openFolderBtn = GenerateUI.Button(
            GenerateUI.Row(content.transform),
            () => {
                try {
                    string path = (service.LinkTufHelperLite ? TufHelperLiteLink.DownloadsRoot() : null) ?? MainCore.Paths.TufLevelsPath;
                    System.IO.Directory.CreateDirectory(path);
                    UnityFileDialog.FileBrowser.Reveal(path);
                } catch(System.Exception e) {
                    MainCore.Log.Err($"[TufSettingsUI] Reveal failed: {e}");
                }
            },
            "Open Levels Folder",
            "tuf_open_folder"
        ).SetSecondary();
        openFolderBtn.Rect.AddToolTip(
            "DESC_TUF_OPEN_FOLDER",
            "Opens the folder where downloaded TUF levels are saved."
        );

        GenerateUI.ToggleTip(
            content.transform,
            false,
            service.LinkTufHelperLite,
            v => {
                service.SetLinkTufHelperLite(v);
                RefreshWarning(v);
            },
            "Link to TUFHelperLite Directories",
            "tuf_link_helper",
            "Saves downloaded levels into TUFHelperLite's Downloads folder (as tuf-<id>) so both mods share one level library. Inside the game folder, UMMMods is checked first, then Mods."
        );
        warningRow = GenerateUI.Row(content.transform, 56f);
        GenerateUI.AddLocalizedMutedText(
            warningRow,
            "TUF_LINK_HELPER_MISSING",
            "TUFHelperLite was not found in the game's Mods or UMMMods folder — without that mod this setting isn't necessary.",
            16f
        ).color = new Color(1f, 0.72f, 0.35f, 0.9f);
        RefreshWarning(service.LinkTufHelperLite);
    }
}
