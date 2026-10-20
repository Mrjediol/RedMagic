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

## Plantilla de enemigos
<!-- gen:plantilla -->
| Enemigo | `id` | Rol | Pregunta al jugador | Ficha |
|---|---|---|---|---|
| Embestidor | `charger` | Tanque de suelo que carga | Saltar o esquivar la carga en el último momento; colocarte delante de una pared para que se estampe y quede vendido. | [embestidor.md](embestidor.md) |
| Acechador | `stalker` | Cazador ágil de manada | Leer la finta (el agachado no siempre acaba en salto) y esquivar por debajo del arco; no dejarse rodear. | [acechador.md](acechador.md) |
| Artillero | `mortar` | Torreta enraizada: niega zonas a distancia | Acercarte esquivando los morteros (no quedarte quieto ni en los charcos) y castigarle cuando está abierto; de cerca se cierra y suelta púas. | [artillero.md](artillero.md) |
| Tejedora | `weaver` | Voladora a distancia que se teletransporta | Apartarte justo cuando el hilo de puntería se queda fijo (blanco), no antes; perseguirla o derribarla con armas a distancia porque huye y reaparece. | [tejedora.md](tejedora.md) |
| Caparazón | `shell` | Blindado con escudo frontal | Rodearlo saltando por encima (gira despacio), pegarle por la espalda o desde arriba, o romperle la guardia con un golpe fuerte; y saltar sus ondas. | [caparazon.md](caparazon.md) |
| Colmena | `hive` | Invocadora voladora (pare zánganos) | Prioridad: ir a por la colmena (lenta, alta, resistente) mientras los zánganos te pican, o limpiar zánganos con armas de área. | [colmena.md](colmena.md) |
| Zángano | `drone` | Cría de la Colmena: frágil, en grupo | Muchos objetivos pequeños que se lanzan en línea: aparta o barre (un golpe los mata) sin perder de vista lo grande. | [zangano.md](zangano.md) |
| Fatuo | `wisp` | Kamikaze flotante (bomba de doble filo) | Matarlo antes de que llegue, apartarte cuando se hincha, o aprovecharlo: su explosión también daña (mucho) a los otros enemigos. | [fatuo.md](fatuo.md) |
| Excavador | `burrower` | Emboscador subterráneo | No quedarte quieto: el montículo te persigue (más lento que tú corriendo) y sale justo debajo. Castigarle cuando está fuera y al hundirse; las plataformas te salvan de la salida. | [excavador.md](excavador.md) |
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
<!-- /gen:encuentros -->

## Cómo se pasa un enemigo a Unity (cuando el usuario lo apruebe)

1. Leer su ficha entera y `ajustes.json` si existe.
2. **Arte**: ahora sí, buscar el arte que encaje con la silueta y los movimientos de la ficha (skill `make-enemy` o pack), con las
   reglas de texturas (múltiplos de 4, ~100-128 px/u, atlas) y root en el centro del cuerpo.
3. **Comportamiento**: cada pieza genérica del motor tiene que existir como movimiento / ataque **genérico** de Unity
   (`docs/architecture/enemies.md`: `EnemyBrain`, la baraja de ataques `BossAttack`, defensas `EnemyDefence`), nunca dentro de un
   enemigo. Parece que ya hay (comprobar al portar): proyectil, abanico, anillo, embestida, salto atrás, onda de choque, zona. Falta seguro (de esta tanda):
   aguante/interrupción, turnos de ataque, escudo frontal por dirección, madriguera (hay `BossBurrowLocomotion` de jefe),
   teletransporte con marca, invocar crías, kamikaze que daña a enemigos, charco que daña.
4. **Datos**: los campos de `tuning` pasan con el mismo nombre (px × 0,025 = unidades; s = s) a `EnemyTuning` / la carta.
5. **Avisos**: con las piezas de aviso que ya hay (`FxTelegraph`, flechas, círculos), siempre del tamaño de lo que avisan.
6. Probar contra lo mismo: duelo + el encuentro de la ficha.

## Historial

- 2026-10-20 · El usuario pide hacer con los enemigos lo mismo que con las armas (prototipo web antes que Unity) y al revés que
  hasta ahora: comportamiento primero, arte después. Creada la jugable y 8 enemigos + la cría de la Colmena, sin probar por el
  usuario. Relacionado: tarea `t1` de `docs/tareas/todo.json` («15 enemigos: 5 por zona en las 3 primeras zonas»).
