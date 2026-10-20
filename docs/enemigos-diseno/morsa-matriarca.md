# Morsa matriarca

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Morsa matriarca)

## Diseño
<!-- gen:diseno -->
**Nivel:** élite · **Mundo:** hielo · **Rol:** Élite de la Morsa: muros dobles y tobogán · **Cuerpo:** de suelo, hitbox 116×76 px (≈ 2.9×1.9 u) · código `e-*.js` id `walrusAlpha`

**Pregunta al jugador:** La Morsa, pero te encierra: los muros salen a los dos lados y cuando estás entre pinchos se tira en tobogán, rebota en la pared y vuelve. Hay que saltarla dos veces sin caer en los pinchos.

**Movimientos**

- **Muro doble** — El muro de carámbanos de la Morsa, hacia ti y a la vez otro más corto hacia atrás.
- **Tobogán** — Se tumba (flechas del alto de su cuerpo) y se lanza deslizándose sobre la tripa; rebota en la pared y vuelve (rebotes ajustables). Al acabar se queda boca arriba jadeando: recibe más daño.
- **Témpano** — Como la Morsa: si estás lejos o subido, te lanza un bloque de hielo.

**Combina con:** Con un Colgante encima: mientras saltas el tobogán, mira arriba.

**Arte (idea, sin decidir):** La Morsa más grande y vieja, con colmillos rotos y vueltos a crecer en hielo, cicatrices y una costra de escarcha gruesa en el lomo; que se lea como la misma especie.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_walrusAlpha`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 650 | 5 – 1500 (paso 5) | Vida |
| `poise` | 160 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.1 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 5 | 0 – 40 (paso 1) | Daño por contacto |
| `keep` | 280 | 0 – 600 (paso 10) | Se queda a esta distancia |
| `walk` | 60 | 0 – 200 (paso 5) | Velocidad |
| `spikeWindup` | 0.75 | 0.2 – 2 (paso 0.05) | Muro: preparación (s) |
| `spikeN` | 6 | 1 – 12 (paso 1) | Muro: carámbanos |
| `spikeGap` | 70 | 20 – 200 (paso 5) | Muro: separación |
| `spikeStep` | 0.12 | 0.02 – 0.5 (paso 0.01) | Muro: tiempo entre carámbanos (s) |
| `spikeWarn` | 0.3 | 0.05 – 1 (paso 0.05) | Muro: grieta antes de salir (s) |
| `spikeH` | 64 | 20 – 160 (paso 2) | Muro: altura |
| `spikeLife` | 1.8 | 0.2 – 6 (paso 0.1) | Muro: se quedan (s) |
| `spikeDmg` | 14 | 0 – 60 (paso 1) | Muro: daño |
| `tossRange` | 380 | 0 – 1000 (paso 10) | Témpano: si estás a más de (o en una plataforma) |
| `tossWindup` | 0.65 | 0.1 – 2 (paso 0.05) | Témpano: preparación (s) |
| `tossFlight` | 1 | 0.3 – 2.5 (paso 0.05) | Témpano: vuelo (s) |
| `tossDmg` | 14 | 0 – 60 (paso 1) | Témpano: daño |
| `tossR` | 40 | 10 – 120 (paso 1) | Témpano: radio |
| `cooldown` | 1.6 | 0.2 – 6 (paso 0.1) | Pausa entre ataques (s) |
| `backN` | 3 | 0 – 10 (paso 1) | Muro doble: carámbanos hacia atrás |
| `slideChance` | 0.45 | 0 – 1 (paso 0.05) | Probabilidad de tobogán |
| `slideRange` | 750 | 100 – 1400 (paso 10) | Tobogán: si estás a menos de (también acorta distancias) |
| `slideWindup` | 0.7 | 0.2 – 2 (paso 0.05) | Tobogán: preparación (s) |
| `slideSpeed` | 540 | 150 – 1000 (paso 10) | Tobogán: velocidad |
| `bounces` | 1 | 0 – 4 (paso 1) | Tobogán: rebotes en la pared |
| `slideDmg` | 18 | 0 – 60 (paso 1) | Tobogán: daño |
| `backDist` | 520 | 100 – 1400 (paso 10) | Tobogán: tras el último rebote, recorre (y se para pasado de ti) |
| `rest` | 1.6 | 0 – 5 (paso 0.1) | Tobogán: boca arriba al acabar (s) |
| `restVuln` | 1.4 | 1 – 3 (paso 0.05) | Boca arriba: × daño recibido |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
