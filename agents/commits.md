# Commit & Push Agent

How commits get made in this repo. Read this before staging anything.

## Shorthands

- **`pcommit`** — commit **and** push. Run the full commit workflow below, then push (see Push). Plain "commit" means commit only, no push.

## Format: Conventional Commits

Every commit subject is `type: summary` (optionally `type(scope): summary`).

```
<type>: <short summary>
```

- `type` is one of the list below, lowercase.
- `summary` is imperative mood ("add", "fix", "reset" — not "added"/"fixes"), lowercase first word, **no trailing period**.
- Keep the subject ≤ ~72 chars. Fold the area/feature into the summary if it isn't obvious (e.g. `fix: reset combo count on built-in level start`).
- Scope is optional. Use it when one type spans an obvious module: `feat(keyviewer): ...`. The house default is **no scope** — the area lives in the summary instead.

### Types

| type | use for |
|------|---------|
| `feat` | a new feature |
| `fix` | a bug fix |
| `chore` | maintenance work, no production code change (deps, config, tooling) |
| `docs` | documentation only |
| `refactor` | code change that's neither a fix nor a feature |
| `style` | formatting, whitespace, semicolons (no logic change) |
| `test` | adding or fixing tests |
| `perf` | performance improvement |
| `build` | build system or dependency changes (here: `build.json` alpha bumps) |
| `ci` | CI config changes |

Pick by intent, not by file: a localization-key add is `chore`, a parser test is `test`, an `alpha` bump in `build.json` is `build`.

### Body (optional)

Subject-only is the norm. Add a blank line + body only when the **why** isn't obvious from the diff — explain the reason, not the mechanics. Don't restate the subject.

## Including yourself
I get you want to include yourself in the commits, but can u just not? do not put @claude as one of the collaborators or co-authors while commiting.

## Splitting: split a lot

One logical change per commit. A working tree with many unrelated edits becomes many commits, not one. Default to fine-grained.

- Group by **concern**, not by file. A feature that touches a settings class, a patch, and a page is **one** commit.
- Keep cross-file references together so each commit compiles on its own: if file A's hunk calls a method added in file B, they land in the same commit.
- Two unrelated fixes in the same file → two commits (split by hunk, see below).

Real example (`e4a07df`…`f4100ed`) — one working tree became these 7 commits:

```
fix:   fall back to hook-held state for Hangul/Hanja keys in key viewer
feat:  add DM Note custom CSS layer to key viewer
test:  add key viewer CSS parser tests
feat:  add panel layer reordering with drag handle
fix:   keep sliders draggable under the reorganize panel
fix:   hide result stats per-column without dropping row-mates
build: bump alpha to 26
```

## Mixed-concern files: hunk-split

When one file holds hunks belonging to different commits (common with `KeyViewerOverlay.cs`, the `en-US.json` / `ko-KR.json` lang pair, `UICore.cs`):

1. Count hunks: `git diff <file> | grep -c '^@@'`. Find the target hunk's position by grepping for a unique line near it.
2. Stage only the wanted hunks with a fed `git add -p` (no TTY needed):
   ```
   printf 'n\nn\ny\nn\n' | git add -p <file>   # y = stage this hunk, n = skip, in file order
   ```
3. **Verify before committing** — never trust the stage blind:
   ```
   git diff --cached <file> | grep -E '^\+' | grep -i '<marker for the hunk you wanted>'
   ```
   Confirm the wanted lines are staged and the others are not.
4. Commit. The remaining hunks stay unstaged for their own commit (a later `git add <file>` picks them all up).

If a hunk boundary won't split the way you need, fall back to a hand-built patch: `git diff <file> > /tmp/p`, trim to the wanted hunks, `git apply --cached /tmp/p`.

## Lang keys

`en-US.json` and `ko-KR.json` are **authored here** and move together — they must stay key-balanced. Attach each feature's new keys to **that feature's** commit (hunk-split the JSON), rather than dumping all locale changes in one catch-all commit — unless the keys are genuinely cross-cutting.

Every other language (`zh-CN`, …) is authored by translators in [PrismMods/Quartz-i18n](https://github.com/PrismMods/Quartz-i18n) and arrives as a PR from the `i18n pull` workflow (hourly, `cron: "0 * * * *"`). Don't hand-edit those here — the next pull merges the translator's values back over yours. The ownership list is `OWNED_HERE = {"en-US", "ko-KR"}` in `scripts/i18n_sync.py`, and both sync directions read it, so change it there and nowhere else.

### A lang commit isn't done until it's pushed

`i18n push` sends `en-US`/`ko-KR` out to the translation repo, and it fires **on push to `main`** filtered to `Quartz/Resource/Export/Lang/**` — never on a local commit. So a lang commit left sitting unpushed has two costs: translators keep working against a stale key set, and the hourly `i18n pull` sees keys in this repo that i18n lacks. It used to propose **deleting** them (that was PR #2 — 31 Korean strings); it now merges instead, so nothing is lost, but the keys still never reach translators until you push.

The usual "push only when asked" below still holds. But when a commit touches `Quartz/Resource/Export/Lang/`, **say it's pending sync and offer to push** rather than leaving it silently local. After pushing, check that the sync actually ran:

```
gh run list --workflow=i18n-push.yml --limit 1
```

A red run means i18n did **not** get the keys. This job has a history of failing on `I18N_PAT` — the preflight prints the fix.

## Workflow

1. `git status --short` and `git diff --stat` — see the whole surface.
2. Read the diffs. Build a mental map of concern → files/hunks.
3. For each concern, in an order that keeps every commit compiling:
   - stage its files (and hunk-split any shared files),
   - verify the stage with `git diff --cached`,
   - commit with a Conventional subject.
4. `git status --short` → must be clean (nothing stranded).
5. Watch for **strays**: edits that appear mid-session and weren't in your plan (e.g. a font-size tweak). Don't fold them into an unrelated commit — give them their own.

## Push

- Push **only when asked**. Committing ≠ pushing.
- This repo's default branch is `main`; work commits straight onto it.
- Check direction before pushing — the remote-tracking ref can be stale, so read the server:
  ```
  git ls-remote origin -h refs/heads/main   # compare to git rev-parse HEAD
  ```
- Normal fast-forward push: `git push origin main`. Then verify `ls-remote` == local HEAD.
- **Force-push only on already-pushed history you rewrote.** If you reword/rebase commits that were never pushed, a plain push still fast-forwards — no force needed.

## Rewriting messages (not yet pushed)

To restyle subjects after the fact without interactive rebase (no TTY): map old→new with `git filter-branch --msg-filter` over `origin/main..HEAD`. The tree must be clean first — `git stash` any stray edit, rewrite, then `git stash pop`.

## Notes

- If tooling appends a `Co-Authored-By: Claude ...` trailer to a message template, **strip it before committing** — see [Including yourself](#including-yourself). Never add one by hand.
- Don't commit or push unless the task calls for it.
