# Reloj de arena (`time`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 en una
sesión sin él). Código: `g-reloj.js` (jugable); dibujo en `fx-armas2.js` (`chronoBubble`, `chronoOrb`, `echoRing`,
`stasisBreak`, `speedStreaks`). Paleta nueva `chrono` (verde agua) en `core.js`. Reglas comunes: [README](README.md) ·
progresión: [progresion.md](progresion.md).

## Rol

**Arma de apoyo que se luce con la otra.** Lanza un orbe que abre una **burbuja de tiempo**: dentro los enemigos y sus balas
van lentos; al cerrarse, golpe de reloj. Sola hace poco daño: su gracia es lo que le hace a tu otra arma — la Estasis guarda
el daño que les metas, el Eco repite tus golpes y la Aceleración recarga tus dos armas. Todo funciona **con el Reloj guardado**:
abres la burbuja, cambias de arma (Q) y castigas.

## Comportamiento (base)

- Clic = orbe que viaja `travelTime` s hasta donde apuntas (máx. `range`, nunca bajo el suelo) y abre la burbuja (`radius`,
  `duration` s; un arco alrededor marca lo que le queda).
- Dentro: los enemigos se mueven a `slowK` (andar, empujones, gravedad, ataques, quemadura y congelación también van lentos —
  tiempo propio del enemigo, `e.timeK`); las balas enemigas a `shotSlow`.
- Al cerrarse: golpe de reloj (`closeDamage`) a los de dentro.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Estasis** (`stasis`) | La burbuja DETIENE el tiempo: los de dentro se quedan quietos y GUARDAN todo el daño que reciben (también el de tu otra arma); al cerrarse lo reciben de golpe, aumentado. | **Interés** (`interest`): El daño guardado crece por cada segundo que pasa dentro.<br>**Onda** (`quake`): Al soltarse, parte del daño guardado salta en área a los que están cerca.<br>**Paradoja** (`paradox`): Si alguno muere al soltarse, se abre otra burbuja de estasis más pequeña en su sitio. |
| **Eco** (`echo`) | Cada golpe de tus armas a un enemigo dentro de la burbuja se repite un instante después (eco a la mitad, con elemento y empuje). | **Reverberación** (`reverb`): Cada eco deja otro eco más débil.<br>**Eco fuerte** (`heavyEcho`): Los ecos son golpes fuertes: rompen a los congelados (añicos).<br>**Ancla** (`anchor`): La burbuja se abre a tu alrededor al instante y te sigue. |
| **Aceleración** (`quick`) | Dentro de la burbuja TU tiempo corre: si estás dentro, tus dos armas recargan mucho más deprisa y te mueves más rápido; tus proyectiles que la cruzan salen acelerados y con más daño. | **Inercia** (`inertia`): Al salir de la burbuja (o al cerrarse) la aceleración te dura unos segundos más.<br>**Bis** (`encore`): Al cerrarse, tu otra arma queda recargada del todo.<br>**Retroceso** (`rewind`): Las balas enemigas que entran se dan la vuelta y pasan a ser tuyas. |
<!-- /gen:tree -->

Detalle:
- **Estasis**: la burbuja **para el tiempo** (`timeK = 0`: ni se mueven ni se les puede empujar). Todo el daño que reciben
  mientras (golpes, quemadura, reacciones, rematar de Ejecución…) se **guarda** (número gris `⏸ N` encima) y no pueden morir.
  Al cerrarse lo reciben de golpe ×`stasisMul`, golpe fuerte (rompe congelados). Sin golpe de reloj.
  - Interés: ×(1 + `interestRate` × segundos dentro). Onda: `quakeFrac` del total a los que estén a `quakeRadius`.
    Paradoja: si muere al soltarse, burbuja de estasis ×`paradoxScale` (tamaño y duración) en su sitio — encadena mientras
    haya bajas (se para cuando es demasiado pequeña).
- **Eco**: cada golpe de tus armas (de cualquiera; no los golpes nacidos de otro golpe) a un enemigo **dentro** se repite
  `echoDelay` s después a ×`echoMul`, con su elemento y medio empuje.
  - Reverberación: segundo eco a ×`reverbMul` del primero. Eco fuerte: los ecos son golpe fuerte → con hielo, cada eco puede
    hacer añicos. Ancla: la burbuja se abre al instante a tu alrededor (×`anchorScale`) y te sigue.
- **Aceleración** (paleta oro): si **tú** estás dentro, tus dos armas recargan ×`quickCd` y te mueves ×`quickMove`. Tus
  proyectiles con velocidad (esquirlas del Abanico, balas devueltas…) que la cruzan salen ×`quickProj` de rápido y ×`quickDmg`
  de daño (una vez por burbuja). Los enemigos dentro siguen lentos.
  - Inercia: al salir (o al cerrarse) te dura `inertiaTime` s. Bis: al cerrarse, la otra arma queda recargada del todo.
    Retroceso: las balas enemigas que entran se dan la vuelta y son tuyas (`rewindDamage`).

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: los de dentro van acumulando congelación (`iceRate`/s) → se congelan solos si se quedan; el golpe de reloj congela un poco.
- **Fuego**: el golpe de reloj quema (`fireBurn`) y deja el suelo ardiendo bajo la burbuja.
- **Duplicador**: dos burbujas, una a cada lado de donde apuntas; con Ancla, una más grande.

## Sinergias

- **Estasis + Golpe estelar / Singularidad**: cargas daño grande en el banco y se suelta multiplicado (y con Onda, en área).
  Estasis + **Rayo · Sobrecarga** o **Media luna · Sierra**: daño continuo que se acumula entero.
- **Eco + Abanico** (muchos golpes pequeños) o **+ hielo con Eco fuerte** (cada eco rompe al congelado).
- **Aceleración + armas de recarga larga** (Golpe estelar, Singularidad): con Bis, lanzas, cierras y vuelves a lanzar.
- **Hunter (Golpe estelar · Cazador)**: los golpes del Reloj también marcan.

## Decisiones

- 2026-10-20 · Creada sin el usuario (pidió «más armas completas hasta quedarte sin tokens», con mejoras que funcionen aunque
  el arma no esté en la mano). Se añadieron al motor, para ella y las siguientes: tiempo propio por enemigo (`e.timeK`),
  daño guardado (`e.stasis`, `o.release`), golpes «al golpear» de cualquier arma (`inst.onAnyHit`, `o.proc` para no hacer bucles)
  y `o.raw` (daño ya multiplicado por la build).

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 5.5 | 0.5 – 15 (paso 0.1) | Base: recarga (s) |
| `range` | 400 | 80 – 800 (paso 10) | Base: distancia máxima (px) |
| `travelTime` | 0.3 | 0.05 – 1 (paso 0.05) | Base: viaje del orbe (s) |
| `radius` | 110 | 40 – 260 (paso 5) | Base: radio de la burbuja |
| `duration` | 3 | 0.5 – 8 (paso 0.1) | Base: duración (s) |
| `slowK` | 0.3 | 0 – 1 (paso 0.05) | Base: velocidad de los enemigos dentro (×) |
| `shotSlow` | 0.2 | 0 – 1 (paso 0.05) | Base: velocidad de las balas enemigas dentro (×) |
| `closeDamage` | 25 | 0 – 150 (paso 1) | Base: golpe de reloj al cerrarse |
| `iceRate` | 0.35 | 0 – 2 (paso 0.05) | Hielo: congelación por segundo dentro |
| `fireBurn` | 2.5 | 0 – 6 (paso 0.1) | Fuego: quemadura del golpe de reloj (s) |
| `stasisMul` | 1.3 | 1 – 3 (paso 0.05) | Estasis: × daño guardado al soltarse |
| `interestRate` | 0.15 | 0 – 0.6 (paso 0.01) | Estasis · Interés: + por segundo dentro |
| `quakeFrac` | 0.4 | 0 – 1.5 (paso 0.05) | Estasis · Onda: fracción del daño que salta |
| `quakeRadius` | 130 | 30 – 300 (paso 5) | Estasis · Onda: radio |
| `paradoxScale` | 0.65 | 0.3 – 1 (paso 0.05) | Estasis · Paradoja: tamaño y duración de la nueva (×) |
| `echoDelay` | 0.7 | 0.1 – 2 (paso 0.05) | Eco: retraso (s) |
| `echoMul` | 0.5 | 0.1 – 1.5 (paso 0.05) | Eco: daño del eco (×) |
| `reverbMul` | 0.5 | 0.1 – 1 (paso 0.05) | Eco · Reverberación: daño del 2.º eco (× del 1.º) |
| `anchorScale` | 1.15 | 0.6 – 2 (paso 0.05) | Eco · Ancla: × radio |
| `quickCd` | 2.5 | 1 – 6 (paso 0.1) | Aceleración: × velocidad de recarga dentro |
| `quickMove` | 1.35 | 1 – 2.5 (paso 0.05) | Aceleración: × tu velocidad dentro |
| `quickProj` | 1.5 | 1 – 3 (paso 0.05) | Aceleración: × velocidad de tus proyectiles |
| `quickDmg` | 1.4 | 1 – 3 (paso 0.05) | Aceleración: × daño de tus proyectiles |
| `inertiaTime` | 2.5 | 0.2 – 8 (paso 0.1) | Aceleración · Inercia: s que dura al salir |
| `rewindDamage` | 15 | 1 – 60 (paso 1) | Aceleración · Retroceso: daño de una bala devuelta |
<!-- /gen:tuning -->
