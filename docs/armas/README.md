# Armas — fichas (fuente de verdad entre la web y Unity)

Desde 2026-10-19 las armas nuevas se diseñan y afinan **en la web** antes de tocar Unity. Esta carpeta es lo que una sesión nueva
(tras `/clear`) lee para saber qué hay, qué se decidió y cómo portarlo. **Regla: cualquier cambio de un arma en la web actualiza su
ficha en la misma sesión** (comportamiento, decisiones) y se vuelve a ejecutar `gen-fichas` (árbol, ajustes, items).

**Modelo de progresión** (2026-10-19, en prueba): [progresion.md](progresion.md). Tres vías de mejora: **árbol propio del arma**
(evolución en nivel 2 entre 3 formas avanzadas + 3 mejoras de esa evolución), **sinergias generales** (reacciones, captura,
marcas) e **items generales** con rareza (elemento, Duplicador…) que funcionan en todas las armas porque cada arma define cómo
los expresa. Árbol completo hoy: Abanico y Golpe estelar; las otras cuatro tienen mejoras provisionales.

## Dónde está cada cosa

| | |
|---|---|
| Clips (vídeo de cada variante contra enemigos) | `docs/bocetos/habilidades/index.html` (`a1-…a6-*.js`) |
| Versión jugable (escenario, movimiento, 2-3 armas con su árbol, build de items, dificultad, ajustes) | `docs/bocetos/habilidades/jugable.html` (`game.js`, `g-*.js`, `jugable-ui.js`) |
| Dibujo compartido por clips y juego (lo que se porta) | `docs/bocetos/habilidades/fx.js` + primitivas de `core.js` |
| Sonidos (síntesis WebAudio, ids `SND.*`) | `sound.js` + el bloque `sound(...)` de cada `a*-*.js` |
| Ajustes afinados por el usuario | `docs/armas/ajustes.json` (exportado con «⬇ Exportar ajustes» y guardado aquí) |
| Sistemas de build propuestos | `docs/builds-sistemas-propuesta.md` |
| Pruebas sin navegador | `node docs/bocetos/habilidades/tools/test-clips.cjs` · `tools/test-jugable.cjs` |
| Tablas de las fichas | `node docs/bocetos/habilidades/tools/gen-fichas.cjs` |

## Fichas

| Arma | `id` | Rol | Ficha |
|---|---|---|---|
| Abanico de cristal | `fan` | escopeta corta, sin carga | [abanico.md](abanico.md) |
| Singularidad | `sing` | atrae y colapsa; prepara a las demás | [singularidad.md](singularidad.md) |
| Rayo | `ray` | canalizar, daño sostenido | [rayo.md](rayo.md) |
| Media luna | `moon` | bumerán, golpea a la ida y a la vuelta | [media-luna.md](media-luna.md) |
| Semilla estelar | `seed` | mina lanzada en parábola | [semilla.md](semilla.md) |
| Golpe estelar | `star` | lanza del cielo diferida | [golpe-estelar.md](golpe-estelar.md) |

## Reglas comunes de la versión jugable (las que valen para todas las armas)

- **Controles** (decisión del usuario 2026-10-19: 2 armas de base, 3 sólo en builds especiales; un botón para cambiar):
  A/D mover, W/Espacio saltar (doble), S bajar de plataforma, Shift dash, ratón apuntar, clic disparar (mantener = repetir o
  canalizar), **Q / rueda = cambiar de arma**, 1-2-3 elegir hueco.
- **Cambiar de arma con el botón pulsado** suelta la actual (`onRelease`) y pulsa la nueva: el rayo se corta, la otra empieza.
- **Recarga por arma**, independiente; la del hueco no activo sigue corriendo.
- **Daño** (`game.js ▸ damage`): un golpe lleva daño, empuje, elemento (`ice` / `fire`), congelación, quemadura y si es
  **fuerte** (`heavy`: rompe congelados). Los números pequeños seguidos se suman en uno.
- **Estados del enemigo**: congelación acumulada (barra cian bajo la vida; al llenarse = congelado `frozenTime` s, no se mueve
  ni le empujan), quemado (tick cada 0,35 s), marcado (rombo encima, 8 s), atraído, estampado (voladores).
- **Reacciones** (interruptor en el panel): congelado + golpe fuerte = **añicos** (daño extra + metralla alrededor); congelado
  + fuego = **deshielo** (daño ×2, quita el hielo); quemado + hielo = **vapor** (explosión pequeña que aturde).
- **Captura** (regla genérica de la Singularidad): todo proyectil del player marcado `capturable` que vuela cerca queda
  orbitando y sale despedido al colapsar con todos sus efectos. Hoy: esquirlas del Abanico.
- **Marcas**: con el Golpe estelar en evolución **Cazador** equipado, los golpes de las otras armas marcan (8 s); el Golpe
  estelar cae sobre los marcados sin apuntar. Con Acumulación se apilan hasta 3 (una por ataque).
- **Elemento**: lo da un item (un hueco para toda la build); cada arma lo expresa a su manera (tabla en progresion.md).
- **Modo run en el panel**: «🎲 Subir de nivel» ofrece 2 evoluciones al azar (nivel 2) y luego 2 mejoras al azar de las que
  faltan; «🎁 Cofre» ofrece 3 items sorteados por rareza (común 60 · raro 30 · legendario 10). En modo libre se marca todo a mano.
- **Dificultad**: vida de los enemigos ×0,5-×4 y que disparen.
- **Feel**: hit-stop (micro-pausa) e impact frames (pantalla oscura + enemigo blanco) en golpes fuertes; temblor de cámara.

### Items generales (vía 3)
<!-- gen:items -->
| Item (id) | Rareza | Efecto |
|---|---|---|
| Esencia de hielo (`ice`) | raro · elemento | Los golpes congelan (cada arma a su manera). Congelado + golpe fuerte = añicos. |
| Esencia de fuego (`fire`) | raro · elemento | Los golpes queman (cada arma a su manera). Congelado + fuego = deshielo. |
| Duplicador (`dup`) | legendario | Dobla los proyectiles de todas tus armas (dos singularidades se funden en una mayor). |
| Gatillo de cambio (`swap`) | raro | Tras cambiar de arma, el siguiente ataque hace más daño y es golpe fuerte. |
| Constelación (`const`) | raro | Cada pocos golpes cae una lanza pequeña sobre el último enemigo golpeado. |
| Corazón de vacío (`void`) | raro | Los golpes fuertes atraen un instante a los enemigos cercanos. |
| Cristal afilado (`sharp`) | común | Más daño en todos los golpes de arma. |
| Engranaje (`gear`) | común | Menos recarga en todas las armas. |
| Sangre de cristal (`blood`) | común | Cada enemigo eliminado cura un poco. |

Ajustes (`G.tuning.items`):

| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `sharpMul` | 1.2 | 1 – 3 (paso 0.05) | Cristal afilado: × daño |
| `gearMul` | 0.85 | 0.3 – 1 (paso 0.01) | Engranaje: × recarga |
| `swapMul` | 1.6 | 1 – 4 (paso 0.05) | Gatillo: × daño |
| `swapTime` | 1.5 | 0.2 – 5 (paso 0.1) | Gatillo: ventana (s) |
| `constEvery` | 8 | 2 – 30 (paso 1) | Constelación: cada N golpes |
| `voidRadius` | 130 | 30 – 400 (paso 5) | Corazón de vacío: radio |
| `voidTime` | 0.4 | 0.1 – 2 (paso 0.05) | Corazón de vacío: duración (s) |
| `bloodHeal` | 3 | 0 – 30 (paso 1) | Sangre de cristal: cura por muerte |
<!-- /gen:items -->

### Ajustes de las reacciones (`G.tuning.react`)
<!-- gen:react -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `frozenTime` | 2.5 | 0.5 – 6 (paso 0.1) | Congelado: duración (s) |
| `shatterDmg` | 45 | 0 – 150 (paso 1) | Añicos: daño extra |
| `shatterRadius` | 80 | 0 – 200 (paso 1) | Añicos: radio |
| `shatterSplash` | 15 | 0 – 80 (paso 1) | Añicos: daño alrededor |
| `vaporDmg` | 25 | 0 – 100 (paso 1) | Vapor: daño |
| `vaporRadius` | 70 | 0 – 200 (paso 1) | Vapor: radio |
<!-- /gen:react -->

## Cómo se pasa un arma a Unity (cuando el usuario la apruebe)

1. Leer su ficha entera (comportamiento + decisiones) y `ajustes.json` si existe (valores afinados).
2. Datos: los campos de `TUNING` pasan tal cual (mismos nombres, px del boceto × ~0,025 = unidades; s = s) a un bloque de
   ajustes del arma en `WeaponDefinition` (como `CometSettings` del Cometa: `docs/architecture/weapons-items.md`).
3. Dibujo: traducir las funciones de `fx.js` que usa a `FxCanvas` casi línea a línea (proceso y verificación con render fuera
   de pantalla: `docs/fx-proceso-bocetos.md`). Dos lienzos por efecto (detrás 2 / delante 40), como en la web (`back` / `front`).
4. Sonido: pasar las piezas `SND.*` a WAV con el mismo código (como `docs/bocetos/render-sonidos-cometa.cjs`) y calificarlas.
5. Proyectiles y efectos con `PrefabPool`; los sistemas comunes (estados, reacciones, captura, marcas) son código genérico,
   nunca dentro de un arma.
6. Probar contra lo mismo que en la web: la escena de prueba del arma reproduce el escenario de `jugable.html`.

## Historial

- 2026-10-19 (tarde) · El usuario pide separar mejoras de arma (árbol profundo, varias formas avanzadas) de items generales
  aplicables a todas, y una curva de roguelite (difícil de base; combo = romper la run). Primer item: Duplicador (dos
  singularidades se funden). Se prueba con Abanico y Golpe estelar → progresion.md.
- 2026-10-19 · Creadas las 6 armas (clips + jugable) y esta carpeta. El usuario: «me han encantado, sobre todo el potencial»;
  le gustó especialmente la combinación Singularidad + Abanico. Decide 2 armas de base (3 en builds especiales). Pide una
  versión jugable para probar combos antes de llevar nada a Unity.
