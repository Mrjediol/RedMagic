# Enemigos diseñados en la web — fichas (fuente de verdad entre la web y Unity)

Desde 2026-10-20 los enemigos se pueden diseñar **al revés** que hasta ahora: primero el comportamiento (movimientos, avisos,
ventanas de castigo) en una jugable web, y cuando el usuario lo aprueba se busca el arte que encaje. Mismo método que las armas
(`docs/armas/README.md`). **Regla: cualquier cambio de un enemigo en la web actualiza su ficha en la misma sesión** («Decisiones y
feedback» a mano) y se vuelve a ejecutar `gen-fichas` (diseño y ajustes salen del código).

No confundir con `docs/enemigos/` (biblioteca de enemigos ya montados en Unity con su arte) ni con `docs/disenos/` (propuestas del
taller 🎨 Diseños).

## Dónde está cada cosa

| | |
|---|---|
| Jugable (arena con paredes y plataformas, duelos y encuentros, tus armas y tu build) | `docs/bocetos/enemigos/jugable.html` |
| Motor de enemigos: piezas genéricas (avisos, disparo, mortero, onda, charco, explosión, aguante, turnos), estados, dibujo | `docs/bocetos/enemigos/motor.js` |
| Un enemigo = un archivo (diseño + IA + dibujo provisional + `tuning`) | `docs/bocetos/enemigos/e-*.js` |
| Encuentros predefinidos (combinaciones) | `docs/bocetos/enemigos/encuentros.js` |
| Panel de enemigos (encuentro, resultado, fichas, opciones, ajustes con ⬇ exportar) | `docs/bocetos/enemigos/ui.js` |
| Player, armas, daño, reacciones, build y su panel: los de la jugable de armas, sin copiar | `docs/bocetos/habilidades/` (`game.js`, `g-*.js`, `jugable-ui.js` con `window.JUGABLE_PAGE`) |
| Prueba sin navegador (cada enemigo hace todos sus movimientos, encuentros contra un bot, casos dirigidos) | `node docs/bocetos/enemigos/tools/test.cjs [id]` |
| Fichas (diseño + ajustes desde el código) | `node docs/bocetos/enemigos/tools/gen-fichas.cjs` |
| Servidor local (Claude in Chrome no abre file://) | `node docs/bocetos/habilidades/tools/serve.cjs` → http://127.0.0.1:8765/enemigos/jugable.html |
| Ajustes afinados por el usuario | `docs/enemigos-diseno/ajustes.json` (⬇ Exportar ajustes en el panel, guardado aquí) |

## Principios de diseño (los que hacen divertido pelear)

- **Cada enemigo hace UNA pregunta distinta al jugador** (saltar a tiempo, leer una finta, elegir prioridad, rodear, no quedarse
  quieto…). Dos enemigos que preguntan lo mismo sobran; la variedad sale de mezclar preguntas en un encuentro.
- **Preparación → golpe → recuperación.** Todo ataque se avisa y deja una ventana de castigo después (derrape, aterrizaje,
  hundirse, guardia rota). El jugador agresivo que lee bien gana.
- **Lenguaje de avisos común** (motor): el cuerpo se pone naranja y el ojo blanco mientras prepara; destello blanco = sale el
  golpe; rojo en el suelo / aire = zona que va a doler, el relleno crece hasta el borde = cuánto falta, borde blanco = ya;
  estrellas = aturdido; anillo rosa = recibe más daño. **Tamaño del aviso = tamaño de lo que avisa**, con mínimo legible
  (regla de siempre del proyecto).
- **Aguante (poise):** el daño recibido mientras prepara se acumula (barra blanca bajo la vida; golpe fuerte cuenta ×2); al
  llenarse, **interrumpido**. Durante el golpe no se interrumpe. Congelar o aturdir mientras prepara también interrumpe.
- **Turnos:** como mucho `Atacando a la vez` enemigos (3) preparan o ejecutan un ataque a la vez → las multitudes son justas.
- **Contacto flojo**, el daño de verdad está en los ataques avisados.
- **Los sistemas de las armas valen contra ellos**: sus balas son las mismas que las de prueba (Égida, Espejo, Singularidad ·
  Escudo de vacío, Reloj, Ráfaga, Látigo, Media luna y Tótem las desvían o capturan), se congelan, se queman, se lanzan, se
  atraen. Algunos además usan a los demás enemigos: el Embestidor los atropella, el Fatuo los revienta.
- **Propuesta abierta: dash invulnerable** (opción del panel, encendida). Los diseños cuentan con esquivar con el dash;
  en Unity el player no lo tiene aún (sólo la invulnerabilidad tras recibir un golpe). Decidirlo antes de portar.

## Niveles de complejidad (decisión del 2026-10-20, en prueba)

| Nivel | Movimientos | Cuántos a la vez | Para qué |
|---|---|---|---|
| **Común** | 1 principal + 1 de situación (de cerca o hacia arriba) | muchos | material para la build; la variedad sale de mezclarlos |
| **Élite** | 3-4 (combos, variaciones, una furia); más vida y aguante | 1 por encuentro | el enemigo "fuerte": se aprende porque dura |
| **Minijefe / jefe** | set completo, fases | solo | cierre de zona |

- Un movimiento sólo se aprende si se ve muchas veces: un común muere en 3-10 s, así que más de 2-3 movimientos no se llegan a ver.
- **Élite = versión de un común** (`e-elites.js`: misma IA, dibujo y ajustes con otros valores + giros). Variar un movimiento
  conocido (carga encadenada, doble salto) antes que añadir uno nuevo.
- Un élite ocupa `Turnos que ocupa un élite` (2) de los `Atacando a la vez` (3): con él en pantalla, el resto ataca menos.

## Verticalidad: ninguna plataforma es un sitio seguro (decisión del 2026-10-20, en prueba)

Problema del usuario: con muchas plataformas, ¿qué hace un enemigo de suelo si estás encima? Respuesta a probar en la jugable:

- **Todo enemigo de suelo tiene una respuesta hacia arriba, y tiene que ser FÍSICA** (lo que sube o llega de verdad):
  Embestidor (la carga acaba en un salto a tu plataforma si es baja; si es alta, espera debajo), Caparazón (te lanza una
  piedra en parábola), Excavador (la columna atraviesa plataformas), Acechador (salta a tu plataforma), Artillero (parábola),
  Erizo de escarcha (lluvia de púas desde arriba). Un melee de suelo sin respuesta hacia arriba no entra.
- ❌ **Descartado (usuario, 2026-10-20): sacudir plataformas a distancia** («sin estar conectada la plataforma con el suelo no
  tiene coherencia»). Nada afecta a una plataforma o te tira de ella sin tocarla. Un empujón de viento sí vale (es físico).
- **Suelo y aire se reparten el trabajo**: los de suelo te echan arriba, los voladores castigan estar arriba.
- **Arena** (selector del panel): plana · pocas plataformas bajas · vertical (como los mapas). Opción **plataformas que se
  rompen** al estar encima (`crumbleT`, 1 s) y vuelven (`respawnT`, 3 s): subir a esquivar sí, acampar no.
- Propuesta de niveles para el usuario: zonas de combate con mucho suelo y pocas plataformas bajas; lo vertical para ir de
  una zona a otra. En móvil (táctil) saltar entre plataformas mientras apuntas es mucho más difícil.
- Coste en Unity: hoy los de suelo no suben a plataformas (sólo sondean bordes); saltar a ellas / alcanzar arriba es trabajo
  nuevo pero genérico.

## Mundo de hielo (2026-10-20, sin probar por el usuario)

**Regla del usuario:** el hielo es **temática visual** (carámbanos, nieve, bloques de hielo, pelo blanco, escarcha) con alguna
mecánica ligera. **Ningún enemigo congela ni ralentiza por ser «de hielo».** Sólo un ataque concreto que antes habría aturdido
congela 1 s (mismo efecto que un aturdido, con aspecto de hielo): la **Pinza helada del Ermitaño**. Pieza genérica
`freezePlayer(s)`: sin moverse, saltar, dash ni disparar; bloque de hielo alrededor del player.

| Enemigo | Nivel | Qué aporta | Lo «de hielo» |
|---|---|---|---|
| Colgante | común | emboscada desde el techo; castigo cuando se clava | es un carámbano vivo; metralla de hielo |
| Erizo de escarcha | común | lluvia desde arriba (también en plataformas); ventana «pelado» | púas = carámbanos |
| Ventisca | común | empuje (sin daño) que combina con todo; el dash lo atraviesa | espíritu de nieve, bolas de nieve |
| Ermitaño de hielo | común | dos fases (coraza → cangrejo rápido); el único que congela | concha de bloque de hielo |
| Morsa | común | la arena cambia: muros de carámbanos que se quedan | colmillos y pinchos de hielo |
| Morsa matriarca | élite | muros dobles + tobogán que rebota | — |

- Los comunes genéricos también sirven en el hielo cambiando sólo el arte (p. ej. Acechador = lobo de escarcha, Fatuo =
  luz boreal); no hace falta que todo el mundo sea nuevo.
- La arena de los encuentros de hielo tiene techo de carámbanos (`CEIL`), del que cuelgan los Colgantes.
- Piezas genéricas nuevas: `freezePlayer`, `groundSpike` (pincho que sale y se queda), aviso `cone`, `later` (secuencias),
  proyectiles `icicle` / `snow` / `ice`, empuje de viento (en la Ventisca; si otro lo necesita, sacarlo a pieza).
- El **Arte** de cada ficha describe silueta, materiales, colores, estados y piezas, para buscarlo o generarlo después
  (con las reglas de texturas: múltiplos de 4, ~100-128 px/u, atlas por personaje; tamaños en unidades en cada ficha).

## Plantilla de enemigos
<!-- gen:plantilla -->
| Enemigo | Nivel | `id` | Rol | Pregunta al jugador | Ficha |
|---|---|---|---|---|---|
| Embestidor | común | `charger` | Tanque de suelo que carga | Saltar o esquivar la carga en el último momento; colocarte delante de una pared para que se estampe y quede vendido. | [embestidor.md](embestidor.md) |
| Acechador | común | `stalker` | Cazador ágil de manada | Leer la finta (el agachado no siempre acaba en salto) y esquivar por debajo del arco; no dejarse rodear. | [acechador.md](acechador.md) |
| Artillero | común | `mortar` | Torreta enraizada: niega zonas a distancia | Acercarte esquivando los morteros (no quedarte quieto ni en los charcos) y castigarle cuando está abierto; de cerca se cierra y suelta púas. | [artillero.md](artillero.md) |
| Tejedora | común | `weaver` | Voladora a distancia que se teletransporta | Apartarte justo cuando el hilo de puntería se queda fijo (blanco), no antes; perseguirla o derribarla con armas a distancia porque huye y reaparece. | [tejedora.md](tejedora.md) |
| Caparazón | común | `shell` | Blindado con escudo frontal | Rodearlo saltando por encima (gira despacio), pegarle por la espalda o desde arriba, o romperle la guardia con un golpe fuerte; y saltar sus ondas. | [caparazon.md](caparazon.md) |
| Colmena | común | `hive` | Invocadora voladora (pare zánganos) | Prioridad: ir a por la colmena (lenta, alta, resistente) mientras los zánganos te pican, o limpiar zánganos con armas de área. | [colmena.md](colmena.md) |
| Zángano | común | `drone` | Cría de la Colmena: frágil, en grupo | Muchos objetivos pequeños que se lanzan en línea: aparta o barre (un golpe los mata) sin perder de vista lo grande. | [zangano.md](zangano.md) |
| Fatuo | común | `wisp` | Kamikaze flotante (bomba de doble filo) | Matarlo antes de que llegue, apartarte cuando se hincha, o aprovecharlo: su explosión también daña (mucho) a los otros enemigos. | [fatuo.md](fatuo.md) |
| Excavador | común | `burrower` | Emboscador subterráneo | No quedarte quieto: el montículo te persigue (más lento que tú corriendo) y sale justo debajo. Castigarle cuando está fuera y al hundirse; las plataformas te salvan de la salida. | [excavador.md](excavador.md) |
| Colgante | común | `icicle` | Emboscador del techo (carámbano vivo) | Mirar también arriba: pasar por debajo lo hace caer. Provocar la caída a propósito y apartarse, para pegarle mientras está clavado en el suelo y mientras vuelve a subir. | [colgante.md](colgante.md) |
| Erizo de escarcha | común | `urchin` | Artillero de suelo: lluvia desde arriba | Salir de las marcas de la lluvia (caen también en tu plataforma: subir no salva) y castigarle cuando se queda sin púas; no pegarte a él cuando se eriza. | [erizo-escarcha.md](erizo-escarcha.md) |
| Ventisca | común | `gale` | Voladora de control: te empuja | Posicionarte contra el viento: el soplo te empuja (físicamente: sí te saca de una plataforma) hacia lo que tengas detrás. El dash lo atraviesa. Y esquivar sus bolas de nieve, que revientan en trozos. | [ventisca.md](ventisca.md) |
| Ermitaño de hielo | común | `hermit` | Blindado en dos fases (coraza → cangrejo) | Romperle la coraza (los golpes fuertes la agrietan mucho más) sin ponerte a tiro de su pinza, que te congela un segundo y te remata. Roto, cambia de ritmo: pequeño, rápido y frágil. | [ermitano.md](ermitano.md) |
| Morsa | común | `walrus` | Pesada de control de suelo | Saltar la hilera de carámbanos que avanza hacia ti y no quedarte encerrado entre los pinchos que deja (duran un rato). Si te subes, te lanza un témpano. | [morsa.md](morsa.md) |
| Embestidor alfa | élite | `chargerAlpha` | Élite del Embestidor: cargas encadenadas y furia | Lo mismo que el Embestidor, pero sin respiro: tras una carga que esquivas viene otra enseguida. Ponerte delante de la pared corta la cadena (el choque sigue siendo tu premio). | [embestidor-alfa.md](embestidor-alfa.md) |
| Acechador alfa | élite | `stalkerAlpha` | Élite del Acechador: jefe de manada | Prioridad y lectura: si no lo matas, aúlla y su manada va más rápido (y si está solo, la llama). De cerca no huye: zarpazo. Sus saltos vienen de dos en dos. | [acechador-alfa.md](acechador-alfa.md) |
| Morsa matriarca | élite | `walrusAlpha` | Élite de la Morsa: muros dobles y tobogán | La Morsa, pero te encierra: los muros salen a los dos lados y cuando estás entre pinchos se tira en tobogán, rebota en la pared y vuelve. Hay que saltarla dos veces sin caer en los pinchos. | [morsa-matriarca.md](morsa-matriarca.md) |
<!-- /gen:plantilla -->

## Encuentros
<!-- gen:encuentros -->
| Encuentro | Quién | Qué prueba |
|---|---|---|
| Manada | Acechador ×3 | 3 Acechadores: siempre hay uno a tu espalda; se turnan para saltar. |
| Muro y cañón | Caparazón, Artillero ×2 | Caparazón delante, dos Artilleros detrás: rodea el escudo mientras caen morteros. |
| Plaga | Colmena, Fatuo ×2 | Colmena + 2 Fatuos: muchos objetivos; un fatuo bien usado limpia zánganos. |
| Emboscada | Excavador, Tejedora | Excavador + Tejedora: muévete por el montículo y por la aguja a la vez. |
| Estampida | Embestidor ×2, Fatuo | 2 Embestidores + 1 Fatuo: haz que se estampen contra la pared o que carguen hacia el fatuo. |
| Cielo cubierto | Tejedora ×2, Colmena | 2 Tejedoras + Colmena: todo vuela; armas a distancia o subir a las plataformas. |
| Todos | Embestidor, Acechador, Artillero, Tejedora, Caparazón, Colmena, Fatuo, Excavador | Uno de cada (sin zánganos sueltos): caos para probar el máximo de atacantes a la vez. |
| Jefe de manada | Acechador alfa, Acechador ×2 | Acechador alfa + 2 Acechadores: mátalo pronto o aúlla (y si quedan pocos, llama a más). |
| Toro y cañón | Embestidor alfa, Artillero | Embestidor alfa + Artillero: cargas encadenadas mientras caen morteros. |
| Cueva de carámbanos | Colgante ×2, Erizo de escarcha | 2 Colgantes + Erizo de escarcha: todo cae desde arriba; mira al techo y al suelo. |
| Tundra | Morsa, Ventisca ×2 | Morsa + 2 Ventiscas: el viento te empuja contra los muros de carámbanos. |
| Orilla helada | Ermitaño de hielo ×2, Erizo de escarcha | 2 Ermitaños + Erizo: no te dejes agarrar mientras esquivas la lluvia. |
| Matriarca | Morsa matriarca, Colgante | Morsa matriarca + Colgante: tobogán y muros mientras algo cuelga encima. |
| Todo el hielo | Colgante, Erizo de escarcha, Ventisca, Ermitaño de hielo, Morsa | Uno de cada común del hielo, para ver cómo se mezclan. |
<!-- /gen:encuentros -->

## Cómo se pasa un enemigo a Unity (cuando el usuario lo apruebe)

1. Leer su ficha entera y `ajustes.json` si existe.
2. **Arte**: ahora sí, buscar el arte que encaje con la silueta y los movimientos de la ficha (skill `make-enemy` o pack), con las
   reglas de texturas (múltiplos de 4, ~100-128 px/u, atlas) y root en el centro del cuerpo.
3. **Comportamiento**: cada pieza genérica del motor tiene que existir como movimiento / ataque **genérico** de Unity
   (`docs/architecture/enemies.md`: `EnemyBrain`, la baraja de ataques `BossAttack`, defensas `EnemyDefence`), nunca dentro de un
   enemigo. Parece que ya hay (comprobar al portar): proyectil, abanico, anillo, embestida, salto atrás, onda de choque, zona. Falta seguro (de esta tanda):
   aguante/interrupción, turnos de ataque, escudo frontal por dirección, madriguera (hay `BossBurrowLocomotion` de jefe),
   teletransporte con marca, invocar crías, kamikaze que daña a enemigos, charco que daña, saltar a plataformas (carga con salto), élite = común + movimientos enganchados (cadenas, furia), congelar al player 1 s, pincho de suelo que se queda, viento que empuja, colgar del techo.
4. **Datos**: los campos de `tuning` pasan con el mismo nombre (px × 0,025 = unidades; s = s) a `EnemyTuning` / la carta.
5. **Avisos**: con las piezas de aviso que ya hay (`FxTelegraph`, flechas, círculos), siempre del tamaño de lo que avisan.
6. Probar contra lo mismo: duelo + el encuentro de la ficha.

## Historial

- 2026-10-20 · El usuario pide hacer con los enemigos lo mismo que con las armas (prototipo web antes que Unity) y al revés que
  hasta ahora: comportamiento primero, arte después. Creada la jugable y 8 enemigos + la cría de la Colmena, sin probar por el
  usuario. El usuario: «no están mal», pero no ve cómo encajan los de suelo (sobre todo melee) con tantas plataformas, y
  pregunta si hacer enemigos más complejos. Decidido probar: respuesta hacia arriba en todos los de suelo, 3 arenas +
  plataformas que se rompen, y niveles común / élite (primeros élites: Embestidor alfa y Acechador alfa). Después: no le
  gusta la sacudida de plataformas a distancia (sin coherencia física) → quitada; en su lugar carga con salto y pedrada.
  Pide un set para el mundo de hielo con la temática visual y mecánicas ligeras (ver «Mundo de hielo»). Relacionado: tarea `t1` de `docs/tareas/todo.json` («15 enemigos: 5 por zona en las 3 primeras zonas»).
