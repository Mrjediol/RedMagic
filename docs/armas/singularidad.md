# Singularidad (`sing`)

**Estado**: prototipo web (clips + jugable), pendiente de prueba. Código: `a2-singularidad.js`, `g-singularidad.js`.
Es la que más gustó al usuario (por la combinación con el Abanico).

## Rol

Orbe lento de vacío que viaja hacia el ratón, se detiene, **atrae** enemigos y proyectiles durante casi un segundo y colapsa
en un estallido. Control de masas y, sobre todo, **preparar a la otra arma**.

## Comportamiento (jugable)

- Clic = lanza el orbe hacia el ratón; se para en el ratón o a `range` px como mucho (y nunca por debajo de 30 px sobre el suelo).
- Viaje de `travelTime` s (frena al final, pequeño arco hacia arriba).
- Atrae `pullTime` s: los enemigos a menos de `pullRadius` se deslizan hacia él a `pullSpeed` (más fuerte cuanto más cerca;
  los congelados la mitad). Los voladores sólo se mueven en horizontal (salvo con Masa).
- **Captura**: los proyectiles del player marcados `capturable` que vuelan a menos de `captureRadius` quedan orbitando
  (la mitad trasera de la órbita detrás). Al colapsar salen en la dirección en que estaban, a `ejectSpeed`, con sus efectos.
- Colapso de 0,12 s (todo se cierra) y estallido: daño en área `burstDamage` en `burstRadius` (cae a la mitad en el borde),
  empuje hacia fuera `burstKnock` y un poco hacia arriba; **golpe fuerte** (rompe congelados → añicos). Hit-stop, impact frames.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Fauces** (`maw`) | Devora: lo que llega a la grieta recibe daño continuo y desaparece dentro. | **Hambre** (`hunger`): Cada baja alarga la grieta y la hace crecer.<br>**Digestión** (`swallow`): Se traga de golpe a los que tienen poca vida.<br>**Regurgitar** (`regurg`): Al colapsar escupe un proyectil teledirigido por cada baja. |
| **Errante** (`drift`) | Mientras atrae, la grieta se desplaza hacia el ratón arrastrando a los enemigos. | **Cazadora** (`hunt`): Va sola, más rápido, a por el enemigo más cercano.<br>**Rastro** (`trail`): Deja grietas pequeñas por el camino que atraen y dañan un momento.<br>**Masa** (`mass`): Más grande y dura más; arranca a los voladores y los estampa contra el suelo. |
| **Arsenal** (`arsenal`) | Captura más lejos; lo capturado sale reforzado y APUNTADO a los enemigos, no en todas direcciones. | **Recarga** (`reload`): Cada proyectil capturado recarga un poco tu otra arma.<br>**Multiplicar** (`multiply`): Cada proyectil capturado sale doble.<br>**Escudo de vacío** (`shield`): También captura las balas enemigas y las devuelve como tuyas. |
<!-- /gen:tree -->

Detalle:
- **Fauces**: la boca (`devourRadius`, aro que late) hace `devourDps` (daño continuo, en ticks de 0,2 s) a lo que llega. Una baja
  dentro alimenta a la grieta (destello y motas que entran).
  - Hambre: cada baja dentro +`hungerTime` s (máx. +2 s) y +`hungerGrow` de tamaño (hasta `hungerMax`).
  - Digestión: los que entran en la boca con menos de `swallowPct` de vida desaparecen al instante («TRAGADO») y cuentan como alimento.
  - Regurgitar: al colapsar, un orbe de vacío grande (estela gruesa, chispas) por cada baja dentro; persigue al más cercano y estalla
    en área al impactar (`regurgDamage` en `regurgRadius`, golpe fuerte).
- **Errante**: mientras atrae se desplaza hacia el ratón a `driftSpeed` (entre 60 px del techo y el suelo), arrastrando a los
  atrapados; atrae un 25 % más fuerte y levanta a los voladores.
  - Cazadora: va sola hacia el enemigo más cercano a `huntSpeed`.
  - Rastro: cada `trailEvery` s deja una grieta pequeña (`trailLife` s) que atrae y hace `trailDps`.
  - Masa: ×`massScale` de tamaño y ×`massTime` de tiempo; al estallar estampa a los voladores contra el suelo.
- **Arsenal**: radio de captura ×`arsenalRadius`; al colapsar, cada capturado sale **apuntado** a un enemigo (repartidos del más
  cercano al más lejano) con ×`arsenalMul` de daño.
  - Recarga: cada captura resta `reloadRefund` s a la recarga de tu otra arma.
  - Multiplicar: cada capturado sale doble (abierto ±0,12 rad; los proyectiles tienen `clone()`).
  - Escudo de vacío: captura también las balas enemigas; al soltarlas son tuyas (`shotDamage`). Para probarlo, «Los enemigos disparan».

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Fuego**: el estallido es de fuego (llamas que se abren por el suelo, brasas) y quema `burn` s. Contra congelados = **deshielo**.
- **Hielo**: el colapso congela a todos los alcanzados (nova de escarcha) → la otra arma los rompe.
- **Duplicador**: salen **dos orbes** en arco que, al llegar, giran uno alrededor del otro `mergeTime` s y abren **una sola grieta**
  ×`mergeScale` de tamaño y radios y ×`mergeDamage` de daño.

## Sinergias

- **Cualquier proyectil capturable** (hoy el Abanico): la Catapulta sale sola, sin mod. Idea a ampliar: bombas de la Semilla,
  hoja de la Media luna orbitando como sierra.
- **Reacciones**: junta enemigos congelados y el estallido los rompe a todos (añicos en cadena).
- **Golpe estelar**: los atraídos quedan quietos en una columna → la lanza los alcanza a todos.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 3.5 | 0.3 – 10 (paso 0.1) | Base: recarga (s) |
| `range` | 360 | 80 – 800 (paso 10) | Base: distancia máxima (px) |
| `travelTime` | 0.5 | 0.15 – 1.5 (paso 0.05) | Base: viaje (s) |
| `pullTime` | 1.8 | 0.2 – 5 (paso 0.05) | Base: tiempo atrayendo (s) |
| `pullRadius` | 150 | 40 – 400 (paso 5) | Base: radio de atracción |
| `pullSpeed` | 150 | 0 – 500 (paso 5) | Base: fuerza de atracción (px/s) |
| `burstDamage` | 40 | 0 – 200 (paso 1) | Base: daño del colapso |
| `burstRadius` | 130 | 30 – 350 (paso 5) | Base: radio del colapso |
| `burstKnock` | 380 | 0 – 900 (paso 10) | Base: empuje del colapso |
| `captureRadius` | 55 | 0 – 250 (paso 5) | Base: radio de captura de proyectiles |
| `ejectSpeed` | 900 | 200 – 2000 (paso 10) | Base: velocidad de salida de lo capturado |
| `burn` | 3 | 0 – 8 (paso 0.1) | Fuego: quemadura (s) |
| `devourRadius` | 40 | 10 – 120 (paso 2) | Fauces: radio de la boca |
| `devourDps` | 70 | 0 – 200 (paso 1) | Fauces: daño por segundo dentro |
| `hungerTime` | 0.5 | 0 – 2 (paso 0.05) | Fauces · Hambre: + s por baja |
| `hungerGrow` | 0.1 | 0 – 0.5 (paso 0.01) | Fauces · Hambre: + tamaño por baja |
| `hungerMax` | 1.6 | 1 – 3 (paso 0.05) | Fauces · Hambre: tamaño máximo |
| `swallowPct` | 0.35 | 0 – 0.6 (paso 0.01) | Fauces · Digestión: traga por debajo de (× vida) |
| `regurgDamage` | 45 | 1 – 150 (paso 1) | Fauces · Regurgitar: daño de cada proyectil |
| `regurgRadius` | 55 | 0 – 150 (paso 5) | Fauces · Regurgitar: radio del estallido al impactar |
| `driftSpeed` | 80 | 10 – 400 (paso 5) | Errante: velocidad (px/s) |
| `huntSpeed` | 130 | 10 – 500 (paso 5) | Errante · Cazadora: velocidad |
| `trailEvery` | 0.35 | 0.1 – 1.5 (paso 0.05) | Errante · Rastro: cada (s) |
| `trailLife` | 1.2 | 0.2 – 4 (paso 0.1) | Errante · Rastro: duración (s) |
| `trailDps` | 10 | 0 – 60 (paso 1) | Errante · Rastro: daño/s |
| `massScale` | 1.4 | 1 – 2.5 (paso 0.05) | Errante · Masa: escala |
| `massTime` | 1.35 | 1 – 3 (paso 0.05) | Errante · Masa: × tiempo atrayendo |
| `arsenalRadius` | 1.25 | 1 – 3 (paso 0.05) | Arsenal: × radio de captura |
| `arsenalMul` | 1.5 | 1 – 4 (paso 0.05) | Arsenal: × daño de lo capturado |
| `reloadRefund` | 0.2 | 0 – 1 (paso 0.01) | Arsenal · Recarga: s por captura |
| `shotDamage` | 12 | 1 – 60 (paso 1) | Arsenal · Escudo: daño de una bala devuelta |
| `look` | 3 | 0 – 5 (paso 1) | Aspecto: 0 anillas · 1 horizonte · 2 remolino · 3 grieta · 4 abismo · 5 lente |
| `over` | 0 | 0 – 1 (paso 1) | Vórtice por encima de los enemigos (0 = no, 1 = sí) |
| `mergeTime` | 0.3 | 0.05 – 1 (paso 0.05) | Duplicador: tiempo de fusión (s) |
| `mergeScale` | 1.35 | 1 – 2.5 (paso 0.05) | Duplicador: escala de la fusionada |
| `mergeDamage` | 1.7 | 1 – 4 (paso 0.05) | Duplicador: × daño de la fusionada |
<!-- /gen:tuning -->

## Visual

Aspecto actual: **Grieta** (`lookRift` + `riftSeed` + `burstRift` en fx-sing.js). Primera versión (anillas): `voidOrb` (fx.js): disco de acreción inclinado (`singularityDisk`, mitad de arriba detrás), núcleo oscuro con aro
(`singularityCore`), partículas en espiral, anillos de succión, polvo del suelo arrastrado (si está cerca del suelo), estela de
distorsión en el viaje y líneas que convergen en el colapso. `voidBurst`: destello, onda de hebras parciales, anillo de suelo,
agujas, chispas, gotas, polvo; + grietas/escombros (Masa) o llamas/brasas (Fuego).

### Ronda de aspectos (2026-10-19) — elegida la Grieta

El usuario: la primera versión «eran simplemente anillas con huecos incluso entre ellas» y es el arma que menos le convencía
visualmente. Cinco aspectos con superficies continuas en `fx-sing.js` (clips: «2b · Singularidad — variantes visuales» en 🛠 En curso;
jugable: Ajustes de la Singularidad ▸ `look` 0-5 y `over`):
**Horizonte de sucesos** (disco liso con degradado, seda que gira, lado brillante por Doppler y anillo de luz sobre la sombra),
**Remolino** (brazos en espiral de tinta), **Grieta** (desgarro vertical con estrellas, bordes que arden, escombros que se cuelan),
**Abismo** (rejilla del espacio que se hunde), **Lente** (refracta y retuerce lo que hay detrás, enemigos incluidos). Estallido
refinado común: onda continua + destello alargado (la Grieta se cierra en una línea de luz; el Abismo hace ondular la rejilla).
Interruptor de capas: con profundidad (mitad trasera detrás de los enemigos) o todo por encima.

## Sonido

`sing_launch`, zumbido en bucle mientras atrae (dos sierras graves con filtro que se abre + trémolo; en la web jugable se genera
en `g-singularidad.js`), `sing_collapse`, `sing_burst` / `sing_burst_fire`, `sing_slam`, `sing_capture`, `sing_eject`.

## Port a Unity (plan)

Arma de recarga larga. La captura es un **sistema genérico** (componente en la singularidad + interfaz «capturable» en los
proyectiles), no lógica del Abanico. Atracción como fuerza sobre el `Rigidbody2D`/movimiento del enemigo (respetando
congelado y estados).

## Decisiones

- 2026-10-19 · Tras la prueba del usuario: Regurgitar «apenas se nota, demasiado débil» → orbe grande que estalla en área (45 de
  daño, golpe fuerte). La captura saltaba desde demasiado lejos y las esquirlas «desaparecían» → radio 55 (antes 95; Arsenal ×1,25)
  y entran girando suave en 0,18 s desde donde estaban, con un destello.

- 2026-10-19 · Árbol: Fauces / Errante / Arsenal (la Masa pasa a ser mejora de Errante). Fauces necesita matar dentro para que
  Hambre y Regurgitar sirvan: `devourDps` 70 y Digestión al 35 % (con 30 / 25 % no llegaba a matar contra 120 de vida).

- 2026-10-19 · **Aspecto elegido: Grieta** (`look` 3, por defecto). Viaja un orbe pequeño con la grieta anunciada en vertical
  (`riftSeed`) y, al llegar, la grieta se abre en el sitio: línea de luz que crece y se rasga con destello y chispas (`openT`).
  El usuario: ver la grieta volando «queda raro». Más duración: atrae 1,8 s (antes 0,8) y recarga 3,5 s (antes 2,5). Sonido `sing_tear`.

- 2026-10-19 · Capas: todo el vórtice se dibuja delante de los enemigos (`voidOrbOver` en fx.js) y el disco se ordena sólo respecto a su
  núcleo; antes la mitad trasera iba detrás de los enemigos y un enemigo dentro del disco cortaba el anillo (lo vio el usuario).
  Para que se lea «se lo traga», el atrapado se estira hacia el centro y se tiñe de violeta (`e.suck`, `drawDummy`).

- 2026-10-19 · La Catapulta no es un mod: es la regla genérica de captura (el usuario la señaló como lo mejor).

## Preguntas abiertas

- ¿Qué más debe ser capturable (bombas, hoja, proyectiles enemigos → escudo)?
