# Semilla estelar (`seed`)

**Estado**: prototipo web (clips + jugable), pendiente de prueba. Código: `a5-semilla.js`, `g-semilla.js`.

## Rol

Mina lanzada en parábola: se clava en el suelo, avisa con pulsos y estalla en una columna de luz que lanza por los aires. Golpe
diferido: se coloca antes de que llegue el enemigo o se lanza por encima de obstáculos.

## Comportamiento (jugable)

- Clic = lanza hacia la x del ratón (como mucho a `range` px en horizontal); cae en el **suelo** bajo ese punto (no en
  plataformas) en `flightTime` s, con un arco más alto cuanto más lejos.
- Se clava (polvo + destello) y se arma: 3 pulsos (anillo en el suelo + tic que sube de tono) y estalla a los `armTime` s.
- Estallido: daño en área `damage` en `radius` (centro a 30 px sobre el suelo), empuje `knockback` hacia fuera y `launch` hacia
  arriba; **golpe fuerte**. Columna de luz, anillo de suelo, agujas hacia arriba, grietas, polvo y escombros.

## Mejoras

<!-- gen:tree -->
Mejoras provisionales (el árbol con evoluciones está pendiente de diseñar):

| Mejora (id) | Qué hace |
|---|---|
| Racimo (`cluster`) | Estallido menor que suelta semillas pequeñas que estallan en cadena. |
| Proximidad (`prox`) | Se arma y espera a que un enemigo se acerque (hasta 3 a la vez). |
<!-- /gen:tree -->

- **Racimo**: estallido principal al 60 % del daño y 75 % del radio, y suelta `clusterCount` semillas pequeñas que saltan a los
  lados (50-170 px) y estallan en cadena (`clusterDamage`, `clusterRadius`).
- **Proximidad**: se arma al instante y espera (zona marcada en el suelo, radio `proxRadius`); cuando un enemigo de suelo entra,
  3 tics rápidos y estalla. Hasta `maxMines` a la vez (la más vieja estalla al poner otra).

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Hielo**: en vez de columna, nova de escarcha (picos que brotan hacia fuera) de radio `novaRadius`: congela al instante a
  todos (y la mitad del daño). Ideal para preparar añicos con otra arma.
- **Fuego**: el estallido quema y deja un charco ardiendo (`puddleTime`, `puddleBurn`).
- **Duplicador**: dos semillas a ±`dupSpread` px del punto.

## Sinergias

- **Reacciones**: Hielo congela un grupo → Golpe estelar / Singularidad lo rompe (añicos en cadena).
- **Singularidad** (idea): que atraiga también a las semillas pequeñas del racimo.
- **Proximidad + Media luna con Órbita**: defensa de zona.

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.8 | 0.1 – 4 (paso 0.05) | Recarga (s) |
| `range` | 420 | 80 – 800 (paso 10) | Alcance (px) |
| `flightTime` | 0.5 | 0.2 – 1.5 (paso 0.05) | Vuelo (s) |
| `armTime` | 0.62 | 0 – 2 (paso 0.02) | Armado hasta estallar (s) |
| `damage` | 45 | 1 – 200 (paso 1) | Daño |
| `radius` | 70 | 20 – 200 (paso 2) | Radio |
| `knockback` | 260 | 0 – 800 (paso 10) | Empuje |
| `launch` | 420 | 0 – 900 (paso 10) | Lanza hacia arriba |
| `clusterCount` | 3 | 1 – 8 (paso 1) | Racimo: semillas |
| `clusterDamage` | 22 | 1 – 100 (paso 1) | Racimo: daño de cada una |
| `clusterRadius` | 48 | 10 – 150 (paso 2) | Racimo: radio |
| `novaRadius` | 130 | 30 – 300 (paso 5) | Hielo: radio de la nova |
| `proxRadius` | 46 | 10 – 150 (paso 2) | Proximidad: radio de disparo |
| `maxMines` | 3 | 1 – 8 (paso 1) | Proximidad: minas a la vez |
| `puddleTime` | 1.5 | 0.2 – 5 (paso 0.1) | Fuego: duración del charco (s) |
| `puddleBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `dupSpread` | 45 | 0 – 150 (paso 1) | Duplicador: separación de las dos semillas (px) |
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

- 2026-10-19 · Cae siempre en el suelo principal (las plataformas no paran las semillas) para simplificar el prototipo.

## Preguntas abiertas

- ¿Que se peguen a plataformas/paredes? ¿Detonador manual (segundo botón)?
