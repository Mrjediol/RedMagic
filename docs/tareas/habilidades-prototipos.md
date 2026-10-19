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
