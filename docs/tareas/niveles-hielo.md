# Niveles de hielo — siguiente sesión

Leer primero: `docs/architecture/levels.md` (sistema en Unity), `docs/biomas-receta.md` (proceso por bioma, qué
funcionó y qué no) y la skill `make-biome` (`.claude/skills/make-biome/SKILL.md`).

## Estado (2026-10-11)

Hecho y validado por el usuario:
- **Fondo de 7 capas** del hielo (Gemini → ComfyUI `finish` + `despill.mjs`): `Assets/Levels/_Biomas/Hielo/Fondo/`.
  Ajustes en `Web/ParallaxTest/parallax-test.html`, importados a Unity con *Importar fondo de parallax (.json)*.
- **Sistema de niveles en Unity** (`Assets/Scripts/Levels/`): `Level` (tamaño en unidades, 1 = 40 px, alto máx. 75),
  terreno pintado en Tilemap (`PaletaTerreno`: tiles Roca y Plataforma), borde de hielo procedural con esquinas
  redondeadas, brillo interior por distancia, collider redondeado, plataformas atravesables (capa Platform),
  parallax por capas, nieve que se posa (`SnowSettings`), bloom/viñeta. Escenas `Assets/Scenes/Test/NivelHielo_{1000,2000,3000}`.
- Orden de dibujo: el nivel en Default con orden negativo (`LevelSorting`), enemigos visibles delante.

Descartado (no repetir): tiras de Gemini dobladas por el contorno; brillo exterior; profundidad del suelo (cara
superior); halo de sombra alrededor de la puerta; montón de nieve al pie de la puerta.

## Pendiente (pasos de la tarea)

1. **Explicar al usuario cómo crear un nivel en Unity con el tile** (primero, en la sesión): crear escena (copiar
   `NivelHielo_1000` o GameObject con `Level` + `Grid/Terreno` + `Grid/Plataformas` + fondo + nieve), poner Ancho/Alto,
   *Abrir paleta de terreno*, pincel/rectángulo/borrar con Roca y Plataforma, la cámara/BG/collider se ajustan solos,
   colocar jugador y enemigos. Si falta algo para que sea cómodo (p. ej. un menú *Crear nivel vacío*), hacerlo.
2. **Herramienta web de creación de niveles**: editor de cuadrícula (pintar roca / plataforma, tamaño en celdas con
   tope 75 de alto, vista previa con el borde del boceto y el fondo de la escena exportada) que exporte
   `*.levelsketch.json` con las plataformas en otra marca (p. ej. `=`), y actualizar `LevelSketchImporter` para leerlas
   al Tilemap de plataformas. Partir de `Web/ParallaxTest/boceto-nivel.html` (ya tiene borde, fondo y niveles) y de la
   pestaña vieja `Web/RedMagicWeb/modules/level-sketch.js`. Decidir con el usuario si vive en la web principal o en
   `Web/ParallaxTest/`.
3. **Objetos de decoración y plataformas con variantes**: pedir a Gemini (prompt en el chat, pieza por petición, magenta,
   márgenes) cristales (con `Light2D` como fuente de luz y luz en los personajes), carámbanos que asomen POR DETRÁS del
   borde (los que gustaron), montones de nieve, rocas, plataformas flotantes y puentes con 2-3 variantes; limpiar con
   ComfyUI `finish` (+ `despill.mjs` si queda rosa), revisar en `comparar/index.html`, hacer prefabs (plataformas con
   collider en capa Platform + efector) y una forma cómoda de colocarlos (paleta de prefabs o tiles de objeto).
4. **Puerta para suelo plano**: el arte de Ice1 se pintó para un suelo con profundidad y no asienta en el borde plano.
   Opciones: pedir una puerta nueva pensada para vista lateral plana (umbral/escalones de piedra al pie) o estilo
   Hollow Knight (escalones + entrar pulsando arriba, cambiando `SectionExit`). Decidir con el usuario.

## Archivos clave

`Assets/Scripts/Levels/{Level,LevelShape,LevelBiome,LevelSorting,ParallaxBackgroundLayer,SnowSettings,FollowCameraView}.cs`,
`Assets/Scripts/Levels/Editor/{LevelSetup,LevelEditor}.cs`, `Assets/Scripts/Run/Editor/LevelSketchImporter.cs`,
`Assets/Art/Levels/Shaders/`, `Assets/Levels/_Biomas/`, `Web/ParallaxTest/`, `Web/RedMagicWeb/tools/comfy/{claude-workflow,despill}.mjs`.
