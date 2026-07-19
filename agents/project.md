# Project Agent Guide

This is the high-level map for AI agents working in this repo. Read this first, then read the narrower guides in this folder when they match the task:

- `agents/commits.md` — commit format and staging rules.
- `agents/i18n.md` — localization keys, JSON parity, and translation audits.
- `agents/release.md` — full GitHub release flow and changelog de-duplication.
- `agents/docs.md` — documentation site (quartzz.xyz/docs) styling and the per-release docs update.

## What this project is

Quartz is an all-in-one mod for **A Dance of Fire and Ice**. It builds one shared C# runtime for two loader targets:

- **MelonLoader**: primary/recommended build, packaged as `dist/Quartz.zip` with `Mods/Quartz.dll` plus `UserData/Quartz/*`.
- **UnityModManager**: alternate build, packaged as `dist/QuartzUmm.zip` as a self-contained `Quartz/` mod folder.

The codebase was formerly KorenResourcePack v2 and still contains migration/self-heal code for old `Koren` user data and `Koren.dll` installs. Treat this as intentional compatibility unless the task is explicitly about removing legacy support.

## Tech stack and build model

- Language: C# with `netstandard2.1` for the mod (`Quartz/Quartz.csproj`).
- Tests: small plain .NET console test project targeting `net10.0` (`Quartz.Tests/Quartz.Tests.csproj`).
- SDK pin: `global.json` asks for .NET SDK `10.0.100` with `rollForward: latestFeature`.
- Formatting: `.editorconfig` uses 4-space C# indentation, tabs for project files, 2-space JSON/Markdown.
- Nullable: mod uses `<Nullable>warnings</Nullable>`; tests use nullable enabled.
- C# style seen in repo: file-scoped namespaces, opening braces on the same line, explicit static feature classes, settings objects with public fields and manual JSON serialize/deserialize.

Local game DLL references come from `Directory.Build.props` / `Directory.Build.example.props` and point at the user's ADOFAI install. Do not assume the project builds on a clean CI machine without the game assemblies.

## Important commands

From repo root:

```sh
./test.sh
```

Runs the console tests in Release:

```sh
dotnet run --project Quartz.Tests/Quartz.Tests.csproj -c Release
```

Build and auto-install/package both loader targets:

```sh
./build.sh [Debug|Release|Debug_IL2CPP|Release_IL2CPP] [ML|UMM|both]
```

Defaults are `Release` and `both`. (`build.sh`'s own header comment only documents the first argument — it's stale; the second one works.)

Examples:

```sh
./build.sh Debug ML
./build.sh Release both
```

Direct builds:

```sh
dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=ML
dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=UMM
```

Release publishing is handled by `tools/release.sh`; read `agents/release.md` before using it.

## Repo map

Top level:

- `README.md` / `README.kr.md` — user-facing install/readme; screenshots in `readme/`.
- `AGENTS.md`, `CLAUDE.md` — agent entry points that point here.
- `CREDITS.md` — attribution.
- `Quartz.slnx` — solution file (no `.sln`; some older docs said `Koren.slnx` — that never existed).
- `Quartz/` — main mod source.
- `Quartz.Tests/` — plain .NET tests for Unity-free code (see Tests below).
- `sdk/` — addon SDK shipped to modders (see Addons system).
- `scripts/` — `i18n_sync.py`, `validate_i18n.py`.
- `tools/release.sh` — GitHub release automation.
- `build.sh` — local build/auto-install/package script.
- `test.sh` — test runner.
- `build.json` — per-version/channel build counter; generated/read by build and release tooling.
- `dist/` — generated release zips; do not hand-edit.
- `agents/` — workflow docs for AI agents.
- `.github/workflows/` — CI plus the `i18n-push` / `i18n-pull` sync jobs (see `agents/commits.md`).

Main `Quartz/` layout:

- `Loader.cs` — MelonLoader entry point, compiled only when `LoaderTarget != UMM`.
- `LoaderUmm.cs` — UnityModManager entry point, compiled only when `LoaderTarget=UMM`.
- `Core/` — runtime lifecycle, version info, keybinds, service/tick orchestration.
- `Compat/` — small host abstraction interfaces used by both loaders.
- `IO/` — settings persistence, atomic files, profile manager, path utilities.
- `Localization/` — language loader and TMP text localization behavior.
- `Resource/` — embedded images/fonts, exported fonts/lang/presets, resource managers.
- `UI/` — in-game settings UI, page factories, reusable controls, drag/resize/reorganize utilities.
- `Features/` — feature modules and their settings/patches/overlays.
- `Addons/` — user addon system (precompiled `.qaddon`/`.dll` loaded from the data root, built against the `sdk/` reference assembly; separate from built-in `Features/`).
- `Update/` — self-update logic against GitHub releases.
- `Async/`, `Tween/`, `GTweens/`, `Utility/` — supporting runtime utilities.

## Runtime architecture

`Loader.cs` and `LoaderUmm.cs` are thin host bridges. Both call `MainCore.Initialize(IQuartzHost)`, which creates a `QuartzRuntime`.

Key files:

- `Quartz/Core/MainCore.cs` — static facade used throughout the mod (`MainCore.Conf`, `MainCore.Tr`, `MainCore.Log`, `MainCore.Root`, etc.). It null-guards teardown paths.
- `Quartz/Core/QuartzRuntime.cs` — owns the lifecycle. It creates paths/config/resources/root object, initializes services, registers ticks, toggles features, and disposes in reverse order.
- `Quartz/Core/RuntimeServices.cs` — service initialize/dispose list with startup timing logs.
- `Quartz/Core/RuntimeTicks.cs` — per-frame tick list.
- `Quartz/Core/FeatureRegistry.cs` — collects per-feature enable/disable steps; `EnableAll()`/`DisableAll()` run them when the mod is toggled on/off.
- `Quartz/Core/Service/*` — `PathService`, `LocalizationService`, `LangUpdateService` (pulls newer lang files from the `Quartz-i18n` repo at runtime), `UIService`, `TweenService`, `HarmonyService`.
- `Quartz/Core/Info.cs` — project identity (`Version`, `Channel`, GitHub repo info). `Build` comes from generated `BuildInfo.g.cs` based on `build.json`.

`QuartzRuntime.Initialize()` is where most global feature setup happens. Before adding another startup hook, check that file for the existing ordering and disposal expectations.

## Loader-specific behavior

MelonLoader (`Loader.cs`):

- Data root is `<game>/UserData/Quartz`.
- Installed DLL is `<game>/Mods/Quartz.dll`.
- Self-update downloads `Quartz.zip` and extracts over the game root.
- Has `[assembly: HarmonyDontPatchAll]`; `HarmonyService` owns patch lifecycle to avoid double patching.

UnityModManager (`LoaderUmm.cs`):

- Data root is the mod's own folder (`.../Quartz/`).
- Build defines `QUARTZ_UMM` and outputs assembly name `QuartzUmm` while keeping root namespace `Quartz`.
- Entry method is `Quartz.LoaderUmm.Load` via `Quartz/Resource/Umm/Info.json`.
- No UMM IMGUI settings panel; settings live in Quartz's own uGUI menu.
- Self-update downloads `QuartzUmm.zip` and extracts over the UMM mods directory.

When changing loader behavior, check both loader files and the packaging targets in `Quartz/Quartz.csproj`.

## Feature modules

Most features live under `Quartz/Features/<Name>/` and commonly have:

- `<Feature>.cs` — runtime/static feature logic.
- `<Feature>Settings.cs` — persisted settings object.
- `<Feature>Patches.cs` or focused patch files — Harmony patches.
- `<Feature>Overlay.cs` — UI shown during gameplay/editor.
- Matching UI controls under `Quartz/UI/Factory/Page/`.

Current feature areas include:

- `AutoDeafen` — Discord/RPC-driven deafen behavior.
- `ChatterBlocker` — input/chat blocking behavior.
- `Combo`, `Judgement`, `ProgressBar`, `SongTitle`, `Panels` — gameplay HUD overlays.
- `Editor` — editor-focused tweaks/readouts/BGA/difficulty behavior.
- `EffectRemover`, `Nostalgia`, `PlanetColors`, `UiHider`, `Tweaks` — visual/gameplay presentation tweaks.
- `InGameOverlay` — applies the mod font to three specific pieces of the game's own native HUD text (song title, countdown, per-hit judgement); replaces an earlier scene-scanning `GameOverlayFont` that was removed for performance.
- `KeyLimiter`, `Restriction` — input/gameplay restrictions.
- `Calibration` — input-offset calibration flow: per-device float offsets, on-death calibration popup, and a detailed timing readout.
- `KeyViewer` — key display overlay and DM Note-compatible CSS parser/rendering.
- `Optimizer` — performance/background execution/process priority toggles.
- `OttoIcon` — Otto icon customization.
- `PlayCount` — run/play count tracking service.
- `Status` — live stat calculations used by panels and overlays.
- `Tuf` — in-game browser for TUF community levels and packs. Contains HTTP API clients (`TufApiClient`, `TufPackApiClient`), zip extraction/asset recovery (`TufArchive`), a shared download cache, and launchers that open levels directly into the live editor scene (`TufLevelLauncher`, `TufLevelActionRunner`). It is the only feature that performs network I/O — all requests go through `TufNetworkPolicy` — and it is wired as a runtime service, not a simple patch module. Its pages form their **own** sidebar category (Levels / Packs / Settings); the backing enum members are named `NostalgiaTuf*` for historical reasons but are not children of the Nostalgia category.
- `Interop` — compatibility bridges/importers for other mods such as XPerfect and UMM settings.

Before editing a feature, trace all three places: the feature module, its settings class, and the page factory that exposes it.

## UI system

The in-game menu is built in code under `Quartz/UI`. It is a two-column sidebar (categories → child pages), not a flat row of tabs.

- `Quartz/UI/UICore.cs` creates the top-level canvas, panel, sidebar, first-run helper, reorganize mode, resize handle, tooltips, and global open/close behavior. It also defines `OriginalMenuState`, the ordered list of built-in pages.
- `Quartz/UI/Factory/MenuFactory.cs` groups `OriginalMenuState` values into sidebar categories via `CategoryChildren`; each category's first value is its representative state. The enum is never persisted by name, so pages can be reordered/regrouped without corrupting saved settings.
- `Quartz/UI/Factory/PageFactory.cs` wires each state to its page builder.
- `Quartz/UI/Factory/Page/Page*.cs` are the settings pages. Several build more than one state via static methods (e.g. `PageGameplay`, `PageVisuals`, `PageTweaks`, `PageEditor`, `NostalgiaUI`), so one file can back a whole sidebar category.
- `Quartz/UI/Panes/PaneHost.cs` — docked context/live-preview panes shown beside a page (currently wired for Key Viewer).
- `Quartz/UI/Generator/GenerateUI*.cs` creates common rows/controls: toggles, buttons, sliders, inputs, dropdowns, color pickers, keybinds, collapsibles.
- `Quartz/UI/Objects/Impl/*` are backing objects for those controls.
- `Quartz/UI/Utility/Reorganizer.cs` and related utility files handle draggable overlay layout.

Localization convention for UI controls is important: many `GenerateUI` controls auto-localize by normalizing the control `id` into a key. Read `agents/i18n.md` before adding or changing user-facing strings.

## Addons system

`Quartz/Addons/` (namespace `Quartz.Addons`) is an addon loader: **precompiled** `.qaddon`/`.dll` assemblies in the data root's `Addons/` folder are loaded as `QuartzAddon` subclasses. Addons are built against `sdk/QuartzAddon.dll` (the mod's public reference assembly); there is no runtime C# compilation. It is registered like a service in `QuartzRuntime` (`AddonService.Service` + `AddonService.Ticker`), not as a `Features/` module.

- `AddonService.cs` — discovery, load/unload lifecycle, import/remove (`AddonsSettings` persists the per-addon enabled map). Precompiled addons are byte-loaded so their file stays unlocked (deletable/reloadable while loaded); the identity resolver unifies the `Quartz`/`QuartzUmm` reference so one `.qaddon` runs on both loaders.
- `QuartzAddon.cs` — base class addon authors subclass.
- `AddonContext.cs`, `AddonEvents.cs`, `AddonTags.cs` — API surface exposed to addons.
- `AddonSettings.cs` — per-addon persisted settings handle.
- `AddonUI.cs` — lets an addon register its own menu pages; these get dynamic menu states past the `OriginalMenuState` enum and appear as children of the Addons sidebar category.
- `sdk/` (repo root) — the addon SDK: `QuartzAddon.dll` (reference assembly, regenerated by the build via `ProduceReferenceAssembly` + the `PublishAddonSdk` target), `QuartzAddon.props`, `Directory.Build.props.example`, README. Modders build `.qaddon`s against it.

## Settings and data files

Core settings:

- `Quartz/IO/CoreSettings.cs` persists main UI/mod settings to the loader data root.
- `MainCore.Conf` is the active `CoreSettings`; `MainCore.ConfMgr.RequestSave()` schedules writes.

Feature settings:

- Use `SettingsFile<T>` with settings classes implementing `ISettingsFile`.
- Most settings classes expose public fields and implement manual `Serialize()` / `Deserialize(JToken)` using `IOUtils.Read`.
- Save through each feature's `ConfMgr.RequestSave()` or helper, not by writing files directly.

Resources:

- Embedded resources: `Quartz/Resource/Embedded/**` are compiled into the DLL.
- Exported resources: `Quartz/Resource/Export/**` are copied into `UserData/Quartz` or UMM mod folder during packaging.
- Languages: `Quartz/Resource/Export/Lang/` ships `en-US.json`, `ko-KR.json` and `zh-CN.json`, plus a `KEY_PREFIXES.md` legend. Only **en-US ↔ ko-KR** parity is enforced (by test and by `scripts/validate_i18n.py`); other languages are translator-authored and may lag.
- Presets/fonts are shipped under `Quartz/Resource/Export/`.

## Tests

`Quartz.Tests/Program.cs` is only the **registry + runner** — a list of `(name, Action)` pairs. The test bodies live in `Quartz.Tests/{Core,IO,Localization,Features}/`; `Support/Assert.cs` is the assertion helper. Add a test by writing it in the matching subfolder and registering it in `Program.cs`.

Current coverage:

- `Core/SemVerTests.cs` — version parsing/formatting, channel and prerelease ordering.
- `IO/` — atomic file replacement, profile name sanitization, profile/preset bundle import.
- `Localization/LocalizationParityTests.cs` — `en-US`/`ko-KR` key parity (only those two; `zh-CN` is translator-owned and allowed to lag).
- `Features/KeyViewer/` — CSS parser (base + extended web effects), layout document round-trip, DM Note import contract, editor snap/zoom/pan, undo-redo history.
- `Features/Tuf/` — network policy, archive safety, difficulty/quantum filters, disk-space probe, install index and library-root guards, pack parsing.

Prefer putting tests here for logic that can be kept free of Unity/ADOFAI types. The test project links selected source files from `Quartz/` instead of referencing the whole Unity mod assembly. This is also the only project CI can build — the mod itself needs local game DLLs.

## Localization rules

Read `agents/i18n.md` before touching strings. Short version:

- Add every key to both `Quartz/Resource/Export/Lang/en-US.json` and `ko-KR.json`. Leave `zh-CN.json` alone — translators own it.
- Builder controls often derive keys from IDs; do not assume the visible label string is enough.
- Missing keys silently fall back to English, so tests may pass while Korean leaks English unless you audit.
- Preserve placeholders, TMP tags, acronyms, and brand terms.

## Build and packaging details

`Quartz/Quartz.csproj` contains important custom MSBuild targets:

- `GenerateBuildInfo` reads `Quartz/Core/Info.cs` and `build.json`, then emits `BuildInfo.g.cs` into `obj/`.
- MelonLoader `PostBuild` copies `Quartz.dll`, exported resources, and native files into package staging and writes `dist/Quartz.zip`.
- UMM `PostBuildUmm` creates a self-contained `Quartz/` mod folder with `Info.json`, exported files, native files, and writes `dist/QuartzUmm.zip`.
- UMM uses separate `obj/umm/...` and `bin/umm/...` paths so it does not clobber the MelonLoader output.

Do not hand-edit `build.json` or `Info.cs` for routine release bumps; use `tools/release.sh` per `agents/release.md`.

## Working-tree caution

This repo often has in-progress user edits. Before editing:

```sh
git status --short
git diff --stat
```

Do not overwrite or casually reformat unrelated work. If a file is already modified, inspect the diff before touching it.

## Common agent workflow

1. Read this file plus any task-specific guide in `agents/`.
2. Check `git status --short` and `git diff --stat`.
3. Locate the relevant feature/page/settings files with search before editing.
4. For UI/user-facing strings, update both language JSON files and run the i18n parity check or `./test.sh`.
5. For feature changes, update the feature logic, settings serialization, and UI page together when needed.
6. Run the narrowest useful verification:
   - `./test.sh` for Unity-free logic/localization/parser changes.
   - `dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=ML` for mod code if local game references are available.
   - Add `-p:LoaderTarget=UMM` when loader/packaging/shared runtime changes may affect UMM.
7. Summarize changed paths and actual verification output. Do not commit or push unless the user asks.

## Quick path lookup

- Project identity/version: `Quartz/Core/Info.cs`, `build.json`.
- Runtime startup/teardown: `Quartz/Core/QuartzRuntime.cs`, `Quartz/Core/MainCore.cs`.
- MelonLoader host: `Quartz/Loader.cs`.
- UMM host: `Quartz/LoaderUmm.cs`, `Quartz/Resource/Umm/Info.json`.
- Packaging/build targets: `Quartz/Quartz.csproj`, `build.sh`.
- Release script: `tools/release.sh`, `agents/release.md`.
- Commit rules: `agents/commits.md`.
- Localization: `Quartz/Localization/*`, `Quartz/Resource/Export/Lang/*.json`, `agents/i18n.md`.
- Settings persistence: `Quartz/IO/SettingsFile.cs`, `Quartz/IO/CoreSettings.cs`, feature `*Settings.cs` files.
- Main UI: `Quartz/UI/UICore.cs`, `Quartz/UI/Factory/Page/*.cs`, `Quartz/UI/Generator/*.cs`.
- Feature modules: `Quartz/Features/<Feature>/`.
- Stat panels: `Quartz/Features/Panels/PanelsOverlay.cs`, `PanelsSettings.cs`, `Quartz/UI/Factory/Page/PagePanels.cs`.
- TUF browser: `Quartz/Features/Tuf/*`, UI in `Quartz/UI/Factory/Page/TufBrowserUI.cs`, `TufPacksUI.cs`, `TufSettingsUI.cs`.
- Calibration: `Quartz/Features/Calibration/*`, `Quartz/Core/OverlayCalibration.cs`, `Quartz/UI/CalibrationPopupUI.cs`, `Quartz/UI/Factory/Page/PageCalibration.cs`.
- Addon system: `Quartz/Addons/*` (service wired in `Quartz/Core/QuartzRuntime.cs`).
- Menu sidebar/pages: `Quartz/UI/UICore.cs` (`OriginalMenuState`), `Quartz/UI/Factory/MenuFactory.cs`, `Quartz/UI/Factory/PageFactory.cs`, `Quartz/UI/Factory/Page/*.cs`.
- Key viewer CSS parser: `Quartz/Features/KeyViewer/KeyViewerCss.cs`, tests in `Quartz.Tests/Program.cs`.
- Tests: `Quartz.Tests/Program.cs`, `test.sh`.
