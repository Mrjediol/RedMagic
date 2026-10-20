# Látigo de luz (`whip`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-latigo.js`; dibujo en `fx-armas2.js` (`whipRope`, `whipCrack`, `lightChain`). Reglas comunes: [README](README.md) ·
progresión: [progresion.md](progresion.md).

## Rol

**Cuerpo a cuerpo de medio alcance con habilidad de distancia.** Latigazo en arco desde arriba hacia donde apuntas: el cuerpo
pega poco, **la punta chasquea** (×`tipMul`, golpe fuerte → rompe congelados). Premia colocarse a la distancia justa. Es el
arma que **conecta enemigos** (Cadena), **los trae** (Garfio) o **los rodea** (Remolino).

## Comportamiento (base)

- Clic = latigazo de `sweepTime` s: el mango gira de arriba (1,15 rad por encima) a un poco por debajo del ratón y cada trozo
  va con retraso, así la punta «latiguea». Alcance `reach`. Cada enemigo, un golpe por latigazo: si lo toca el último tramo
  (desde `tipZone`) es **punta** (destello en cruz + chispas + sonido de chasquido); si no, golpe de cuerpo.
- Sin acertar a nadie, chasquido al aire (feedback).

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Cadena** (`link`) | El latigazo ENCADENA a los que golpea (si es uno, con el más cercano): mientras dura, parte del daño que recibe uno — de CUALQUIER arma — les llega a los demás. | **Grillete** (`shackle`): Los encadenados se atraen entre sí: no pueden separarse.<br>**Conducción** (`conduct`): Por la cadena pasan también el hielo, el fuego y las marcas.<br>**Ruptura** (`snap`): Al romperse la cadena (se acaba o muere un eslabón) estalla en cada enemigo. |
| **Garfio** (`hook`) | Mantén: la punta engancha al enemigo y lo arrastra hacia ti. Al llegar (o al soltar) queda aturdido y vulnerable un momento. | **Lanzamiento** (`fling`): Soltar mientras arrastras lo lanza hacia donde apuntas: choca con los demás.<br>**Red** (`reel`): Engancha a todos los que toca el latigazo (hasta 3).<br>**Remate** (`finish`): Cuando llega a ti, onda de choque alrededor (golpe fuerte). |
| **Remolino** (`spin`) | Mantén: giras el látigo a tu alrededor golpeando todo lo que toca (te mueves más lento). Al soltar, latigazo final fuerte hacia donde apuntas. | **Vendaval** (`gale`): El remolino atrae a los enemigos cercanos hacia el aro.<br>**Aceleración** (`accel`): Cuanto más tiempo giras, más rápido va y más daño hace.<br>**Desvío** (`deflect`): El aro devuelve las balas enemigas que toca, como tuyas. |
<!-- /gen:tree -->

Detalle:
- **Cadena**: al acabar el latigazo, los golpeados quedan **encadenados** `linkTime` s (si sólo golpeó a uno, con el más cercano
  a `linkRange`; máx. 3 cadenas a la vez). Mientras: **cualquier golpe de arma** a un eslabón (también los de tu otra arma,
  **con el látigo guardado**) pasa ×`linkShare` a los demás (eslabones que brillan). Esos golpes compartidos son `proc` (no se
  vuelven a compartir).
  - Grillete: se atraen hacia el centro de la cadena (`shackleSpeed`) → montón para áreas. Conducción: pasa también el
    hielo (60 %), el fuego y las marcas del Cazador. Ruptura: cuando se acaba o muere un eslabón, estallido en cada uno
    (`snapDamage`, `snapRadius`).
- **Garfio**: si mantienes y la **punta** toca a alguien, queda enganchado: lo arrastras hacia ti (`hookSpeed`; voladores a la
  altura del cristal). Al llegar (o al soltar): aturdido `hookStun` y **vulnerable** ×`hookVuln` `hookVulnTime` s.
  - Lanzamiento: soltar mientras arrastras lo **lanza** hacia el ratón (`flingSpeed`, regla «lanzado»: choca con los demás).
    Red: engancha a todos los que toca el latigazo (hasta `reelMax`). Remate: al llegar, onda (`finishDamage`, golpe fuerte).
- **Remolino**: mantener = el látigo gira a tu alrededor (`spinRate`), cada enemigo recibe `spinDamage` cada `spinTick` s
  (punta ×1,5); vas a ×`spinMove`. Al soltar, latigazo final dorado ×`finalMul` (más largo, golpe fuerte).
  - Vendaval: atrae hacia el aro a los que están a `galeRadius`. Aceleración: giro y daño suben hasta ×`accelMax` en
    `accelTime` s (el latigazo final también). Desvío: el aro devuelve las balas enemigas como tuyas.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: congela `iceFreeze` por golpe (la punta, el doble) → la siguiente punta lo rompe (añicos).
- **Fuego**: quema `fireBurn`.
- **Duplicador**: segundo latigazo 0,07 s después, un poco más bajo; en Remolino, dos látigos opuestos.

## Sinergias

- **Cadena + armas de golpe único fuerte** (Golpe estelar, Singularidad, Sobrecarga del Rayo): un golpe se reparte a todos.
  Con **Reloj · Estasis**, lo compartido también se guarda.
- **Garfio + Lanzamiento + Ráfaga · Bolos**: cadenas de choques. Garfio deja **vulnerable**: momento de cambiar al arma fuerte.
- **Hielo + punta**: congelas con el cuerpo y rompes con la punta.

## Decisiones

- 2026-10-20 · Creada sin el usuario.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.5 | 0.1 – 3 (paso 0.05) | Base: recarga (s) |
| `reach` | 190 | 60 – 400 (paso 5) | Base: alcance |
| `sweepTime` | 0.16 | 0.06 – 0.5 (paso 0.01) | Base: duración del latigazo (s) |
| `damage` | 12 | 1 – 60 (paso 1) | Base: daño del cuerpo |
| `tipMul` | 2.2 | 1 – 5 (paso 0.05) | Base: × daño de la punta (golpe fuerte) |
| `tipZone` | 0.72 | 0.3 – 0.95 (paso 0.01) | Base: dónde empieza la punta (fracción) |
| `knock` | 140 | 0 – 500 (paso 5) | Base: empuje |
| `iceFreeze` | 0.3 | 0 – 1 (paso 0.05) | Hielo: congelación por golpe (punta ×2) |
| `fireBurn` | 1.5 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `linkTime` | 6 | 1 – 15 (paso 0.5) | Cadena: duración (s) |
| `linkShare` | 0.35 | 0 – 1 (paso 0.05) | Cadena: fracción del daño que pasa a los demás |
| `linkRange` | 220 | 50 – 500 (paso 10) | Cadena: alcance para encadenar al más cercano |
| `shackleSpeed` | 80 | 0 – 300 (paso 5) | Cadena · Grillete: atracción |
| `snapDamage` | 25 | 0 – 120 (paso 1) | Cadena · Ruptura: daño |
| `snapRadius` | 70 | 0 – 200 (paso 5) | Cadena · Ruptura: radio |
| `hookSpeed` | 560 | 100 – 1500 (paso 10) | Garfio: velocidad del arrastre |
| `hookStun` | 0.8 | 0 – 3 (paso 0.1) | Garfio: aturdido al llegar (s) |
| `hookVuln` | 1.4 | 1 – 3 (paso 0.05) | Garfio: × daño que recibe al llegar |
| `hookVulnTime` | 2 | 0 – 6 (paso 0.1) | Garfio: s vulnerable |
| `flingSpeed` | 950 | 200 – 2000 (paso 10) | Garfio · Lanzamiento: velocidad |
| `finishDamage` | 30 | 0 – 150 (paso 1) | Garfio · Remate: daño |
| `finishRadius` | 90 | 20 – 250 (paso 5) | Garfio · Remate: radio |
| `reelMax` | 3 | 1 – 6 (paso 1) | Garfio · Red: enganchados máx. |
| `spinTick` | 0.22 | 0.05 – 1 (paso 0.01) | Remolino: cada cuánto golpea al mismo (s) |
| `spinDamage` | 9 | 1 – 50 (paso 1) | Remolino: daño por golpe |
| `spinRate` | 9 | 2 – 25 (paso 0.5) | Remolino: giro (rad/s) |
| `spinMove` | 0.75 | 0.2 – 1 (paso 0.05) | Remolino: × tu velocidad |
| `finalMul` | 2.5 | 1 – 6 (paso 0.1) | Remolino: × daño del latigazo final |
| `galeRadius` | 260 | 50 – 500 (paso 10) | Remolino · Vendaval: radio |
| `galeSpeed` | 90 | 0 – 300 (paso 5) | Remolino · Vendaval: atracción |
| `accelMax` | 2 | 1 – 4 (paso 0.1) | Remolino · Aceleración: × máximo |
| `accelTime` | 2 | 0.3 – 6 (paso 0.1) | Remolino · Aceleración: s hasta el máximo |
<!-- /gen:tuning -->
