# Localization & display settings (`Assets/Scripts/Localization/`, `Assets/Scripts/Settings/`)
**Read `Assets/_Pipeline/LOCALIZATION_PIPELINE.md` before adding any player-visible text.**
**Standing rule: never hardcode a display string.** Every new player-facing string (item names,
descriptions, tooltips, UI labels, prompts, popups, effect summaries) gets a key with an entry in
BOTH `es.txt` and `en.txt` — write the translation yourself — and the task ends with *Auditar
claves* at 0 missing.
- **No hardcoded UI strings.** `Loc.Get("key", args)` in code; `text="#key"` in UXML + `LocalizedUi.BindTree(root)`
  in the controller's `OnEnable` (before `DressWithSkin`, which moves button text into a
  `LocalizedUi.ButtonCaptionName` child); `LocalizedUi.Bind(label, key | refresh)` for code-built
  static labels; `LocalizedText` for uGUI/TMP; `InteractionPromptUi.ShowKey` for prompts.
- One file per language, `Assets/Resources/Localization/<code>.txt` (`key = value`, `{0}` format slots,
  `@name`/`@system` metadata); a new file = a new language in the Options selector. Default/fallback
  language + device-language-on-first-launch in `Resources/GameSettings.asset`; choice in
  `PlayerPrefs["settings.language"]`; `Loc.Changed` repaints live, no scene reload.
- Content assets (items, weapons, legendary passives, bosses) carry a `textKey`; their
  `DisplayName`/`Description`/`Title` go through `Loc.ForAsset` and fall back to the asset's own text.
  `UpgradeTree` nodes use `upgrade.<id>.*`, `SynergyConfig` tiers `synergy.<tag>.tier<n>`.
  **Tools ▸ RedMagic ▸ Localización ▸ Sincronizar textos de assets** assigns keys + adds Spanish to
  `es.txt`; **Auditar claves** lists missing keys per language. Use literal keys (switch), never
  `$"prefix.{x}"`, so the audit sees them.
- **Display presets** (`GameSettings ▸ resolutionPresets`: PC 1920×1080 16:9, Mobile 2340×1080 20:9)
  → `DisplaySettings` (`PlayerPrefs["settings.resolutionPreset"]`, applied `BeforeSceneLoad`):
  `Screen.SetResolution` only in a windowed desktop build; everywhere else `DisplayLetterbox` clips
  every camera's `Camera.rect` to the preset aspect (a depth -100 backdrop camera clears the bars) and
  pads each UI Toolkit panel's `visualTree` into the same rect (`letterboxUi`). `CameraFollow` reads
  `camera.aspect` per frame, so its BG clamp follows. Orientation is landscape-only (PlayerSettings +
  `Screen.autorotate*`).
