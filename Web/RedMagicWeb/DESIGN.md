# RedMagic · Taller — design notes

How the web tool looks and why, so the next change keeps the same hand without re-deriving it.
Code map: `ARCHITECTURE.md`. Everything visual lives in `styles.css`; the tokens are its first block.

## Where the look comes from

The game's own UI (`Assets/Art/UI/Menus/`, `MainMenu.jpeg`): **carved teal stone**, **runes that glow**,
the **emerald of the crystals and the portal**, and the **amber of fallen leaves**. The tool is the
workshop where that game is made, so it is built from the same materials — not a generic dark dashboard.

- The **tab bar is a beam of bark** over the stone; the open tab is a *lit rune* (crystal-green text,
  a glowing line under it). The brand mark is the main menu's portal (`assets/icon.png`, cropped from
  `MainMenu.jpeg` — re-crop it there if the menu art changes).
- **Panels are slabs** (`--panel`), fields are **cut into** them (`--well`, inset shadow), buttons
  **stand out** of them (`--raise`, lit top edge). Canvases and thumbnails sit in the **pit** (`--pit`).
- **Green means "this is the thing"** (primary action, selection, the open tab). **Amber names sections.**
  Red is only for errors and destructive actions.

## Tokens (never type a grey)

| Token | Use |
|---|---|
| `--bg` `--panel` `--raise` `--raise-hover` | page · slab · raised control · hovered |
| `--well` `--pit` `--checker` | inputs and card bodies · where art is shown · transparency checker |
| `--border` `--border-lit` | slab edge · lit / hovered edge |
| `--accent` `--accent2` `--danger` | crystal green · amber · red |
| `--selected` `--glow` | selected fill · the rune glow (`box-shadow`) |
| `--text` `--muted` `--faint` `--on-accent` | bone · lichen · disabled · text on green |
| `--bark` `--bark-lit` | the tab bar |
| `--font-ui` `--font-carved` | Alegreya Sans (everything) · Cinzel (brand, `h1`, the chosen effect's name) |

Canvas drawing code (`app.js`, the composers) uses the same green / amber as literals — keep them in
step with `--accent` / `--accent2` if those ever change.

## Rules

- **Type is sized for a 2560 px monitor**: body 15 px, hints 13–14 px, nothing under 12 px. Labels are
  sentence case — no ALL CAPS, no letter-spaced eyebrows.
- **A form never stretches across the screen**: sections are slabs at most ~1120 px wide, left-aligned;
  the preview beside it takes the rest.
- **One primary button per decision.** Secondary actions are stone buttons.
- **Carved type (Cinzel) only for names of things** (the brand, a tab's title, the selected effect).
  Never for body text, labels or buttons.
- **Tabs are ordered by use**: the four used most first (Enemy Studio, VFX Lab, Boss Creator, Biblioteca),
  a divider, then the rest. **Alt+1…8** opens the tab in that position and each tab shows its number
  (`app.js ▸ tabShortcuts`); the order of the buttons in `index.html` is the only place that mapping lives.
  Ctrl+number belongs to the browser and cannot be used.
- **The Biblioteca is a shelf**: each saved thing is a slab with its art in a lit niche; the colour of its
  top edge and first badge says what kind it is (`.libCard[data-kind]`).
- **Rebuilding a panel must not move the user**: keep scroll positions (see `vfx-lab.js ▸ renderForm`),
  and build long lists once and patch them (the Lab's gallery) instead of re-rendering.
- **The last open tab is remembered** (`localStorage`, `app.js ▸ restoreLastTab`).
- Motion: none on its own. Hover and press feedback only.
- Emoji in tab names and buttons are the tool's vernacular — they stay.

## Keys and conveniences (`modules/shell-tools.js`, wired in `app.js`)

| Key | Does |
|---|---|
| Alt+1…8 | opens the tab in that position |
| Ctrl+S | saves what the open tab saves (`saveActions`) — a tab that only exports says so |
| Ctrl+K | the finder: tabs, VFX Lab effects, saved entries, actions |
| Space | "▶ Probar" of the open tab (VFX Lab; the open gesture in Enemy Studio) — never while typing in a field |

- A tab with edits that were not saved shows an **amber dot**; closing the page with any pending asks first.
  Saving or exporting (Ctrl+S, or any 💾 / ⬇ button of that tab) clears it. Mark view-only controls
  `data-nodirty` so that looking is not counted as editing.
- What a shortcut did is said in a **toast** (`shell.toast`), since nothing else on screen says it.
- A new tab gets these by adding its entry to `saveActions` / `playActions` / `dirtyTabs` in `app.js` —
  never its own key handler.
- Biblioteca: search, order, **Duplicar** per entry, and **Copia de seguridad / Restaurar** (one JSON with
  entries, maps and pieces; restore replaces same-id records and leaves the rest).
