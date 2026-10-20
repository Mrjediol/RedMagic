# Jabalina (`spear`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-jabalina.js`; dibujo en `fx-armas2.js` (`crystalSpear`, `rodSparks`, `rodBolt`, `blinkTrail`). Paleta nueva `storm`
(violeta eléctrico, sólo el Pararrayos). Reglas comunes: [README](README.md) · progresión: [progresion.md](progresion.md).

## Rol

**Un solo proyectil que hay que recuperar.** Mantén para cargar, suelta: jabalina de cristal en parábola que atraviesa a uno y
**se queda clavada** (suelo, plataforma o enemigo). Mientras está fuera, el arma no dispara: o pasas por encima (la recoges al
instante) o pulsas otra vez (vuelve volando, y recarga corta). Ese hueco es el momento de **cambiar a la otra arma**, y las
evoluciones convierten la jabalina clavada en algo útil mientras tanto.

## Comportamiento (base)

- Mantener = carga (`chargeTime`; la jabalina se echa atrás en la mano y brilla al máximo). Soltar = lanza a
  `speedMin`→`speedMax` con gravedad `gravity`; daño `dmgMin`→`dmgMax`; con carga llena, golpe fuerte + impact frame.
- Atraviesa `pierce` enemigos; en el siguiente se clava (lo sigue). En suelo/plataforma se clava con su ángulo (grietas).
- Clavada en el suelo: pasar a `pickupRadius` la recoge (tras 0,5 s de gracia, para que no se recoja sola al caer a tus pies;
  aro que late = «recógela»). Pulsar = vuelve (`recallTime`). A los `stickTime` s vuelve sola. En mano: `throwCd`.
- Es **capturable** por la Singularidad (vuela y sale despedida con sus efectos) y la aceleran el Reloj · Aceleración y la
  Ráfaga · Corriente.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Pararrayos** (`rod`) | Clavada, es un pararrayos: cada golpe de CUALQUIERA de tus armas a un enemigo cerca de ella hace caer un rayo desde la jabalina, que salta a otro. | **Tormenta** (`storm`): Además, lanza un rayo sola cada poco al enemigo más cercano.<br>**Conductor** (`conduct`): Clavada en un enemigo, su radio crece y cada golpe a ese enemigo electrocuta a todos los de alrededor.<br>**Descarga** (`dump`): Cada rayo carga la jabalina; al recuperarla, suelta toda la carga en una explosión. |
| **Empalar** (`impale`) | Cargada, se lleva al primer enemigo ensartado y lo deja clavado donde para: inmóvil y vulnerable un rato. | **Brocheta** (`skewer`): Ensarta a todos los que atraviesa (hasta 4).<br>**Clavado** (`nail`): Los clavados reciben aún más daño y se quedan más tiempo.<br>**Arrancar** (`rip`): Al recuperarla la arrancas: daño fuerte a los clavados y los arrastra hacia ti. |
| **Destello** (`blink`) | Pulsar con la jabalina fuera te TELETRANSPORTA a ella, con onda de choque al llegar, y vuelve a tu mano. | **Estela** (`trail`): Atraviesas y dañas a todo lo que hay entre donde estabas y la jabalina.<br>**Fase** (`phase`): Al llegar eres intocable un momento y tu siguiente ataque (de cualquier arma) es golpe fuerte.<br>**Relevo** (`relay`): Cada destello recarga un poco tu otra arma. |
<!-- /gen:tree -->

Detalle:
- **Pararrayos** (paleta tormenta): clavada, desde su extremo de arriba, cualquier golpe de **tus armas** (también con la
  jabalina fuera de la mano, que es lo normal) a un enemigo a menos de `rodRadius` hace caer un rayo a ese enemigo
  (`rodDamage`) que salta al más cercano (×`rodChain`). Mínimo `rodGap` s entre rayos al mismo enemigo.
  - Tormenta: rayo solo cada `stormEvery` s al más cercano. Conductor: clavada **en un enemigo**, radio ×`conductRadius` y cada
    golpe a ese enemigo electrocuta a todos los de alrededor. Descarga: cada rayo suma una carga (⚡N encima); al recogerla o
    hacerla volver, explosión de `dumpPer` × cargas (máx. 150) en `dumpRadius`.
- **Empalar**: con al menos `impaleMin` de carga, el primer enemigo que toca se queda **ensartado** y viaja con la jabalina; donde
  se clava, queda **clavado** `pinTime` s (inmóvil, aturdido) y **vulnerable** ×`pinVuln`. Los voladores acaban clavados en el suelo.
  - Brocheta: ensarta a todos los que atraviesa (hasta `skewerMax`). Clavado: ×`nailVuln` y ×`nailTime` de tiempo.
    Arrancar: al hacerla volver, `ripDamage` (golpe fuerte) a los clavados y los arrastra hacia ti.
- **Destello**: pulsar con la jabalina fuera te **teletransporta** a ella (si está en un enemigo, a su lado), onda de choque al
  llegar (`blinkDamage`, `blinkRadius`) y vuelve a tu mano lista.
  - Estela: daño (golpe fuerte) a todo lo que hay en la línea entre donde estabas y donde llegas. Fase: `phaseTime` s intocable y
    el siguiente ataque **de cualquier arma** hace ×`phaseMul` y es golpe fuerte. Relevo: cada destello quita `relayRefund` s
    a la recarga de la otra arma.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: el impacto congela `iceFreeze` (con la carga llena, golpe fuerte → contra un congelado, añicos).
- **Fuego**: quema `fireBurn` y donde se clava en el suelo deja una zona ardiendo.
- **Duplicador**: dos jabalinas un poco abiertas; pulsar las trae a las dos (Destello te lleva a la primera).

## Sinergias

- **Pararrayos + armas de muchos golpes** (Abanico, Rayo, Látigo · Remolino): cada golpe cerca de la jabalina = rayo extra.
- **Empalar + Golpe estelar / Reloj · Estasis**: el clavado no se mueve y recibe más.
- **Destello · Fase + Golpe estelar o Singularidad**: llegas y el siguiente golpe fuerte lo potencia.
- **Ráfaga · Corriente**: la jabalina que la cruza va más lejos y pega más.

## Decisiones

- 2026-10-20 · Creada sin el usuario. Radio del pararrayos subido de 170 a 220 tras medir: la jabalina suele clavarse a
  ~150 px del enemigo y con 170 casi no alcanzaba.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `chargeTime` | 0.5 | 0.05 – 2 (paso 0.05) | Base: carga máxima (s) |
| `speedMin` | 700 | 200 – 2000 (paso 10) | Base: velocidad sin carga |
| `speedMax` | 1250 | 200 – 2500 (paso 10) | Base: velocidad con carga |
| `gravity` | 420 | 0 – 1500 (paso 10) | Base: gravedad (parábola) |
| `dmgMin` | 25 | 1 – 150 (paso 1) | Base: daño sin carga |
| `dmgMax` | 70 | 1 – 300 (paso 1) | Base: daño con carga (golpe fuerte) |
| `pierce` | 1 | 0 – 5 (paso 1) | Base: enemigos que atraviesa antes de clavarse |
| `knock` | 160 | 0 – 600 (paso 5) | Base: empuje |
| `throwCd` | 0.25 | 0 – 3 (paso 0.05) | Base: recarga tras tenerla en la mano (s) |
| `stickTime` | 6 | 1 – 20 (paso 0.5) | Base: s clavada antes de volver sola |
| `pickupRadius` | 45 | 10 – 120 (paso 1) | Base: radio para recogerla |
| `recallTime` | 0.3 | 0.1 – 1 (paso 0.05) | Base: vuelo de vuelta (s) |
| `iceFreeze` | 0.6 | 0 – 1 (paso 0.05) | Hielo: congelación del impacto |
| `fireBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (y suelo ardiendo donde se clava) |
| `rodRadius` | 220 | 40 – 500 (paso 5) | Pararrayos: radio |
| `rodDamage` | 12 | 1 – 60 (paso 1) | Pararrayos: daño del rayo |
| `rodChain` | 0.6 | 0 – 1.5 (paso 0.05) | Pararrayos: × daño del salto |
| `rodGap` | 0.2 | 0.03 – 1 (paso 0.01) | Pararrayos: s mínimo entre rayos al mismo enemigo |
| `stormEvery` | 0.9 | 0.2 – 4 (paso 0.05) | Pararrayos · Tormenta: cada (s) |
| `conductRadius` | 1.5 | 1 – 3 (paso 0.05) | Pararrayos · Conductor: × radio clavada en un enemigo |
| `dumpPer` | 8 | 0 – 40 (paso 1) | Pararrayos · Descarga: daño por carga |
| `dumpRadius` | 120 | 30 – 300 (paso 5) | Pararrayos · Descarga: radio |
| `impaleMin` | 0.5 | 0 – 1 (paso 0.05) | Empalar: carga mínima para ensartar |
| `pinTime` | 2.5 | 0.3 – 8 (paso 0.1) | Empalar: s clavado |
| `pinVuln` | 1.3 | 1 – 3 (paso 0.05) | Empalar: × daño que recibe clavado |
| `skewerMax` | 4 | 1 – 8 (paso 1) | Empalar · Brocheta: ensartados máx. |
| `nailVuln` | 1.6 | 1 – 4 (paso 0.05) | Empalar · Clavado: × daño (en vez de pinVuln) |
| `nailTime` | 1.6 | 1 – 3 (paso 0.05) | Empalar · Clavado: × tiempo |
| `ripDamage` | 40 | 0 – 200 (paso 1) | Empalar · Arrancar: daño |
| `blinkDamage` | 25 | 0 – 150 (paso 1) | Destello: daño de la onda |
| `blinkRadius` | 90 | 20 – 250 (paso 5) | Destello: radio |
| `trailDamage` | 30 | 0 – 150 (paso 1) | Destello · Estela: daño |
| `phaseMul` | 1.6 | 1 – 4 (paso 0.05) | Destello · Fase: × daño del siguiente ataque |
| `phaseTime` | 0.8 | 0 – 3 (paso 0.05) | Destello · Fase: s intocable |
| `relayRefund` | 1.5 | 0 – 6 (paso 0.1) | Destello · Relevo: s de recarga a la otra arma |
<!-- /gen:tuning -->
