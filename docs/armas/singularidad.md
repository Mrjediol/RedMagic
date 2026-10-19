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

## Mejoras

<!-- gen:tree -->
Mejoras provisionales (el árbol con evoluciones está pendiente de diseñar):

| Mejora (id) | Qué hace |
|---|---|
| Masa (`mass`) | Más grande y dura más; arranca a los voladores y los estampa contra el suelo. |
<!-- /gen:tree -->

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Fuego**: el estallido es de fuego (paleta fuego, llamas que se abren por el suelo, brasas) y quema `burn` s. Contra
  congelados = **deshielo** (daño ×2).
- **Hielo**: el colapso congela a todos los alcanzados (nova de escarcha) → la otra arma los rompe.
- **Duplicador**: salen **dos orbes** en arco (uno por cada lado) que, al llegar, giran uno alrededor del otro `mergeTime` s y se
  **funden** en uno ×`mergeScale` de tamaño y radios y ×`mergeDamage` de daño (petición del usuario, 2026-10-19).

Mejora provisional:
- **Masa**: ×`massScale` de tamaño y radios, ×`massTime` de tiempo atrayendo; levanta a los voladores hacia el centro y al
  estallar los **estampa** contra el suelo (se quedan 1,6 s en el suelo). Grietas y escombros en el estallido.

## Sinergias

- **Cualquier proyectil capturable** (hoy el Abanico): la Catapulta sale sola, sin mod. Idea a ampliar: bombas de la Semilla,
  hoja de la Media luna orbitando como sierra.
- **Reacciones**: junta enemigos congelados y el estallido los rompe a todos (añicos en cadena).
- **Golpe estelar**: los atraídos quedan quietos en una columna → la lanza los alcanza a todos.

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 2.5 | 0.3 – 8 (paso 0.1) | Recarga (s) |
| `range` | 360 | 80 – 800 (paso 10) | Distancia máxima (px) |
| `travelTime` | 0.5 | 0.15 – 1.5 (paso 0.05) | Viaje (s) |
| `pullTime` | 0.8 | 0.2 – 3 (paso 0.05) | Tiempo atrayendo (s) |
| `pullRadius` | 150 | 40 – 400 (paso 5) | Radio de atracción |
| `pullSpeed` | 150 | 0 – 500 (paso 5) | Fuerza de atracción (px/s) |
| `burstDamage` | 40 | 0 – 200 (paso 1) | Daño del colapso |
| `burstRadius` | 130 | 30 – 350 (paso 5) | Radio del colapso |
| `burstKnock` | 380 | 0 – 900 (paso 10) | Empuje del colapso |
| `captureRadius` | 95 | 0 – 250 (paso 5) | Radio de captura de proyectiles |
| `ejectSpeed` | 900 | 200 – 2000 (paso 10) | Velocidad de salida de lo capturado |
| `burn` | 3 | 0 – 8 (paso 0.1) | Fuego: quemadura (s) |
| `massScale` | 1.4 | 1 – 2.5 (paso 0.05) | Masa: escala |
| `massTime` | 1.35 | 1 – 3 (paso 0.05) | Masa: × tiempo atrayendo |
| `look` | 1 | 0 – 5 (paso 1) | Aspecto: 0 anillas · 1 horizonte · 2 remolino · 3 grieta · 4 abismo · 5 lente |
| `over` | 0 | 0 – 1 (paso 1) | Vórtice por encima de los enemigos (0 = no, 1 = sí) |
| `mergeTime` | 0.3 | 0.05 – 1 (paso 0.05) | Duplicador: tiempo de fusión (s) |
| `mergeScale` | 1.35 | 1 – 2.5 (paso 0.05) | Duplicador: escala de la fusionada |
| `mergeDamage` | 1.7 | 1 – 4 (paso 0.05) | Duplicador: × daño de la fusionada |
<!-- /gen:tuning -->

## Visual

`voidOrb` (fx.js): disco de acreción inclinado (`singularityDisk`, mitad de arriba detrás), núcleo oscuro con aro
(`singularityCore`), partículas en espiral, anillos de succión, polvo del suelo arrastrado (si está cerca del suelo), estela de
distorsión en el viaje y líneas que convergen en el colapso. `voidBurst`: destello, onda de hebras parciales, anillo de suelo,
agujas, chispas, gotas, polvo; + grietas/escombros (Masa) o llamas/brasas (Fuego).

### Ronda de aspectos (2026-10-19, pendiente de elegir)

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

- 2026-10-19 · Capas: todo el vórtice se dibuja delante de los enemigos (`voidOrbOver` en fx.js) y el disco se ordena sólo respecto a su
  núcleo; antes la mitad trasera iba detrás de los enemigos y un enemigo dentro del disco cortaba el anillo (lo vio el usuario).
  Para que se lea «se lo traga», el atrapado se estira hacia el centro y se tiñe de violeta (`e.suck`, `drawDummy`).

- 2026-10-19 · La Catapulta no es un mod: es la regla genérica de captura (el usuario la señaló como lo mejor).

## Preguntas abiertas

- ¿Qué más debe ser capturable (bombas, hoja, proyectiles enemigos → escudo)?
