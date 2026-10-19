# Efectos dibujados por código: de boceto web a Unity

Proceso con el que se hicieron el dash, el picado y los impactos (los efectos que mejor han quedado en el
juego). Úsalo para cualquier efecto nuevo — próximo: **ataque a distancia del jugador** con proyectiles
hechos sólo de efectos, sin sprite.

## 1. Referencia → entender por fases
- El usuario pasa frames de referencia (capturas de otro juego) y, si hace falta, un garabato.
- Antes de programar, describir el efecto **por fases** (salida, viaje, rastro, llegada/impacto) y por
  **piezas** dentro de cada fase. Confirmar que se ha entendido.

## 2. Boceto HTML animado (barato, sin Unity)
- Un solo `.html` con `<canvas>`; plantilla: `docs/bocetos/dash-conceptos.html`.
- Varios conceptos lado a lado (p. ej. "sustituye al player" vs "efecto encima"), en bucle, con un muñeco
  de player para la escala.
- Controles obligatorios: **velocidad** (cámara lenta), **pausa**, **ver capas** (tiñe naranja lo que va
  detrás del player y verde lo que va delante).
- Todo en "px" del boceto; paleta del set (abajo). Comprobar sintaxis con `node --check` del `<script>`
  y abrirlo con `start "" archivo.html`.
- Iterar aquí lo que el usuario pida (formas, tiempos, capas). Es lo barato. No llevar a Unity hasta que
  diga que el concepto está bien.

## 3. Port a Unity
- **`Fx.FxCanvas`** (`Assets/Scripts/Fx/FxCanvas.cs`): lienzo que se vacía y redibuja cada frame en una
  malla con color por vértice (shader `RedMagic/Fx Vertex Color`, material
  `Assets/Art/Fx/Materials/Fx_VertexColor.mat` — asignarlo en el prefab para que el shader entre en la
  build). Primitivas: `Stroke`, `StrokeVar` (ancho/color por punto, `soft` = bordes a alfa 0), `StrokeSoft`,
  `Line` (con `taper`), `EllipseArc` (con trazos discontinuos), `Disc`, `Glow`, `Ellipse`, `Teardrop`,
  `Triangle`. Posiciones en mundo.
- **Dos lienzos por efecto**: `back` (sortingOrder 2, detrás del player que está en 10) y `front` (40).
  Repartir cada pieza según la profundidad: mitad lejana de anillos/ondas detrás, cercana delante;
  partículas por encima del centro detrás, por debajo delante. Esto es lo que da la sensación de volumen.
- Traducir el boceto casi línea a línea: medidas en "px" × `pixel` (≈0.016 u/px), tiempos con `Seg`,
  `EaseOut`, `EaseIn`, aleatorio determinista por semilla (`Rng`) para que el patrón no parpadee.
- **Que no parezcan formas geométricas** (lo que más subió la calidad):
  - trazos con **puntas afiladas** y grosor que **respira** (`StrokeVar` + `sin`);
  - radios con **ruido** suave (wobble) en anillos;
  - **varias pasadas de color** por pieza: resplandor morado suave → cian → núcleo blanco;
  - formas **compuestas de piezas pequeñas** (la onda de choque son hebras parciales + gotitas que
    resbalan + chispas + brillo, no una media luna sólida);
  - estelas que siguen el **camino real** y miden todo el recorrido, afiladas en la cola, con lenguas
    (ancho con ruido que se mueve) y gotas que se desprenden;
  - polvo con sombra y brillo (no discos planos).
- Componentes de referencia: `Fx.DashTrailFx` (salida + hilo + cometa + onda de choque + llamitas;
  `replaceTarget` oculta los SpriteRenderers del objetivo; `ReplacingFor`/`SetHeadTint` para que el parry
  tiña la bola) y `Fx.ImpactBurstFx` (suelo / enemigo).
- Pooling: `PrefabPool`; el componente se devuelve solo al pool cuando todo se ha apagado.
- Prefabs + asignación al jugador con un generador de menú (`Fx/Editor/DashFxPack.cs`) — sobrescribe.

## 4. Verificar sin Play Mode
- Render fuera de pantalla: escena **aditiva** temporal con cámara propia, mover el objetivo y avanzar
  el `_t` del efecto por reflexión, `cam.Render()` a RenderTexture, PNG al scratchpad, cerrar la escena
  sin guardar. Script de ejemplo: `docs/bocetos/sim-render-offscreen.cs.txt` (para `unity command eval`).
  Nunca tocar ni cerrar las escenas abiertas del usuario.
- Mirar 2–3 instantes (durante, al llegar, después) y corregir antes de pasarlo al usuario.

## Paleta del set (tajos melee, dash, picado, impactos)
| | RGB |
|---|---|
| Blanco / núcleo | `1, 1, 1` · núcleo cálido `1, 0.9, 0.92` |
| Cian claro | `0.62, 0.95, 1` |
| Cian | `0.16, 0.76, 1` |
| Azul profundo | `0.11, 0.36, 0.86` |
| Morado (resplandor / estela) | `0.46, 0.22, 0.86` |
| Polvo (sólo impacto de suelo) | `0.85, 0.8, 0.71` · oscuro `0.72, 0.65, 0.55` |

## Página de estados del ataque a distancia (`docs/bocetos/ataque-cometa-estados.html`)
Evolución de la plantilla para el ataque del jugador (cometa). Un solo `.html` con pestañas:
**✔ Guardados** (lo aprobado: 4 ataques — Base, Cadena, Perforación, Persecución — cada uno en versión base y UPGRADE),
**🛠 En curso** (sólo la ronda actual; al empezar otra, mover lo anterior a Guardados o Archivo), **📁 Archivo** (descartes) y
**🔊 Sonidos** (síntesis WebAudio para elegir carácter: carga, listo, disparo, impactos, primer golpe de la cadena).
- Cada clip tiene barra de tiempo (rebobinar, pasos ◀ ▶), línea de tiempo carga → «listo» (en bucle, ajustable) → soltar.
- Interruptores de cabecera: velocidad, ver capas, espera en «listo», impact frames, sonido y un selector por pieza de sonido.
- Los efectos se definen por parámetros (`up` = upgrade, `mark`/`amber` = homing, `proj` = forma del proyectil), no por copia de código.
- Decisiones tomadas: carga = orbe pequeño que recibe partículas + «listo» con pulso; perforador = punta de lanza; homing = esquinas de
  fijación + color ámbar; upgrade = «Upgrade A» (onda de choque de hebras, cola con capas oscuras y hilos intensos, impacto compacto
  estilo suelo del picado con temblor suave); sonidos: carga C turbina, listo A descarga seca, disparo A láser grave, impactos B energía eléctrica.
- Trampas conocidas: el `rand(seed)` del boceto debe mezclar la semilla (la primera tirada con semilla pequeña era ~0 y alineaba partículas);
  las ondas de choque van enteras por delante (no partidas detrás/delante del enemigo); las estelas nacen en el centro y acaban en punta.
- **Port a Unity hecho** (arma Cometa): ver `docs/architecture/weapons-items.md` ▸ Cometa. Verificado con render fuera de pantalla (vuelo de las 4 variantes, impactos, perforación, cadena y carga). Los sonidos elegidos se pasan a WAV con `docs/bocetos/render-sonidos-cometa.cjs` (mismo código de síntesis, en un OfflineAudioContext de Node).

## Página de prototipos de habilidades (`docs/bocetos/habilidades/`)
Sucesora de la página del cometa para **varias habilidades a la vez**: un archivo por habilidad (`a1-…a6-*.js`, cada uno con sus
sonidos, `build(P)` y sus clips) sobre una base común (`core.js`: escena, paletas por etiqueta —set, hielo, fuego, oro, vida,
vacío—, primitivas, enemigo con estados congelado/ardiendo/atraído, impactos). Filtro por habilidad, botón **⬇ vídeo** (webm de
una vuelta), pestañas 🧩 Sistemas de build y 🔊 Sonidos. Estado y siguiente paso: `docs/tareas/habilidades-prototipos.md`.
