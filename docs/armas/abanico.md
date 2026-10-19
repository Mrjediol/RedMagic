# Abanico de cristal (`fan`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras) en prueba. Código: `g-abanico.js` (jugable), `a1-abanico.js`
(clips de la primera versión, con los mods antiguos). Reglas comunes: [README](README.md) · progresión: [progresion.md](progresion.md).

## Rol

Disparo inmediato sin carga, varias esquirlas. La base es una escopeta corta (mucho daño de cerca). Cada evolución la convierte en
otra arma: **Erizo** (clavar y detonar alternando armas), **Lanza** (precisión de lejos que perfora), **Caleidoscopio** (rebotes).

## Comportamiento (base)

- Clic = ráfaga; mantener = ráfaga cada `cooldown`.
- `count` esquirlas repartidas en ±`spread` rad (azar ±0,01 rad y ±5 % de velocidad), recto a `speed` durante `life` s.
- Chocan con el primer enemigo (elipse) o el suelo; atraviesan plataformas (salvo el Caleidoscopio, que rebota en ellas).
- `damage` + `knockback` por esquirla. Son **capturables** por la Singularidad mientras vuelan.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Erizo** (`spike`) | Se clavan en enemigos y suelo y estallan al cambiar de arma (o solas al rato). Premia alternar armas. | **Retorno** (`ret`): Tras estallar vuelven al cristal atravesando enemigos; cada una recarga el arma de reserva.<br>**Metralla** (`shrap`): Cada estallido suelta esquirlas pequeñas en todas direcciones.<br>**Hemorragia** (`bleed`): Cada esquirla clavada hace daño continuo mientras está dentro. |
| **Lanza** (`lance`) | Ráfaga en línea, rápida y de largo alcance, que atraviesa al primer enemigo. | **Perforación** (`pierce`): Atraviesan a todos; cada enemigo atravesado suma daño.<br>**Aceleración** (`accel`): Manteniendo el disparo, cada ráfaga sale antes que la anterior.<br>**Cristal mayor** (`big`): La última de cada ráfaga es una lanza grande: mucho daño y golpe fuerte. |
| **Caleidoscopio** (`kal`) | Rebotan en suelo, plataformas y enemigos, y cada rebote suma daño. | **Buscador** (`seek`): Todos los rebotes van hacia el enemigo más cercano.<br>**Prisma** (`split`): Al rebotar en un enemigo se divide en dos.<br>**Eterno** (`eternal`): +3 rebotes y no caducan mientras sigan rebotando. |
<!-- /gen:tree -->

Detalle:
- **Erizo**: TODA esquirla que toca algo se clava (enemigo: se mueve con él; suelo: se queda). Estallan cuando **cambias de
  arma** (cualquier cambio después de clavarse) o a los `spikeLife` s: daño `spikeBlast` en `spikeBlastRadius`, **golpe fuerte**
  (rompe congelados → añicos). Un arma lenta en la otra mano + Erizo = cargar, cambiar, reventar.
  - Retorno: tras estallar vuelven en curva al cristal; lo que atraviesan recibe `returnDamage`; cada una que llega resta
    `refund` s a la recarga del arma de reserva.
  - Metralla: `shrapCount` esquirlas pequeñas (`shrapDamage`, vuelan 0,35 s) por estallido, hacia fuera (hacia arriba si la
    esquirla estaba en el suelo); no chocan con el enemigo que la tenía clavada.
  - Hemorragia: `bleedDps` por esquirla clavada (cada 0,5 s; es daño continuo: no activa items de golpe).
- **Lanza**: recarga `lanceCooldown`; las esquirlas BASE del arma (`count`) en fila, una cada `lanceGap` s, hacia donde apuntas **en ese momento**
  (puedes barrer durante la ráfaga), a `lanceSpeed` durante `lanceLife` s; cada una atraviesa 1 enemigo.
  - Perforación: atraviesan a todos; +`pierceDmgUp` de daño por enemigo atravesado.
  - Aceleración: manteniendo, cada ráfaga recarga `accelStep` antes (hasta ×`accelMin`); al soltar vuelve a empezar.
  - Cristal mayor: la última de cada ráfaga es grande (`bigSize`), ×`bigMul` de daño, golpe fuerte, empuja más.
- **Caleidoscopio**: paleta oro; `kalBounces` rebotes (suelo, plataformas, enemigos); cada rebote +`kalBounceDmg` de daño.
  En un enemigo rebota según la superficie del cuerpo (reflejo sobre la normal de la elipse); en suelo y plataformas, rebota.
  - Buscador: todos los rebotes (enemigo, suelo, plataforma) van hacia el enemigo más cercano que no haya tocado (≤ `kalSeekRange`).
  - Prisma: al rebotar en un enemigo se divide en dos que se abren ±0,35 rad (con Buscador, la copia va al segundo más cercano);
    la copia hace ×`splitMul` de daño, tiene un rebote menos y no vuelve a dividirse.
  - Eterno: +3 rebotes y el alcance se renueva en cada rebote.

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Hielo**: cada esquirla suma `freeze` (con 3 se congela); el estallido del Erizo y el Cristal mayor rompen congelados.
- **Fuego**: cada esquirla quema `burn` s.
- **Duplicador**: base/Caleidoscopio = el doble de esquirlas con un 35 % más de apertura; Lanza = dos filas paralelas.

## Sinergias

- **Singularidad**: captura las esquirlas en vuelo y las suelta en todas direcciones (Catapulta). Con hielo, congela al grupo.
- **Erizo + Gatillo de cambio** (item): el cambio que detona además hace que el siguiente ataque sea fuerte.
- **Golpe estelar · Cazador**: cada esquirla marca; la Lanza perforando marca a una fila entera.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.45 | 0.1 – 2 (paso 0.01) | Base: recarga (s) |
| `count` | 5 | 1 – 12 (paso 1) | Base: esquirlas |
| `spread` | 0.2 | 0 – 0.8 (paso 0.01) | Base: apertura (rad, media) |
| `speed` | 950 | 300 – 2000 (paso 10) | Base: velocidad (px/s) |
| `damage` | 9 | 1 – 60 (paso 1) | Base: daño por esquirla |
| `knockback` | 60 | 0 – 400 (paso 5) | Base: empuje |
| `life` | 0.6 | 0.1 – 2 (paso 0.05) | Base: alcance (s de vuelo) |
| `freeze` | 0.34 | 0 – 1 (paso 0.01) | Hielo: congelación por esquirla |
| `burn` | 1.5 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `spikeLife` | 2.5 | 0.3 – 8 (paso 0.1) | Erizo: estallan solas a los (s) |
| `spikeBlast` | 10 | 0 – 60 (paso 1) | Erizo: daño del estallido |
| `spikeBlastRadius` | 34 | 8 – 120 (paso 2) | Erizo: radio del estallido |
| `returnDamage` | 7 | 0 – 60 (paso 1) | Erizo · Retorno: daño al volver |
| `refund` | 0.15 | 0 – 1 (paso 0.01) | Erizo · Retorno: recarga del arma de reserva por esquirla (s) |
| `shrapCount` | 3 | 1 – 8 (paso 1) | Erizo · Metralla: esquirlas por estallido |
| `shrapDamage` | 4 | 1 – 30 (paso 1) | Erizo · Metralla: daño |
| `bleedDps` | 4 | 0 – 30 (paso 0.5) | Erizo · Hemorragia: daño/s por clavada |
| `lanceCooldown` | 0.55 | 0.1 – 2 (paso 0.01) | Lanza: recarga (s) |
| `lanceGap` | 0.035 | 0.01 – 0.15 (paso 0.005) | Lanza: tiempo entre esquirlas (s) |
| `lanceSpeed` | 1350 | 400 – 2500 (paso 10) | Lanza: velocidad |
| `lanceLife` | 0.75 | 0.1 – 2 (paso 0.05) | Lanza: alcance (s) |
| `pierceDmgUp` | 0.25 | 0 – 1 (paso 0.05) | Lanza · Perforación: + daño por atravesado |
| `accelStep` | 0.12 | 0 – 0.5 (paso 0.01) | Lanza · Aceleración: − recarga por ráfaga |
| `accelMin` | 0.5 | 0.1 – 1 (paso 0.05) | Lanza · Aceleración: recarga mínima (×) |
| `bigMul` | 3 | 1 – 8 (paso 0.1) | Lanza · Cristal mayor: × daño |
| `bigSize` | 1.8 | 1 – 3 (paso 0.05) | Lanza · Cristal mayor: tamaño |
| `kalBounces` | 3 | 1 – 10 (paso 1) | Caleidoscopio: rebotes |
| `kalBounceDmg` | 0.25 | 0 – 1 (paso 0.05) | Caleidoscopio: + daño por rebote |
| `kalSeekRange` | 500 | 100 – 900 (paso 10) | Caleidoscopio: alcance al buscar |
| `splitMul` | 0.5 | 0.1 – 1 (paso 0.05) | Caleidoscopio · Prisma: daño de la copia (×) |
<!-- /gen:tuning -->

## Visual

`crystalShard` (core.js) + estela `ribbon` en 3 pasadas; impacto `impact(…, 0,45)` (0,8 la grande). Clavada: la esquirla
retrocedida 8 px + destello `star4`; tiembla en los últimos 0,4 s antes de estallar sola. Estallido del Erizo: `impact` radial +
chispas. Añicos: `hitFx('shatter')`.

## Sonido

`fan_release`, `fan_hit`, `fan_ground`, `fan_stick`, `fan_ricochet`, `fan_rip`, `fan_absorb`; estallido del Erizo =
`SFXLIB.hitEnergy` suave.

## Port a Unity (plan)

Arma con forma «abanico». El árbol vive en la propia arma (evolución + mejoras como datos de `WeaponDefinition`, elegidas en la
forja); la esquirla es un proyectil con pool y estados (vuela / clavada / vuelve). El estallido «al cambiar de arma» necesita el
evento de cambio en `WeaponUser` (2 armas activas).

## Decisiones

- 2026-10-19 · Apertura 0,2 rad (con 0,24 pasaban dos por encima del enemigo).
- 2026-10-19 · El usuario: las mejoras de un arma no pueden ser items generales; árbol profundo con varias formas avanzadas.
  Se prueban Erizo / Lanza / Caleidoscopio. El hielo pasa a ser item (elemento de la build), no mod del arma.
- 2026-10-19 · Erizo detona al cambiar de arma (propuesta para dar sentido a las 2 armas; pendiente de opinión).

- 2026-10-19 · Arreglos tras la prueba del usuario: Metralla no salía, el Caleidoscopio base ya buscaba al siguiente (sólo debe
  con Buscador) y el Prisma no se dividía. Causa común de Metralla y Prisma: lo creado durante la actualización de una entidad se
  perdía (arreglado en `game.js ▸ step`; afectaba también a la estela de fuego, el racimo, los cráteres y algunos impactos).

- 2026-10-19 · Lanza: la ráfaga usa las esquirlas base del arma (`count`; se quitó `lanceCount`) y con Duplicador cada esquirla sale
  doble en el mismo instante (antes la segunda fila esperaba a que acabara la primera y salía de golpe al final).

## Preguntas abiertas

- ¿El Erizo debería detonar también con un clic largo, para quien juegue con una sola arma?
- Equilibrio: la Lanza sale bastante más fuerte que la base (es lo esperado de una evolución, pero hay que comparar las tres).
