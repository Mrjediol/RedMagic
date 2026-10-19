# Semilla estelar (`seed`)

**Estado**: prototipo web (clips + jugable); árbol de evoluciones hecho 2026-10-19, pendiente de la prueba del usuario. Código: `a5-semilla.js`, `g-semilla.js`.

## Rol

Mina lanzada en parábola: se clava en el suelo, avisa con pulsos y estalla en una columna de luz que lanza por los aires. Golpe
diferido: se coloca antes de que llegue el enemigo o se lanza por encima de obstáculos.

## Comportamiento (jugable)

- Clic = lanza hacia la x del ratón (como mucho a `range` px en horizontal); cae en el **suelo** bajo ese punto (no en
  plataformas) en `flightTime` s, con un arco más alto cuanto más lejos.
- Se clava (polvo + destello) y se arma: 3 pulsos (anillo en el suelo + tic que sube de tono) y estalla a los `armTime` s.
- Estallido: daño en área `damage` en `radius` (centro a 30 px sobre el suelo), empuje `knockback` hacia fuera y `launch` hacia
  arriba; **golpe fuerte**. Columna de luz, anillo de suelo, agujas hacia arriba, grietas, polvo y escombros.

## Árbol (evolución en nivel 2 + 3 mejoras)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Campo de minas** (`prox`) | Se arma y espera a que un enemigo se acerque (hasta 3 a la vez; la más vieja estalla al poner otra). | **Reacción en cadena** (`chain`): Una mina que estalla hace estallar a las cercanas, y cada una de la cadena hace más daño.<br>**Raíces** (`root`): Al activarse, unas raíces sujetan a los de alrededor y estalla un poco después.<br>**Semillero** (`sprout`): Cuanto más tiempo lleva puesta una mina, más crece: más daño y más radio. |
| **Racimo** (`cluster`) | Estallido menor que suelta semillas pequeñas que estallan en cadena. | **Semillas vivas** (`seek`): Las semillas pequeñas saltan hacia los enemigos cercanos.<br>**Generaciones** (`gen`): Cada semilla pequeña suelta a su vez otras dos aún más pequeñas.<br>**Siembra** (`plant`): Las semillas pequeñas que no dan a nadie se quedan como minas un rato. |
| **Madura** (`ripe`) | Mantén para que la semilla madure en la mano: más daño, radio y alcance; suelta para lanzarla. Verde es más débil. | **Supernova** (`super`): Madura del todo, el estallido se repite dos veces más.<br>**Implosión** (`implode`): Antes de estallar atrae a los enemigos cercanos (más cuanto más madura).<br>**Cosecha** (`harvest`): Cada baja de una semilla deja la siguiente ya medio madura. |
<!-- /gen:tree -->

Detalle:
- **Campo de minas** (era la mejora Proximidad): se arma al instante y espera (zona marcada en el suelo, radio `proxRadius`); cuando un
  enemigo de suelo entra, tic rápido y estalla. Hasta `maxMines` a la vez (la más vieja estalla al poner otra).
  - Reacción en cadena: al estallar, las minas armadas a menos de `chainRadius` estallan 0,12 s después, cada eslabón +`chainBonus` de
    daño («CADENA ×N»).
  - Raíces: al activarse, rayos-raíz sujetan a los de suelo a menos de `rootRadius` (no se mueven ni con empujones) y estalla a los
    `rootDelay` s, cuando ya han llegado más.
  - Semillero: la mina crece con el tiempo puesta (más pinchos, estrella mayor): al cabo de `sproutTime` s, +`sproutMul` de daño y
    +`sproutSize` de radio.
- **Racimo**: estallido principal al 60 % del daño y 75 % del radio; suelta `clusterCount` semillas pequeñas que saltan a los lados
  (50-170 px) y estallan en cadena (`clusterDamage`, `clusterRadius`).
  - Semillas vivas: cada pequeña salta hacia un enemigo cercano (≤ `seekRange`) que el estallido principal no tocó.
  - Generaciones: cada pequeña suelta `genCount` todavía más pequeñas (`genDamage`).
  - Siembra: las pequeñas que no dan a nadie se quedan `plantLife` s como minas (radio de disparo `plantRadius`).
- **Madura**: mantener = la semilla crece en el cristal (anillo de madurez; dorada y con un tic al llenarse en `matureTime` s); soltar
  (o cambiar de arma) la lanza. Daño de ×`ripeMin` (verde) a ×`ripeMax` (madura), radio con la raíz de eso, alcance hasta ×`ripeRange`.
  - Supernova: madura del todo, el estallido se repite dos veces cada `superGap` s (×`superMul`).
  - Implosión: mientras se arma atrae a los enemigos a menos de `implodeRadius` (más cuanto más madura) y tarda `implodeTime` s más.
  - Cosecha: si una semilla mata, la siguiente empieza con `harvestRipe` de madurez.

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Hielo**: en vez de columna, nova de escarcha (picos que brotan hacia fuera) de radio `novaRadius`: congela al instante a
  todos (y la mitad del daño). Ideal para preparar añicos con otra arma.
- **Fuego**: el estallido quema y deja un charco ardiendo (`puddleTime`, `puddleBurn`).
- **Duplicador**: dos semillas a ±`dupSpread` px del punto.

## Sinergias

- **Reacciones**: Hielo congela un grupo → Golpe estelar / Singularidad lo rompe (añicos en cadena).
- **Singularidad** (idea): que atraiga también a las semillas pequeñas del racimo.
- **Campo de minas + Media luna Satélite**: defensa de zona.
- **Madura + Implosión**: junta un grupo para la Singularidad o el Golpe estelar.

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.8 | 0.1 – 4 (paso 0.05) | Base: recarga (s) |
| `range` | 420 | 80 – 800 (paso 10) | Base: alcance (px) |
| `flightTime` | 0.5 | 0.2 – 1.5 (paso 0.05) | Base: vuelo (s) |
| `armTime` | 0.62 | 0 – 2 (paso 0.02) | Base: armado hasta estallar (s) |
| `damage` | 45 | 1 – 200 (paso 1) | Base: daño |
| `radius` | 70 | 20 – 200 (paso 2) | Base: radio |
| `knockback` | 260 | 0 – 800 (paso 10) | Base: empuje |
| `launch` | 420 | 0 – 900 (paso 10) | Base: lanza hacia arriba |
| `novaRadius` | 130 | 30 – 300 (paso 5) | Hielo: radio de la nova |
| `puddleTime` | 1.5 | 0.2 – 5 (paso 0.1) | Fuego: duración del charco (s) |
| `puddleBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `dupSpread` | 45 | 0 – 150 (paso 1) | Duplicador: separación de las dos semillas (px) |
| `proxRadius` | 46 | 10 – 150 (paso 2) | Campo de minas: radio de disparo |
| `maxMines` | 3 | 1 – 8 (paso 1) | Campo de minas: minas a la vez |
| `chainRadius` | 150 | 30 – 400 (paso 5) | Campo de minas · Reacción: radio |
| `chainBonus` | 0.3 | 0 – 1.5 (paso 0.05) | Campo de minas · Reacción: + daño por mina de la cadena |
| `rootRadius` | 100 | 20 – 250 (paso 5) | Campo de minas · Raíces: radio |
| `rootDelay` | 0.6 | 0.1 – 2 (paso 0.05) | Campo de minas · Raíces: espera hasta estallar (s) |
| `sproutTime` | 4 | 0.5 – 15 (paso 0.5) | Campo de minas · Semillero: s hasta crecer del todo |
| `sproutMul` | 1 | 0 – 3 (paso 0.1) | Campo de minas · Semillero: + daño al máximo |
| `sproutSize` | 0.5 | 0 – 2 (paso 0.05) | Campo de minas · Semillero: + radio al máximo |
| `clusterCount` | 3 | 1 – 8 (paso 1) | Racimo: semillas |
| `clusterDamage` | 22 | 1 – 100 (paso 1) | Racimo: daño de cada una |
| `clusterRadius` | 48 | 10 – 150 (paso 2) | Racimo: radio |
| `seekRange` | 240 | 50 – 500 (paso 10) | Racimo · Semillas vivas: alcance |
| `genCount` | 2 | 1 – 5 (paso 1) | Racimo · Generaciones: semillas por semilla |
| `genDamage` | 12 | 1 – 60 (paso 1) | Racimo · Generaciones: daño |
| `plantLife` | 4 | 0.5 – 12 (paso 0.5) | Racimo · Siembra: duración de la mina (s) |
| `plantRadius` | 40 | 10 – 120 (paso 2) | Racimo · Siembra: radio de disparo |
| `matureTime` | 1.2 | 0.2 – 4 (paso 0.05) | Madura: s hasta madurar del todo |
| `ripeMin` | 0.5 | 0.1 – 1 (paso 0.05) | Madura: × daño sin madurar |
| `ripeMax` | 1.8 | 1 – 4 (paso 0.05) | Madura: × daño madura |
| `ripeRange` | 1.5 | 1 – 3 (paso 0.05) | Madura: × alcance madura |
| `superGap` | 0.3 | 0.1 – 1 (paso 0.05) | Madura · Supernova: tiempo entre estallidos (s) |
| `superMul` | 0.6 | 0.1 – 1.5 (paso 0.05) | Madura · Supernova: × daño de cada réplica |
| `implodeRadius` | 180 | 40 – 400 (paso 5) | Madura · Implosión: radio |
| `implodeTime` | 0.45 | 0.1 – 1.5 (paso 0.05) | Madura · Implosión: + s de armado atrayendo |
| `implodeSpeed` | 320 | 50 – 800 (paso 10) | Madura · Implosión: fuerza |
| `harvestRipe` | 0.5 | 0 – 1 (paso 0.05) | Madura · Cosecha: madurez con la que empieza tras una baja |
<!-- /gen:tuning -->

## Visual

`starSeed` (fx.js: estrella de 4 puntas en capas que gira), estela corta; clavada con rayitos hacia arriba y pulsos de anillo;
`lightColumn`, `groundImpact`, `cracks`; `frostNova` para el hielo.

## Sonido

`seed_lob`, `seed_land`, tic de armado (en la jugable `seedTick` en `g-semilla.js`), `seed_armed`, `seed_trigger`,
`seed_boom`, `seed_split`, `seed_mini`, `seed_nova`.

## Port a Unity (plan)

Trayectoria «lanzada» (con gravedad) — no existe en el pipeline: sería un modificador de Trayectoria nuevo que cualquier
proyectil podría usar. Estado mina con temporizador o detector de proximidad (trigger).

## Decisiones

- 2026-10-19 · Árbol: Campo de minas / Racimo / Madura (Proximidad y Racimo, provisionales, pasan a evoluciones). Semillas vivas
  apuntaba al del centro (ya golpeado) → busca sólo a los que el estallido no tocó.

- 2026-10-19 · Cae siempre en el suelo principal (las plataformas no paran las semillas) para simplificar el prototipo.

## Preguntas abiertas

- ¿Que se peguen a plataformas/paredes?
