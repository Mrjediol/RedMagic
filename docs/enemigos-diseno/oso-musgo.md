# Oso de musgo

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Oso de musgo)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** bosque · **Rol:** Pesado cuerpo a cuerpo que trepa · **Cuerpo:** de suelo, hitbox 92×80 px (≈ 2.3×2.0 u) · código `e-*.js` id `bear`

**Pregunta al jugador:** Esquivar el combo de zarpazos y no castigar antes de tiempo: a veces hay un tercero, más lento y fuerte. Subirte no te salva: trepa (despacio) a tu plataforma y desde arriba se tira en panzazo.

**Movimientos**

- **Zarpazos** — De cerca: dos zarpazos rápidos que avanzan un poco (cada uno levanta la zarpa y se enciende). A veces, tras una pausa que parece el final, se levanta sobre las patas y da un tercero más fuerte.
- **Trepar** — Si te quedas en una plataforma, va hasta el borde, se agacha y trepa de un salto torpe. Una vez arriba, sigue luchando.
- **Panzazo** — Si está en una plataforma y tú abajo cerca, se deja caer encima de ti: zona en el suelo y, al caer, dos ondas a los lados.

**Combina con:** Con Búho: arriba te espera el búho, abajo el oso. Con Saltasetas: no puedes quedarte quieto esquivando zarpazos.

**Arte (idea, sin decidir):** Oso pardo enorme cubierto de musgo, con un racimo de cristales turquesa creciéndole en el lomo y un arbolito o setas en el hombro; ojos turquesa que brillan. Anda a cuatro patas, se levanta a dos para el zarpazo fuerte. Partes: cuerpo, cabeza, 4 patas (zarpa delantera articulada), cristales. ~2,3×2 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_bear`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 320 | 5 – 1500 (paso 5) | Vida |
| `poise` | 90 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.25 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 6 | 0 – 40 (paso 1) | Daño por contacto |
| `walk` | 75 | 0 – 250 (paso 5) | Velocidad |
| `keepMin` | 70 | 0 – 300 (paso 5) | Se acerca hasta (mín.) |
| `keepMax` | 140 | 30 – 400 (paso 5) | Se acerca hasta (máx.) |
| `comboRange` | 140 | 40 – 300 (paso 5) | Zarpazos: si estás a menos de |
| `swipeWindup` | 0.45 | 0.1 – 1.5 (paso 0.05) | Zarpazo: preparación (s) |
| `swipeGap` | 0.3 | 0.05 – 1 (paso 0.05) | Zarpazos: pausa entre los dos (s) |
| `swipeDmg` | 12 | 0 – 50 (paso 1) | Zarpazo: daño |
| `lungeX` | 50 | 0 – 150 (paso 5) | Zarpazo: avance |
| `thirdChance` | 0.45 | 0 – 1 (paso 0.05) | Probabilidad del tercer zarpazo |
| `thirdDelay` | 0.6 | 0.2 – 2 (paso 0.05) | Tercer zarpazo: preparación (s) |
| `thirdDmg` | 18 | 0 – 60 (paso 1) | Tercer zarpazo: daño |
| `comboRec` | 0.7 | 0 – 2 (paso 0.05) | Tras el combo: recuperación (s, castigo) |
| `climbDelay` | 0.8 | 0 – 4 (paso 0.1) | Trepar: si estás en una plataforma más de (s) |
| `climbReach` | 170 | 40 – 300 (paso 5) | Trepar: altura máxima |
| `flopRange` | 160 | 30 – 400 (paso 5) | Panzazo: si estás debajo a menos de |
| `flopWindup` | 0.5 | 0.1 – 2 (paso 0.05) | Panzazo: preparación (s) |
| `flopR` | 70 | 20 – 200 (paso 5) | Panzazo: radio |
| `flopDmg` | 16 | 0 – 60 (paso 1) | Panzazo: daño |
| `flopWaveDmg` | 10 | 0 – 40 (paso 1) | Panzazo: daño de las ondas |
| `cooldown` | 1.2 | 0 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
