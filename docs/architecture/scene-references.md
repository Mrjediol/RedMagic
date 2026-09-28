# Scene references — never store a scene by name string
`Assets/Scripts/SceneReference.cs` wraps a `SceneAsset` (editor-only) and bakes its path/name into
serialized fields for the build. Every cross-scene link in this project
(`MainMenuController.hubScene`, `PauseMenuController.mainMenuScene`, `RunManager.hubScene`, every
`WorldDefinition` section/boss slot) uses this instead of a plain
`[SerializeField] string sceneName`. **Follow this convention for any new scene link** — a raw
string breaks silently on rename (nothing fails to compile, nothing turns pink in the Inspector,
the only symptom is `SceneManager.LoadScene` failing at runtime). Call `.ResolveForLoad(this)`
before mutating any state (pausing, stopping music) so a bad reference doesn't leave things
half-changed.

Gotcha: if you hand-edit a `SerializeField` name in a script while a scene referencing it is open
in the Editor, Unity's live scene doesn't get the renamed field until the scene is reloaded from
disk after the recompile — `open_scene` on that path (or closing/reopening it) is often necessary
to pick up the change.
