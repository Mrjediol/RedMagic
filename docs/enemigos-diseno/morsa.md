# Morsa

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Morsa)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** hielo · **Rol:** Pesada de control de suelo · **Cuerpo:** de suelo, hitbox 96×64 px (≈ 2.4×1.6 u) · código `e-*.js` id `walrus`

**Pregunta al jugador:** Saltar la hilera de carámbanos que avanza hacia ti y no quedarte encerrado entre los pinchos que deja (duran un rato). Si te subes, te lanza un témpano.

**Movimientos**

- **Muro de carámbanos** — Levanta la cabeza y clava los colmillos en el suelo: salen carámbanos uno tras otro hacia ti (cada uno avisa con una grieta) y se quedan clavados un rato como obstáculo que hace daño; luego se rompen.
- **Témpano** — Si estás lejos o en una plataforma, arranca un bloque de hielo y te lo lanza en parábola (marca dónde cae).

**Combina con:** Con Ventisca: el viento te empuja contra los carámbanos. Con Acechadores (lobos): los pinchos te cortan la huida y saltan a por ti.

**Arte (idea, sin decidir):** Morsa enorme y rechoncha, piel gris-azulada con escarcha en el lomo, bigotes blancos, colmillos de hielo larguísimos (azul pálido, translúcidos). Se mueve a saltitos sobre las aletas. Al atacar levanta la cabeza (colmillos arriba) y la estampa. Partes: cuerpo, cabeza, colmillos, 2 aletas delanteras, cola. ~2,4×1,6 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_walrus`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 300 | 5 – 1500 (paso 5) | Vida |
| `poise` | 90 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.2 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 5 | 0 – 40 (paso 1) | Daño por contacto |
| `keep` | 280 | 0 – 600 (paso 10) | Se queda a esta distancia |
| `walk` | 45 | 0 – 200 (paso 5) | Velocidad |
| `spikeWindup` | 0.75 | 0.2 – 2 (paso 0.05) | Muro: preparación (s) |
| `spikeN` | 5 | 1 – 12 (paso 1) | Muro: carámbanos |
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
| `cooldown` | 1.8 | 0.2 – 6 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
