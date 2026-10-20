# Golem de escoria

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Golem de escoria)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** volcan · **Rol:** Pesado que construye cobertura · **Cuerpo:** de suelo, hitbox 88×92 px (≈ 2.2×2.3 u) · código `e-*.js` id `slaggolem`

**Pregunta al jugador:** Sus pilares tapan tus disparos: rodéalos (saltando por encima) o rómpelos tú antes de que los reviente, porque si lo hace él te lanza la metralla. De cerca, golpe al suelo.

**Movimientos**

- **Alzar pilar** — Golpea el suelo (marca en el suelo) y sale un pilar de obsidiana entre tú y él. Los pilares paran tus disparos y duran un rato (como mucho dos a la vez). Se pueden romper.
- **Reventar pilar** — Si hay un pilar entre los dos, va hacia él, carga el puño y lo revienta: la metralla sale en abanico hacia ti. Si lo rompes tú antes, no hay metralla.
- **Golpe al suelo** — Si estás cerca: levanta los dos puños y golpea el suelo (zona alrededor).

**Combina con:** Con la Salamandra: pilares + rastros te dejan muy poco suelo. Con el Escorpión: dos que piden paciencia.

**Arte (idea, sin decidir):** Golem grande de bloques de basalto y escoria con las juntas llenas de magma que brilla, un núcleo ardiente en el pecho, cabeza pequeña hundida entre los hombros y puños enormes. Los pilares: columnas de obsidiana negra brillante con filos violáceos. Partes: torso, cabeza, 2 brazos (puño), 2 piernas, núcleo; pilar (3 estados de grietas). ~2,2×2,3 u; pilar ~0,8×2,8 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_slaggolem`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 320 | 5 – 1500 (paso 5) | Vida |
| `poise` | 110 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.15 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 6 | 0 – 40 (paso 1) | Daño por contacto |
| `speed` | 50 | 0 – 200 (paso 5) | Velocidad |
| `keepMin` | 200 | 0 – 500 (paso 10) | Le gusta estar a (mín.) |
| `keepMax` | 320 | 50 – 700 (paso 10) | Le gusta estar a (máx.) |
| `pillarWindup` | 0.7 | 0.2 – 2 (paso 0.05) | Alzar pilar: preparación (s) |
| `maxPillars` | 2 | 0 – 4 (paso 1) | Pilares a la vez |
| `pillarHp` | 60 | 5 – 400 (paso 5) | Pilar: vida |
| `pillarLife` | 9 | 1 – 30 (paso 0.5) | Pilar: dura (s) |
| `pillarGap` | 110 | 40 – 300 (paso 5) | Pilar: a esta distancia de él |
| `punchWindup` | 0.55 | 0.1 – 2 (paso 0.05) | Reventar: preparación (s) |
| `shardN` | 5 | 1 – 12 (paso 1) | Reventar: trozos |
| `shardSpeed` | 440 | 100 – 900 (paso 10) | Reventar: velocidad |
| `shardDmg` | 8 | 0 – 40 (paso 1) | Reventar: daño de cada trozo |
| `slamRange` | 110 | 30 – 300 (paso 5) | Golpe al suelo: si estás a menos de |
| `slamWindup` | 0.6 | 0.1 – 2 (paso 0.05) | Golpe al suelo: preparación (s) |
| `slamR` | 110 | 30 – 300 (paso 5) | Golpe al suelo: radio |
| `slamDmg` | 14 | 0 – 50 (paso 1) | Golpe al suelo: daño |
| `cooldown` | 1.6 | 0.2 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
