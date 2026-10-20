# Colgante

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Colgante)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** hielo · **Rol:** Emboscador del techo (carámbano vivo) · **Cuerpo:** volador, hitbox 32×52 px (≈ 0.8×1.3 u) · código `e-*.js` id `icicle`

**Pregunta al jugador:** Mirar también arriba: pasar por debajo lo hace caer. Provocar la caída a propósito y apartarse, para pegarle mientras está clavado en el suelo y mientras vuelve a subir.

**Movimientos**

- **Caída** — Cuelga del techo de hielo y se arrastra por él hacia ti. Si pasas por debajo, tiembla (polvo de escarcha, zona en el suelo del tamaño del impacto) y cae en picado. Al chocar salta metralla de hielo a ras de suelo hacia los dos lados (saltarla) y se queda clavado un rato (recibe más daño).
- **Goteo** — Si estás lejos, de vez en cuando suelta un carámbano pequeño que cae recto donde estás (marca pequeña en el suelo).
- **Volver** — Tras clavarse, despliega las alas y sube despacio al techo: se le puede seguir pegando.

**Combina con:** Encima de un Erizo o una Morsa: arriba y abajo a la vez. Con Ventisca: el viento te empuja debajo de él.

**Arte (idea, sin decidir):** Murciélago colgado boca abajo cuyas alas plegadas SON carámbanos (de lejos parece una estalactita más). Cuerpo de pelo azul marino oscuro, cara pequeña con dos ojos claros arriba, garras agarradas al techo. Al caer es un carámbano con cara; al subir las alas se abren (membrana de hielo translúcida). Partes: cuerpo, 2 alas (plegada / abierta), cabeza. ~0,8×1,3 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_icicle`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 70 | 5 – 1500 (paso 5) | Vida |
| `poise` | 25 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.3 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `crawl` | 60 | 0 – 200 (paso 5) | Se arrastra por el techo a |
| `trigger` | 70 | 10 – 250 (paso 5) | Cae si pasas a menos de |
| `shiver` | 0.5 | 0.1 – 2 (paso 0.05) | Caída: temblor antes de caer (s) |
| `fallSpeed` | 900 | 200 – 2000 (paso 10) | Caída: velocidad |
| `impactR` | 46 | 15 – 150 (paso 1) | Caída: radio del impacto |
| `impactDmg` | 16 | 0 – 60 (paso 1) | Caída: daño |
| `shards` | 6 | 0 – 12 (paso 1) | Metralla: trozos |
| `shardSpeed` | 260 | 50 – 600 (paso 10) | Metralla: velocidad |
| `shardDmg` | 6 | 0 – 40 (paso 1) | Metralla: daño |
| `stuck` | 1.3 | 0 – 4 (paso 0.1) | Clavado en el suelo (s) |
| `stuckVuln` | 1.3 | 1 – 3 (paso 0.05) | Clavado: × daño recibido |
| `climb` | 110 | 20 – 400 (paso 5) | Vuelve al techo a |
| `rehang` | 1.5 | 0 – 6 (paso 0.1) | Tras volver: tarda en poder caer otra vez (s) |
| `dripEvery` | 2.4 | 0.5 – 10 (paso 0.1) | Goteo: cada (s) |
| `dripDmg` | 6 | 0 – 40 (paso 1) | Goteo: daño |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
