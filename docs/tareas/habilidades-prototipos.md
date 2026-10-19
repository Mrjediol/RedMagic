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
