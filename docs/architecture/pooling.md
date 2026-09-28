# Object pooling — spawn-heavy objects are never Instantiate/Destroy'd
Anything that spawns repeatedly goes through a pool (`Assets/Scripts/Core/Pool.cs`,
`PrefabPool.cs`), built on `UnityEngine.Pool.ObjectPool<T>`:

- **`Pool<T>`** — for MonoBehaviours that build their own GameObject in code. `new Pool<T>(factory,
  prewarm)`, then `Get()` (creates one more only when empty) / `Release()` (deactivates, keeps for
  reuse). Optional `IPooled.OnReturnedToPool()` for reset. Used by `ShotProjectile`, `ShotBeam`,
  `DamagePopup`, `AbilityVfx` (the `AbilityFx.Flash` impact/muzzle flash), and the code-built
  `Projectile` path in `ProjectileFactory`.
- **`PrefabPool`** — one pool per prefab, keyed by the prefab asset. `Spawn(prefab, pos, rot)` /
  `Despawn(go)` (via the `PooledInstance` marker it stamps on). Used by `VfxOneShot`, `FxTelegraph`,
  and the prefab path of `Projectile` / `ShotProjectile` / `ShotBeam` / the boss runtime visuals
  (`BossShockwave`, `BossSweepBeam`, `BossHazard`, `BossPlatform`, `BossAnchor`) — those all fall
  back to `Pool<T>` when no placeholder prefab is assigned. `PrefabPool` has no per-instance reset
  hook, so those types reset transient state in `OnEnable`.
- **`PoolRunner`** — persistent `DontDestroyOnLoad` root that every pooled instance is parented
  under (so a scene unload never destroys the reserve) and that force-releases everything still
  active on each `sceneLoaded`, so nothing bleeds between sections.
- Domain Reload is off, so every `static` pool field is nulled in a
  `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, and `PoolRunner` clears its releaser list
  once at runtime start.
- Low-churn spawns (one `Corpse` per death, boss reward, shop, `AbilityFx.SpawnSprite` for a
  seconds-long zone/turret/orb) are still plain `new GameObject`/`Instantiate` — pool them if they
  ever become hot.
