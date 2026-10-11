# Niveles de hielo: cómo conseguir el terreno y prompts de arte

Estado: estructura validada con el boceto `parallax-hielo.html` (fuera del repo, en el scratchpad de la sesión).
Aquí: (1) el método para el terreno, (2) los prompts de Gemini, una pieza por petición.

## 1. Método del terreno (la pregunta clave)

Lo que funcionó en el boceto: el terreno es una **cuadrícula sólido/vacío** (la que ya dibuja la web). De ella se
sacan los **contornos de las cavidades** (bucles de aristas) y las esquinas se **redondean con geometría**
(arcos de radio fijo). El arte no resuelve esquinas: sólo aporta texturas rectas.

Por qué lo de antes falló: tiras Tiled por orientación + pieza de esquina por caso = hay que pintar y encajar
cada combinación (convexa, cóncava, diagonal…) y la IA no da piezas que encajen.

Plan en Unity (cuando se pase del boceto):
- **Relleno**: textura seamless que se repite dentro de la malla del sólido (recortada al contorno redondeado).
- **Borde**: el contorno se convierte en una spline cerrada y un **SpriteShape** (paquete 2D) dibuja a lo largo
  una tira recta seamless; en las curvas la dobla sola. Sprites distintos por ángulo: suelo (cara hacia arriba),
  techo (cara hacia abajo, con carámbanos), pared (cara lateral). Sin piezas de esquina.
- **Decoración** suelta por encima (carámbanos, nieve, cristales), colocada al importar según la orientación de
  cada arista, con semilla fija.
- **Marco del mapa** = el mismo sistema (anillo de 1 celda sólido); no es un caso aparte.
- **Collider**: el que ya genera `LevelSketchImporter` (cuadrícula → CompositeCollider2D).
- El importador toma el `*.levelsketch.json` de la web: el usuario dibuja el mapa a mano, sin límite de tamaño.

Fallback si las tiras de Gemini no quedan bien: el renderer del boceto (contorno + bandas) con la textura de
relleno pintada. Ya se ve limpio.

Límites a tener en cuenta: Gemini no entrega seamless perfecto; las tiras y el relleno hay que cerrarlos con
ComfyUI (mezcla de bordes) antes de usarlos.

## 2. Reglas de los prompts (de `niveles-handoff.md`)

- Sin imágenes de referencia: el estilo se describe con palabras. Una pieza por petición.
- Misma redacción que el prompt de mapas que funcionó. Bloque de estilo (pegar en todos):

> Hand-painted cartoon style with clean dark navy outlines, deep blue palette (#0b1d3b to #7cc6ee) with pale
> frosty highlights and glowing cyan crystals. 2D side-view video game asset, flat orthographic front view, no
> perspective. No characters, no text, no UI, no watermark, no snow particles.

- Fondo magenta puro (#FF00FF) en lo que ha de ser transparente; recorte después con ComfyUI, no con distancia
  de color. Fondos completos (capas 1-2) sin magenta.

## 3. Capas de parallax (cada una una imagen aparte, repetible en horizontal)

Todas: "seamless horizontally, the left edge continues exactly into the right edge, wide 21:9".

**P1 Fondo lejano (sin magenta)**
> 2D side-view video game level background layer, ice cave theme, the farthest layer. Deep blue vertical
> gradient, very soft pale light shafts falling from above, faint blurred shapes of distant cave arches, thick
> cold fog toward the bottom. Very low contrast, no outlines, no foreground objects, nothing in the center that
> reads as a platform. [bloque de estilo, sin outlines] Seamless horizontally.

**P2 Cueva lejana (magenta arriba/abajo si hace falta)**
> Same theme, second layer: large rounded cave arches and rock walls in muted mid-blue, softly blurred by
> atmospheric haze, a few small hanging icicles at the very top, darker at the base. Mid-low contrast, simple
> shapes, no glowing crystals. Empty areas pure magenta #FF00FF. Seamless horizontally.

**P3 Pilares y cascada**
> Third layer: three or four tall frozen rock-and-ice pillars running the full image height (stone-brick texture
> on one, smooth ice on another), and one frozen waterfall of pale blue vertical streaks ending in a misty pool.
> Medium contrast, dark navy outlines, slightly desaturated. Space between objects pure magenta #FF00FF.
> Seamless horizontally.

**P4 Cristales y rocas**
> Fourth layer: large clusters of glowing cyan crystals and translucent pale-blue ice crystals growing from
> rounded rock mounds at the bottom, plus large stalactites and a few icicles hanging from the top edge. Strong
> contrast, clear outlines, glowing cyan highlights. Everything else pure magenta #FF00FF. Seamless
> horizontally.

**P5 Primer plano (opcional)**: siluetas casi negras (rocas del suelo y colmillos del techo), sólo azul marino,
sin detalle. La nieve es de partículas, no de arte.

## 4. Terreno (lo importante)

Todas tiras/texturas **rectas**, mismas reglas de bloque de estilo.

**T1 Relleno (seamless 1:1, sin magenta)**
> Seamless tileable texture, 1:1, flat front view. Dark navy-blue rough stone blocks with thin darker joints and
> faint frost speckles, even lighting with no gradient and no vignette, no objects, no outline around the
> image. Every edge continues exactly into the opposite edge.

**T2 Borde de suelo (tira, cara hacia arriba)**
> Horizontal seamless strip, aspect 8:1, flat front view: the top surface of an ice cave floor. Top part: a lip
> of white snow and pale-blue ice with a clean dark navy outline on top and small rounded bumps. Below it the
> ice fades through two bands of light blue into dark navy stone at the bottom edge. Above the strip pure
> magenta #FF00FF. Left and right edges continue exactly into each other. No icicles, no crystals.

**T3 Borde de techo (tira, cara hacia abajo)**
> Same as T2 but the ceiling: dark navy stone on top fading into pale-blue ice, ending in a ragged frosted
> underside with a short fringe of small icicles of equal-ish size hanging down; clean dark outline on the
> underside. Below the strip pure magenta. Seamless horizontally. (Los carámbanos grandes van como decoración.)

**T4 Borde de pared (tira, cara lateral)**
> Same as T2 but a vertical wall face seen from the front: a band of pale-blue ice with vertical frost streaks
> and a clean dark outline on the exposed side, fading into dark navy stone on the other side. Draw it as a
> horizontal strip (the exposed side at the top), pure magenta above. Seamless horizontally.

## 5. Hojas de decoración (magenta, se cortan con `SheetSlicer` + ComfyUI)

Una hoja por tipo, piezas separadas con mucho margen magenta entre sí:
- **D1 Carámbanos**: 12 icicles hanging down, 4 sizes (tiny to very long), pale ice with dark outline.
- **D2 Nieve**: 10 small snow mounds and drifts for the top of ledges, white with pale-blue shadow.
- **D3 Cristales**: 9 glowing cyan crystal clusters, 3 sizes, growing upward from a tiny rock base.
- **D4 Estalagmitas / rocas**: 8 stalagmites and small rounded rocks with frost on top.
- **D5 Plataformas flotantes**: 6 floating ice-and-snow platforms of different lengths, with icicles
  underneath (para huecos entre cuadrículas).
- **D6 Puentes**: 3 rope bridge segments with wooden planks and posts.

## 6. Objetos aparte

**O1 Columna de hielo de la pared (estirable en vertical)**
> A tall frozen ice column, front view, full image height, wide at the top where it meets the ceiling, pale-blue
> faceted ice with vertical crack lines and a clean dark outline on the inner edge; the outer edge flat and
> straight. Seamless vertically in the middle section so it can be stretched. Background pure magenta.

**O2 Puerta**
> A small stone archway door, front view: gray-blue stone bricks around a round arch, very dark navy opening,
> clean dark outline, frost on top of the arch. Background pure magenta.

## 7. Orden sugerido (validar cada paso con el usuario)

1. T1 + T2 → probar relleno y borde recto en una cuadrícula pequeña.
2. T3 + T4 → techo y pared.
3. D1-D4 → decoración.
4. P1-P4 → parallax.
5. O1, O2, D5, D6.

## 8. Reglas del parallax (validadas con `Web/ParallaxTest/parallax-test.html`)

Página de prueba aparte de la web principal: pegar/subir imagen por capa, jugador con vuelo libre y cámara que
sigue (vertical instantánea), altura de nivel configurable. Las imágenes y ajustes quedan en el navegador.

- **Ocupación visible por capa** (el resto, magenta): 1 fondo 100 %, 2 cueva lejana 45-50 %, 3 pilares 35-40 %,
  4 colgantes 25-30 %, 5 primer plano 15-20 %. Más cerca = más hueco.
- **Capas lejanas sin contorno ni brillos**, difuminadas y atenuadas hacia la niebla, para que no se confundan
  con roca del mapa (el terreno sí lleva contorno oscuro y borde claro).
- **Anclaje vertical por capa**: *proporcional* (fondos 1-3: la imagen se reparte por la altura del nivel; sirve
  igual con 1000 que con 3000 px) · *techo* (colgantes, vel. vertical ≈ 0.9: cuelgan del techo real y se
  alcanzan al subir) · *suelo* (primer plano, vel. vertical ≈ 0.9-1.0).
- Pedir a Gemini imágenes con **margen de magenta en ambos laterales** (≥ 8-10 %) para poder repetir en espejo
  sin fusionar formas. Pilares: fuste recto sin ensanchar, formato 1:1.
- Primer plano: 4-6 siluetas casi negras desenfocadas en el borde inferior, delante del jugador, velocidad
  horizontal > 1 (≈ 1.15-1.4) y solo ~20 % de la altura.
- Valores de partida usados: capa 1 vel 0.03; capa 2 0.08-0.12; capa 3 0.12; capa 4 0.31 (techo, vert 0.95,
  tamaño ~80 %); capa 5 0.55 (suelo, vert 0.91, tamaño 65 %).
- Pendiente: regenerar la capa 2 solo con la cortina de estalactitas; recortar magenta con ComfyUI; reproducir
  esto en Unity (`ParallaxLayer`: factor horizontal, modo vertical proporcional/techo/suelo, delante/detrás).

## 9. Experimento: bordes con tiras de Gemini dobladas por el contorno (resultado: NO apto tal cual)

Montaje: contorno de la cuadrícula → polilínea con esquinas redondeadas → una tira por orientación (suelo T2,
techo T3, pared T4) dibujada a rebanadas de ~3 px orientadas por tangente/normal, en espejo para evitar la
costura. Relleno de roca con T1 (patrón). Probado en una cuadrícula de 32×14.

- **Funciona**: continuidad del borde sin piezas de esquina; T1 queda bien como roca; T3 (techo con carámbanos)
  y T2 (suelo con nieve y bloques de hielo) se leen muy bien en tramos rectos.
- **Falla**: (1) al doblar, las rebanadas se abren en abanico y salen cuñas blancas; (2) al cambiar de tira de
  suelo a pared a techo en mitad de la curva, se solapan dos tiras ("alas" bajo las plataformas); (3) con radio
  de esquina grande (≥ 30) se nota más; (4) el relleno de huecos con un trazo ancho deja un halo pálido.
- **El usuario lo juzgó muy peor que el borde procedural del boceto v2** (`hielo-v2.html`, aprobado).
- **Si se reintenta**: no rebanadas; construir una malla de cinta continua (como SpriteShape) con vértices
  desplazados por la normal y coordenada u por longitud de arco, una tira por tramo recto (no por muestra) y
  fundido entre tiras solo en las esquinas. Alternativa más segura: borde procedural + T1 de relleno + T2/T3
  recortadas como sprites de decoración sueltos (nieve, carámbanos) colocados sobre el borde.
- Prompts de T2/T3/T4 usados: ver las secciones 4 y la versión "banda central" pedida en 16:9; las imágenes
  buenas están en `Downloads/Gemini_Generated_Image_{yizgiyizgiyizgiy,bq6g6tbq6g6tbq6g,spp7gvspp7gvspp7}.jpeg`
  y T1 en `…wya2nzwya2nzwya2.jpeg`.

## 10. Pared y puerta (versiones A y B)

**A · pared + puerta en una imagen** (9:16):
> 2D side-view video game level art, ice cave theme. ONE tall frozen ice wall column seen from the front, filling
> the full height of the image, on a flat pure magenta #FF00FF background. The LEFT edge of the column is perfectly
> straight and touches the left border of the image from top to bottom; the RIGHT edge is irregular faceted ice,
> a little wider at the top. A fringe of icicles hangs from the top of the column. At the bottom of the column, a
> small stone archway door built into the ice: gray-blue stone bricks, round arch with a keystone, very dark
> navy opening, frost on top, casting a soft shadow on the ice around it. Pale-blue faceted ice with vertical
> cracks, clean dark navy outlines, hand-painted cartoon style, deep blue palette with pale frosty highlights.
> The column takes about 40 percent of the image width; everything else is pure magenta. ONE single column, no
> repeated copies. Portrait 9:16. No characters, no text, no UI, no watermark.

**B · pared sola**: el mismo prompt cambiando la frase de la puerta por "The base of the column is plain ice with
a little snow, NO door, NO opening."

**B · puerta sola** (1:1):
> 2D side-view video game level art, ice cave theme. ONE small stone archway door seen from the front, centered on
> a flat pure magenta #FF00FF background, taking about 55 percent of the image height: gray-blue stone bricks
> around a round arch with a keystone, very dark navy opening with a faint cold glow at the bottom, frost and a
> little snow on top of the arch, a flat stone threshold step at the bottom, clean dark navy outlines,
> hand-painted cartoon style, deep blue palette. NO wall around it, NO ground, NO background. ONE single door.
> Square 1:1. No characters, no text, no UI, no watermark.
