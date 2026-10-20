# Armas — fichas (fuente de verdad entre la web y Unity)

Desde 2026-10-19 las armas nuevas se diseñan y afinan **en la web** antes de tocar Unity. Esta carpeta es lo que una sesión nueva
(tras `/clear`) lee para saber qué hay, qué se decidió y cómo portarlo. **Regla: cualquier cambio de un arma en la web actualiza su
ficha en la misma sesión** (comportamiento, decisiones) y se vuelve a ejecutar `gen-fichas` (árbol, ajustes, items).

**Modelo de progresión** (2026-10-19, en prueba): [progresion.md](progresion.md). Tres vías de mejora: **árbol propio del arma**
(evolución en nivel 2 entre 3 formas avanzadas + 3 mejoras de esa evolución), **sinergias generales** (reacciones, captura,
marcas) e **items generales** con rareza (elemento, Duplicador…) que funcionan en todas las armas porque cada arma define cómo
los expresa. Árbol completo hoy en las seis armas.

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
| Servidor local (Claude in Chrome no abre file://) | `node docs/bocetos/habilidades/tools/serve.cjs` → http://127.0.0.1:8765/habilidades/jugable.html |
| Retomar el trabajo de armas | `docs/tareas/habilidades-prototipos.md` ▸ «RETOMAR AQUÍ» |
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
| Reloj de arena | `time` | burbuja de tiempo; potencia a la otra arma | [reloj.md](reloj.md) |
| Ráfaga | `gust` | soplo que empuja y lanza (control) | [rafaga.md](rafaga.md) |
| Látigo de luz | `whip` | latigazo con chasquido en la punta; encadena, engancha | [latigo.md](latigo.md) |
| Jabalina | `spear` | se carga, se clava y hay que recuperarla | [jabalina.md](jabalina.md) |
| Enjambre | `swarm` | luciérnagas que pican solas; compañero, contagio o faroles | [enjambre.md](enjambre.md) |
| Tótem de cristal | `totem` | torreta plantada; copia tus golpes, dispara o provoca | [totem.md](totem.md) |
| Espejo | `mirror` | desvía tus proyectiles y el Rayo hacia los enemigos; lupa; portales | [espejo.md](espejo.md) |
| Acumulador | `volt` | se carga con los golpes de la otra arma y descarga | [acumulador.md](acumulador.md) |

Armas 7+ (2026-10-20, hechas sin el usuario, **sin probar**): dibujo en `fx-armas2.js`, pruebas dirigidas en
`tools/test-armas2.cjs` (usa `tools/sim.cjs`, que carga la jugable en Node para medir lo que hace un arma).

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
- **Efectos «al golpear» de cualquier arma** (2026-10-20): una instancia de arma puede tener `onAnyHit(e, o, dmg)` y
  `onAnyKill(e, o)`; el motor los llama para **todas** las armas equipadas (también la que no está en la mano) en cada golpe de
  arma. Los golpes que nacen de otro golpe (ecos, rayos del pararrayos, cadenas…) llevan `proc: true` y no vuelven a disparar
  estos efectos → nunca hay bucles. Así se hacen las mejoras que funcionan con el arma guardada (Reloj · Eco…).
- **Tiempo propio del enemigo** (`e.timeK`, lo pone cada frame quien lo ralentiza; 0 = parado): andar, empujones, gravedad,
  ataques, quemadura y congelación van a ese ritmo. Las balas enemigas igual. **Estasis** (`e.stasis`): el daño se guarda y
  no puede morir hasta que se suelta (`o.release`).
- **Vulnerable** (`makeVuln(e, k, s)`): recibe ×k de todo durante s (anillo rosa que late). **Aturdido** (`stunT`): estrellitas.
- **Lanzado** (`o.launch` = s): un enemigo lanzado que choca a velocidad con otro hace daño a los dos (`impactDmg` +
  `impactPerSpeed` × velocidad; golpe fuerte si va rápido → contra un congelado, añicos). Con `o.pins`, bolos.
- **Marcas**: con el Golpe estelar en evolución **Cazador** equipado, los golpes de las otras armas marcan (8 s); el Golpe
  estelar cae sobre los marcados sin apuntar. Con Acumulación se apilan hasta 3 (una por ataque).
- **Build** (como el juego): 6 items, cada uno con una etiqueta elemental (Hielo / Fuego / Oro) y una universal (Tanque / Prisa /
  Robo de vida / Reset); tramos a 2 / 4 / 6. **El elemento se aplica con 4** de ese elemento (sólo uno puede llegar a 4); el
  tramo 2 es un bonus compatible (2 hielo = recarga, 2 fuego = daño). Cada arma expresa el elemento a su manera (progresion.md).
- **Modo run en el panel**: «🎲 Subir de nivel» ofrece 2 evoluciones al azar (nivel 2) y luego 2 mejoras al azar de las que
  faltan; «🎁 Cofre» ofrece 3 items sorteados por rareza (común 50 · raro 30 · épico 15 · legendario 5). En modo libre se marca todo a mano.
- **Dificultad**: vida de los enemigos ×0,5-×4 y que disparen.
- **Feel**: hit-stop (micro-pausa) e impact frames (pantalla oscura + enemigo blanco) en golpes fuertes; temblor de cámara.

### Sinergias e items (vía 3)
<!-- gen:items -->
**Sinergias**

| Etiqueta | 2 | 4 | 6 |
|---|---|---|---|
| **Hielo** (elemental) | −15 % de recarga en todas las armas | Tus golpes aplican HIELO (cada arma a su manera; congelado + golpe fuerte = añicos) | Congelado dura +50 % y los añicos hacen el doble |
| **Fuego** (elemental) | +15 % de daño en todas las armas | Tus golpes aplican FUEGO (quemadura; congelado + fuego = deshielo) | Al morir un enemigo quemado, la quemadura salta a los cercanos |
| **Oro** (elemental) | +15 % de monedas (sin efecto en el prototipo) | +5 % de golpes dorados (×2 de daño) | Botín ×3 con la marca de oro (sin efecto en el prototipo) |
| **Tanque** (universal) | +20 % de vida máxima | Regeneras 1 de vida por segundo | Escudo de 30 al bajar del 30 % de vida (una vez) |
| **Prisa** (universal) | Con un enemigo con hielo en pantalla, la recarga corre ×1,5 | −20 % de recarga | −35 % de recarga |
| **Robo de vida** (universal) | Cada baja cura 3 | 10 % de robo de vida en los golpes de arma | La curación sobrante se convierte en escudo (hasta 30) |
| **Reset** (universal) | Cada baja quita 2 s a la recarga del arma activa | Cada baja quita también 2 s a la de reserva | Cada baja recarga del todo el arma activa |

**Items**

| Item (id) | Rareza | Etiquetas | Efecto propio |
|---|---|---|---|
| Anillo Glacial (`anillo`) | raro | Hielo + Robo de vida | Los golpes a enemigos con hielo hacen un extra según su vida máxima. |
| Bastón Helado (`baston`) | legendario | Hielo + Prisa | Al matar, el siguiente ataque hace ×3 y el arma activa se recarga. |
| Botas Rúnicas (`botasR`) | épico | Hielo + Prisa | Con un enemigo con hielo en pantalla, la recarga corre ×1,5. |
| Capa de Escarcha (`capa`) | épico | Hielo + Reset | Al matar, un fragmento de hielo sale del cuerpo hacia el enemigo más cercano. |
| Grimorio Glacial (`grimorio`) | raro | Hielo + Reset | Los enemigos con hielo reciben +30 % de daño. |
| Yelmo Rúnico (`yelmo`) | legendario | Hielo + Robo de vida | Cada 5 bajas, estallido de hielo a tu alrededor que congela. |
| Duplicador (`dup`) | legendario | Fuego + Reset | Dobla los proyectiles de todas tus armas (dos singularidades se funden en una mayor). |
| Gatillo de cambio (`swap`) | épico | Fuego + Prisa | Tras cambiar de arma, el siguiente ataque hace más daño y es golpe fuerte. |
| Constelación (`const`) | épico | Fuego + Reset | Cada pocos golpes cae una lanza pequeña sobre el último enemigo golpeado. |
| Corazón de vacío (`void`) | raro | Fuego + Tanque | Los golpes fuertes atraen un instante a los enemigos cercanos. |
| Cristal afilado (`sharp`) | común | Fuego + Robo de vida | +20 % de daño en todos los golpes de arma. |
| Sangre de cristal (`blood`) | común | Fuego + Robo de vida | Cada baja cura un poco. |
| Engranaje (`gear`) | común | Hielo + Prisa | −15 % de recarga en todas las armas. |
| Espada de Oro (`espadaO`) | épico | Oro + Robo de vida | +20 % de daño (y monedas al matar, sin efecto aquí). |
| Botas de Oro (`botasO`) | legendario | Oro + Prisa | Golpes dorados (×2) con probabilidad que sube con el daño del golpe. |
| Guanteletes de Oro (`guantesO`) | épico | Oro + Prisa | Cada 4 golpes, uno dorado (×2). |

Ajustes (`G.tuning.items`):

| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `ice2Cooldown` | 0.85 | 0.5 – 1 (paso 0.01) | Hielo 2: × recarga |
| `fire2Damage` | 1.15 | 1 – 2 (paso 0.01) | Fuego 2: × daño |
| `sharpMul` | 1.2 | 1 – 3 (paso 0.05) | Cristal afilado: × daño |
| `gearMul` | 0.85 | 0.3 – 1 (paso 0.01) | Engranaje: × recarga |
| `swapMul` | 1.6 | 1 – 4 (paso 0.05) | Gatillo: × daño |
| `swapTime` | 1.5 | 0.2 – 5 (paso 0.1) | Gatillo: ventana (s) |
| `constEvery` | 8 | 2 – 30 (paso 1) | Constelación: cada N golpes |
| `voidRadius` | 130 | 30 – 400 (paso 5) | Corazón de vacío: radio |
| `voidTime` | 0.4 | 0.1 – 2 (paso 0.05) | Corazón de vacío: duración (s) |
| `bloodHeal` | 3 | 0 – 30 (paso 1) | Sangre de cristal: cura por baja |
| `anilloPct` | 0.04 | 0 – 0.2 (paso 0.005) | Anillo Glacial: extra (× vida máx. del enemigo) |
| `grimorioMul` | 1.3 | 1 – 3 (paso 0.05) | Grimorio: × daño a enemigos con hielo |
| `yelmoKills` | 5 | 1 – 20 (paso 1) | Yelmo: bajas por estallido |
| `yelmoDamage` | 40 | 0 – 200 (paso 1) | Yelmo: daño |
| `yelmoRadius` | 160 | 30 – 400 (paso 5) | Yelmo: radio |
| `gildChance` | 0.05 | 0 – 0.5 (paso 0.01) | Oro 4: probabilidad de golpe dorado |
| `botasOBase` | 0.05 | 0 – 0.5 (paso 0.01) | Botas de Oro: probabilidad base |
| `botasOPerDmg` | 0.004 | 0 – 0.05 (paso 0.001) | Botas de Oro: + por punto de daño |
| `botasOMax` | 0.5 | 0 – 1 (paso 0.05) | Botas de Oro: máximo |
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
| `impactDmg` | 10 | 0 – 80 (paso 1) | Lanzado: daño base del choque |
| `impactPerSpeed` | 0.035 | 0 – 0.2 (paso 0.005) | Lanzado: + daño por px/s de velocidad |
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
