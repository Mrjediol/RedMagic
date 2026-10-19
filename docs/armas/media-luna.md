# Media luna (`moon`)

**Estado**: prototipo web (clips + jugable), pendiente de prueba. Código: `a4-media-luna.js`, `g-media-luna.js`.

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

## Mejoras

<!-- gen:tree -->
Mejoras provisionales (el árbol con evoluciones está pendiente de diseñar):

| Mejora (id) | Qué hace |
|---|---|
| Doble (`twin`) | Sale otra hoja en arco alto (cubre arriba y abajo). |
| Órbita (`orbit`) | Al volver da una vuelta alrededor del player antes de atraparse. |
<!-- /gen:tree -->

- **Doble**: paleta oro; segunda hoja con arco de `twinArc` px hacia arriba (cubre voladores). La recarga empieza cuando vuelven las dos.
- **Órbita**: al volver no se atrapa: da una vuelta de radio `orbitRadius` en `orbitTime` s alrededor del player (mitad trasera
  por detrás de él) golpeando a lo que se acerque por la espalda.

## Elemento y Duplicador (items generales)

- **Fuego**: deja trozos de llama colgados en el aire durante `fireLife` s que queman (`fireBurn` s) a quien los toca.
- **Hielo**: la ida congela (`iceFreeze`) y **la vuelta es golpe fuerte** → congelar y romper con un solo lanzamiento.
- **Duplicador**: cada hoja sale doble; la copia hace el arco hacia abajo.

## Sinergias

- **Hielo** (idea de la ficha de clips): congelar a la ida y romper a la vuelta con el mismo lanzamiento.
- **Singularidad** (idea): si pasa por una, orbitarla como sierra hasta el colapso.
- **Golpe estelar con Marcas**: marca a todo lo que atraviesa en las dos pasadas.

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `damage` | 18 | 1 – 80 (paso 1) | Daño por pasada |
| `range` | 360 | 100 – 700 (paso 10) | Alcance (px) |
| `outTime` | 0.55 | 0.15 – 1.5 (paso 0.05) | Ida (s) |
| `hoverTime` | 0.14 | 0 – 1 (paso 0.02) | Pausa en el extremo (s) |
| `backTime` | 0.48 | 0.15 – 1.5 (paso 0.05) | Vuelta (s) |
| `knockback` | 130 | 0 – 500 (paso 5) | Empuje |
| `hitRadius` | 22 | 6 – 60 (paso 1) | Radio de golpe |
| `cooldown` | 0.15 | 0 – 2 (paso 0.05) | Recarga tras atraparla (s) |
| `twinArc` | 120 | 20 – 250 (paso 5) | Doble: altura del arco |
| `orbitRadius` | 64 | 30 – 140 (paso 2) | Órbita: radio |
| `orbitTime` | 0.42 | 0.15 – 1.5 (paso 0.02) | Órbita: duración (s) |
| `fireLife` | 0.75 | 0.2 – 2 (paso 0.05) | Fuego: duración de la estela (s) |
| `iceFreeze` | 0.5 | 0 – 1 (paso 0.05) | Hielo: congelación en la ida |
| `fireBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
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

- 2026-10-19 · La vuelta persigue la posición actual del player (no el punto de lanzamiento).

## Preguntas abiertas

- ¿Atrapada perfecta (moverse hacia la hoja al atraparla) con premio?
