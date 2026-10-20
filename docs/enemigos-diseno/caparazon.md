# Caparazón

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Caparazón)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Rol:** Blindado con escudo frontal · **Cuerpo:** de suelo, hitbox 80×60 px (≈ 2.0×1.5 u) · código `e-*.js` id `shell`

**Pregunta al jugador:** Rodearlo saltando por encima (gira despacio), pegarle por la espalda o desde arriba, o romperle la guardia con un golpe fuerte; y saltar sus ondas.

**Movimientos**

- **Escudo** — Lo que le llega de frente hace sólo ×0,15 (BLOQUEO). Por la espalda, daño completo. Tarda en girarse cuando le pasas al otro lado. Un golpe fuerte de frente le rompe la guardia: queda vulnerable un rato.
- **Golpe de escudo** — Si estás delante y cerca: retrocede un poco y embiste corto con el escudo. Empuja mucho.
- **Sacudida** — Cada pocos segundos se levanta (¡enseña la tripa: ahí el escudo no tapa!) y golpea el suelo: dos ondas que corren por el suelo a los dos lados. Hay que saltarlas.
- **Pedrada** — Si te subes a una plataforma, arranca una piedra del suelo con el escudo y te la lanza en parábola (marca dónde cae, en tu plataforma). Mientras lanza, la tripa queda al aire.

**Combina con:** Delante de Artilleros o una Colmena: tapa a los que disparan. Con Embestidor: dos muros que te encierran.

**Arte (idea, sin decidir):** Tortuga o escarabajo de piedra con musgo, una placa frontal grande (escudo) de cristal-piedra; cabeza pequeña asomando.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_shell`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 220 | 5 – 1500 (paso 5) | Vida |
| `poise` | 60 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.25 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `walk` | 45 | 0 – 200 (paso 5) | Velocidad andando |
| `keep` | 90 | 0 – 300 (paso 5) | Se acerca hasta |
| `turnDelay` | 0.9 | 0 – 3 (paso 0.05) | Tarda en girarse (s) |
| `frontMul` | 0.15 | 0 – 1 (paso 0.05) | Escudo: × daño de frente |
| `breakTime` | 1.6 | 0 – 5 (paso 0.1) | Guardia rota: duración (s) |
| `breakVuln` | 1.3 | 1 – 3 (paso 0.05) | Guardia rota: × daño recibido |
| `bashRange` | 150 | 40 – 400 (paso 5) | Golpe de escudo: si estás a menos de |
| `bashWindup` | 0.55 | 0.1 – 2 (paso 0.05) | Golpe de escudo: preparación (s) |
| `bashDist` | 120 | 20 – 300 (paso 5) | Golpe de escudo: avance |
| `bashDmg` | 14 | 0 – 60 (paso 1) | Golpe de escudo: daño |
| `quakeEvery` | 6 | 1 – 20 (paso 0.5) | Sacudida: cada (s) |
| `quakeWindup` | 0.8 | 0.2 – 2 (paso 0.05) | Sacudida: preparación (s) |
| `waveSpeed` | 330 | 80 – 800 (paso 10) | Onda: velocidad |
| `waveDist` | 520 | 50 – 1400 (paso 10) | Onda: recorrido |
| `waveH` | 34 | 10 – 90 (paso 1) | Onda: altura |
| `waveDmg` | 12 | 0 – 60 (paso 1) | Onda: daño |
| `cooldown` | 1.2 | 0 – 5 (paso 0.1) | Pausa entre ataques (s) |
| `upDelay` | 0.5 | 0 – 3 (paso 0.05) | Pedrada: si estás en una plataforma más de (s) |
| `rockRange` | 600 | 100 – 1200 (paso 10) | Pedrada: alcance |
| `rockWindup` | 0.6 | 0.1 – 2 (paso 0.05) | Pedrada: preparación (s) |
| `rockFlight` | 0.9 | 0.3 – 2 (paso 0.05) | Pedrada: vuelo (s) |
| `rockDmg` | 10 | 0 – 60 (paso 1) | Pedrada: daño |
| `rockR` | 30 | 10 – 100 (paso 1) | Pedrada: radio del impacto |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
- 2026-10-20 · Respuesta hacia arriba (el usuario no veía cómo encajan los de suelo con tantas plataformas): ver «Movimientos»; ajustes nuevos en la tabla.
- 2026-10-20 · El usuario descarta la sacudida de plataformas a distancia (sin coherencia física): sustituida por una respuesta física (ver «Movimientos»).
