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
