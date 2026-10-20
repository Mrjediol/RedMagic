# Espejo (`mirror`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-espejo.js`; dibujo en `fx-armas2.js` (`mirrorPane`, `glintRay`). Regla común nueva en `game.js`: espejos
(`G.mirrors`, `mirrorCross`, `segSeg`) y en `g-rayo.js ▸ rayPath` (el haz rebota). Reglas comunes: [README](README.md) ·
progresión: [progresion.md](progresion.md).

## Rol

**El arma que más depende de la otra.** Clic = un panel de cristal flotando donde apuntas, mirando hacia ti. Lo que hace es
cambiar el resto de tus armas: **tus proyectiles que lo cruzan salen desviados hacia el enemigo más cercano** (y pegan más),
**el Rayo rebota en él** igual, y **las balas enemigas vuelven como tuyas**. Solo, destella de vez en cuando.

## Comportamiento (base)

- Clic = espejo en el punto apuntado (máx. `range`, nunca bajo el suelo), largo 2·`half`, perpendicular a la línea tú→espejo.
  Dura `life` s; hasta `maxMirrors` (el más viejo se rompe).
- Proyectiles con velocidad que lo cruzan (Abanico, Tótem, Jabalina, balas devueltas, esquirlas…): salen hacia el enemigo más
  cercano a `redirectRange` (si no hay, reflejo físico), ×`mirrorMul` de daño, vida extendida y pueden volver a golpear al
  mismo enemigo. Un proyectil no puede cruzar dos espejos en 0,1 s.
- **Rayo**: el haz que toca el espejo sigue desde él hacia el enemigo más cercano (×`mirrorMul`; hasta 2 rebotes).
- Balas enemigas: vuelven hacia el enemigo más cercano como tuyas (`reflectDamage`).
- Destello propio cada `glintEvery` s al más cercano a `glintRange` (`glintDamage`).

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Facetas** (`facet`) | Cada proyectil que desvía se parte en dos; el Rayo que lo toca suelta además un haz al segundo enemigo. | **Tres caras** (`three`): Se parte en tres.<br>**Brillo** (`shine`): Destella mucho más a menudo y también cada vez que desvía algo.<br>**Añicos** (`shatter`): Al acabarse, el espejo revienta en esquirlas que van a por los enemigos. |
| **Lupa** (`lens`) | Ya no desvía: CONCENTRA. Lo que la atraviesa sale con mucho más daño (el Rayo, más grueso y fuerte). | **Foco** (`focus`): Concentra la luz en el suelo de debajo: quema a quien pise el punto.<br>**Gran angular** (`wide`): Lupa mucho más grande.<br>**Convergencia** (`converge`): Además de concentrar, desvía lo que pasa hacia el enemigo más cercano. |
| **Portal** (`portal`) | Dos espejos enlazados: lo que entra por uno sale por el otro — tus proyectiles, el Rayo y los ENEMIGOS que pasan por él. | **Bucle** (`loop`): Cada vez que un proyectil tuyo atraviesa, gana daño (hasta 3 veces).<br>**Trampa** (`trap`): Los enemigos que atraviesan salen aturdidos y vulnerables.<br>**Atajo** (`walk`): Tú también puedes atravesarlos. |
<!-- /gen:tree -->

Detalle:
- **Facetas**: cada proyectil desviado se parte en dos (abertura `facetSpread`; los que no se pueden copiar, no); con el Rayo
  encima, además un haz fino al **segundo** enemigo más cercano (`sideBeamDps`).
  - Tres caras: se parte en tres. Brillo: destella cada `shineEvery` s y cada vez que desvía. Añicos: al acabarse revienta
    en `shatterShards` esquirlas (`shatterDamage`) hacia los enemigos (capturables).
- **Lupa** (dorada): ya no desvía, **concentra**: lo que la atraviesa sigue recto ×`lensMul` (el Rayo, más grueso y ×`lensMul`).
  - Foco: cono de luz al suelo de debajo; el punto quema (`focusDps`). Gran angular: ×`wideMul` de tamaño.
    Convergencia: además, desvía hacia el enemigo más cercano.
- **Portal** (violeta, óvalo vertical): el primer clic pone A, el segundo B (después se va sustituyendo el más viejo). Lo que
  entra por uno **sale por el otro** con la misma velocidad: tus proyectiles, el Rayo (el haz salta) y **los enemigos** que lo
  atraviesan andando o empujados (los de suelo salen en el aire si B está alto y caen). Mismo enemigo: espera `portalCd`.
  - Bucle: cada paso suma ×`loopMul` de daño (hasta `loopMax`). Trampa: los enemigos salen aturdidos `trapStun` y
    vulnerables ×`trapVuln`. Atajo: tú también los atraviesas.

## Elemento (con 4 de ese elemento) y Duplicador

- Los proyectiles conservan su elemento. Los destellos, el haz extra, el foco y las esquirlas propias aplican hielo o fuego.
- **Duplicador**: espejos ×1,4 de largos y cada destello va a dos enemigos.

## Sinergias

- **Abanico** (sobre todo Facetas: cada esquirla se parte). **Rayo** (rebote y, con Lupa, mucho más daño en línea).
- **Tótem**: pon el espejo delante del tótem → todos sus cristales van a los enemigos y pegan más.
- **Ráfaga · Martillo + Portal**: lanzas al enemigo por el portal.
- **Singularidad · Arsenal**: los proyectiles que suelta pasan por el espejo y se desvían otra vez.

## Decisiones

- 2026-10-20 · Creada sin el usuario. El espejo desvía «hacia el enemigo más cercano» en vez de reflejar físicamente: con el
  espejo mirando hacia ti, un reflejo de verdad te devolvería tus disparos. Panel engrosado tras verlo en el navegador.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 1.2 | 0.2 – 6 (paso 0.05) | Base: recarga (s) |
| `range` | 380 | 80 – 800 (paso 10) | Base: distancia máxima |
| `half` | 44 | 10 – 120 (paso 1) | Base: medio largo del espejo |
| `life` | 7 | 1 – 20 (paso 0.5) | Base: duración (s) |
| `maxMirrors` | 2 | 1 – 5 (paso 1) | Base: espejos a la vez |
| `mirrorMul` | 1.25 | 1 – 3 (paso 0.05) | Base: × daño de lo desviado |
| `redirectRange` | 600 | 100 – 1200 (paso 10) | Base: alcance para buscar enemigo al desviar |
| `reflectDamage` | 14 | 1 – 60 (paso 1) | Base: daño de una bala enemiga devuelta |
| `glintEvery` | 1.4 | 0.2 – 5 (paso 0.05) | Base: destello propio cada (s) |
| `glintDamage` | 10 | 0 – 60 (paso 1) | Base: daño del destello |
| `glintRange` | 300 | 50 – 600 (paso 10) | Base: alcance del destello |
| `facetSpread` | 0.22 | 0.05 – 0.8 (paso 0.01) | Facetas: abertura de las copias (rad) |
| `sideBeamDps` | 30 | 0 – 150 (paso 1) | Facetas: daño/s del haz extra del Rayo |
| `shineEvery` | 0.5 | 0.1 – 2 (paso 0.05) | Facetas · Brillo: destello cada (s) |
| `shatterShards` | 6 | 1 – 16 (paso 1) | Facetas · Añicos: esquirlas |
| `shatterDamage` | 15 | 1 – 80 (paso 1) | Facetas · Añicos: daño |
| `lensMul` | 1.8 | 1 – 4 (paso 0.05) | Lupa: × daño de lo que la atraviesa |
| `wideMul` | 1.8 | 1 – 3 (paso 0.05) | Lupa · Gran angular: × tamaño |
| `focusDps` | 16 | 0 – 80 (paso 1) | Lupa · Foco: daño/s en el punto |
| `loopMul` | 1.3 | 1 – 3 (paso 0.05) | Portal · Bucle: × daño por pasada |
| `loopMax` | 3 | 1 – 6 (paso 1) | Portal · Bucle: pasadas que suman |
| `trapStun` | 1 | 0 – 3 (paso 0.1) | Portal · Trampa: aturdido (s) |
| `trapVuln` | 1.3 | 1 – 3 (paso 0.05) | Portal · Trampa: × daño que reciben |
| `portalCd` | 0.6 | 0.1 – 3 (paso 0.05) | Portal: s antes de que el mismo enemigo pueda volver a pasar |
<!-- /gen:tuning -->
