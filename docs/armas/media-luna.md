# Media luna (`moon`)

**Estado**: prototipo web (clips + jugable); árbol de evoluciones hecho 2026-10-19, pendiente de la prueba del usuario. Código: `a4-media-luna.js`, `g-media-luna.js`.

## Rol

Bumerán de energía de una sola hoja: sale girando, se frena en el extremo, vuelve al player y se atrapa. Golpea a la ida y a la
vuelta; mientras está fuera no se puede volver a lanzar (el ritmo lo marca la vuelta, no una recarga).

## Comportamiento (jugable)

- Clic = lanza hacia el ratón: recorre `range` px en `outTime` s (frena al final, arco leve hacia arriba).
- Pausa `hoverTime` s girando en el extremo; vuelve en `backTime` s acelerando **hacia la posición actual del player** (si te
  mueves, te sigue).
- Golpea a cada enemigo **una vez por pasada** (ida / vuelta / órbita) si su elipse ampliada en `hitRadius` toca la hoja:
  `damage`, empuje en la dirección de la hoja, micro hit-stop. Tajo diagonal que se abre y se afina + chispas.
- Al atraparla: destello en el cristal y `cooldown` corto.

## Árbol (evolución en nivel 2 + 3 mejoras)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Sierra** (`saw`) | Se queda girando donde toca al primer enemigo (o en el extremo) y lo tritura; vuelve al pulsar otra vez o sola al rato. | **Tirón** (`yank`): A la vuelta arrastra hacia ti a los que corta.<br>**Desgarro** (`rend`): Cada corte seguido al mismo enemigo hace más daño.<br>**Imán** (`magnet`): Mientras tritura, atrae hacia la sierra a los enemigos cercanos. |
| **Satélite** (`orbit`) | La hoja vive girando a tu alrededor y corta lo que se acerca; al lanzarla vuelve a la órbita, sin recarga. | **Doble** (`twin`): Dos hojas en órbita; cada clic lanza una.<br>**Luna creciente** (`wax`): Cada vuelta en órbita sin lanzarla la hace crecer y suma daño al próximo lanzamiento.<br>**Escudo** (`guard`): Las hojas en órbita destruyen las balas enemigas que tocan. |
| **Malabar** (`juggle`) | Atrapada perfecta: pulsa justo cuando vuelve y sale otra vez al instante, más rápida y fuerte (racha de hasta 3). Si la dejas llegar, la racha se pierde. | **Filo creciente** (`edge`): Con cada punto de racha la hoja es más grande y corta más lejos.<br>**Eco** (`echo`): Cada atrapada perfecta deja una hoja fantasma que repite el lanzamiento anterior.<br>**Gran final** (`finale`): Con la racha llena, la siguiente atrapada perfecta lanza una hoja enorme (golpe fuerte) y la racha vuelve a empezar. |
<!-- /gen:tree -->

Detalle:
- **Sierra**: a la ida se queda en el primer enemigo que toca (lo sigue) o, si no toca a nadie, en el extremo; gira más rápido y corta
  cada `sawTick` s a todo lo que toca (`sawDamage`, sin empuje). Vuelve al pulsar otra vez o sola a los `sawTime` s; la vuelta golpea normal.
  - Tirón: los cortados en la vuelta vienen detrás de la hoja (`yankSpeed`) y siguen 0,45 s hacia ti tras atraparla (hasta 70 px).
  - Desgarro: cada corte seguido al mismo enemigo +`rendStep` (hasta +`rendMax`); si deja de tocarlo, se reinicia.
  - Imán: atrae hacia la sierra a los que estén a menos de `magnetRadius` (no al que tiene clavado: lo arrastraba sin fin).
- **Satélite** (era la mejora Órbita): la hoja gira siempre a tu alrededor (`satRadius`, una vuelta cada `satLap` s; mitad trasera por
  detrás del player) y golpea a cada enemigo una vez por vuelta (×`satMul`), también con la otra arma en la mano. Clic = sale desde
  donde está hacia el ratón (ida, pausa, vuelta) y vuelve a su sitio en la órbita; sin recarga.
  - Doble (era la mejora Doble): dos hojas opuestas, cada clic lanza una (doradas).
  - Luna creciente: cada vuelta en órbita sin lanzarla +`waxStep` de daño (hasta +`waxMax`) y crece; al lanzarla lo gasta.
  - Escudo: las hojas en órbita rompen las balas enemigas que tocan (para probarlo, «Los enemigos disparan»).
- **Malabar** (la «atrapada perfecta» de las preguntas abiertas): pulsar cuando a la hoja le quedan menos de `perfectWindow` s de vuelta
  (o hasta `perfectLate` s después de atraparla) la vuelve a lanzar al instante sin recarga y sube la racha (hasta `rachaMax`):
  +`jugDmg` de daño y −`jugSpeed` de tiempo de vuelo por punto. Aviso: la hoja y el cristal brillan en dorado durante la ventana.
  Si la dejas llegar sin pulsar, «racha perdida».
  - Filo creciente: +`edgeGrow` de tamaño y radio de golpe por punto de racha.
  - Eco: cada atrapada perfecta lanza además una hoja fantasma (translúcida, ×`echoMul`) que repite el lanzamiento anterior y se desvanece.
  - Gran final: con la racha llena, la siguiente perfecta lanza una hoja enorme (×`finaleMul` de daño, ×`finaleSize`, golpe fuerte) y la racha vuelve a 0.

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Fuego**: deja trozos de llama colgados en el aire durante `fireLife` s que queman (`fireBurn` s) a quien los toca.
- **Hielo**: la ida congela (`iceFreeze`) y **la vuelta es golpe fuerte** → congelar y romper con un solo lanzamiento.
- **Duplicador**: cada hoja sale doble; la copia hace el arco hacia abajo. Con Satélite, el doble de hojas en órbita.

## Sinergias

- **Hielo** (idea de la ficha de clips): congelar a la ida y romper a la vuelta con el mismo lanzamiento.
- **Singularidad** (idea): si pasa por una, orbitarla como sierra hasta el colapso.
- **Golpe estelar con Marcas**: marca a todo lo que atraviesa en las dos pasadas.

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `damage` | 18 | 1 – 80 (paso 1) | Base: daño por pasada |
| `range` | 360 | 100 – 700 (paso 10) | Base: alcance (px) |
| `outTime` | 0.55 | 0.15 – 1.5 (paso 0.05) | Base: ida (s) |
| `hoverTime` | 0.14 | 0 – 1 (paso 0.02) | Base: pausa en el extremo (s) |
| `backTime` | 0.48 | 0.15 – 1.5 (paso 0.05) | Base: vuelta (s) |
| `knockback` | 130 | 0 – 500 (paso 5) | Base: empuje |
| `hitRadius` | 22 | 6 – 60 (paso 1) | Base: radio de golpe |
| `cooldown` | 0.15 | 0 – 2 (paso 0.05) | Base: recarga tras atraparla (s) |
| `iceFreeze` | 0.5 | 0 – 1 (paso 0.05) | Hielo: congelación en la ida |
| `fireLife` | 0.75 | 0.2 – 2 (paso 0.05) | Fuego: duración de la estela (s) |
| `fireBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `sawTime` | 2.5 | 0.3 – 8 (paso 0.1) | Sierra: tiempo triturando si no la llamas (s) |
| `sawTick` | 0.15 | 0.05 – 0.5 (paso 0.01) | Sierra: cada cuánto corta (s) |
| `sawDamage` | 5 | 1 – 40 (paso 1) | Sierra: daño por corte |
| `yankSpeed` | 420 | 50 – 1000 (paso 10) | Sierra · Tirón: velocidad del arrastre |
| `rendStep` | 0.15 | 0 – 1 (paso 0.05) | Sierra · Desgarro: + daño por corte seguido |
| `rendMax` | 1.5 | 0 – 5 (paso 0.1) | Sierra · Desgarro: + daño máximo |
| `magnetRadius` | 170 | 30 – 300 (paso 5) | Sierra · Imán: radio |
| `magnetSpeed` | 110 | 20 – 400 (paso 5) | Sierra · Imán: fuerza |
| `satRadius` | 70 | 30 – 160 (paso 2) | Satélite: radio de la órbita |
| `satLap` | 0.9 | 0.3 – 3 (paso 0.05) | Satélite: tiempo por vuelta (s) |
| `satMul` | 0.6 | 0.1 – 2 (paso 0.05) | Satélite: daño en órbita (×) |
| `waxStep` | 0.25 | 0 – 1 (paso 0.05) | Satélite · Luna creciente: + daño por vuelta |
| `waxMax` | 1.5 | 0 – 5 (paso 0.1) | Satélite · Luna creciente: + daño máximo |
| `perfectWindow` | 0.2 | 0.05 – 0.6 (paso 0.01) | Malabar: ventana antes de llegar (s) |
| `perfectLate` | 0.1 | 0 – 0.4 (paso 0.01) | Malabar: margen después de atraparla (s) |
| `rachaMax` | 3 | 1 – 6 (paso 1) | Malabar: racha máxima |
| `jugDmg` | 0.35 | 0 – 1 (paso 0.05) | Malabar: + daño por punto de racha |
| `jugSpeed` | 0.12 | 0 – 0.25 (paso 0.01) | Malabar: − tiempo de vuelo por punto de racha |
| `edgeGrow` | 0.25 | 0 – 1 (paso 0.05) | Malabar · Filo creciente: + tamaño por punto de racha |
| `echoMul` | 0.6 | 0.1 – 2 (paso 0.05) | Malabar · Eco: daño de la hoja fantasma (×) |
| `finaleMul` | 3 | 1 – 8 (paso 0.1) | Malabar · Gran final: × daño |
| `finaleSize` | 2.2 | 1 – 4 (paso 0.1) | Malabar · Gran final: tamaño |
<!-- /gen:tuning -->

## Visual

`crescentBlade` (fx.js: media luna en 4 capas + filo blanco), desenfoque de giro (2 copias giradas detrás), copias que se quedan
atrás en el camino, y dos estelas en espiral desde las puntas (posición de cada punta en los últimos 0,075 s).

## Sonido

`moon_throw`, giro en bucle (ruido filtrado con trémolo de 15 Hz; en la jugable en `g-media-luna.js`), `moon_hit` (tajo),
`moon_catch`, `moon_orbit`, `moon_fire`.

## Port a Unity (plan)

Proyectil con estado (ida / pausa / vuelta / órbita) y «munición» de 1. La vuelta persigue al player. Abre la familia de
efectos «al atrapar» del sistema Retorno.

## Decisiones

- 2026-10-19 · Árbol: Sierra / Satélite / Malabar. Órbita y Doble (provisionales) pasan a Satélite. Imán sube a 170 px de radio
  (con 130 no llegaba al vecino) y no arrastra al enemigo clavado. Tirón sigue arrastrando 0,45 s tras atraparla (sólo con la vuelta
  movía ~90 px).

- 2026-10-19 · La vuelta persigue la posición actual del player (no el punto de lanzamiento).

## Preguntas abiertas

- ¿El Satélite debería seguir girando cuando la Media luna no es el arma activa? (hoy sí).
