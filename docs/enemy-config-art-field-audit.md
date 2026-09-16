# `EnemyConfig.art` vs. the sprite-import tool that actually produced `OgroSpriteTestController` — audit

Read-only pass. No code changed.

## 1. What produces `OgroSpriteTestController`

**`Assets/Editor/EnemyImporter.cs`** (menu: `Tools > Enemy Importer > Build Enemy From Folder`).
Takes the `.zip` the web Sprites tab exports (`manifest.json` + one folder of `frame_NNN.png` per
animation), unzipped under `Assets/`, and generates:

- `Assets/Enemies/<Name>/Animations/*.anim` — plain `AnimationClip` assets (one per manifest
  animation), built by `BuildClip()`.
- `Assets/Enemies/<Name>/<Name>Controller.controller` — an `AnimatorController`
  (`AnimatorController.CreateAnimatorControllerAtPath`), one state per clip, trigger-driven
  transitions from `AnyState`. This is `OgroSpriteTestController.controller`, confirmed present at
  `Assets/Enemies/OgroSpriteTest/OgroSpriteTestController.controller`.
- `Assets/Prefabs/Enemies/<Name>.prefab` — a hand-built `GameObject` (`SpriteRenderer` +
  `Animator` + `Rigidbody2D` + `BoxCollider2D`, `BuildEnemyPrefab()`). Confirmed present:
  `Assets/Prefabs/Enemies/OgroSpriteTest.prefab`.
- `Assets/Prefabs/Projectiles/<ProjName>.prefab` for any manifest projectile.

`Assets/Enemies/OgroSpriteTest/manifest.json` on disk matches the web tool's export shape exactly
(`{enemyName, animations:[{name,fps,loop,frameCount,projectile}], projectiles:[]}`), confirming
this is genuinely the output of that pipeline, not a hand-made test fixture.

**Nowhere in this chain is a `SpriteSheetRecipe` asset created.** `EnemyImporter.cs` doesn't
reference the type at all.

## 2. What `EnemyConfigImporter.cs`'s `art` field expects

`Assets/Scripts/Pipeline/Editor/ConfigImport/EnemyConfigImporter.cs:38`:

```csharp
var art = ConfigJson.ReadAsset<SpriteSheetRecipe>(artToken, $"{enemyName}.art");
```

`ConfigJson.ReadAsset<T>` resolves the path via `AssetDatabase.LoadAssetAtPath<T>(path)` and throws
`ConfigImportException` if the asset at that path isn't an instance of `T`. So `art` must point at
a real `SpriteSheetRecipe` asset, full stop — matching `docs/schemas/enemy-config.schema.json`'s
own description of `art` ("The already-sliced SpriteSheetRecipe asset").

**`SpriteSheetRecipe` is not invented.** It's a real, existing class:
`Assets/Scripts/Pipeline/SpriteSheetRecipe.cs`, `namespace RedMagic.Pipeline`,
`[CreateAssetMenu]`. It's produced by a **completely different** pipeline:
`Tools > RedMagic > Pipeline > 2 · Cortar hoja + animar`, i.e. `SheetSlicer.Slice(recipe)` /
`SpritePipeline.RunSheet(recipe)`, which slices a raw sheet PNG under `Assets/Art/Characters/` and
writes a `<Name>.sheet.asset` recipe alongside it (e.g. `Assets/Art/Characters/Ogro/Ogro.sheet.asset`,
confirmed present on disk). `EnemyFactory.Generate` then reads that `SpriteSheetRecipe` to build the
prefab through the full `EnemyStats`/`EnemyBrain`/`EnemyAnimation`/`EnemyAttack` quartet.

## 3. Verdict: two disconnected systems

They don't interoperate. There are **two separate, unrelated sprite-to-enemy pipelines** in this
project:

| | Web Sprites tab → `EnemyImporter.cs` | RedMagic sheet pipeline → `EnemyFactory` |
|---|---|---|
| Input | `.zip` (manifest + per-anim PNG folders) | a `SpriteSheetRecipe` asset pointing at one raw sheet PNG |
| Output | `AnimationClip`s + `AnimatorController` + a hand-built prefab (SpriteRenderer/Animator/Rigidbody2D/BoxCollider2D only) | `AnimationClip`s + `AnimatorController`/`SpriteStateMachine` + a full `EnemyStats`/`EnemyBrain`/`EnemyAnimation`/`EnemyAttack` prefab |
| Produces a `SpriteSheetRecipe`? | **No** | Yes — that's its whole first step |

`EnemyConfigImporter`'s `art` field was designed against the **second** pipeline (correctly — that
matches the schema's own `$comment`, and `SpriteSheetRecipe` is real). But the tool that actually
produced `OgroSpriteTestController` in this session is the **first** one, which has no
`SpriteSheetRecipe` anywhere in it. Pointing `EnemyConfig.art` at
`Assets/Enemies/OgroSpriteTest/OgroSpriteTestController.controller` or at
`Assets/Prefabs/Enemies/OgroSpriteTest.prefab` fails immediately: `AssetDatabase.LoadAssetAtPath<
SpriteSheetRecipe>` returns null for either (wrong type), and `ConfigJson.ReadAsset` throws.

## Which side should change?

**The sprite importer side, not `EnemyConfigImporter`.** Reasoning:

- `EnemyConfigImporter.art` → `SpriteSheetRecipe` is correct **relative to the schema it
  implements** (`docs/schemas/enemy-config.schema.json`), which in turn mirrors
  `EnemyFactory.Generate`'s actual requirement — `EnemyFactory` needs a `SpriteSheetRecipe` to do
  anything (dress the sprite, build the Animator/flipbook, extract the projectile prop). There is
  no version of "generate a full pipeline enemy" that skips it; changing `EnemyConfigImporter` to
  accept an `AnimatorController` + prefab instead would mean writing a second, parallel enemy
  generator that doesn't go through `EnemyFactory`/`EnemyStats`/`EnemyBrain` at all — a much bigger
  change than it sounds, and it would produce enemies that don't have the AI/attack/knockback
  stack every other enemy in the project has.
- `EnemyImporter.cs` (the web-tool-facing importer) is the newer, narrower tool — a generic
  "any manifest+frames zip" importer that predates/bypasses the RedMagic sheet pipeline entirely.
  It would need to additionally emit a `SpriteSheetRecipe`-shaped asset (or the web Sprites tab
  would need to export in the shape `SheetSlicer` expects, and go through
  `Tools > RedMagic > Pipeline > 2` instead of `EnemyImporter.cs`).

Not implementing either fix per your instruction — flagging for a decision: either (a) extend
`EnemyImporter.cs` to also produce a `SpriteSheetRecipe` pointing at its generated clips/sprites,
or (b) redirect the web Sprites tab's export toward the RedMagic sheet-slicing pipeline instead of
`EnemyImporter.cs`, so `EnemyConfig.art` always has a real target to point at.
