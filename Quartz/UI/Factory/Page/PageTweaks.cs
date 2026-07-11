using Quartz.Features.Optimizer;
using Quartz.Features.Tweaks;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
internal static class PageTweaks {
    public static void GeneralPage(RectTransform parent) {
        Tweaks.EnsureConf();
        TweaksSettings conf = Tweaks.Conf;
        TweaksSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "TWEAKS_GENERAL", "General");
        GenerateUI.ToggleTip(
            content.transform,
            def.DisableAutoPause,
            conf.DisableAutoPause,
            v => { conf.DisableAutoPause = v; Tweaks.Save(); },
            "Disable Auto Pause",
            "tw_nopause",
            "While auto-play is on, the game pauses itself (e.g. when the window loses focus). This blocks those automatic pauses — pausing manually still works."
        );
        GenerateUI.ToggleTip(
            content.transform,
            def.BlockMouseWheelScrollWhilePlaying,
            conf.BlockMouseWheelScrollWhilePlaying,
            v => { conf.BlockMouseWheelScrollWhilePlaying = v; Tweaks.Save(); },
            "Block Scroll While Playing",
            "tw_scroll",
            "Ignores mouse wheel input while a level is being played, so accidental scrolling can't affect the game mid-run."
        );
    }
    public static void OptimizerPage(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        Optimizer.EnsureConf();
        OptimizerSettings opt = Optimizer.Conf;
        OptimizerSettings optDef = new();
        var optimizerSec = GenerateUI.FlatSection(content.transform, "Optimizer");
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.SmoothGC,
            opt.SmoothGC,
            v => { opt.SmoothGC = v; Optimizer.Apply(); Optimizer.Save(); },
            "Smooth GC",
            "opt_smoothgc",
            "Holds off garbage collection while a level is playing and runs it when the run ends, so a GC pause can't land mid-run and nudge your timing. The heap grows during the run (a safety collect kicks in on very long levels). Best paired with Clean Heap On Load."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.LeakGuard,
            opt.LeakGuard,
            v => { opt.LeakGuard = v; Optimizer.Apply(); Optimizer.Save(); },
            "Fix Game Memory Leaks",
            "opt_leakguard",
            "Patches known memory leaks in the game itself: decoration render textures and materials that survive level unloads, frame-rate-effect screen buffers, workshop thumbnails, practice-mode waveforms, and internal caches that only ever grow. Reduces RAM creep during long sessions."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.CollectOnLevelLoad,
            opt.CollectOnLevelLoad,
            v => { opt.CollectOnLevelLoad = v; Optimizer.Apply(); Optimizer.Save(); },
            "Clean Heap On Load",
            "opt_collectonload",
            "Runs a garbage collection every time a scene loads, so each run starts from a clean heap. The load screen already hitches, so the collection is free here."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.BoostProcessPriority,
            opt.BoostProcessPriority,
            v => { opt.BoostProcessPriority = v; Optimizer.Apply(); Optimizer.Save(); },
            "Boost Process Priority",
            "opt_priority",
            "Asks the OS to give the game more consistent CPU time (Above Normal priority). Takes effect on Windows; ignored where the system doesn't allow it (usually macOS/Linux)."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.RunInBackground,
            opt.RunInBackground,
            v => { opt.RunInBackground = v; Optimizer.Apply(); Optimizer.Save(); },
            "Run In Background",
            "opt_runinbg",
            "Keeps the game running at full speed when its window loses focus, so a run or practice session doesn't stall when you alt-tab."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.LossyTextureCompression,
            opt.LossyTextureCompression,
            v => { opt.LossyTextureCompression = v; Optimizer.Save(); },
            "Lossy Texture Compression",
            "opt_lossytexture",
            "Compresses custom textures loaded from disk (DXT) to cut their memory use ~4-8x, with a small visual quality cost. Applies to textures loaded after it's turned on."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.FastBloom,
            opt.FastBloom,
            v => { opt.FastBloom = v; Optimizer.Save(); },
            "Fast Bloom",
            "opt_fastbloom",
            "Forces ADOFAI's bloom post-process to use its cheaper low-quality render path while bloom is active. This targets real GPU work and can improve FPS on bloom-heavy levels, with softer/less precise bloom."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.SkipNoOpScreenFilters,
            opt.SkipNoOpScreenFilters,
            v => { opt.SkipNoOpScreenFilters = v; Optimizer.Save(); },
            "Skip No-Op Screen Filters",
            "opt_skipnoopfilters",
            "Skips ADOFAI full-screen screen-tile/screen-scroll shader passes when their current values are visually identity, replacing the shader pass with a plain copy. This removes real render work without wrapping an existing game setting."
        );
    }
    public static void MainMenuPage(RectTransform parent) {
        Tweaks.EnsureConf();
        TweaksSettings conf = Tweaks.Conf;
        TweaksSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var mainMenuSec = GenerateUI.FlatSection(content.transform, "Main Menu");
        GenerateUI.ToggleTip(
            mainMenuSec.Body,
            def.DisableMenuMusic,
            conf.DisableMenuMusic,
            v => { conf.DisableMenuMusic = v; Tweaks.Save(); },
            "Disable Menu Music",
            "tw_menumusic",
            "Mutes the theme song on the title and island-select screens. Takes effect immediately; gameplay music is untouched."
        );
        GenerateUI.ToggleTip(
            mainMenuSec.Body,
            def.MenuBpmEnabled,
            conf.MenuBpmEnabled,
            v => { conf.MenuBpmEnabled = v; Tweaks.Save(); },
            "Custom Menu BPM",
            "tw_menubpm",
            "Sets the menu rabbit's two speeds to the BPMs below instead of the default 1x / 2x. Re-open the menu to apply."
        );
        UISlider slowBpm = GenerateUI.Slider(
            GenerateUI.Row(mainMenuSec.Body),
            def.MenuSlowBpm, 30f, 600f, conf.MenuSlowBpm,
            Mathf.Round, v => conf.MenuSlowBpm = v,
            v => { conf.MenuSlowBpm = v; Tweaks.Save(); },
            "Slow BPM", "tw_menuslowbpm"
        );
        slowBpm.Format = "0";
        UISlider highBpm = GenerateUI.Slider(
            GenerateUI.Row(mainMenuSec.Body),
            def.MenuHighBpm, 30f, 600f, conf.MenuHighBpm,
            Mathf.Round, v => conf.MenuHighBpm = v,
            v => { conf.MenuHighBpm = v; Tweaks.Save(); },
            "High BPM", "tw_menuhighbpm"
        );
        highBpm.Format = "0";
    }
    public static void ResultsPage(RectTransform parent) {
        Tweaks.EnsureConf();
        TweaksSettings conf = Tweaks.Conf;
        TweaksSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var resultsSec = GenerateUI.FlatSection(content.transform, "Detailed Results");
        GenerateUI.ToggleTip(
            resultsSec.Body,
            def.HideResultXAccuracy,
            conf.HideResultXAccuracy,
            v => { conf.HideResultXAccuracy = v; Tweaks.Save(); },
            "Hide X-Accuracy",
            "tw_result_xacc",
            "Removes the X-Accuracy row from the detailed results screen."
        );
        GenerateUI.ToggleTip(
            resultsSec.Body,
            def.HideResultAccuracy,
            conf.HideResultAccuracy,
            v => { conf.HideResultAccuracy = v; Tweaks.Save(); },
            "Hide Accuracy",
            "tw_result_acc",
            "Removes the Accuracy row from the detailed results screen."
        );
        GenerateUI.ToggleTip(
            resultsSec.Body,
            def.HideResultCheckpoints,
            conf.HideResultCheckpoints,
            v => { conf.HideResultCheckpoints = v; Tweaks.Save(); },
            "Hide Checkpoints Used",
            "tw_result_checkpoints",
            "Removes the Checkpoints Used row from the detailed results screen."
        );
        GenerateUI.ToggleTip(
            resultsSec.Body,
            def.HideResultMaximumUsedKeys,
            conf.HideResultMaximumUsedKeys,
            v => { conf.HideResultMaximumUsedKeys = v; Tweaks.Save(); },
            "Hide Maximum Used Keys",
            "tw_result_maxkeys",
            "Removes the Maximum Used Keys row from the detailed results screen."
        );
    }
}
