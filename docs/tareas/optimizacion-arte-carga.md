# Optimización del arte y de la carga de niveles

Pendiente desde 2026-10-17. Origen: salir del hub a LargeLevelTest tarda ~1,5 s. Medido con el registro de `RunManager`
(`[RunManager] Transición: …`): **cargar la escena 1.153 ms** de 1.520 ms totales. El terreno ya está horneado y la prioridad de
carga ya es alta; lo que queda es lo que la escena arrastra.

## Lo que se midió (editor, 2026-10-17)

| | NivelHielo_1000 | LargeLevelTest |
|---|---|---|
| Texturas que carga la escena | 125 (~107 MB) | 472 (~461 MB) |
| Prefabs | 17 | 67 (20 de enemigos en el WaveManager, incluido IceBoss2) |

- Todo el proyecto (sin Legacy/Test): **2.058 texturas, 1.092 con ancho o alto que no es múltiplo de 4**.
- Con el destino actual (Android, ETC2) una textura que no es múltiplo de 4 **no se comprime**: se queda en RGBA32 (4 bytes
  por píxel). Ej.: `capa6.png` 806×8192 → ~26 MB en vez de ~3-6 MB; partes de enemigos como `Torso.png` 1022×1052.
- En PC (DXT/BC) pasa lo mismo. ASTC (Android) admite cualquier tamaño: **comprobarlo en el paso 1** antes de contar con ello.
- Se cargan **dos fondos**: el del nivel (`Fondo_HieloSinCapaDelante`) y el del bioma (`Fondo_Hielo`, referenciado desde
  `Hielo.asset` ▸ `background`), que el nivel no usa.
- Los enemigos de **todas** las oleadas se cargan con la escena (el WaveManager guarda referencias directas a sus prefabs),
  aunque la mayoría no salga hasta mucho después.

## Reglas para el arte (a partir de ahora, también para lo nuevo)

1. **Ancho y alto múltiplos de 4** (mejor potencias de 2 en lo que vaya a un atlas). Si no, no hay compresión.
2. **Presupuesto de píxeles**: la cámara ve 20 unidades de alto y las pantallas objetivo tienen 1080 px de alto → ~54 px por
   unidad en pantalla. El arte no necesita más de **~2× eso (≈ 100-128 px por unidad)**. Un enemigo de 2 unidades de alto =
   ~200-256 px de alto en total, no 1000+ por pieza. Capas de fondo: alto en píxeles ≈ lo que ocupan en pantalla × 1080 × 1,25.
3. **Sprites en atlas** (`SpriteAtlas`, uno por enemigo / jefe / jugador / conjunto de props): el atlas tiene tamaño potencia de 2
   y se comprime aunque cada pieza tenga un tamaño cualquiera, y además baja las llamadas de dibujo.
4. **Formatos**: Android ASTC (6×6 para arte grande, 4×4 para UI y detalle), PC BC7/DXT5. Sin mipmaps en 2D salvo que se vea
   ruido al alejar.
5. **Los importadores lo aplican solos** (ComfyUI `finish`, `ParallaxImporter`, importador de enemigos, exportadores de la web):
   escalar al presupuesto y rellenar hasta múltiplo de 4 al importar, para que nadie tenga que acordarse.

## Plan (por pasos, validando cada uno)

1. **Auditoría y prueba aislada** — menú de editor que liste todas las texturas: tamaño, formato por plataforma, memoria, si
   no es múltiplo de 4 y si se pasa del presupuesto. Probar en **una capa de fondo y un enemigo**: redimensionar/rellenar +
   comprimir (y ASTC sin redimensionar), comparar antes/después en imagen y en memoria. **Adoptar sólo con el visto bueno del
   usuario** (riesgo visual: artefactos de compresión en arte pintado).
2. **Fondos**: aplicar lo aprobado a las capas de `Assets/Levels/_Biomas/*/Fondo*`; `ParallaxImporter` y la pestaña 🏞️ Fondos
   recortan a múltiplos de 4 y al presupuesto al exportar.
3. **Enemigos, jefes y jugador**: un `SpriteAtlas` por personaje (piezas del rig y hojas); el pipeline de sprites
   (`docs/architecture/sprite-pipeline.md`, skill `make-enemy`) fija el presupuesto y el atlas al importar.
4. **UI**: atlas de UI y tamaños al presupuesto.
5. **Pipelines al día**: `docs/biomas-receta.md` / skill `make-biome`, `make-enemy`, `fx-placeholders`, exportadores de la web,
   para que el arte nuevo nazca con las reglas.
6. **Fondo del bioma fuera de la carga**: `LevelBiome.background` (y `particles`) sólo los usa el editor (crear escenas, datos de
   la web). Pasarlos a una referencia que no se cargue en el juego (un asset compañero sólo de editor, o guardar la ruta/GUID),
   para que cada nivel deje de cargar el fondo por defecto del bioma.
7. **Enemigos bajo demanda**: que el WaveManager no tenga referencias directas a los prefabs. Opción recomendada: paquete
   **Addressables** (`AssetReferenceGameObject` en cada `EnemySpawn`), precargando en segundo plano los de una oleada antes de
   que empiece (al cargar el nivel los de las primeras; al acercarse a una sala, los suyos) y soltándolos al salir. Con el
   pooling de enemigos que ya pide la regla del proyecto.
8. **Medir** tras cada paso con el registro de la transición (línea base: cargar LargeLevelTest 1.153 ms) y en una build del
   móvil.

## Decisiones del usuario

- 2026-10-17: queda como pendiente por ser un cambio grande. Pidió el plan de reestructuración del arte (píxeles óptimos),
  optimizar la carga de enemigos y quitar la carga del fondo del bioma.
- Lo del terreno ya está hecho (horneado al guardar, `docs/architecture/levels.md`) y la carga con prioridad alta.

## Hecho

- 2026-10-19 · **Paso 6**: `LevelBiome.background` / `particles` pasan a GUID (`[EditorOnlyAsset]`, propiedades sólo en editor
  con los mismos nombres). Los niveles ya no arrastran `Fondo_Hielo` ni la nieve del bioma. Falta medir la transición (paso 8).
