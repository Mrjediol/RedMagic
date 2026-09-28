# Persistent singletons
Three `DontDestroyOnLoad` singletons carry state across scene loads, all following the same
pattern (`Instance` static property, `Destroy(gameObject)` in `Awake` if a duplicate already
exists, manual state reset in `Awake` because Domain Reload is disabled so statics survive between
Play sessions):

- **`GameStateManager`** (`Assets/Scripts/GameStateManager.cs`) — the pause system. Menus call
  `SetPaused(true/false)`, which increments/decrements an open-menu counter so stacking a second
  menu (e.g. Options over Pause) doesn't unpause on close. `CanPlayerAct` is a **static** bool that
  every gameplay script (`PlayerMovement`, `PlayerAttack`, `RangedAttack`, `EnemyController`) polls
  directly, without needing a reference to the singleton. `ForceResume()` zeroes the counter
  unconditionally (used when leaving to gameplay/hub, where no menu should stay "open").
- **`AudioManager`** (`Assets/Audio/AudioManager.cs`) — **one prefab**, `Assets/Resources/AudioManager.prefab`,
  instantiated `BeforeSceneLoad` (never place one in a scene). See **Audio** below.
- **`RunManager`** (`Assets/Scripts/Run/RunManager.cs`) — owns the run loop. See below.
