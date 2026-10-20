# Fénix

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Fénix)

## Diseño
<!-- gen:diseno -->
**Nivel:** élite · **Mundo:** volcan · **Rol:** Élite del Ave brasa: ida y vuelta, llamas y renace · **Cuerpo:** volador, hitbox 56×44 px (≈ 1.4×1.1 u) · código `e-*.js` id `phoenix`

**Pregunta al jugador:** La pasada del Ave brasa, pero de ida y vuelta y dejando llamas en el suelo donde caen las brasas; al morir su huevo aguanta más y renace antes: hay que ir a romperlo sí o sí.

**Movimientos**

- **Pasada de ida y vuelta** — Cruza soltando brasas (franja marcada) y, al llegar, se da la vuelta y vuelve por encima (otra franja).
- **Llamas** — Las brasas que tocan el suelo dejan llamas un momento.
- **Renacer** — Como el Ave brasa, pero el huevo tiene más vida y eclosiona antes (renace a media vida, una sola vez).

**Combina con:** Con el Escorpión: no puedes esperar su guardia quieto bajo las llamas.

**Arte (idea, sin decidir):** El Ave brasa grande: alas largas de fuego con plumas de cristal ardiente, cresta de llamas, cola de tres plumas largas; brilla más que nada en pantalla.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_phoenix`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 260 | 5 – 1500 (paso 5) | Vida |
| `poise` | 60 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.5 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `height` | 150 | 40 – 260 (paso 5) | Altura sobre ti |
| `speed` | 220 | 40 – 500 (paso 10) | Velocidad |
| `runWindup` | 0.55 | 0.1 – 2 (paso 0.05) | Pasada: preparación (s) |
| `runSpeed` | 560 | 150 – 1000 (paso 10) | Pasada: velocidad |
| `runDist` | 700 | 200 – 1300 (paso 10) | Pasada: recorrido |
| `dropEvery` | 0.09 | 0.03 – 0.5 (paso 0.01) | Pasada: brasa cada (s) |
| `emberDmg` | 8 | 0 – 30 (paso 1) | Brasa: daño |
| `runDmg` | 10 | 0 – 40 (paso 1) | Pasada: daño al tocarla |
| `eggHp` | 70 | 1 – 200 (paso 1) | Huevo: vida |
| `reviveTime` | 2.5 | 0.5 – 10 (paso 0.1) | Huevo: renace a los (s) |
| `reviveHp` | 0.5 | 0.1 – 1 (paso 0.05) | Al renacer: fracción de vida |
| `cooldown` | 1.8 | 0.2 – 5 (paso 0.1) | Pausa entre pasadas (s) |
| `emberZone` | 1.4 | 0 – 5 (paso 0.1) | Llamas: duran (s) |
| `backWindup` | 0.35 | 0.1 – 1.5 (paso 0.05) | Vuelta: preparación (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
