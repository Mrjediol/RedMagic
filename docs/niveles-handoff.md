# Creación de niveles — traspaso (2026-10-10)

Léelo entero antes de tocar nada de niveles. Es el estado real tras una sesión larga de pruebas.

## Objetivo del usuario

Poder hacerse mapas de CUALQUIER tamaño (300 celdas de ancho, 200 de alto…) sin depender de un
mapa entero generado por Gemini (5:1, sin verticalidad, sin control de plataformas, escala distinta
en cada imagen). **No es artista** y las IAs no dan buenos tilesets. El listón de calidad es un mapa
entero de Gemini (`Assets/Prefabs/Maps/Hielo/Ice1.png`): marco de piedra, paredes de hielo que dan
sentido a la puerta, carámbanos, cristales, plataformas, fondo de cueva con profundidad.

**Lo fácil** (plataformas, decoración, puentes, puertas como objeto aparte): ya hay hojas buenas.
**Lo difícil, sin resolver**: (1) un fondo parallax de verdad, 4-5 capas mínimo, infinito;
(2) un buen terreno y un buen borde de mapa. El usuario quiere ir **por pasos**, validando cada uno.

## Lo que existe (todo commiteado en la rama task/movil)

- **Web** `Web/RedMagicWeb/modules/level-sketch.js`, pestaña "🧱 Boceto de nivel": cuadrícula, pinta
  terreno, 1 celda = 1 unidad Unity, silueta del jugador. Exporta `*.levelsketch.json`
  (`rows` de `#`/`.`, fila 0 = arriba). Bocetos en `Assets/LevelsBoceto/`.
- **Unity** `Assets/Scripts/Run/Editor/`:
  - `LevelSketchImporter` (Tools ▸ RedMagic ▸ Niveles ▸ Importar boceto): boceto → prefab en
    `Assets/Levels/<nombre>/` con `BG` (límites de cámara), `Parallax_i`, `Fill` (malla de rectángulos),
    `Edges` (tiras Tiled giradas por orientación, esquinas convexas alargadas), `Collision`
    (BoxCollider2D → CompositeCollider2D, capa Ground), `SectionEntry`/`SectionExit` automáticos.
  - `LevelKit` + `LevelKitRecipe`: kit de arte (relleno, tiras suelo/techo/pared, capas parallax) sacado
    de recortes de un mapa de Gemini + imagen de fondo. Receta de Ice1 en `Assets/Levels/_Kits/Ice1/`.
  - `SheetSlicer`: corta hojas de piezas sobre magenta en PNG sueltos.
- **Runtime** `Assets/Scripts/Gameplay/ParallaxLayer.cs`: una capa = un sprite Tiled que sigue a la cámara
  a una fracción; ancla = centro del nivel. El fondo se genera en espejo 2×2 para repetirse sin costura.
- `CameraFollow` ya acotaba al objeto `BG` de la escena: no hizo falta cámara nueva. No renombrar la raíz
  del prefab a `BG`.
- Escena de prueba `Assets/Scenes/Test/TestMap.unity` (sin commitear, el usuario la guarda).

## Decisiones tomadas

- 1 celda = 1 unidad: con esto `PlayerScaleConfig` (escala por escena) queda obsoleto; migrar y retirarlo
  del todo cuando los niveles nuevos sustituyan a los viejos (no dejar dos sistemas).
- Arte de Gemini, nunca packs gratis. Plataformas con collider de un solo sentido: capa `Platform` +
  `PlatformEffector2D` (convención del proyecto).
- No Play Mode por CLI; el usuario prueba. Verificación visual aceptada: `unity command capture_game_view
  --save_path Assets/x.png` en modo edición (borrar el archivo después).

## Qué salió mal (no repetir)

1. **Gemini con imagen de referencia = malo**: copia cosas de la imagen. Describir el estilo con palabras.
   El prompt que funcionó es el de los mapas enteros (ver abajo); pedir cada elemento "desde cero" con esa
   misma redacción.
2. **Quitar el magenta con distancia de color en JPEG = halo y borde magenta** en todas las piezas. El
   usuario quiere ComfyUI (ya hay pipeline de recorte de fondo, ver `docs/architecture/sprite-pipeline.md` y
   `Web/RedMagicWeb/modules/background-removal.js` / `despill-edge.js`). `SheetSlicer` debe usarlo o
   cambiar de técnica; hoy sus recortes NO sirven.
3. **Mi prompt metió la puerta dentro de la pared**; el usuario dijo que la puerta es un objeto aparte
   (escala fija, pared estirable al alto del nivel). Releer lo que dice el usuario antes de redactar prompts.
4. **El terreno generado (relleno + tiras recortadas de Ice1) no llega ni de lejos**: se ve como papel
   pintado con borde fino; las paredes recortadas quedaron feas; las diagonales salen en escalera.
5. Una capa de fondo hecha reduciendo el mapa entero deja "fantasmas" de plataformas que parecen jugables.
6. Elegir piezas por número sin verlas (puente flotando, rocas como plataformas). Hace falta paleta visual.
7. Se avanzó demasiado seguido sin validar con el usuario. Ir paso a paso; mostrar captura y esperar.

## Pendiente, en el orden que quiere el usuario

1. Fondo parallax de 4-5 capas (lejana → cercana), infinito, hecho de imágenes aisladas/repetibles.
2. Terreno y borde de mapa de calidad Ice1 (marco de piedra, paredes de hielo con presencia, esquinas
   que cierren, bordes que se fundan con el fondo). Sin resolver: probar enfoques distintos con el usuario.
3. Luego: paleta/colocación de piezas, pared+puerta como objetos, pendientes en vez de escalones.

## Prompt de Gemini que sí funciona (el de los mapas de hielo del usuario)

Estructura: "2D side-view video game level background, ice cave theme… Hand-painted cartoon style with clean
dark outlines, deep blue palette with glowing cyan crystals… BORDERS / DOORS / LAYOUT en porcentajes /
BACKGROUND / IMPORTANT: NO snow, NO particles… No characters, no text, no UI, no watermark." Hojas de piezas
buenas ya generadas: `Assets/Levels/nivel 1/Gemini_Generated_Image_70270…jpeg` (decoración) y
`…8o9r5c…jpeg` (plataformas, puentes); fondo de cueva `…8w9tld…jpeg`.

## Trampas técnicas

- La herramienta Bash falla con heredocs largos que mezclan comillas simples: escribir archivos con la
  herramienta Write y ejecutarlos.
- `unity command eval`: el código va en una línea (los `//` comentan el resto); calificar `UnityEngine.Object`.
- Texturas generadas por código: poner `spriteImportMode = Single` o `LoadAssetAtPath<Sprite>` da null.
- El shader de sprites 2D ignora el color de vértice de una malla: hornear el tinte en la textura.
- `AssetDatabase.CreateAsset` sobre un asset existente funciona y mantiene el GUID; el prefab se
  sobrescribe en sitio con `SaveAsPrefabAsset`.
