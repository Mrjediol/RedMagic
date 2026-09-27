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


