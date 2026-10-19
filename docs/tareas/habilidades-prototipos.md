# Habilidades nuevas: prototipos web (antes de meter nada en Unity)

Empezada el 2026-10-19 a petición del usuario: «nuevos diseños de disparos como el cometa, prototipos de varias habilidades con
sonidos y efectos, pensar sinergias/builds que las cambien; en web, vídeo del ataque contra un enemigo; luego reviso 1 a 1 y
retocamos hasta que queden bien». Y: «no te limites a lo que existe; si un arma pide un sistema que aún no está, proponlo».

## Estado

- Página: `docs/bocetos/habilidades/index.html` (abrir con doble clic; funciona en `file://`). Misma mecánica que
  `ataque-cometa-estados.html`: bucle contra enemigos, velocidad, pausa, capas, impact frames, barra por clip, 🔊 por clip.
  Extra: filtro por habilidad (para revisar de una en una), **⬇ vídeo** (graba una vuelta a `.webm`, con sonido si 🔊),
  pestaña 🧩 Sistemas de build y 🔊 Sonidos (cada pieza suelta).
- 6 habilidades × 4 clips (base + 3 variantes de build), todas en 🛠 En curso:
  1. **Abanico de cristal** (escopeta) — Hielo→Añicos · Rebote · Retorno
  2. **Singularidad** (atrae y colapsa) — Fuego · Masa · Catapulta (sinergia con el Abanico)
  3. **Rayo** (canalizar) — Prisma · Hielo · Robo de vida
  4. **Media luna** (bumerán) — Doble · Órbita · Fuego
  5. **Semilla estelar** (mina lanzada) — Racimo · Hielo→nova · Proximidad
  6. **Golpe estelar** (lanza del cielo) — Marcas · Fuego→cráter · Lluvia
- Sistemas de build que proponen: `docs/builds-sistemas-propuesta.md` (con 3 preguntas abiertas).

## Archivos

`core.js` (escena, paletas, primitivas, enemigo, impactos), `sound.js` (síntesis, piezas comunes), `a1-…a6-*.js` (una por
habilidad: sus sonidos, `build(P)` y sus clips), `systems.js` (pestaña 🧩), `ui.js`. Para revisar fotogramas desde la consola:
`__sheet('fan', [0.5, 0.6], 0.6, [x, y, w, h])` (hoja de contactos con recorte). Comprobación sin navegador: un arnés de Node que
ejecuta los scripts con un canvas falso (scratchpad de la sesión; se puede rehacer: carga los `<script>` del index en un `vm`
y llama `scene()` en 400 instantes por clip).

## Siguiente

- El usuario revisa 1 a 1 (filtro de habilidad) y pide retoques; lo aprobado pasa a ✔ Guardados (`tab: 'saved'` en el clip),
  lo descartado a 📁 Archivo (`tab: 'arc'`).
- Lo aprobado sigue el proceso de `docs/fx-proceso-bocetos.md` (port a Unity con `FxCanvas`), y los sistemas elegidos se
  diseñan aparte.

## 2026-10-19 (tarde) — versión jugable

El usuario revisó los clips: le encantaron (sobre todo la Singularidad + Abanico) y decide **2 armas de base con un botón para
cambiar (Q)**, 3 sólo en builds especiales. Pidió probarlas jugando antes de llevar nada a Unity, y una forma de que lo aprendido
en la web no se pierda entre `/clear`s → **fichas en `docs/armas/`** (fuente de verdad; leer `docs/armas/README.md` primero).

Hecho:
- `jugable.html` (+ `game.js`, `g-*.js`, `jugable-ui.js`): escenario con plataformas, 10 dianas (suelo, andan, vuelan) que
  reaparecen, movimiento con doble salto y dash, 2-3 huecos de arma con sus mods, reacciones, captura, marcas, hit-stop,
  impact frames, números de daño, DPS, sonido, velocidad del juego, panel de ajustes por arma (se guardan en el navegador;
  ⬇ Exportar → guardar como `docs/armas/ajustes.json`).
- Dibujo compartido clips/juego en `fx.js`; `tools/test-clips.cjs`, `tools/test-jugable.cjs` (sin navegador) y
  `tools/gen-fichas.cjs` (tablas de mods y ajustes de las fichas desde el código).

Siguiente: el usuario juega, pide retoques arma a arma; cada retoque actualiza su ficha (decisiones) y se regeneran las tablas.

## 2026-10-19 (noche) — progresión en prueba

El usuario: las mejoras de un arma no pueden ser items generales; quiere árbol profundo (varias formas avanzadas por arma), items
generales aplicables a todas, y curva de roguelite (difícil de base, combo = romper la run). Diseño en `docs/armas/progresion.md`.
Hecho en la jugable: Abanico (Erizo / Lanza / Caleidoscopio) y Golpe estelar (Bombardeo / Cazador / Tormenta) con 3 mejoras cada
evolución; 9 items con rareza (Esencias de hielo/fuego = elemento de la build, Duplicador, Gatillo de cambio, Constelación,
Corazón de vacío, Cristal afilado, Engranaje, Sangre de cristal); modo run (🎲 subir de nivel, 🎁 cofre); dificultad (vida ×).
Las otras cuatro armas: sus mods quedan como mejoras provisionales y expresan elemento y Duplicador.

## 2026-10-19 (noche, 2) — arreglos y sinergias como el juego

- Arreglos tras la prueba del usuario: Metralla no salía, Caleidoscopio base buscaba enemigos sin Buscador, Prisma no dividía.
  Causa de fondo: lo creado durante la actualización de una entidad se perdía (`game.js ▸ step`).
- Build = sistema del juego: 6 items con etiqueta elemental + universal, tramos 2/4/6; **el elemento se aplica con 4** (propuesta
  del usuario: así nunca hay dos estados elementales a la vez); 2 hielo = recarga, 2 fuego = daño. Items de hielo y oro copiados
  del juego; los generales de las pruebas llevan Fuego + universal. Tablas en `docs/armas/README.md`.

- 2026-10-19 · Ronda de aspectos de la Singularidad (fx-sing.js, clips 2b en 🛠 En curso; el resto de clips pasan a ✔ Guardados por defecto). Pendiente: que el usuario elija.
- 2026-10-19 · Árbol de la Semilla: Campo de minas / Racimo / Madura. El usuario probará Rayo, Media luna y Semilla juntos.
- 2026-10-19 · Árbol de la Media luna: Sierra / Satélite / Malabar. Pendiente: opinión del usuario.
- 2026-10-19 · Rayo: el usuario no ve utilidad a Reflejo → Corona (haces que giran alrededor de la gema).
- 2026-10-19 · Árbol del Rayo: Sobrecarga / Prisma / Faro (ficha `docs/armas/rayo.md`). Pendiente: opinión del usuario.
- 2026-10-19 · Elegida la Grieta: orbe que viaja y abre la grieta en el sitio; dura más. La jugable guarda en el navegador sólo los ajustes cambiados a mano (v4).

---

## ▶ RETOMAR AQUÍ — «retoma tarea de armas» (traspaso 2026-10-19, fin de sesión)

**Estado**: las seis armas tienen su árbol (3 evoluciones × 3 mejoras). Validados por el usuario: Abanico, Golpe estelar, Singularidad.
**Hechos sin probar** (el usuario los prueba todos juntos): Rayo (Sobrecarga / Prisma / Faro; Reflejo → Corona a petición suya),
Media luna (Sierra / Satélite / Malabar), Semilla (Campo de minas / Racimo / Madura).

**Qué toca**: recoger la opinión del usuario sobre esas tres armas y arreglar, un arma cada vez, con el método de abajo (pasos 2-6).
Después: decidir con él qué armas se portan a Unity primero (README ▸ «Cómo se pasa un arma a Unity»).

**Leer primero** (en este orden): `docs/armas/README.md` (reglas comunes y cómo se porta), `docs/armas/progresion.md` (las tres
vías: árbol propio, sinergias, items con etiquetas 2/4/6; el elemento se aplica con 4), la ficha del arma que toca
(`docs/armas/rayo.md`…) y, como modelo de árbol ya hecho, `g-abanico.js` / `g-singularidad.js` + sus fichas.

**Método que pidió el usuario** (cada arma):
1. Proponer en el chat el árbol: **3 evoluciones** (formas avanzadas que cambian cómo se juega, no «más proyectiles»), cada una con
   **3 mejoras**. Las mejoras provisionales actuales del arma se reaprovechan dentro de alguna evolución (como Masa → Errante).
   Nada de mejoras que sean items generales (elemento, Duplicador → son items; cada arma sólo define cómo los expresa).
2. Implementar en `g-<arma>.js`: `archetypes: [{ id, name, desc, ups: [3] }]` en lugar de `ups`, comportamiento con `hasMod(slot, id)`
   y `slot.arch`, todos los números en `tuning` con prefijo «Evolución: …» / «Evolución · Mejora: …». Mantener elemento
   (`elemOf()`), Duplicador (`copies()`), `cdMul()` y lo que ya funciona.
3. Probar: prueba dirigida en Node de cada evolución (patrón: cargar los scripts de `jugable.html` en un `vm`, preparar huecos y
   build, `step(1/60)` y medir lo que debe pasar: daño, entidades creadas, posiciones) + `tools/test-jugable.cjs` (matriz completa,
   debe dar «todo ok») + `tools/test-clips.cjs`. Si algo no llega a activarse con los valores (como Fauces, que no mataba),
   subir los valores hasta que la evolución cumpla su papel.
4. Mirar en el navegador lo nuevo (servidor: `node docs/bocetos/habilidades/tools/serve.cjs`, luego
   `http://127.0.0.1:8765/habilidades/jugable.html`; desde la consola `__G` da el estado; disparar con
   `G.slots[i].inst.onPress(aimInfo())` tras fijar `G.mouse.sx/sy` y esperar un frame; congelar con `G.timeScale = 0.0001`).
5. Ficha del arma: sección «Árbol» con `<!-- gen:tree -->` + «Detalle» a mano, «Elemento y Duplicador», decisiones con fecha;
   `node docs/bocetos/habilidades/tools/gen-fichas.cjs`. Commit (`feat: árbol del <arma>: …`).
6. **Parar y avisar al usuario** con: qué es cada evolución y mejora, lo que se ajustó, y una lista corta de qué probar. Esperar su
   opinión, arreglar lo que diga, y sólo entonces pasar a la siguiente arma.

**Trampas aprendidas en esta sesión**:
- Bash + `node -e "…"` con backticks o `${}` en el texto rompe (bash los ejecuta): para scripts de edición largos, escribir un
  `.js` con la herramienta Write y ejecutarlo; para cambios cortos, la herramienta Edit.
- Lo que una entidad crea durante su `update` ya no se pierde (`game.js ▸ step` recorre una copia) — no volver al `filter` directo.
- La jugable guarda en el navegador sólo los ajustes cambiados a mano (clave `hab.jugable.v4`): cambiar un valor por defecto en el
  código sí le llega al usuario. Si se cambia la estructura del guardado, subir la versión de la clave.
- Proyectiles capturables por la Singularidad: `capturable: true`, `state: 'fly'`, respetar `captured`, y si se quiere Multiplicar,
  método `clone()`. Las balas enemigas son `hostile`.
- Dibujo nuevo que se comparta entre clips y juego → `fx.js` (o `fx-sing.js`), nunca duplicado en el `g-*.js`.
- Ronda de bocetos en los clips (`index.html`): lo nuevo con `tab: 'wip'`; sin `tab` = Guardados.

**Pendiente aparte (no tocar sin que lo pida)**: `optimizacion-arte-carga` (ASTC + quitar mipmaps esperando visto bueno),
`rigtrans`, `armas-meta-upgrades` (mejoras de arma fuera de la run, para Unity).
