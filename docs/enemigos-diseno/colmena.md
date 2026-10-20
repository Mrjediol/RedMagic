# Colmena

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Colmena)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Rol:** Invocadora voladora (pare zánganos) · **Cuerpo:** volador, hitbox 64×68 px (≈ 1.6×1.7 u) · código `e-*.js` id `hive`

**Pregunta al jugador:** Prioridad: ir a por la colmena (lenta, alta, resistente) mientras los zánganos te pican, o limpiar zánganos con armas de área.

**Movimientos**

- **Parir** — Cada pocos segundos se hincha (se puede interrumpir con daño) y suelta 2 zánganos, hasta un máximo vivos a la vez.
- **Enjambre** — Si le haces mucho daño seguido, suelta 3 de golpe y huye al otro lado.
- **Furia** — Al morir, sus zánganos se enfurecen (más rápidos) unos segundos y luego caen muertos.

**Combina con:** Detrás de un Caparazón. Con Fatuos: muchos objetivos pequeños, las armas de área brillan.

**Arte (idea, sin decidir):** Nido colgante y abultado con agujeros que brillan, dos alitas que zumban. Panal de ámbar, nido de avispas de corteza.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_hive`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 180 | 5 – 1500 (paso 5) | Vida |
| `poise` | 50 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.4 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `height` | 170 | 60 – 280 (paso 5) | Altura sobre el suelo |
| `followDist` | 260 | 0 – 700 (paso 10) | Se queda a esta distancia |
| `speed` | 55 | 0 – 300 (paso 5) | Velocidad |
| `spawnEvery` | 3.6 | 0.5 – 15 (paso 0.1) | Parir: cada (s) |
| `spawnN` | 2 | 1 – 6 (paso 1) | Parir: zánganos |
| `maxDrones` | 5 | 1 – 15 (paso 1) | Zánganos vivos como mucho |
| `birthWindup` | 0.6 | 0.1 – 2 (paso 0.05) | Parir: preparación (s) |
| `panicHurt` | 45 | 0 – 300 (paso 5) | Enjambre: si recibe este daño en 2 s |
| `panicN` | 3 | 0 – 8 (paso 1) | Enjambre: zánganos |
| `panicCd` | 8 | 1 – 30 (paso 0.5) | Enjambre: pausa (s) |
| `frenzyTime` | 3 | 0 – 10 (paso 0.5) | Furia: duración (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
