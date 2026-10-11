# Limpieza total del proyecto

**Cuándo**: cuando esté hecho el player nuevo por piezas (sistema procedural). Hasta entonces lo viejo se aparta a
`Assets/Legacy/` (hecho el 2026-10-11, rama `task/legacy-reorg`) en vez de borrarse.

## Estado

- Todo lo que se sabe que no se va a usar está en `Assets/Legacy/` (estructura en `docs/architecture/legacy.md`).
- **`LegacyGuard`** (`Assets/Scripts/Editor/LegacyGuard.cs`) vigila en Play si se usa algo de Legacy: aviso `[Legacy]`
  en consola y una línea en `docs/legacy-usage.log`. Si el archivo no existe o no crece en semanas, Legacy no se usa.
- *Tools ▸ RedMagic ▸ Legacy ▸ Comprobar qué usa Legacy* escribe `docs/legacy-report.md`. Estado al apartar:
  - Código: `WeaponUser` y `ChestLootContainer` → `AbilityUser`; `HubLootContainer` → `AbilityChest`;
    `ProceduralRigTool` → `ProceduralSway` (migración que lo quita de un prefab).
  - Assets: `Player.prefab` (viejo) → `AbilityUser`; `World1-3` y `PlayerScaleConfig` → escenas viejas;
    recetas `.sheet.asset` de enemigos por sprite → sus láminas originales (ahora en `Legacy/Sprites`).

## Plan

1. Leer `docs/legacy-usage.log` y regenerar `docs/legacy-report.md`.
2. Inventario de lo que se queda (escenas, player, enemigos, herramientas) → sus dependencias con `AssetDatabase`
   → lista de lo que nada usa, revisada por el usuario antes de borrar.
3. Fase 2 del player (`docs/player-audit.md` §6) y rampas (`docs/movement-audit.md`).
4. Pipeline de enemigos por sprite: `EnemyImporter`, `CombinedBundleImporter`, packs de `Pipeline/Editor`, pestaña
   EnemiesSprite de la Biblioteca, `Art/Characters` y `Prefabs/Fx/Enemies` de esos enemigos.
5. Ataques de `Resources` que aún usan arte viejo (tarea *revisar-ataques*).
6. Borrar `Assets/Legacy/`, compilar y probar, en rama.
