# Legacy (`Assets/Legacy/`, `Assets/Resources/Legacy/`) — apartado, no se construye encima

Lo que ya no se usa se **mueve** aquí en vez de borrarse (los `.meta` viajan con los archivos: GUIDs intactos, todo
sigue compilando). Se borrará entero en la limpieza total (`docs/tareas/limpieza-total.md`). Nunca se añade nada nuevo
que dependa de Legacy.

| Carpeta | Qué hay |
|---|---|
| `Legacy/Scripts/` | Código viejo (antes `Assets/Scripts/Legacy/`): sistema de **habilidades** (`AbilityUser`, `AbilityDefinition`, `AbilityLibrary`, menú K), **niveles de arma 1-3** (`AbilityLevelManager`, `WeaponUpgradeAltar`), niveles por kits (`Levels/`), `ProceduralSway`, interactuables viejos. `Editor/`: migraciones de una vez y el Map Tracer (`CollisionImporter`, `MapTracer.html`) |
| `Legacy/Scenes/` | Todas las escenas viejas (Bosque, Fire, Ice2-4, `Worlds/`, `Test/` — incluidas `NivelHielo_2000/3000` y `TestMap`) |
| `Legacy/OldEnemies/` | Prefabs de enemigos **por sprite** y sus jefes (`Enemy_*`, `Boss_*`) |
| `Legacy/Prefabs/` | `Maps/` (mapas viejos), `Projectiles/` (proyectiles de los enemigos por sprite), `Fx/` sin uso |
| `Legacy/Art/Characters/` | Arte de enemigos por sprite que ya no usa nada |
| `Legacy/Sprites/` | La antigua `Assets/Sprites` (láminas originales, referencias) |
| `Legacy/Levels/` | El intento de niveles por kits (`nivel 1`, `_Kits`, `Test1`) — sustituido por `Level` + Tilemap |
| `Legacy/Run/` + `Legacy/Scripts/Run/` | `PlayerScaleConfig` (escala del jugador por escena; ya no hace falta) |
| `Legacy/Json/`, `Legacy/LevelsBoceto/` | Pruebas |
| `Legacy/AdaptivePerformance/` | Ajustes huérfanos del paquete Adaptive Performance (no está instalado) |
| `Resources/Legacy/Abilities/` | Los `AbilityDefinition` (por ruta de Resources, se quedan ahí) |

Nada de Legacy se **autocrea** (`RuntimeInitializeOnLoadMethod` quitado de `AbilityLevelManager`, `AbilityMenuController`, `WeaponUpgradeMenuController` y los HUD viejos): si no lo pone una escena o un prefab, no corre.

**Sigue vivo a propósito** (lo usan armas y jefes): `Scripts/Abilities/` — `AbilityContext`, `AbilityHit`, `AbilityFx`,
`ProjectileSpec`/`ProjectileFactory`.

## LegacyGuard — saber si algo de Legacy se usa

`Assets/Scripts/Editor/LegacyGuard.cs` (sólo editor):
- **En Play**, automático: al cargar una escena revisa sus dependencias y cada 3 s lo vivo (scripts, sprites,
  materiales, audio, animators — también lo instanciado por código o por pool). Cada uso nuevo: aviso `[Legacy]` en
  consola y una línea (una vez por asset y escena) en **`docs/legacy-usage.log`**. Si no aparece en semanas, Legacy no
  se usa.
- **Tools ▸ RedMagic ▸ Legacy ▸ Comprobar qué usa Legacy**: todos los assets de fuera y el código de fuera (tipos
  principales declarados sólo en Legacy, sin contar comentarios) → `docs/legacy-report.md`.
- `LegacyGuard.IsLegacyPath(path)` es el filtro común: lo usan la **Biblioteca Jere** (todas las pestañas), el registro
  de **sonidos** (`SoundSlotScanner.IsIgnored`) y el puente web (`WebBridgeRoutes.SkipFolders`), así que lo de Legacy
  no sale en ninguna lista.
- Exentos (referencia a propósito): `Assets/Scenes/Ice1.unity`. `Assets/_Recovery/` (copia de un crash de Unity, fuera
  de git) no cuenta.

## Organización de carpetas (2026-10-11)

- `Assets/Prefabs/Enemies/`: los enemigos **por piezas** (antes en `Procedural/`). Se pueden repartir en subcarpetas
  por zona: los importadores los encuentran por nombre (`ProceduralEnemyPacks.PathFor`).
- `Assets/Prefabs/Enemies/_Comportamientos/`: piezas sueltas que se añaden a un enemigo (escudo, nota de `HeatGlow`).
  El `_` la deja fuera de las listas de enemigos.
- `Assets/Scripts/Editor/`: las herramientas que vivían en `Assets/Editor/` (Biblioteca Jere, `PlayModePerfMonitor`…).
- `Assets/Test/`: lo nuevo o experimental va aquí primero (las herramientas que generan escenas de previsualización
  escriben en `Assets/Test/Scenes/`).
- DOTween **desinstalado** (plugin, ajustes, defines y los dos scripts que lo usaban).
