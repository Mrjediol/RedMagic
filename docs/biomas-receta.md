# Receta de niveles por bioma (reutilizable)

Hielo es el primer bioma; bosque y volcán repiten este proceso. Aquí va solo lo que **funcionó** (y lo que no,
para no repetirlo). Ejemplo resuelto con valores: `docs/niveles-prompts-hielo.md`. Regla: cada buen resultado
nuevo se apunta aquí en el mismo momento.

## Herramientas (`Web/ParallaxTest/`, aparte de la web principal)

| Página | Para qué |
|---|---|
| `parallax-test.html` | Montar las 5 capas de fondo: pegar (Ctrl+V) / arrastrar / subir, velocidad, tamaño, anclaje vertical (proporcional / techo / suelo), espejo, quitar magenta (vista previa), difuminar, atenuar, delante del jugador. Jugador volador + cámara que sigue. **Exportar escena (.json)**. |
| `boceto-nivel.html` | Nivel completo: carga la escena .json, terreno (borde procedural + roca pintada), pared+puerta en versión A (una imagen) o B (separadas) con atenuar/tamaño/posición, nieve que se posa. |

Ambas guardan en el navegador (IndexedDB). Para otro bioma: cambiar textura de roca, colores del borde y
partículas (ver "Qué cambia por bioma").

## Orden de trabajo (validar cada paso con el usuario antes del siguiente)

1. Capas de parallax 1→5 en `parallax-test.html`.
2. Roca de relleno (T1) y pared/puerta.
3. Montaje en `boceto-nivel.html` con el terreno.
4. Objetos sueltos (cristales/plataformas/decoración) pedidos uno a uno con variantes; luces.
5. Pasar a Unity.

## Reglas de los prompts (todas las imágenes)

- Sin imágenes de referencia; describir el estilo con palabras. Una pieza por petición.
- Bloque de estilo fijo del bioma (hielo: "Hand-painted cartoon style with clean dark navy outlines, deep blue
  palette with pale frosty highlights and glowing cyan crystals").
- Fondo **magenta puro #FF00FF** en lo que será transparente; pedir **márgenes de magenta en los laterales**
  (≥ 8-10 %) para poder repetir en espejo. Recorte final con ComfyUI (el de las páginas es solo vista previa).
- Decir "ONE single …, no repeated copies": Gemini tiende a duplicar la escena o hacer mosaicos 2×2.
- Gemini ignora proporciones raras (8:1): elegir 16:9 / 1:1 / 9:16 en su selector y pedir que el objeto ocupe
  una franja; luego se recorta.

## Fondo: 5 capas

| Capa | Contenido | Ocupación | Anclaje vertical | Notas |
|---|---|---|---|---|
| 1 | Fondo lejano completo, sin magenta | 100 % | proporcional | sin contornos, bajo contraste, centro vacío |
| 2 | Formas lejanas aisladas (estalactitas/copas/peñascos) | 45-50 % | proporcional | difuminar + atenuar |
| 3 | Pilares/troncos de fuste recto, 1:1 | 35-40 % | proporcional | sin ensanchar arriba/abajo |
| 4 | Colgantes del techo (más cercanos, algo de brillo) | 25-30 % | techo, vel. vert ≈ 0.9 | tamaño limita cuánto bajan |
| 5 | Siluetas casi negras en el borde inferior | 15-20 % | suelo, vel. vert ≈ 0.9-1 | delante del jugador, vel. horiz > 1 |

Capas lejanas **sin contorno ni brillos** (el terreno sí los lleva) para no confundirse con el mapa.

Plantillas (cambiar lo marcado `{…}`): ver los prompts de hielo en `niveles-prompts-hielo.md` §3 y las versiones
corregidas de la conversación (capa 2 "ONLY isolated shapes…", capa 3 "exactly THREE slim pillars…").

## Terreno

- **Funciona**: cuadrícula sólido/vacío → contorno → polilínea con esquinas redondeadas (radio 16 px con celda
  de 40) → **borde procedural** de grosor variable en 4 tonos + contorno oscuro + brillo; relleno con la
  **textura de roca pintada** (patrón, escala ~0.42 de un recorte 512) oscurecida en profundidad.
- **No funciona**: tiras de Gemini dobladas a lo largo del contorno (cuñas, solapes entre tiras). Detalle en
  `niveles-prompts-hielo.md` §9.
- Decoración (carámbanos, nieve, cristales): **no** generarla del borde; pedir objetos sueltos con variantes y
  colocarlos. Los carámbanos que mejor quedan son los que asoman **por detrás** del borde.

### Prompt de roca (T1), plantilla
> Seamless tileable texture, 1:1, flat front view, for the solid {rock} inside the ground of a 2D side-view video
> game, {bioma} theme. {material description}. Hand-painted cartoon style, {palette}, even soft lighting with NO
> gradient, NO vignette, NO big objects, NO outline around the image. Every edge continues exactly into the
> opposite edge. No characters, no text, no UI, no watermark.

## Pared y puerta (objetos de fondo en los extremos del nivel)

Probar las dos versiones en `boceto-nivel.html`:
- **A, una imagen**: la puerta queda integrada con su sombra, pero atenuar la pared atenúa la puerta.
- **B, separadas**: se atenúan por separado.

Plantillas: ver §10 de `niveles-prompts-hielo.md` (pared: borde exterior recto pegado al lateral de
la imagen, borde interior irregular, 9:16; puerta: arco de piedra frontal, 1:1).

### Quitar el fondo y ampliar (magenta o blanco) — proceso validado

- **Validado por el usuario**: `node tools/comfy/claude-workflow.mjs finish --ref <img> --name <p>` (amplía ×4 y
  recorta con RMBG) y, solo si quedan restos rosas en el borde, `node tools/comfy/despill.mjs <p>_cut.png out.png`
  (por defecto: toca **solo una franja de 10 px junto a lo transparente** y solo píxeles con tinte magenta claro;
  el interior y sus texturas no cambian; una raya magenta sólida se vuelve transparente).
- **Descartado** (lo probé y el usuario lo rechazó): el flujo `clean` (recortar antes de ampliar, con
  `refine_foreground`) dejó artefactos; un despill que recolorea también el interior borró texturas de roca;
  `--erode` en la pared hizo el contorno aún más fino. La pared se quedó con `finish` tal cual (contorno azul, vale).
- Puerta y capas con bordes nítidos: `finish` sale perfecto sin más.
- Revisar en `comparar/index.html` (original vs nueva, zoom a píxel, fondos negro/blanco/azul juego).
- Cargar los PNG con **Subir** o arrastrando: Ctrl+C/Ctrl+V de Windows pierde la transparencia.

### Sacar pared/puerta de un mapa de Gemini con ComfyUI (funcionó con Ice1)

1. Recortar la zona del mapa original a resolución completa (Ice1 mide 9280×1856; recorte de 1040×1856 con
   System.Drawing en PowerShell) y subirlo a Comfy con `curl -F image=@… http://127.0.0.1:8188/upload/image`.
2. `tools/comfy/claude-workflow.mjs run --ref <recorte> --keep-size` con un prompt de edición: "Keep ONLY …
   exactly as drawn: same shapes, colors, cracks, **same thick dark navy outlines**, same saturation … Remove
   everything else … pure flat white background". Pedir pieza por pieza (pared sin puerta, puerta sola) sale
   mejor que las dos juntas (juntas perdió contornos y carámbanos).
3. Para una versión más alta lo que cuenta es la **proporción**, no los píxeles (Qwen no pasa de 2048 por lado):
   pedirla **fina** y reescalar. `run --ref "claude/<salida>_raw_00001_.png [output]" --size 256x2048 --upscale`
   con "the same … as in the reference image, but very tall and narrow so it fills this very tall thin image".
   Mantuvo el estilo; ×4 → 1448×11585, recortada y reducida a la mitad (575×5719) para la web.
4. Usar la salida `_cut` (fondo quitado) recortada a su contenido. Montar como capa con Repetir = "extremos del
   nivel" en `parallax-test.html`.

## Qué cambia por bioma

| | Hielo | Bosque (por definir) | Volcán (por definir) |
|---|---|---|---|
| Borde procedural | blancos/azules, glints | musgo/hierba | roca oscura con brillo de lava |
| Roca de relleno | sillares azul marino | tierra/raíces | basalto |
| Partículas | nieve (una parte se posa) | hojas/esporas | ceniza/brasas |
| Luces | cristales cian | hongos/luciérnagas | lava |
