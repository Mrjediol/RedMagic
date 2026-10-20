# Salamandra de magma

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Salamandra de magma)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** volcan · **Rol:** Corredora que deja rastro (cambia el suelo) · **Cuerpo:** de suelo, hitbox 60×28 px (≈ 1.5×0.7 u) · código `e-*.js` id `salamander`

**Pregunta al jugador:** El suelo cambia: cada carrera deja una franja de lava que dura unos segundos. No quedarte encerrado entre rastros, saltar la carrera, y mirar las paredes: trepa y se lanza desde ellas.

**Movimientos**

- **Carrera** — Se agacha (franja marcada en el suelo por donde va a pasar) y cruza corriendo a través de ti; detrás deja un rastro de lava que quema al pisarlo. Al acabar derrapa (castigo).
- **Salto de pared** — A veces corre a la pared de la arena, trepa por ella y se lanza en arco hacia donde estás (marca dónde cae, también en tu plataforma).

**Combina con:** Con el Golem: los pilares y los rastros te dejan poco suelo. Con Ave brasa: arriba brasas, abajo lava.

**Arte (idea, sin decidir):** Salamandra o lagartija de piel negra como obsidiana con grietas de magma naranja que brillan (más al correr), cola larga que gotea lava, ojos amarillos. Muy baja y alargada; patas que se mueven rápido. Partes: cuerpo, cabeza, cola (2 segmentos), 4 patas. ~1,5×0,7 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_salamander`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 80 | 5 – 1500 (paso 5) | Vida |
| `poise` | 25 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 3 | 0 – 40 (paso 1) | Daño por contacto |
| `speed` | 150 | 30 – 400 (paso 5) | Velocidad |
| `keepMin` | 160 | 0 – 500 (paso 10) | Le gusta estar a (mín.) |
| `keepMax` | 300 | 50 – 700 (paso 10) | Le gusta estar a (máx.) |
| `dashWindup` | 0.45 | 0.1 – 1.5 (paso 0.05) | Carrera: preparación (s) |
| `dashSpeed` | 540 | 150 – 1000 (paso 10) | Carrera: velocidad |
| `dashDist` | 460 | 100 – 1000 (paso 10) | Carrera: recorrido |
| `dashDmg` | 10 | 0 – 40 (paso 1) | Carrera: daño |
| `trailLife` | 2.4 | 0 – 8 (paso 0.1) | Rastro: dura (s) |
| `trailDmg` | 6 | 0 – 30 (paso 1) | Rastro: daño al pisarlo |
| `wallChance` | 0.35 | 0 – 1 (paso 0.05) | Probabilidad de salto de pared (si hay pared cerca) |
| `wallNear` | 380 | 50 – 900 (paso 10) | Salto de pared: si la pared está a menos de |
| `climbSpeed` | 260 | 50 – 600 (paso 10) | Trepar: velocidad |
| `wallH` | 150 | 40 – 260 (paso 5) | Trepar: hasta esta altura |
| `pounceWindup` | 0.4 | 0.1 – 1.5 (paso 0.05) | Salto de pared: preparación (s) |
| `pounceDmg` | 12 | 0 – 40 (paso 1) | Salto de pared: daño |
| `skid` | 0.5 | 0 – 2 (paso 0.05) | Derrape (s, castigo) |
| `cooldown` | 1.4 | 0.2 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
