# Auditoría de texturas

Generado por *Tools ▸ RedMagic ▸ Optimización ▸ Auditar texturas* el 2026-10-19 14:28, destino **Android**. Memoria = formato importado real × mips (lo que ocupa en GPU). «Legacy» = `Assets/Legacy`, `Assets/_Reference`, `Assets/Test`, packs vendorizados.

## Resumen

| | Texturas | Memoria | No múltiplo de 4 | Sin comprimir | PPU > 128 |
|---|---:|---:|---:|---:|---:|
| Todo | 2248 | 1945,0 MB | 1213 | 1220 (1501,7 MB) | 126 |
| Sin legacy/test/packs | 2120 | 1733,8 MB | 1145 | 1146 (1377,4 MB) | 122 |

Formatos (sin legacy): `RGBA32` 1138 · 1332,2 MB, `ETC2_RGBA8` 932 · 311,9 MB, `RGB24` 8 · 45,3 MB, `ETC2_RGB` 42 · 44,5 MB

## Por escena de la build (texturas que arrastra al cargarse)

| Escena | Texturas | Memoria | Sin comprimir | Las 3 más pesadas |
|---|---:|---:|---:|---|
| MainMenu | 1 | 516 KB | 0 | MainMenu.jpeg 516 KB |
| MainHub | 46 | 105,4 MB | 22 | MainHub.png 12,0 MB; frame_004.png 4,0 MB; frame_002.png 4,0 MB |
| Shop | 3 | 17,7 MB | 1 | Shop1.png 12,7 MB; Forest-3.png 4,0 MB; Reroll.png 1,0 MB |
| LargeLevelTest | 460 | 271,7 MB | 95 | capa5.png 33,6 MB; capa3.png 21,3 MB; Torso__stage1.png 7,0 MB |
| NivelHielo_1000 | 124 | 97,8 MB | 18 | capa6.png 33,6 MB; capa3.png 21,3 MB; capa2.png 5,3 MB |

## Por carpeta (sin legacy, las 25 que más ocupan)

| Carpeta | Texturas | Memoria | No múltiplo de 4 | Sin comprimir |
|---|---:|---:|---:|---:|
| `Assets/Art/EnemyImports/BossIce` | 50 | 279,4 MB | 48 | 48 |
| `Assets/Levels/_Biomas/Hielo` | 53 | 172,5 MB | 34 | 34 |
| `Assets/Art/Characters/Player` | 103 | 121,9 MB | 95 | 72 |
| `Assets/Art/Bosses/PruebaGolem` | 45 | 87,1 MB | 24 | 24 |
| `Assets/Art/EnemyImports/IceGolem` | 30 | 74,9 MB | 29 | 29 |
| `Assets/Art/EnemyImports/IceSpirit` | 30 | 74,9 MB | 29 | 29 |
| `Assets/Art/EnemyImports/IceWolf` | 30 | 74,9 MB | 29 | 29 |
| `Assets/Art/EnemyImports/Newworl` | 30 | 74,9 MB | 29 | 29 |
| `Assets/Art/BossEffects/PowerPunch` | 5 | 33,3 MB | 5 | 5 |
| `Assets/Art/WebLibrary/projectile` | 142 | 33,2 MB | 70 | 70 |
| `Assets/Ui/HudSprites/HD` | 6 | 30,6 MB | 1 | 1 |
| `Assets/Art/UI` | 2 | 27,9 MB | 0 | 1 |
| `Assets/Art/UI/ItemsUi` | 14 | 27,8 MB | 14 | 14 |
| `Assets/Art/EnemyBody/Boss2` | 9 | 27,0 MB | 0 | 0 |
| `Assets/Art/UI/Menus` | 13 | 26,8 MB | 13 | 13 |
| `Assets/Art/BossEffects/Boulder` | 20 | 25,0 MB | 0 | 0 |
| `Assets/Art/BossEffects/FistImpact` | 4 | 25,0 MB | 0 | 0 |
| `Assets/Art/BossEffects/Warning` | 10 | 25,0 MB | 0 | 0 |
| `Assets/Art/RigImports/ShroomShaman` | 73 | 23,1 MB | 8 | 8 |
| `Assets/Art/EnemyBody/jungle` | 5 | 20,0 MB | 0 | 0 |
| `Assets/Art/Map` | 19 | 19,6 MB | 17 | 17 |
| `Assets/Art/RigImports/ThornFairy` | 66 | 18,0 MB | 8 | 8 |
| `Assets/Art/RigImports/BossCenter` | 8 | 17,9 MB | 8 | 8 |
| `Assets/Art/RigImports/BossCenter2` | 8 | 17,9 MB | 8 | 8 |
| `Assets/Art/Shop` | 2 | 16,7 MB | 0 | 1 |

## Las 40 más pesadas (sin legacy)

| Textura | Origen | Importada | Formato | Memoria | ×4 | PPU | Mips |
|---|---:|---:|---|---:|:-:|---:|:-:|
| `Levels/_Biomas/Hielo/Fondo/capa6.png` | 1124×11419 | 806×8192 | RGBA32 | 33,6 MB | ✗ |  | sí |
| `Levels/_Biomas/Hielo/Fondos/HieloSinCapaDelante/capa5.png` | 1124×11419 | 806×8192 | RGBA32 | 33,6 MB | ✗ |  | sí |
| `Art/UI/necrotic_bone_shrine_03.png` | 5376×3072 | 4096×2341 | RGB24 | 27,4 MB | ✓ |  |  |
| `Levels/_Biomas/Hielo/Fondo/capa3.png` | 3146×1333 | 3146×1333 | RGBA32 | 21,3 MB | ✗ |  | sí |
| `Levels/_Biomas/Hielo/Fondos/HieloSinCapaDelante/capa3.png` | 3146×1333 | 3146×1333 | RGBA32 | 21,3 MB | ✗ |  | sí |
| `Art/Shop/Shop1.png` | 7920×3360 | 7920×3360 | ETC2_RGB | 12,7 MB | ✓ | 100 |  |
| `Art/Hub/MainHub.png` | 10240×3840 | 8192×3072 | ETC2_RGB | 12,0 MB | ✓ | 100 |  |
| `Ui/HudSprites/HD/KeyCap_Wide.png` | 1665×2519 | 1354×2048 | RGBA32 | 10,6 MB | ✗ | 100 |  |
| `Art/RigImports/BossCenter/Parts/Torso__stage1.png` | 1466×1256 | 1466×1256 | RGBA32 | 7,0 MB | ✗ | 100 |  |
| `Art/RigImports/BossCenter2/Parts/Torso__stage1.png` | 1466×1256 | 1466×1256 | RGBA32 | 7,0 MB | ✗ | 100 |  |
| `Art/RigImports/BossCenter/Parts/Torso.png` | 1465×1254 | 1465×1254 | RGBA32 | 7,0 MB | ✗ | 100 |  |
| `Art/RigImports/BossCenter2/Parts/Torso.png` | 1465×1254 | 1465×1254 | RGBA32 | 7,0 MB | ✗ | 100 |  |
| `Art/RigImports/ThornFairy/Parts/Accessory.png` | 1581×1128 | 1581×1128 | RGBA32 | 6,8 MB | ✗ | 100 |  |
| `Art/BossEffects/PowerPunch/Row_1/frame_000.png` | 1024×1706 | 1024×1706 | RGBA32 | 6,7 MB | ✗ | 100 |  |
| `Art/BossEffects/PowerPunch/Row_1/frame_001.png` | 1024×1706 | 1024×1706 | RGBA32 | 6,7 MB | ✗ | 100 |  |
| `Art/BossEffects/PowerPunch/Row_1/frame_002.png` | 1024×1706 | 1024×1706 | RGBA32 | 6,7 MB | ✗ | 100 |  |
| `Art/BossEffects/PowerPunch/Row_1/frame_003.png` | 1024×1706 | 1024×1706 | RGBA32 | 6,7 MB | ✗ | 100 |  |
| `Art/BossEffects/PowerPunch/Row_1/frame_004.png` | 1024×1706 | 1024×1706 | RGBA32 | 6,7 MB | ✗ | 100 |  |
| `Art/BossEffects/FistImpact/Awake/frame_000.png` | 2560×2560 | 2560×2560 | ETC2_RGBA8 | 6,3 MB | ✓ | 100 |  |
| `Art/BossEffects/FistImpact/Awake/frame_001.png` | 2560×2560 | 2560×2560 | ETC2_RGBA8 | 6,3 MB | ✓ | 100 |  |
| `Art/BossEffects/FistImpact/Awake/frame_002.png` | 2560×2560 | 2560×2560 | ETC2_RGBA8 | 6,3 MB | ✓ | 100 |  |
| `Art/BossEffects/FistImpact/Awake/frame_003.png` | 2560×2560 | 2560×2560 | ETC2_RGBA8 | 6,3 MB | ✓ | 100 |  |
| `Art/Map/Border.png` | 1774×887 | 1774×887 | RGBA32 | 6,0 MB | ✗ | 100 |  |
| `Art/Icons/Grimorio.png` | 1254×1254 | 1254×1254 | RGBA32 | 6,0 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Dash/frame_000.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Dash/frame_001.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Dash/frame_003.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Dash/frame_004.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Death/frame_000.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Death/frame_001.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Death/frame_003.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/Death/frame_004.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/FistBarrage/frame_000.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/FistBarrage/frame_001.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/FistBarrage/frame_003.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/FistBarrage/frame_004.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/GroundSlam/frame_000.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/GroundSlam/frame_001.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/GroundSlam/frame_003.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |
| `Art/EnemyImports/BossIce/GroundSlam/frame_004.png` | 1229×1229 | 1229×1229 | RGBA32 | 5,8 MB | ✗ | 100 |  |

## Qué ganaría cada medida (estimación sobre lo no-legacy)

- Rellenar a múltiplo de 4 las que no se comprimen por eso: 1317,5 MB → ~329,4 MB (RGBA32 4 B/px → ETC2 RGBA 1 B/px).
- Todo lo que hoy no se comprime (por cualquier motivo): 1377,4 MB → ~344,4 MB.
- Quitar mips de los sprites que los tienen: ~1,0 MB.
- Sprites con PPU > 128 (más resolución de la que se ve): 122 texturas, 43,2 MB; bajar al presupuesto reduce ~(PPU/128)² cada una.
