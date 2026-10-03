# Traspaso — sesión del 2026-10-03 (FrostKnight, rendimiento del editor, 📋 Tareas y cola de Claude)

Documento de arranque para una sesión nueva tras `/clear`. Lee esto, luego `docs/tareas/prueba-coste.md` (lo que toca
hacer ahora). Rama: `task/humanoid-walker`. Idioma con el usuario: español, respuestas cortas (ver CLAUDE.md).

## 1 · Lo hecho hoy (todo en commits de la rama)

**Enemigo humanoide `FrostKnight`** (primer enemigo andante, plantilla nueva `groundHumanoid`)
- Arte: 8 piezas flotantes estilo "Fracture" (Cape, BackHand, BackFoot, Hips, Torso, Head, FrontFoot, WeaponHand) +
  variantes (Head: attacking/hurt/dead; WeaponHand: gesture_Slash; BackHand: gesture_DarkOrb). Originales a 1024 en
  `%LOCALAPPDATA%\Comfy-Desktop\ComfyUI-Shared\output\claude\` (base `kn_fix2_raw_00002_`, manos `kn2_BackHand_raw_*`),
  finales 2048 en `claude/knight/final/`. Prefab: `Assets/Prefabs/Enemies/Procedural/FrostKnight.prefab`; brief:
  `Web/RedMagicWeb/tools/comfy/enemies/FrostKnight.json`.
- **Andar** (`ProceduralRig.gait`, `RigDriverCode.ApplyGait`): pies que pisan sin patinar, ciclo por distancia; doc en
  `docs/architecture/procedural-rig.md` ▸ Walking. El usuario le dio un 5/10 ("decente, mejorable en el futuro").
- Gestos `Slash`, `DarkOrb`, `BackDash`; defensas (cerca 1,6 / al recibir golpe 45 % / a mitad de vida). Las defensas
  reproducen su gesto entero (`RigDriver.PlayMotion`; cambia también IceRobot/IceRobot2: su `Finger` ahora hace el golpe
  al final del dash — avisado, pendiente de que el usuario lo mire).
- **Apuntado por paso** (`RigGestureStep.aimParts`): varias piezas pueden apuntar (manos) con su ángulo de cañón;
  viaja dentro del gesto (web 🎬 → Guardar en Unity lo lleva). `DarkOrb` apunta la mano izquierda (−90°).
- Herramientas: `RigFilmstrip` (tiras de poses/paso desde el editor, sin Play), `compare-variant --part-of`,
  `contact-sheet --tight`, skill make-enemy con la regla "Fracture" y el prompt "borra X, deja solo Y".

**Rendimiento del editor**: el lag era el bug de Unity 6.3 con DX12 (`WaitForLastPresentation.WaitForGPU`). Arreglado con
`-force-d3d11` en Unity Hub (mínimos ~400 fps). El editor va a vSync (`PerformanceBootstrap`, solo `#if UNITY_EDITOR`).
`PlayModeProbe` (Tools ▸ RedMagic ▸ Sonda de rendimiento) está apagada por defecto (su captura a los 8 s da un tirón).
Memoria: `editor-stutter-is-dx12`.

**📋 Tareas + cola de Claude** (diseño completo: `docs/architecture/tareas.md`)
- Pestaña 📋 Tareas en la web (`modules/todo-panel.js`), lista en `docs/tareas/todo.json`.
- `Web/RedMagicWeb/tools/taller-server/` (`start.cmd`): sirve la web en `localhost:3000` y la API; cola que lanza
  `claude -p` de una en una (`--permission-mode auto --permission-prompts none`, deny de push/reset/rebase/clean),
  espera si hay una terminal de Claude abierta, abre ComfyUI/Unity si la tarea los necesita, informe bajo la tarea,
  "↩ Responder" reanuda la sesión. Probado de punta a punta con tareas de prueba (ya borradas).
- Regla nueva en CLAUDE.md ▸ "Pending work": mirar `todo.json` al empezar y avisar de choques; "déjalo pendiente" =
  escribir `docs/tareas/<id>.md` + tarea con `doc`/`steps`; no tocar los campos de estado (`status`, `result`…).

## 2 · Lo que se descubrió sobre el coste (decisivo)

- El usuario tiene el **plan Pro (18 €/mes), sin créditos de API**. El "0,10 $" que muestra la CLI es solo el precio de
  lista, no un cargo; lo que se gasta es el **límite de uso del plan** (ventana de 5 h y semanal). El estado salió en el
  evento `rate_limit_event` de la CLI: **76 % del límite semanal** gastado (hoy, casi todo esta sesión).
- Medido con `Web/RedMagicWeb/tools/taller-server/session-usage.mjs` sobre los transcripts de `~/.claude/projects/…`:
  - la sesión larga de hoy (293 llamadas, Opus casi todo): **14,2 M de unidades equivalentes**; 97,9 M de tokens leídos de
    caché (cada mensaje relee todo el contexto) y 326 k de salida;
  - una tarea lanzada por la cola con un prompt trivial: **~59 k** (arranque en frío ≈ 34 k tokens: system prompt,
    CLAUDE.md, índice de memoria).
  - Conclusión: lo caro es la **conversación larga**, no el arranque en frío. La cola vale la pena si las tareas son
    conversaciones pequeñas (justo el objetivo del usuario). Falta confirmarlo con una tarea real: ver `prueba-coste.md`.

## 3 · Pendiente (ordenado)

1. **Prueba de coste terminal vs web** (`docs/tareas/prueba-coste.md`) — lo siguiente.
2. **Ajustes de la cola por el plan Pro** (propuestos al usuario, esperando su "sí"): mostrar en la web el uso real
   (5 h / semana, % que ya viene en `rate_limit_event`) en vez de dólares; parar la cola sola por encima de un umbral
   (p. ej. 85 % semanal); Sonnet + esfuerzo bajo/medio por defecto (`config.json` ya trae sonnet/medium). Quitar el
   `$` de los informes de la web (`todo-panel.js ▸ resultBlock`).
3. `.claude/settings.json` permite `git push` (contradice CLAUDE.md "nunca push"): proponer quitarlo.
4. **FrostKnight**: el estado `DarkOrb` de la mano es un cambio muy fuerte (todo violeta con runas) y no tiene efecto de
   carga visible — el efecto `orbCharge` va en el gesto (escala 1, sospecha de que se ve diminuto): revisar tamaño y
   rehacer la variante más sutil desde `kn2_BackHand_raw_00001_.png` (1024). Sonido de pasos no sincronizado con las
   pisadas. Previsualizar `gait` en la web. Guardar el cuerpo desde Enemy Studio sobre un enemigo existente (hoy solo
   gestos/ataques) — el usuario lo aceptó como función aparte.
5. Cambios sin commit que NO son de estas tareas (no tocar sin preguntar): prefabs IceRobot/ShroomShaman/koros,
   `Assets/Scenes/Ice1.unity`, `ProjectSettings/EditorBuildSettings.asset`, JSON de `Assets/Resources/PerformanceTest*`
   borrados.
6. Fase 3 de 📋 Tareas: galería de sprites (salida de Comfy) para elegir y mandar de vuelta como tarea. Fase 4: móvil con
   Tailscale + token + notificación (ntfy/Telegram) al vaciarse la cola.
7. `RigConfigRoundTrip`: un caso sintético falla ("sprites con nombre reimportados son 5, no 4"); no es del trabajo de hoy,
   no se confirmó si ya fallaba antes.

## 4 · Preferencias del usuario vistas hoy

Respuestas terse; que decida yo cuando haya una opción clara; pruebas de juego las hace él (listas cortas en el chat);
arte siempre en estilo Fracture (piezas flotantes con huecos); cada enemigo nuevo = herramienta reutilizable; quiere
conversaciones pequeñas por tarea para gastar menos del plan. Modelo/esfuerzo se cambia con `/model` y `/effort`.
