# HUD — prompts de arte (paso 2)

Arte fijo del HUD para Gemini / ChatGPT. Lo que se mueve (relleno de vida, barrido de cooldown,
destellos, números) lo sigue pintando el código **dentro** de estos marcos: por eso los marcos se
piden **huecos**.

## Cómo pedirlo

1. Abre **un solo chat** y adjunta como referencia de estilo, en el primer mensaje:
   - `Assets/Ui/UiSprites/Container.jpeg`
   - `Assets/Ui/UiSprites/CloseButtonWithHover.jpeg`
2. Las referencias son sólo para el **color y el material**, no para el grosor (los prompts lo dicen).
3. Pega los prompts de abajo **uno por mensaje**, en ese mismo chat (así el estilo no se desvía).
   Cada prompt ya lleva el bloque de estilo al final.
4. Guarda cada imagen buena en **`Assets/Ui/HudSprites/`** con el nombre de su sección
   (`HealthBarFrame.png`, `EnemyBarFrame.png`…). Formato PNG o JPEG, el tamaño que salga.
5. Con la primera que pase la revisión basta; repite sólo las que fallen, cambiando el prompt.

### Rechaza la imagen si…
- el fondo es **gris o de cuadros** (transparencia falsa): tiene que ser **blanco liso**;
- el **interior** de un marco hueco no es blanco liso (lleva relleno, cristal, sombra o líquido);
- hay **texto, letras o números**;
- está en **perspectiva** o inclinada;
- los **lados largos** de una barra tienen adornos en el centro (se estiran: tienen que ser lisos);
- el **borde es grueso** o parece un marco de los menús (piedra tallada, nudos, biseles): el HUD lo quiere **fino**;
- se sale del borde de la imagen o queda pegada a él.

---

## Bloque de estilo (ya incluido en cada prompt)

```
Style: use the attached reference images ONLY for the colour palette and material (dark teal-green stone, small cyan rune glow) — NOT for the border thickness or the amount of ornament. This is a sleek, minimal in-game HUD element, far thinner and simpler than the reference menu frames: a fine, delicate rim, almost hairline, with clean straight lines, no chunky stone blocks, no thick bevels, no knotwork along the edges; at most one tiny accent. Hand-painted dark-fantasy look, but restrained and elegant. Front view, flat orthographic, no perspective, no tilt. No text, no letters, no numbers. Isolated on a pure flat white background (#FFFFFF): no checkerboard, no gray, no gradient, no cast shadow on the background. Generous empty white margin around the object. Crisp clean edges.
```

---

## 1. `HealthBarFrame` — marco de la barra de vida

```
A long horizontal health bar frame for a game HUD. Wide landscape image; the bar is centered and spans about 90% of the image width; the bar's own proportions are about 8:1 (width:height). The frame is a FINE, thin rim — its thickness is only about 1/10 of the bar height — with gently rounded ends and one tiny glowing cyan rune dot on the LEFT end. Nothing else. The inside of the frame is completely EMPTY: the whole inner area is pure white (#FFFFFF), exactly like the background — no red fill, no liquid, no glass, no shading inside. The long top and bottom edges are plain and uniform along the middle (they will be stretched); all decoration stays at the two ends.
Style: use the attached reference images ONLY for the colour palette and material (dark teal-green stone, small cyan rune glow) — NOT for the border thickness or the amount of ornament. This is a sleek, minimal in-game HUD element, far thinner and simpler than the reference menu frames: a fine, delicate rim, almost hairline, with clean straight lines, no chunky stone blocks, no thick bevels, no knotwork along the edges; at most one tiny accent. Hand-painted dark-fantasy look, but restrained and elegant. Front view, flat orthographic, no perspective, no tilt. No text, no letters, no numbers. Isolated on a pure flat white background (#FFFFFF): no checkerboard, no gray, no gradient, no cast shadow on the background. Generous empty white margin around the object. Crisp clean edges.
```

## 2. `EnemyBarFrame` — marco de las barras de enemigo

```
A long horizontal health bar frame for ENEMIES in a game HUD, clearly hostile so it never gets confused with the player's bar. Wide landscape image; the bar is centered and spans about 90% of the image width; the bar's own proportions are about 10:1 (width:height). Even simpler than a player bar: a very fine dark rim with a bronze tint (about 1/10 of the bar height), a tiny notch at each end, and a faint ember-orange glow instead of cyan. The inside of the frame is completely EMPTY: the whole inner area is pure white (#FFFFFF), exactly like the background — no fill, no glass, no shading inside. The long top and bottom edges are plain and uniform along the middle (they will be stretched); decoration only at the two ends.
Style: use the attached reference images ONLY for the colour palette and material (dark teal-green stone, small cyan rune glow) — NOT for the border thickness or the amount of ornament. This is a sleek, minimal in-game HUD element, far thinner and simpler than the reference menu frames: a fine, delicate rim, almost hairline, with clean straight lines, no chunky stone blocks, no thick bevels, no knotwork along the edges; at most one tiny accent. Hand-painted dark-fantasy look, but restrained and elegant. Front view, flat orthographic, no perspective, no tilt. No text, no letters, no numbers. Isolated on a pure flat white background (#FFFFFF): no checkerboard, no gray, no gradient, no cast shadow on the background. Generous empty white margin around the object. Crisp clean edges.
```

## 3. `AbilitySlot` — casilla de habilidad, 2 estados

```
Two circular ability slot rings for a game HUD, side by side, identical size, each centered in its own half of a 2:1 landscape image. LEFT: idle state — a thin, clean dark teal stone ring, no knotwork. RIGHT: the exact same ring in the ready state — a thin cyan glow line along the ring and a soft cyan halo around the outside of the ring. The inside of each ring (the hole, about 70% of the diameter) is pure white (#FFFFFF), exactly like the background — empty, no icon, no glass, no shading. Ring thickness only about 8% of the diameter. Both rings must be drawn in exactly the same position and size within their half.
Style: use the attached reference images ONLY for the colour palette and material (dark teal-green stone, small cyan rune glow) — NOT for the border thickness or the amount of ornament. This is a sleek, minimal in-game HUD element, far thinner and simpler than the reference menu frames: a fine, delicate rim, almost hairline, with clean straight lines, no chunky stone blocks, no thick bevels, no knotwork along the edges; at most one tiny accent. Hand-painted dark-fantasy look, but restrained and elegant. Front view, flat orthographic, no perspective, no tilt. No text, no letters, no numbers. Isolated on a pure flat white background (#FFFFFF): no checkerboard, no gray, no gradient, no cast shadow on the background. Generous empty white margin around the object. Crisp clean edges.
```

## 4. `AbilityIcons` — iconos de ataque, dash y parry

```
Three game ability icons in one row, evenly spaced, identical size, each centered in its own third of a 3:1 landscape image. No frame, no circle, no background shape behind them — just the icon. 1) ATTACK: a short glowing blade with a curved slash arc, warm gold and amber. 2) DASH: a burst of three swept speed streaks forming a forward double chevron, cyan energy. 3) PARRY: a small round buckler shield with a bright white-gold spark where a blade strikes it, lime-green glow. Bold, simple, instantly readable silhouettes that still read at 48 pixels; thick shapes, no fine detail.
Style: use the attached reference images ONLY for the colour palette and material (dark teal-green stone, small cyan rune glow) — NOT for the border thickness or the amount of ornament. This is a sleek, minimal in-game HUD element, far thinner and simpler than the reference menu frames: a fine, delicate rim, almost hairline, with clean straight lines, no chunky stone blocks, no thick bevels, no knotwork along the edges; at most one tiny accent. Hand-painted dark-fantasy look, but restrained and elegant. Front view, flat orthographic, no perspective, no tilt. No text, no letters, no numbers. Isolated on a pure flat white background (#FFFFFF): no checkerboard, no gray, no gradient, no cast shadow on the background. Generous empty white margin around the object. Crisp clean edges.
```

## 5. `CurrencyPlaque` — chapa de las monedas

```
A small horizontal stone plaque for a game HUD that will hold one icon and a number. Landscape image; the plaque is centered; its own proportions are about 3:1 (width:height). Rounded rectangle with a fine thin rim and no decoration. The inner surface is a smooth, flat, dark teal stone panel with no decoration in the middle (an icon and a number will be drawn on it). The long top and bottom edges are plain along the middle (they will be stretched).
Style: use the attached reference images ONLY for the colour palette and material (dark teal-green stone, small cyan rune glow) — NOT for the border thickness or the amount of ornament. This is a sleek, minimal in-game HUD element, far thinner and simpler than the reference menu frames: a fine, delicate rim, almost hairline, with clean straight lines, no chunky stone blocks, no thick bevels, no knotwork along the edges; at most one tiny accent. Hand-painted dark-fantasy look, but restrained and elegant. Front view, flat orthographic, no perspective, no tilt. No text, no letters, no numbers. Isolated on a pure flat white background (#FFFFFF): no checkerboard, no gray, no gradient, no cast shadow on the background. Generous empty white margin around the object. Crisp clean edges.
```

## 6. `KeyCap` — tecla para la chuleta de controles

```
Two keyboard keycaps made of carved dark stone for a game HUD, side by side in a landscape image, same height and same style. LEFT: a square keycap. RIGHT: a wide keycap, about three times as wide, for words like "Shift" or "Space". A thin rim, a flat smooth dark top surface with NO letter or symbol on it, a faint cyan rune line along the bottom bevel. The long edges of the wide key are plain along the middle (it will be stretched).
Style: use the attached reference images ONLY for the colour palette and material (dark teal-green stone, small cyan rune glow) — NOT for the border thickness or the amount of ornament. This is a sleek, minimal in-game HUD element, far thinner and simpler than the reference menu frames: a fine, delicate rim, almost hairline, with clean straight lines, no chunky stone blocks, no thick bevels, no knotwork along the edges; at most one tiny accent. Hand-painted dark-fantasy look, but restrained and elegant. Front view, flat orthographic, no perspective, no tilt. No text, no letters, no numbers. Isolated on a pure flat white background (#FFFFFF): no checkerboard, no gray, no gradient, no cast shadow on the background. Generous empty white margin around the object. Crisp clean edges.
```

---

## Qué pasa después (paso 3)

Con las imágenes en `Assets/Ui/HudSprites/`: receta `HudSprites.uikit.asset` + `HudUiPack` →
`Resources/HudSkin.asset` (mismo pipeline que los menús, `UI_ART_PIPELINE.md`), y el HUD se viste
encima de lo vectorial. Sin una pieza, esa pieza sigue con el aspecto actual.
Los colores de acento de las casillas (`HudStyle.Accent`) se ajustan a los iconos que salgan.
