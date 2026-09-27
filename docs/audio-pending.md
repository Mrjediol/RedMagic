# Sonidos pendientes

Inventario de partida de lo que falta o hay que sustituir en el audio. Independiente de la migración
(histórico en `docs/audio-migration-log.md`). Se amplía con el registro de sonidos de la siguiente fase.

## Migrado pero hay que sustituir

Copiados tal cual de la tabla antigua: son placeholders, no el sonido final.

- `Assets/Prefabs/Player.prefab` ▸ SoundEmitter ▸ **OnHit** = `enemydmg1` (antes `Health.hurtSfxId = SFX_PlayerHurt`).
  El golpe al jugador usa un clip de daño a enemigo.
- `Assets/Prefabs/Player.prefab` ▸ SoundEmitter ▸ **OnAttack** = `enemydmg1` a **pitch 0.38** (antes
  `PlayerAttack.attackSfxId = SFX_PlayerAttack`). Clip de daño a enemigo y pitch sin sentido.
- `Assets/Scenes/MainMenu.unity` ▸ MainMenuController ▸ **menuMusic** = `Sly_2_…_00367` (antes
  `menuMusicId = Music_Menu`). Placeholder aceptado: un clip corto en bucle como música de menú.

## Sonidos pendientes — campos vaciados porque su id no tenía clip

Semilla del inventario de sonidos pendientes. Cada fila es un hueco nuevo que existe y está vacío.

| Hueco nuevo (vacío) | Dónde | Id antiguo |
|---|---|---|
| SoundEmitter ▸ OnInteract | `Prefabs/Eviroment/Anvil.prefab` (AnvilInteractable) | `SFX_ButtonClick` |
| SoundEmitter ▸ OnInteract | `Prefabs/Eviroment/Mirror.prefab` (MirrorInteractable) | `SFX_ButtonClick` |
| SoundEmitter ▸ OnInteract (abrir) | `Prefabs/Eviroment/GoldChest.prefab` (HubLootContainer) | `SFX_ButtonClick` |
| SoundEmitter ▸ OnInteract (abrir) | `Prefabs/Eviroment/BookLectern.prefab` (HubLootContainer) | `SFX_ButtonClick` |
| SoundEmitter ▸ OnInteract (abrir) | `Prefabs/Eviroment/Wardrobe.prefab` (HubLootContainer) | `SFX_ButtonClick` |
| AudioManager ▸ UI Click (al continuar) | `Resources/PassiveDropCinematicSettings.asset` | `SFX_ButtonClick` |
| ShopManager ▸ Deny Sound | `Scenes/Shop.unity` | `SFX_ButtonClick` |
| ShopManager ▸ Reroll Sound | `Scenes/Shop.unity` | `SFX_ButtonClick` |
| SoundEmitter ▸ OnAttack (disparo) | `Prefabs/Player.prefab` (RangedAttack) | `SFX_Fireball` |
| SectionClearTracker ▸ Clear Sound | 13 escenas: MainHub, Bosque2, Fire1, Fire1 1, Ice1-4, World1/Bosque1, Bosque3, Bosque4, BosqueBoss, testboss | `SFX_Fireball` |

