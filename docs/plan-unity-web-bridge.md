# Plan — the Unity ⇄ web bridge (Step 4 of the gesture / attack editor)

Written 2026-10-02, to be executed in the next session(s). Parent plan:
`docs/plan-gesture-attack-editor.md` (steps 1–3 built, on branch `task/gesture-attack-editor`).

## Status — built (2026-10-02): phases A, B and C

Pending: the user's feedback from playing it (in chat — test steps are given in the reply, never written in docs). What differs from the plan below:

- **Body**: a library record of that name authored in the web is still preferred (it has the original canvases);
  otherwise the body is read from Unity (`readRigDocument`) into a record marked `unityBody`, never exported whole.
- **Art in attacks** comes from Unity always as `{ "project": path, "libraryId"? }` and goes back the same way:
  the library entry is only a preview, the prefab is never rebuilt unless another art is picked in the web.
- **Revision** = hash of gestures, idle gestures, state sprites, deck cards (full), defences. A change elsewhere
  on the prefab (e.g. move speed) is not a conflict: the save does not touch it.
- **Deep links**: one "Abrir la web en…" foldout under the button (a "✎ web" per gesture and per card), not a
  drawer per row.
- Verified live: open (both body sources), save with no edits = prefab and attack assets unchanged, 409 +
  overwrite, the domain reload (same port and token). Not verified by hand: Prefab Mode reload after a save,
  the Inspector buttons themselves, sound playback by ear.
- Fixed after the first user test (2026-10-02): a save that carried new library art mirrored `Art/` and
  deleted the frames of every art the bundle did not bring (invisible projectiles) — now merged, never pruned.
  Idle gestures never played on an enemy that sleeps until it detects — they now play while it sleeps
  (when its body does not fold). Idea noted by the user: *transition* gestures between attacks (not built).
- Second round (2026-10-03, user feedback): the gesture's sound is **editable** in the web (a 🔊 lane: clips from
  `GET /sounds`, volume, pitch, its own instant — `GestureSoundMoment.At`); the timeline gained zoom, frame
  keys, duplicate (Ctrl+D) and split (Ctrl+B); the selection's fields moved to a panel in the left column; a
  gesture no attack uses shows no ghost release; **transition gestures** (`ProceduralRig.transition`) built.
  Bridge bug fixed: after a domain reload the listener hopped to the next port and the open tab got "Unity no
  responde" — it now resumes on the same port.
- Third round (2026-10-03): the bridge starts with the editor and the page opened by hand finds it
  (`GET /hello`, token only to the configured origin); 🎬 lists the project's prefabs (`GET /enemies`) and
  Enemy Studio saves a whole enemy into Unity (`POST /rig`) — no .zip for new enemies either. Decision 2's
  "idle until called" is superseded.
- Known limits: clearing an art slot in the web does not clear it in Unity (an absent key keeps the value);
  a card deleted in the web leaves its asset in the project, out of the deck.

## ▶ How to start (after /clear)

> Lee `docs/plan-unity-web-bridge.md` y ejecuta la Fase A.

Read first: `CLAUDE.md`, this file, `docs/plan-gesture-attack-editor.md ▸ Steps 2–3`,
`Web/RedMagicWeb/ARCHITECTURE.md ▸ Gestures ⇄ attacks`, and the code named in "What already exists".
Stay on `task/gesture-attack-editor` (not merged yet: the user's test pass of steps 1–3 is still open).

## Goal (the user's words, 2026-10-02)

From Unity: an "edit gesture / attack" button on an existing enemy opens the web with that enemy, that
attack and gesture loaded, sound included; saving in the web writes it into Unity — no export / import.

## What already exists (verified 2026-10-02)

| Piece | Where | State |
|---|---|---|
| Web → Unity payload | `rig-export.js ▸ buildGesturesBundle` (a `*.gestos.zip`: partial `rig-config.json` + `enemy-config.json` with the deck + `Art/`) | done — the bridge POSTs this blob instead of downloading it |
| Applying it | `RigBundleImporter.Import(path)` → `ImportPartial` (zip or folder; never touches body / tuning; keeps each gesture's `sound` by name) | done — returns a text summary, never throws |
| Unity → web, body + gestures | `RigConfigExporter.Export(root, prefabPath, warnings)` → `JObject` (sprites as project asset paths) | done |
| Unity → web, deck | `RigConfigExporter.EnemyConfig(root, name)` | **only asset paths** per card (`attack: "Assets/…asset"`) — the attack's own numbers are NOT exported |
| Web reading Unity's gestures | `rig-export.js ▸ readRigGestures(doc, state, ctx)` (matches parts by path) | done — needs a document that already has the parts |
| Web document from a rig-config (body) | — | **missing**: today the body only comes from the web library record |
| The focused tab | `enemy-studio.js ▸ show('focus')`, `rig-composer.js ▸ setFocusMode / linkLibraryEntry` | done — picks an enemy from the library |
| Attack catalog (types + fields, by reflection) | `BossCatalogExporter` → `boss-catalog.js`; import: `BossConfigImporter`, `EnemyDeckImporter` | done one way (web → asset); **asset → card JSON missing** |
| Gesture sounds | `RigGesture.sounds` (list of `RigGestureSound`); not in the file rig-config | Edited in the web, travel through the bridge (`gestures[].sounds`) |
| Inspector hooks | `EnemyStatsEditor` (`[CustomEditor(typeof(EnemyStats))]`), `BossDefinitionEditor` (BossController); `ProceduralRig` has no custom editor | — |
| Web hosting | static server the user starts (`npx serve Web/RedMagicWeb`); the library is IndexedDB = **per origin** | — |
| Unity CLI | already listens on port 7800 | pick another port |

## Decisions

1. **The bridge is an API, not a web host.** Unity does not serve `Web/RedMagicWeb`: the user's library
   (projectile / effect art the attack cards pick from) lives in IndexedDB under the origin they already
   use; a second origin would open an empty library. Unity opens the browser at the user's usual web URL
   (an `EditorPrefs` setting, default `http://localhost:3000`, editable in Biblioteca Jere) with
   `?bridge=<port>&token=<t>&prefab=<asset path>[&gesture=<name>][&attack=<index>]`.
2. **`HttpListener` on `127.0.0.1` only**, default port **7810** (next free one if taken), editor-only
   assembly folder, **idle until called**: it starts the first time the button is pressed and restarts
   itself after a domain reload only if it was running (`SessionState`). CORS: `Access-Control-Allow-Origin`
   = the configured web origin, `OPTIONS` preflight answered (incl. `Access-Control-Allow-Private-Network`),
   and a random per-session token required on every call — so no other page can write into the project.
   **The token is invisible**: Unity generates it and puts it in the URL it opens; the user never sees,
   types or configures anything (user, 2026-10-02: no passwords, no prompts). If the web is opened by hand
   without it, the bridge is simply off — never a "enter your key" field.
3. **Requests are executed on the main thread** (`EditorApplication.update` drains a queue): `AssetDatabase`
   and `PrefabUtility` are not callable from the listener's thread.
4. **Unity is the source of truth for what it owns**: on open, gestures always come from the prefab
   (`readRigGestures` over what the bridge sends), even when the library has the enemy.
5. **Who wins with unsaved Inspector changes** (the open question of the parent plan):
   - *Opening*: if the prefab is open in Prefab Mode with unsaved changes → dialog "Guardar y abrir /
     Cancelar". The web always reads the saved asset.
   - *Saving from the web*: the bridge sent a `revision` (hash of the exported gestures + deck) on open;
     the POST carries it back. Prefab changed since, or Prefab Mode dirty → **409**, nothing written; the
     web shows "Cambió en Unity" with **Recargar desde Unity** / **Sobrescribir** (resends with `force`).
     Never a silent overwrite, in either direction.
   - Play Mode: saving is refused (409 "sal de Play Mode") — a prefab rewrite mid-play is not worth it.
6. **One editor, still**: no new editor UI in the web. The bridge adds a client module, a banner and a
   "💾 Guardar en Unity" button to the focused tab; the download button stays as the offline path.
7. **No player-facing text** is added (editor + web tool strings, Spanish like the rest of the tooling).

## Phases (one per session)

### Phase A — the pipe: open from Unity, save into Unity (enemy already in the web library)

The smallest thing that removes export / import. Works for every enemy authored in the web.

Unity (`Assets/Scripts/Rigging/Editor/Bridge/`):
- `WebBridgeServer` — listener, token, CORS, main-thread queue, lifecycle (decisions 2–3).
- `WebBridgeRoutes`:
  - `GET /ping` → `{ project, unity, bridge: 1 }`.
  - `GET /enemy?prefab=` → `{ name, kind, revision, rigConfig, enemyConfig, warnings }`
    (`RigConfigExporter.Export` + `.EnemyConfig`).
  - `GET /file?path=Assets/…` → raw bytes; only under `Assets/`, only `png / wav / ogg / mp3`.
  - `POST /gestures?prefab=&revision=[&force=1]`, body = the partial zip → temp file →
    `RigBundleImporter.Import` → `{ ok, summary, warnings, revision }` or 409 (decision 5).
- `WebBridgeMenu` — the entry points, all calling one `Open(prefabPath, gesture = null, attack = -1)`:
  a button at the top of `EnemyStatsEditor` (part-based enemies only: needs `ProceduralRig`),
  `CONTEXT/ProceduralRig/Editar gestos y ataques en la web`, and a row button in Biblioteca Jere ▸ Enemies.
  The web URL setting lives in Biblioteca Jere. If the web URL does not answer: a dialog with the
  one-line command to start it.

Web:
- `modules/unity-bridge.js` — reads the query params, `ping`, `fetchEnemy`, `fetchFile`, `saveGestures`;
  no DOM. Absent params = the module is inert and the app behaves exactly as today.
- `app.js` — with `?bridge=`, open the 🎬 tab directly.
- `enemy-studio.js` (focus mode) — find the library record by name → `rig.linkLibraryEntry(id)` →
  overlay Unity's gestures (`readRigGestures`) → select `gesture` / `attack` if given. Banner
  "🔌 Unity · <name>", "💾 Guardar en Unity" (`buildGesturesBundle` → `saveGestures`), the importer's
  summary and warnings shown in the panel, the 409 choices. Not in the library → a clear message (Phase B
  removes this limit).

Docs: `Web/RedMagicWeb/ARCHITECTURE.md` (new section + "where to change" rows),
`docs/architecture/procedural-rig.md` (the bridge as a third way in, next to import / export),
`docs/web-tools-guide.md` (how to use it), `CLAUDE.md` pointer line.

Known limit of A: the attack cards shown are the library's, not Unity's. An attack tuned by hand in the
Inspector is overwritten by the web's version on save — the banner says so until Phase B.

### Phase B — Unity as the source: any enemy, attacks included

- **Body from Unity**: `rig-export.js ▸ readRigDocument(doc, fetchFile)` — the inverse of `buildRigExport`
  for what focus mode needs to *draw and pose* (nodes, sprites, pivots, sorting, roles, rig fields, state
  sprites). The bridge's `/enemy` adds per-sprite metadata (`rect`, `pivot`, `ppu`) and the PNGs come
  through `/file`. The body stays read-only in focus mode, so this reader never has to round-trip a body.
  Removes "must be in the library" (enemies made by `make-enemy.mjs` or by a Unity pack open too).
- **Attacks from Unity**: `BossAttackExporter` (editor) — a `BossAttack` asset → the card JSON the web
  reads, by reflection over the same fields `BossCatalogExporter` publishes (so it cannot drift from the
  catalog). `/enemy` returns the deck with full cards. Art fields that point at a project prefab without
  a `WebLibrarySource` id come back as `{ "project": "<asset path>" }`: shown as "arte de Unity (sin
  vista previa)" and re-imported **as it came** — `EnemyDeckImporter` must accept that form and keep the
  reference.
- Check with `RigConfigRoundTrip`-style test: export deck → import → assets unchanged (add it to the
  existing round-trip menu, do not create a second one).

### Phase C — sound, and the polish the first two leave out

- `/enemy` adds each gesture's sound: `{ gesture, moment, clip: "<asset path>", volume, pitch }`; the test
  stage plays it at the right moment through the web's existing audio path (`vfx-sounds.js` /
  `game-sounds.js` player), clip fetched via `/file`. Read-only in the web: sounds are still assigned and
  graded in Unity (sound registry rules).
- Deep links per card: an "✎ web" button on each deck entry and each gesture row in the Inspector.
- After a save, Unity pings the prefab and, if it is open in Prefab Mode, reloads the stage.
- Bosses: the same button on `BossController` (gestures only — a boss's deck is its `BossDefinition`).

## Risks to check early in Phase A

- Chrome's Private Network Access rules for `http://localhost:<web>` → `http://127.0.0.1:7810`: both are
  loopback so it should pass with the preflight header; verify with one real fetch before building on it.
- `HttpListener` on Windows without admin: `http://127.0.0.1:<port>/` works unelevated; `http://+:…` does not.
- Domain reload kills the listener mid-request: the web client retries a failed call once after 1 s.
- `RigBundleImporter.Import` shows no dialogs on the partial path — confirm, a modal would hang the request.
- Library record ↔ prefab are matched **by name**; a renamed prefab will not be found (Phase B makes this moot).

## What to verify (Claude's notes; the user gets their checklist in the chat)

1. Unity ▸ select IceRobot2 ▸ Inspector ▸ "Editar gestos y ataques en la web": the browser opens on
   🎬 with IceRobot2 loaded and the banner "🔌 Unity".
2. Change a step's duration ▸ "💾 Guardar en Unity": the summary appears in the web; in Unity the prefab's
   gesture has the new value, and its sound is still assigned.
3. Change something on the prefab in the Inspector, save, then save again from the web without reloading:
   the web says "Cambió en Unity" and offers the two choices.
4. Close Unity's bridge (restart the editor) and press save in the web: a clear "Unity no responde", no data lost.
