# Ráfaga (`gust`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-rafaga.js`; dibujo en `fx-armas2.js` (`gustCone`, `windSwirl`, `tornadoFx`, `windCurrent`, `ramCharge`, `ramRing`).
Paleta nueva `wind` (blanco azulado). Reglas comunes: [README](README.md) · progresión: [progresion.md](progresion.md).

## Rol

**Control: recolocar enemigos.** Soplo en cono con poco daño y mucho empuje; los enemigos salen **lanzados** (regla común): si
chocan con otro a velocidad, daño a los dos (contra un congelado = añicos). Sirve para meterlos en la Singularidad, en la
columna del Golpe estelar, sobre las minas de la Semilla o en la burbuja del Reloj. Disipa las balas enemigas del cono.

## Comportamiento (base)

- Clic = soplo desde el cristal hacia el ratón: cono de `range` px y medio ángulo `half`. A cada enemigo dentro: `damage`
  (menos cuanto más lejos, hasta −40 %), empuje `push` en la dirección del soplo (los de suelo dan un saltito `lift`) y quedan
  **lanzados** `launchTime` s (resbalan mucho más mientras: chocan). Retroceso pequeño del player.
- Las balas enemigas del cono se deshacen.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Tornado** (`tornado`) | Cada soplo deja un tornado que avanza, levanta a los enemigos y los mantiene girando en el aire: ahí arriba reciben más daño de todo. | **Persecución** (`chase`): El tornado va hacia donde apuntas.<br>**Estampida** (`slam`): Al deshacerse, estampa contra el suelo a los que llevaba (golpe fuerte).<br>**Absorción** (`absorb`): Si toca fuego (suelo ardiendo, un quemado) se vuelve tornado de fuego; si toca un congelado, de hielo. |
| **Corriente** (`current`) | El soplo deja una corriente de viento: arrastra a los enemigos en su dirección y lleva a TUS PROYECTILES (de cualquier arma) a favor, más rápidos y fuertes. | **Viento de cola** (`tail`): Dentro de la corriente corres más y saltas más alto.<br>**Filo** (`blade`): La corriente corta: daño continuo a los que están dentro.<br>**Remolque** (`tow`): Arrastra a los enemigos hasta el final de la corriente y, al acabarse, los lanza por los aires. |
| **Martillo** (`ram`) | Mantén para comprimir el aire y suelta: un cañonazo estrecho que dispara al enemigo como un proyectil (más carga = más lejos y más fuerte). | **Bolos** (`pins`): A quien golpea un lanzado sale lanzado también.<br>**Estampido** (`crash`): Cuando el lanzado se para, estalla en área.<br>**Contragolpe** (`counter`): El cañonazo devuelve las balas enemigas y redirige tus proyectiles en su dirección, reforzados. |
<!-- /gen:tree -->

Detalle:
- **Tornado** (recarga `tornadoCooldown`): el soplo deja un tornado (`tornadoW` × `tornadoH`) que avanza `tornadoSpeed` hacia
  donde soplaste durante `tornadoTime` s. Atrapa hasta `tornadoMax` enemigos (también voladores), los sube y los hace girar;
  `tornadoDps` y **vulnerables** ×`tornadoVuln` (reciben más de **todo**, también de tu otra arma). Al deshacerse caen.
  - Persecución: va hacia el ratón (`chaseSpeed`). Estampida: al deshacerse los estampa (`slamDamage`, golpe fuerte; los
    voladores quedan estampados en el suelo). Absorción: si pasa por suelo ardiendo o lleva a un quemado → tornado de fuego
    (quema); si lleva a un congelado/casi congelado → de hielo (congela). Sólo una vez y sólo si la build no da ya elemento.
- **Corriente** (recarga `currentCooldown`): franja de viento (`currentLen` × 2·`currentWid`) desde el cristal en la dirección
  del soplo, `currentTime` s. Arrastra a los enemigos a favor (`currentPush`) y **tus proyectiles** (cualquiera con velocidad:
  esquirlas, balas devueltas…) que la cruzan se alinean con ella, ×`currentProj` de velocidad y ×`currentDmg` de daño (una vez
  por corriente). Las balas enemigas se desvían a favor. **Sigue funcionando con la otra arma en la mano.**
  - Viento de cola: dentro corres ×`tailMove` y saltas ×`tailJump`. Filo: `bladeDps` a los de dentro. Remolque: los arrastra
    al final (`towSpeed`) y al acabarse los lanza hacia arriba (`towDamage`, golpe fuerte).
- **Martillo** (recarga `ramCooldown`): mantener = comprimir (hasta `ramCharge` s, espiral en el cristal); soltar = cañonazo
  estrecho (`ramHalf`) que dispara **al más cercano** del cono: daño `ramMin`→`ramMax` y empuje `ramPushMin`→`ramPushMax`
  según la carga; con carga llena, golpe fuerte e impact frame. El lanzado es un proyectil que choca con los demás.
  - Bolos: a quien golpea el lanzado sale lanzado también (cadenas). Estampido: cuando el lanzado se para, estalla en área
    (`crashRadius`, `crashDamage`). Contragolpe: devuelve las balas enemigas como tuyas y **redirige tus proyectiles** del cono
    en la dirección del cañonazo, ×`counterMul` de daño.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: cada soplo congela un poco (`iceFreeze`) → los lanzados chocan contra congelados = añicos.
- **Fuego**: quema (`fireBurn`) y **aviva**: si golpea a un quemado, la quemadura salta a los que estén a `fireSpread`.
- **Duplicador**: un segundo soplo 0,12 s después, un poco desviado (dos tornados / dos corrientes).

## Sinergias

- **Singularidad / Reloj (burbuja)**: amontonas a todos en el centro. **Golpe estelar**: los alineas en la columna.
- **Abanico** a favor de una **Corriente**: esquirlas más rápidas y fuertes; con el Martillo · Contragolpe, recoges las
  esquirlas que fallaron y las mandas al enemigo.
- **Hielo**: congelas con la otra arma y estampas a uno contra el congelado → añicos.
- **Tornado** = vulnerables: es el momento de la lanza del Golpe estelar o de la Sobrecarga del Rayo.

## Decisiones

- 2026-10-20 · Creada sin el usuario. Se añadió al motor la regla «lanzado» (`o.launch`, `o.pins`, `launchCollide`; mientras
  dura, el enemigo resbala ×12 más) y `makeVuln`.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.85 | 0.1 – 4 (paso 0.05) | Base: recarga (s) |
| `range` | 230 | 60 – 500 (paso 5) | Base: alcance del soplo |
| `half` | 0.5 | 0.1 – 1.4 (paso 0.02) | Base: medio ángulo del cono (rad) |
| `damage` | 10 | 0 – 60 (paso 1) | Base: daño |
| `push` | 560 | 0 – 1500 (paso 10) | Base: empuje |
| `lift` | 200 | 0 – 800 (paso 10) | Base: salto que da a los de suelo |
| `launchTime` | 0.6 | 0 – 2 (paso 0.05) | Base: s que va lanzado (choca) |
| `iceFreeze` | 0.35 | 0 – 1 (paso 0.05) | Hielo: congelación por soplo |
| `fireBurn` | 1.5 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `fireSpread` | 90 | 0 – 250 (paso 5) | Fuego: aviva — radio al que salta la quemadura |
| `tornadoCooldown` | 2 | 0.3 – 6 (paso 0.1) | Tornado: recarga (s) |
| `tornadoSpeed` | 90 | 0 – 300 (paso 5) | Tornado: velocidad |
| `tornadoTime` | 2.6 | 0.5 – 8 (paso 0.1) | Tornado: duración (s) |
| `tornadoW` | 55 | 20 – 150 (paso 1) | Tornado: ancho |
| `tornadoH` | 170 | 60 – 300 (paso 5) | Tornado: alto |
| `tornadoMax` | 6 | 1 – 12 (paso 1) | Tornado: enemigos que lleva |
| `tornadoDps` | 16 | 0 – 80 (paso 1) | Tornado: daño/s |
| `tornadoVuln` | 1.3 | 1 – 3 (paso 0.05) | Tornado: × daño que reciben en el aire |
| `chaseSpeed` | 170 | 20 – 400 (paso 5) | Tornado · Persecución: velocidad |
| `slamDamage` | 35 | 0 – 150 (paso 1) | Tornado · Estampida: daño |
| `currentCooldown` | 2.6 | 0.3 – 8 (paso 0.1) | Corriente: recarga (s) |
| `currentLen` | 380 | 100 – 800 (paso 10) | Corriente: largo |
| `currentWid` | 45 | 15 – 120 (paso 1) | Corriente: medio ancho |
| `currentTime` | 3.5 | 0.5 – 10 (paso 0.1) | Corriente: duración (s) |
| `currentPush` | 110 | 0 – 400 (paso 5) | Corriente: arrastre (px/s) |
| `currentProj` | 1.35 | 1 – 3 (paso 0.05) | Corriente: × velocidad de tus proyectiles |
| `currentDmg` | 1.35 | 1 – 3 (paso 0.05) | Corriente: × daño de tus proyectiles |
| `tailMove` | 1.4 | 1 – 2.5 (paso 0.05) | Corriente · Viento de cola: × velocidad |
| `tailJump` | 1.3 | 1 – 2 (paso 0.05) | Corriente · Viento de cola: × salto |
| `bladeDps` | 14 | 0 – 80 (paso 1) | Corriente · Filo: daño/s |
| `towSpeed` | 170 | 20 – 500 (paso 5) | Corriente · Remolque: arrastre |
| `towDamage` | 25 | 0 – 120 (paso 1) | Corriente · Remolque: daño al lanzarlos |
| `ramCharge` | 0.8 | 0.1 – 2 (paso 0.05) | Martillo: carga máxima (s) |
| `ramCooldown` | 1.1 | 0.1 – 4 (paso 0.05) | Martillo: recarga (s) |
| `ramRange` | 320 | 80 – 600 (paso 10) | Martillo: alcance |
| `ramHalf` | 0.25 | 0.05 – 0.8 (paso 0.01) | Martillo: medio ángulo |
| `ramMin` | 15 | 0 – 100 (paso 1) | Martillo: daño sin carga |
| `ramMax` | 50 | 0 – 200 (paso 1) | Martillo: daño con carga |
| `ramPushMin` | 500 | 0 – 2000 (paso 10) | Martillo: empuje sin carga |
| `ramPushMax` | 1300 | 0 – 3000 (paso 10) | Martillo: empuje con carga |
| `crashRadius` | 85 | 20 – 250 (paso 5) | Martillo · Estampido: radio |
| `crashDamage` | 30 | 0 – 150 (paso 1) | Martillo · Estampido: daño |
| `counterMul` | 1.5 | 1 – 4 (paso 0.05) | Martillo · Contragolpe: × daño de lo redirigido |
<!-- /gen:tuning -->
