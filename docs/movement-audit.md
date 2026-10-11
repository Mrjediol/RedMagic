# Auditoría de movimiento — player y enemigos (2026-10-11)

Contexto: decisión definitiva — **sin rampas ni superficies inclinadas**; el terreno es Tilemap (celdas cuadradas,
`Level` + `LevelShape`). Esta auditoría lista qué código de rampas sobra y revisa coyote time e input buffer
(salto y dash). **No se ha cambiado código**: es el inventario para el refactor.

---

## 1. Resumen

| Tema | Estado | Veredicto |
|---|---|---|
| Código de rampas (player) | ~35 % de `PlayerMovement.cs` (2147 líneas) existe sólo para rampas | Eliminar (lista §2.1) |
| Código de rampas (enemigos) | `GroundMotion` (Probe/AlongSlope/Halt) + 5 llamadas en `EnemyBrain`/`EnemyController` | Eliminar (§2.2) |
| **Geometría del nivel** | El collider del Tilemap tiene **esquinas redondeadas** (radio 0.4, 4 segmentos) → micro-rampas en cada esquina | **Bloqueante**: hacer el collider cuadrado ANTES de quitar el código (§2.3) |
| Coyote time | **Sí existe** (0.1 s) y funciona | Correcto; 2 mejoras menores (§3) |
| Jump buffer | **Sí existe** (0.1 s), con 3 fallos | Arreglar (§4) |
| Dash buffer | **No existe** | Implementar (§5) — causa directa de que dash→salto→dash cueste |

---

## 2. Código de rampas que sobra

### 2.1 `Assets/Scripts/Gameplay/PlayerMovement.cs`

Borrar entero:

| Líneas | Qué | Nota |
|---|---|---|
| 85-92 | `GroundNormal`, `SlopeAngle`, `IsOnSlope` (API pública) | Sin llamadas fuera del archivo (verificado) |
| 179-196 | Header *SLOPES*: `enableSlopes`, `maxSlopeAngle`, `slopeCheckDistance`, `slopeSnapDistance` | `Player.prefab:203-206` los tiene serializados; Unity los descarta solo |
| 245-254 | `groundStepMinFaceAngle` | Existe sólo para distinguir "rampa muy empinada" de "escalón" |
| 375-380, 396-397, 400-401 | `_groundNormal`, `_slopeAngle`, `_onWalkableSlope`, `_steepBlockDir`, `_sweepLift`, `_surfaceResolvedGround`, `_groundSteepDir`, `_groundSteepTime` | |
| 712-717 | `IsWalkableSurface` | Y el parámetro `ignoreWalkable` de `RunDetection` (704-709): los rayos laterales vuelven a ser "cualquier impacto = pared" |
| 1077-1104 | `BlockAgainstSteepSlope()` y sus llamadas (849, 1275) | |
| 1902-1914 | `ProjectOnSlope()` y sus usos (1487, 2137) | |
| 2006-2038 | `DebugReportSurfaceLift()` | Diagnóstico del `rise` de rampa |
| 2075 | Clamp de `groundStepMinFaceAngle` en `OnValidate` | |
| 2142-2144 | Gizmo de la normal del suelo | |

Simplificar (la función se queda, la parte de rampa se va):

| Líneas | Función | Qué queda |
|---|---|---|
| 725-802 | `ProbeGround` | Quitar `descending`/`slopeSnapDistance` (735-740) y el bloque de ángulo/`steepest*` (772-793). Queda: distancia más cercana, plataforma sí/no, collider pisado. ⚠ El alcance extendido al bajar (0.35) también "pega" al jugador a un suelo 0.35 más bajo al salir de un borde; al quitarlo, salir de un escalón será caer de verdad (correcto) |
| 881-999 | `EvaluateStepContacts` | `walkableFace` pasa a ser "normal casi vertical" (p. ej. `normal.y > 0.7`) en vez de `<= maxSlopeAngle`; fuera la rama `steepGroundRamp` (941-951, 983-989) |
| 1207 | `TryDropThroughPlatform` | Quitar `_onWalkableSlope = false` |
| 1481-1528 | `MoveCharacter` | Fuera `sweepLift`/`solverOffset` (1491-1501): la caja del barrido es siempre la del cuerpo menos `solverSkin` |
| 1587-1657 | `SweepAgainstGround` | Fuera `forgiveWalkable` y la franja inferior extra (1615-1625). Barrido limpio por ejes |
| 1665-1688 | `SweepAxisAgainstGround` | Fuera el parámetro `forgiveWalkable` (1680-1682) |
| 1700-1707 | `GroundProbeBox` | Sin `lift`: caja = cuerpo − `groundClampInset` |
| 1784-1806 | `CrossedGround` | `angle <= maxSlopeAngle` → `normal.y > 0.7` (sigue haciendo falta: `Level` genera `EdgeCollider2D`, que no tiene "dentro") |
| 1929-2004 | `ResolveSurface` | Sólo queda el aterrizaje en plataformas (`fall`). Fuera `rise` por `tan(maxSlopeAngle)`, `groundAngle`, `sweepLift`, la rama `ground` |
| 1762, 1895 | Logs de `debugGroundClamp` | Quitar `rampa=…` / `sweepLift=…` |
| comentarios | 22-35, 194-196, 435-438, 701-703, 1219, 1431-1432, 1494-1498, 1513-1522, 1579-1583, 1694-1698, 1823-1825 | Reescribir sin rampas |

Opcional, no es de rampas pero queda muerto: el resolvedor viejo de `MoveCharacter` (1530-1568) sólo corre con
`groundIsImpassable = false`. Si nadie lo apaga, borrar el toggle y ese camino (*replace fully*).

Se queda tal cual: `SnapToGround` (corrige el flotar de `_detectionRayLength`), step-up (escalones del Tilemap),
plataformas de un sentido, red de seguridad (`ValidateGroundContainment`).

### 2.2 Enemigos

| Archivo | Líneas | Acción |
|---|---|---|
| `Gameplay/GroundMotion.cs` | 5-37 `GroundContact`; 57-81 `MaxSlopeAngle`/`FlatTolerance`/`RestingGap`/constantes de sondeo; 95-186 `Probe`/`AlongSlope`/`Halt` | Borrar. Quedan `TerrainMask` y `PhaseThroughTerrain` (los usan `BossContext`, `ItemPickup`, `ContentAudit`, `EnemyStats`). Reescribir el `<summary>` de la clase |
| `Enemies/EnemyBrain.cs` | 142 (`_ground`), 398-401 (Probe), 527-531, 864 (`AlongSlope`), 576-588 (`Halt`) | `new Vector2(dir * speed, vel.y)` / `new Vector2(0, vel.y)` |
| `Gameplay/EnemyController.cs` | 121, 172-175, 325-329, 376-385 | Igual |
| `Enemies/EnemyTuning.cs` | 331 | Tooltip: quitar "en la rampa no encuentra suelo que seguir" |
| `Pipeline/Editor/ContentAudit.cs` | 130-131 | Comentario: quitar la frase de la rampa |
| `docs/architecture/enemies.md` | 59-68 | Sección *Slopes* fuera |
| `docs/architecture/levels.md` | 78 | Pendiente "Slopes (45° cells)" → borrar (contradice la decisión) |

Jefes: `BossController` no tiene lógica de rampas (las coincidencias de búsqueda eran "trampa"/`speedRampPerWave`).

### 2.3 ✅ Collider del nivel cuadrado (hecho 2026-10-11)

`Levels/Level.cs:268` (`BuildCollision`) y `:289` (`BuildPlatforms`) construyen el collider con
`LevelShape.Rounded(loop, radius, 1000f, 4)`, con `cornerRadius = 0.4` (`Hielo.asset`). Cada esquina — convexa y
cóncava — es un arco de 4 segmentos inclinados (~11°, 34°, 56°, 79°). Hoy el código de rampas es lo que hace que
esas esquinas se anden bien; si se quita sin más:

- esquina cóncava (suelo→pared): segmentos de 56-79° leídos como pared/escalón raro;
- borde de plataforma redondeado: al salir, el pie "resbala" por la curva;
- aterrizar justo en una esquina convexa: normal inclinada.

**Fix**: collider con radio 0 (cuadrado puro), borde visual (`BuildRim`) redondeado como está. Desfase visual
máximo ≈ 0.14 u (≈ 6 px) en las esquinas: imperceptible. Hacer esto **primero**, regenerar los niveles, y después
borrar el código.

Escenas viejas con `EdgeCollider2D` a mano (`Bosque1/3/4`, `BosqueBoss`, `testboss`): si tienen tramos
inclinados, dejarán de andarse. Migrarlas a `Level` o darlas por legacy.

---

## 3. Coyote time — existe

`PlayerMovement.cs:298` `_coyoteTimeThreshold = 0.1f`; `:451` `CanUseCoyote`; `:682` marca el despegue; `:687`
lo rearma al aterrizar. Usarlo hace salto de suelo (no gasta el doble salto). Funciona también saliendo de un
dash por un borde (saltar cancela el dash, 587-588).

Mejoras:
1. **No es configurable en el Inspector con nombre claro** (`_coyoteTimeThreshold` sin tooltip). Añadir tooltip y
   subir a 0.12 s (valor habitual 0.1-0.15).
2. Con `slopeSnapDistance` el jugador sigue "apoyado" hasta 0.35 u por debajo del borde, así que hoy el coyote
   empieza a contar tarde (coyote efectivo > 0.1 s de forma irregular). Al quitar rampas (§2.1) se vuelve exacto.

---

## 4. Jump buffer — existe, con fallos

`:299` `_jumpBuffer = 0.1f`; `:662` guarda `_lastJumpPressed`; `:452` `HasBufferedJump` dispara al tocar suelo.

Fallos:
1. **El doble salto se come el buffer.** Si queda doble salto, pulsar justo antes de aterrizar lo gasta en el aire
   (rama `else if` de `CalculateJump`, 1156) en vez de esperar al suelo. El buffer sólo funciona sin saltos
   aéreos restantes. Fix: si se cae (`vy < 0`) y hay suelo a ≤ `vy·jumpBuffer` debajo (un raycast), guardar la
   pulsación para el salto de suelo y no gastar el doble salto.
2. **Un toque corto guardado da salto completo.** El `JumpUp` llega *antes* de que el salto salga, y
   `_endedJumpEarly` sólo se activa en el frame de soltar (1170). Fix: en `Jump()`, si el botón ya no está
   pulsado, `_endedJumpEarly = true`.
3. **Táctil (móvil) no tiene altura variable nunca.** `TouchInput` sólo tiene `QueueJump`/`ConsumeJump`
   (`Ui/TouchControlsController.cs:203` sólo escucha `PointerDown`); `jumpUp` sólo lee el Input System (649).
   En móvil todos los saltos son de altura máxima. Fix: `TouchInput.JumpHeld` (PointerDown/Up) y leerlo en
   `GatherInput` igual que el teclado.
4. Menor: durante el retroceso (`IsKnockedBack`, 556-561) la pulsación se descarta. Opcional: guardarla para que
   salga al recuperar el control si cae dentro del buffer.

---

## 5. Dash buffer — no existe

`TryStartDash` (1245-1265) exige `_input.DashDown` **en ese mismo frame**. Si el dash no está disponible justo
entonces, la pulsación se pierde:

- pulsar dash durante el dash anterior (`_dashTimer > 0`);
- pulsar dash en el mismo frame del salto o el frame anterior al despegue: aún en suelo con cooldown activo
  (`dashCooldown 0.55 + duración 0.18 = 0.73 s`); el cooldown se resetea al despegar (683), un frame tarde;
- pulsar sin dashes aéreos un instante antes de aterrizar.

Eso es exactamente lo que hace difícil **dash → salto → dash**.

Fix propuesto (mismo patrón que el salto):
- `[SerializeField] float dashBuffer = 0.12f` con tooltip; `_lastDashPressed = Time.time` en `GatherInput`.
- `TryStartDash` usa `_lastDashPressed + dashBuffer > Time.time` y lo limpia al usarlo (y en pausa/muerte, como
  `_lastJumpPressed` en 544).
- Dirección del dash: la del input **al ejecutarse**, no al pulsar (más natural con buffer).
- Orden en `Update`: hoy `TryStartDash` corre antes de que el salto cancele el dash (580 vs 587). Con salto + dash
  en el mismo frame en suelo, el dash arranca, se cancela al instante y suena/lanza FX igualmente. Mover
  `TryStartDash` después de `CalculateJump` (o no arrancar dash si `JumpingThisFrame`) y dejar que el buffer lo
  lance al frame siguiente, ya en el aire.

---

## 6. Orden sugerido del refactor

1. `Level`: collider y plataformas cuadrados; regenerar `NivelHielo_*` e `Ice1`. Commit.
2. Enemigos: quitar `GroundContact`/`Probe`/`AlongSlope`/`Halt`. Commit.
3. Player: quitar rampas (§2.1). Rama `task/no-slopes` (refactor grande). Commit.
4. Player: arreglos de jump buffer + `TouchInput.JumpHeld` + dash buffer. Commit.
5. Docs: `enemies.md`, `levels.md`, cabecera de `PlayerMovement`.
