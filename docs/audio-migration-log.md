## 2026-09-27 13:40:20 — Previsualización (todo) (en seco)

Cambios: 43 · Problemas: 0

### Cambios
- Assets/Prefabs/Eviroment/Anvil.prefab ▸ Anvil · AnvilInteractable.useSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/BookLectern.prefab ▸ BookLectern · HubLootContainer.openSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/GoldChest.prefab ▸ GoldChest · HubLootContainer.openSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/Mirror.prefab ▸ Mirror · MirrorInteractable.useSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/Wardrobe.prefab ▸ Wardrobe · HubLootContainer.openSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Player.prefab ▸ Player · Health.hurtSfxId = 'SFX_PlayerHurt': → SoundEmitter ▸ OnHit = SFX_PlayerHurt (clip enemydmg1, vol 1, pitch 1, loop False, grupo SFX)
- Assets/Prefabs/Player.prefab ▸ Player · PlayerAttack.attackSfxId = 'SFX_PlayerAttack': → SoundEmitter ▸ OnAttack = SFX_PlayerAttack (clip enemydmg1, vol 1, pitch 0,38, loop False, grupo SFX)
- Assets/Prefabs/Player.prefab ▸ Player · RangedAttack.attackSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Resources/PassiveDropCinematicSettings.asset · PassiveDropCinematicSettings.continueSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Bosque2.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Bosque2.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Fire1 1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Fire1 1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Fire1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Fire1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Ice1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Ice2.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice2.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Ice3.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice3.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Ice4.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice4.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/MainHub.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/MainHub.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Shop.unity ▸ ShopManager · ShopManager.denySfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Shop.unity ▸ ShopManager · ShopManager.rerollSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Worlds/World1/Bosque1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Worlds/World1/Bosque3.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque3.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Worlds/World1/Bosque4.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque4.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Worlds/World1/BosqueBoss.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/BosqueBoss.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/Worlds/World1/testboss.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/testboss.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/_Recovery/0 (1).unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/_Recovery/0 (1).unity ▸ MainMenuUI · MainMenuController.menuMusicId = 'Music_Menu': → menuMusic = 'Sly_2_-_Band_of_Thieves_(USA).iso_00367'
- Assets/_Recovery/0.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/_Recovery/0.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/MainMenu.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/MainMenu.unity ▸ MainMenuUI · MainMenuController.menuMusicId = 'Music_Menu': → menuMusic = 'Sly_2_-_Band_of_Thieves_(USA).iso_00367'


## 2026-09-27 13:40:36 — Assets + Assets/Scenes/MainHub.unity (aplicado)

Cambios: 11 · Problemas: 0

### Cambios
- Assets/Prefabs/Eviroment/Anvil.prefab ▸ Anvil · AnvilInteractable.useSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/BookLectern.prefab ▸ BookLectern · HubLootContainer.openSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/GoldChest.prefab ▸ GoldChest · HubLootContainer.openSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/Mirror.prefab ▸ Mirror · MirrorInteractable.useSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Eviroment/Wardrobe.prefab ▸ Wardrobe · HubLootContainer.openSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Prefabs/Player.prefab ▸ Player · Health.hurtSfxId = 'SFX_PlayerHurt': → SoundEmitter ▸ OnHit = SFX_PlayerHurt (clip enemydmg1, vol 1, pitch 1, loop False, grupo SFX)
- Assets/Prefabs/Player.prefab ▸ Player · PlayerAttack.attackSfxId = 'SFX_PlayerAttack': → SoundEmitter ▸ OnAttack = SFX_PlayerAttack (clip enemydmg1, vol 1, pitch 0,38, loop False, grupo SFX)
- Assets/Prefabs/Player.prefab ▸ Player · RangedAttack.attackSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Resources/PassiveDropCinematicSettings.asset · PassiveDropCinematicSettings.continueSfxId = 'SFX_ButtonClick': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.
- Assets/Scenes/MainHub.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/MainHub.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip — no hay sonido que migrar; se vacía el campo.


## 2026-09-27 13:41:25 — Assets + Assets/Scenes/MainHub.unity (aplicado)

Cambios: 0 · Problemas: 0


## 2026-09-27 13:48:21 — Previsualización (todo) (en seco)

Cambios: 28 (sin clip: 14) · Problemas: 0

### Cambios
- Assets/Scenes/Bosque2.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Fire1 1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Fire1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice2.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice3.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice4.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque3.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque4.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/BosqueBoss.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/testboss.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/MainMenu.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/MainMenu.unity ▸ MainMenuUI · MainMenuController.menuMusicId = 'Music_Menu': → menuMusic = 'Sly_2_-_Band_of_Thieves_(USA).iso_00367'

### Vaciados sin clip — sonidos pendientes
- Assets/Scenes/Bosque2.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Fire1 1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Fire1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice2.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice3.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice4.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Shop.unity ▸ ShopManager · ShopManager.denySfxId = 'SFX_ButtonClick': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Shop.unity ▸ ShopManager · ShopManager.rerollSfxId = 'SFX_ButtonClick': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/Bosque1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/Bosque3.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/Bosque4.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/BosqueBoss.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/testboss.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.


## 2026-09-27 13:48:34 — Todas las escenas (aplicado)

Cambios: 28 (sin clip: 14) · Problemas: 0

### Cambios
- Assets/Scenes/Bosque2.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Fire1 1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Fire1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice2.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice3.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Ice4.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque1.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque3.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/Bosque4.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/BosqueBoss.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/Worlds/World1/testboss.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/MainMenu.unity ▸ AudioManager: copia del AudioManager borrada — tabla de 11 entradas idéntica a MainMenu; su AudioListener se va con ella (lo pone el prefab).
- Assets/Scenes/MainMenu.unity ▸ MainMenuUI · MainMenuController.menuMusicId = 'Music_Menu': → menuMusic = 'Sly_2_-_Band_of_Thieves_(USA).iso_00367'

### Vaciados sin clip — sonidos pendientes
- Assets/Scenes/Bosque2.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Fire1 1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Fire1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice2.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice3.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Ice4.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Shop.unity ▸ ShopManager · ShopManager.denySfxId = 'SFX_ButtonClick': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Shop.unity ▸ ShopManager · ShopManager.rerollSfxId = 'SFX_ButtonClick': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/Bosque1.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/Bosque3.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/Bosque4.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/BosqueBoss.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.
- Assets/Scenes/Worlds/World1/testboss.unity ▸ SectionClearTracker · SectionClearTracker.clearSfxId = 'SFX_Fireball': la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.


## 2026-09-27 13:48:56 — Todas las escenas (aplicado)

Cambios: 0 (sin clip: 0) · Problemas: 0


## Resumen de la migración (fase 4, pasos 1 y 2)

Copias del AudioManager con su tabla borradas: 14 (todas idénticas a MainMenu, que fue la última).
`Assets/_Recovery/` (3 escenas de recuperación, 2 con copia) no se migró: se borró tras comprobar que
su contenido ya existe en MainMenu.unity y en MainHub.unity del commit `e4f0e59`, y se añadió a `.gitignore`.

### Migrado pero hay que sustituir

Copiados tal cual de la tabla antigua: son placeholders, no el sonido final.

- `Assets/Prefabs/Player.prefab` ▸ SoundEmitter ▸ **OnHit** = `enemydmg1` (antes `Health.hurtSfxId = SFX_PlayerHurt`).
  El golpe al jugador usa un clip de daño a enemigo.
- `Assets/Prefabs/Player.prefab` ▸ SoundEmitter ▸ **OnAttack** = `enemydmg1` a **pitch 0.38** (antes
  `PlayerAttack.attackSfxId = SFX_PlayerAttack`). Clip de daño a enemigo y pitch sin sentido.
- `Assets/Scenes/MainMenu.unity` ▸ MainMenuController ▸ **menuMusic** = `Sly_2_…_00367` (antes
  `menuMusicId = Music_Menu`). Placeholder aceptado: un clip corto en bucle como música de menú.

### Sonidos pendientes — campos vaciados porque su id no tenía clip

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

