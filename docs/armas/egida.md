# Égida (`aegis`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-egida.js`; dibujo en `fx-armas2.js` (`crystalShield`, `crystalWall`; usa `ramRing`, `shockRing`). Reglas comunes:
[README](README.md) · progresión: [progresion.md](progresion.md). **Para probarla: Dificultad ▸ «Los enemigos disparan».**

## Rol

**Defensa activa con premio.** Mantener = escudo hacia el ratón: para las balas y aparta a los que se acercan (vas más lento).
Si una bala llega justo al levantarlo → **¡PARADA!**: vuelve como tuya y **tu siguiente ataque, de cualquier arma**, es golpe
fuerte y más dañino (micro-pausa). Soltar = **golpe de escudo** con un pequeño avance que lanza (regla «lanzado»).

## Comportamiento (base)

- Mantener: escudo de radio `shieldR` que cubre ±`arc`; vas a ×`holdMove`. Balas enemigas que lo tocan: se deshacen.
  Enemigos que lo tocan: cada 0,3 s `contactDamage` y empuje `contactPush`.
- Parada: bala bloqueada en los primeros `parryWindow` s tras levantarlo → vuelve al enemigo más cercano (`parryDamage`), el
  siguiente ataque hace ×`parryMul` y es golpe fuerte (sistema `G.empower`, el del Bastón Helado).
- Soltar: avance `bashDist` (usa el dash) y a los que toque delante `bashDamage`, empuje `bashPush`, lanzados 0,5 s.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Reflejo** (`reflect`) | TODAS las balas que para salen devueltas hacia el enemigo más cercano (no sólo las de la parada) y la parada es más fácil. | **Rebote triple** (`split`): Cada bala devuelta sale triple.<br>**Contraataque** (`riposte`): Cada parada recarga del todo tu otra arma.<br>**Absorción** (`absorb`): Cada bala parada carga el escudo; el golpe de escudo suelta esa carga en una onda. |
| **Bastión** (`bastion`) | Al soltar, el escudo se queda PLANTADO como un muro: para balas y enemigos mientras usas la otra arma. Pulsar con el muro puesto lo hace estallar. | **Púas** (`spikes`): El muro hace mucho daño a los que lo tocan.<br>**Muralla** (`tall`): Muro el doble de alto (para voladores) y dura el doble.<br>**Estallido** (`blast`): La explosión del muro es mayor y lanza a los enemigos. |
| **Embestida** (`charge`) | Mantén para cargar y suelta: embestida larga con el escudo por delante que arrastra a los enemigos y los estampa al final. | **Arrollar** (`trample`): Atraviesas a los enemigos haciéndoles daño en vez de empujarlos.<br>**Imparable** (`unstoppable`): Durante la embestida eres intocable y rompes las balas.<br>**Remate** (`finisher`): Al terminar, onda de choque que aturde. |
<!-- /gen:tree -->

Detalle:
- **Reflejo**: toda bala parada vuelve al enemigo más cercano (`reflectDamage`); ventana de parada ×`reflectWindow`.
  - Rebote triple: cada bala devuelta sale triple. Contraataque: cada parada recarga del todo la otra arma. Absorción: cada
    bala parada suma una carga; el golpe de escudo la suelta en una onda frontal (`absorbPer` × cargas, golpe fuerte, lanza).
- **Bastión**: al soltar, el escudo se planta como **muro** 50 px delante (`wallH` de alto, `wallTime` s): para balas y
  enemigos (los caminantes se dan la vuelta), `wallDps` a los que lo tocan. **Con el muro puesto, pulsar lo hace estallar**
  (`wallBlast`, `blastRadius`, golpe fuerte); también estalla al acabarse. Sigue con la otra arma en la mano.
  - Púas: ×`spikeMul` de daño al tocarlo. Muralla: doble de alto (para voladores) y de duración. Estallido: ×`blastMul` y lanza.
- **Embestida**: mantener = carga (`chargeTime`; vas a ×0,4); soltar = embestida de `chargeMin`→`chargeMax` px que **arrastra**
  a los que encuentra delante y al final los estampa (`slamMin`→`slamMax`, golpe fuerte, lanzados).
  - Arrollar: los atraviesas haciendo `trampleDamage`. Imparable: intocable durante la embestida y rompe balas. Remate: onda
    al final que aturde `finisherStun`.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo / Fuego**: el golpe de escudo y la embestida congelan (`iceFreeze`) o queman (`fireBurn`); el estallido del muro, también.
- **Duplicador**: el golpe de escudo alcanza ×1,6; en Bastión, dos muros seguidos.

## Sinergias

- **Parada → arma de golpe grande**: paras con la Égida, cambias (Q) y la lanza del Golpe estelar o la Jabalina cargada salen
  potenciadas. Con **Gatillo de cambio** (item), doble potenciación.
- **Bastión + Abanico / Rayo / Tótem**: los enemigos se amontonan contra el muro.
- **Embestida + Ráfaga · Bolos / Singularidad**: los llevas a donde quieras.

## Decisiones

- 2026-10-20 · Creada sin el usuario. La prueba de la matriz (`test-jugable.cjs`) pasa a tener «Los enemigos disparan» activado
  para cubrir paradas, reflejos y señuelos.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.8 | 0.1 – 4 (paso 0.05) | Base: recarga tras soltar (s) |
| `shieldR` | 46 | 20 – 120 (paso 1) | Base: radio del escudo |
| `arc` | 0.95 | 0.3 – 1.6 (paso 0.05) | Base: medio ángulo que cubre (rad) |
| `holdMove` | 0.6 | 0.1 – 1 (paso 0.05) | Base: × tu velocidad con el escudo arriba |
| `parryWindow` | 0.22 | 0.05 – 0.6 (paso 0.01) | Base: ventana de parada tras levantarlo (s) |
| `parryDamage` | 30 | 1 – 120 (paso 1) | Base: daño de la bala devuelta en una parada |
| `parryMul` | 1.6 | 1 – 4 (paso 0.05) | Base: × siguiente ataque tras una parada |
| `contactDamage` | 6 | 0 – 40 (paso 1) | Base: daño al apartar a los que tocan el escudo |
| `contactPush` | 300 | 0 – 900 (paso 10) | Base: empuje al apartarlos |
| `bashDist` | 140 | 40 – 400 (paso 5) | Base: avance del golpe de escudo |
| `bashDamage` | 22 | 1 – 120 (paso 1) | Base: daño del golpe de escudo |
| `bashPush` | 420 | 0 – 1200 (paso 10) | Base: empuje del golpe (lanzado) |
| `iceFreeze` | 0.5 | 0 – 1 (paso 0.05) | Hielo: congelación del golpe |
| `fireBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura del golpe (s) |
| `reflectDamage` | 18 | 1 – 80 (paso 1) | Reflejo: daño de las balas devueltas |
| `reflectWindow` | 2 | 1 – 4 (paso 0.1) | Reflejo: × ventana de parada |
| `absorbPer` | 12 | 0 – 60 (paso 1) | Reflejo · Absorción: daño de la onda por bala |
| `wallTime` | 6 | 1 – 20 (paso 0.5) | Bastión: duración del muro (s) |
| `wallH` | 110 | 40 – 300 (paso 5) | Bastión: alto del muro |
| `wallDps` | 8 | 0 – 60 (paso 1) | Bastión: daño/s a los que lo tocan |
| `spikeMul` | 3.5 | 1 – 8 (paso 0.1) | Bastión · Púas: × daño |
| `wallBlast` | 50 | 0 – 200 (paso 1) | Bastión: daño al estallar |
| `blastRadius` | 120 | 30 – 300 (paso 5) | Bastión: radio del estallido |
| `blastMul` | 1.6 | 1 – 3 (paso 0.05) | Bastión · Estallido: × daño y radio |
| `chargeTime` | 0.8 | 0.1 – 2 (paso 0.05) | Embestida: carga máxima (s) |
| `chargeMin` | 160 | 40 – 500 (paso 5) | Embestida: distancia sin carga |
| `chargeMax` | 420 | 40 – 900 (paso 10) | Embestida: distancia con carga |
| `slamMin` | 20 | 0 – 150 (paso 1) | Embestida: daño final sin carga |
| `slamMax` | 60 | 0 – 300 (paso 1) | Embestida: daño final con carga |
| `trampleDamage` | 25 | 0 – 150 (paso 1) | Embestida · Arrollar: daño al atravesar |
| `finisherRadius` | 130 | 30 – 300 (paso 5) | Embestida · Remate: radio |
| `finisherStun` | 1 | 0 – 3 (paso 0.1) | Embestida · Remate: aturdido (s) |
<!-- /gen:tuning -->
