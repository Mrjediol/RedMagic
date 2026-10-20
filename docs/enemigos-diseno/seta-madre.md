# Seta madre

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Seta madre)

## Diseño
<!-- gen:diseno -->
**Nivel:** élite · **Mundo:** bosque · **Rol:** Élite de la Saltaseta: ondas y se divide · **Cuerpo:** de suelo, hitbox 68×72 px (≈ 1.7×1.8 u) · código `e-*.js` id `shroomAlpha`

**Pregunta al jugador:** El ritmo de la Saltaseta, más corto (gran salto cada dos) y con ondas al caer que hay que saltar; y al perder vida suelta Saltasetas: ¿la rematas o limpias a las crías?

**Movimientos**

- **Gran salto con ondas** — Como la Saltaseta, pero cada dos saltitos; al caer, además del ¡PLAF!, dos ondas de esporas corren por el suelo a los lados.
- **Esporada** — Al bajar de 2/3 y de 1/3 de vida se sacude y suelta dos Saltasetas, que saltan hacia los lados.

**Combina con:** Con una Zarza: muros mientras saltas ondas.

**Arte (idea, sin decidir):** La Saltaseta grande y vieja: sombrero ancho lleno de motas-cristal que brillan, musgo y setas pequeñas creciéndole encima (las crías que suelta).
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_shroomAlpha`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 420 | 5 – 1500 (paso 5) | Vida |
| `poise` | 120 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.3 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `hopV` | 340 | 100 – 600 (paso 10) | Saltito: impulso |
| `hopX` | 150 | 0 – 300 (paso 5) | Saltito: avance |
| `hopRest` | 0.28 | 0.05 – 1 (paso 0.01) | Saltito: pausa en el suelo (s) |
| `keep` | 140 | 0 – 400 (paso 5) | Deja de acercarse a |
| `bigEvery` | 2 | 1 – 8 (paso 1) | Gran salto: cada N saltitos |
| `bigWindup` | 0.45 | 0.1 – 1.5 (paso 0.05) | Gran salto: preparación (s) |
| `bigH` | 170 | 40 – 300 (paso 5) | Gran salto: altura |
| `lead` | 0.25 | 0 – 1 (paso 0.05) | Gran salto: adelanta tu movimiento (s) |
| `slamR` | 80 | 15 – 160 (paso 1) | Gran salto: radio |
| `slamDmg` | 18 | 0 – 60 (paso 1) | Gran salto: daño |
| `rest` | 0.6 | 0 – 2 (paso 0.05) | Tras el gran salto: aturdida (s) |
| `waveSpeed` | 320 | 80 – 800 (paso 10) | Ondas: velocidad |
| `waveDist` | 420 | 50 – 1200 (paso 10) | Ondas: recorrido |
| `waveH` | 26 | 10 – 80 (paso 1) | Ondas: altura |
| `waveDmg` | 10 | 0 – 40 (paso 1) | Ondas: daño |
| `splitN` | 2 | 0 – 5 (paso 1) | Esporada: crías |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
