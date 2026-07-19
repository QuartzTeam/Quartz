# Docs Agent

How to keep the documentation site — <https://quartzz.xyz/docs/> — in sync with the mod. **Every release ends with a docs pass**: after publishing per [`agents/release.md`](release.md), fold the changelog into the docs. The site must never lag a shipped build.

## Shorthand

- **`update docs`** — sync the docs site with the latest published release: new/changed feature pages, catalog rows, install/troubleshooting updates, nav entries, verify, commit, push.

## Where the docs live

The docs are **not in this repo**. They live in the website repo and are built with [Zensical](https://zensical.org/) from plain markdown. The config is still `mkdocs.yml` — Zensical reads it natively; don't convert it to `zensical.toml` until the Zensical team ships their migration tooling.

| Piece | Where |
|-------|-------|
| repo | `PrismMods/QuartzWebsite` — expected as a sibling checkout at **`../Quartz-Website`** (hyphenated on disk, unhyphenated on GitHub). Clone with an explicit target so the path matches: `gh repo clone PrismMods/QuartzWebsite ../Quartz-Website` |
| content | `docs/**.md` |
| nav + config | `mkdocs.yml` (repo root) |
| brand styling | `docs/stylesheets/quartz.css` — palette/fonts matched to the homepage; not touched in content updates |
| images | `docs/assets/` |
| homepage | `src/public/` — **separate from the docs**, leave it alone in a docs pass |
| deploy | `.github/workflows/pages.yml` — push to `main` builds and publishes to GitHub Pages |

## Page structure

- `index.md` — overview, loader choice, links.
- `install.md` — install/update flows for both loaders.
- `troubleshooting.md` — symptom-shaped H2 sections.
- `features/index.md` — the catalog: every feature area as a table row, linked when a dedicated page exists.
- `features/<feature>.md` — one page per feature area, kebab-case, named after the `Quartz/Features/<Name>` module (`features/key-viewer.md`).

A new page is live only when it's in **both** the `nav:` in `mkdocs.yml` **and** the catalog table. Orphan pages don't appear anywhere — always register both.

## Styling rules

Write for a player, not a committer — same voice rule as the changelog in `release.md`.

- **Headings.** One H1 (the page/feature name), H2 sections in sentence case, H3 sparingly, nothing deeper.
- **Feature page shape.** 1–2 sentence intro of what it does → `## Where to find it` (which menu tab) → highlights/settings → tips as admonitions. `features/key-viewer.md` is the template.
- **UI names are sacred.** Settings, tabs, and buttons appear exactly as in the UI, in **bold**. English source of truth: `Quartz/Resource/Export/Lang/en-US.json` (keys nested under `en-US`). Never guess a setting name — check the lang file or the feature code.
- **Backticks** for files, keys, code, package names (`Quartz.zip`). Fenced blocks for anything copy-pasteable.
- **Admonitions over shouting.** `!!! note` / `!!! tip` / `!!! warning` instead of bold ALL-CAPS callouts. Body indented **4 spaces** (2 spaces silently breaks rendering). Titled variant: `!!! tip "macOS"`.
- **Tables** for enumerable facts (packages, codecs, catalog rows) with explanation in surrounding prose.
- **Links** between docs pages are relative `.md` paths (`[Install](install.md)`, `[Key Viewer](features/key-viewer.md)`) — MkDocs rewrites them. External links get full URLs.
- **Images** go in `docs/assets/`, PNG, with alt text.
- **Game name.** Spell out **A Dance of Fire and Ice** on first mention per page; the abbreviation is always **ADOFAI** — never `ADoFaI`. ADOFAI is the form the community actually uses.
- **Language.** English only for now. Do not half-translate; a Korean docs tree is a deliberate future decision.
- Extensions available: admonitions, collapsible `???` details, tabbed blocks, `++ctrl+k++` key syntax, `:material-*:` icons, grid cards.

## Procedure (after every release)

1. **Get what shipped.** `gh release view <tag> --json name,body -q '.name + "\n" + .body'` — the curated changelog you just published is the work list.
2. **Fresh checkout.** `git -C ../Quartz-Website pull` (clone first if missing). Docs work happens in that repo, on its `main`.
3. **Map bullets to pages.**
   - `### New` feature → new `features/<feature>.md` + `nav:` entry + linked catalog row. Small additions to an existing area → extend that page instead.
   - `### Improved` / behavior changes → update the feature's page so it describes **current** behavior. Docs are a snapshot, not a changelog — don't append "as of vX.Y" deltas.
   - `### Fixed` → usually nothing; update `troubleshooting.md` if a documented symptom/workaround is now obsolete.
   - Install/loader/packaging changes → `install.md`.
4. **Verify.** From the website repo root: `pip install zensical` (venv is fine), then `zensical build -s` (strict) — it must end with `No issues found`. To eyeball it, `zensical serve`.
5. **Commit + push.** Conventional subject per [`agents/commits.md`](commits.md), e.g. `docs: cover v2.0.0-alpha-85`. Pushing `main` **is** publishing — the Pages workflow deploys on push. Confirm it: `gh run list -R PrismMods/QuartzWebsite --workflow pages.yml -L 1` should end `success`.

## Don'ts

- Don't document unreleased or experimental work — the site describes the latest published build only.
- Don't paste changelog bullets into pages verbatim; rewrite as current behavior.
- Don't invent or paraphrase setting names — lang file or code, always.
- Don't add a page without its `nav:` entry and catalog row.
- Don't touch `src/public/` (homepage), `quartz.css`, or `pages.yml` in a routine docs pass — content changes only.
- Don't hand-write HTML in the markdown; the extensions above cover the layouts you need.
- Don't ship a strict-mode failure or warning.
- Don't leak internals: class names, file paths, patch names, or `Features/<Name>` module jargon stay out of player-facing prose.
