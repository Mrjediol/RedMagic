# Auditoría: fugas del jugador a través de la capa `Ground`

Ámbito: `Assets/Scripts/Gameplay/PlayerMovement.cs` (1501 líneas), `Assets/Scripts/Combat/Knockback.cs`.

Capas del proyecto: `Ground` = 6 (`m_Bits: 64`), `Platform` = 8 (`m_Bits: 256`).

`Assets/Prefabs/Player.prefab`: `Rigidbody2D` Kinematic, `BoxCollider2D` 0.7×1.5 offset (0,0),
`_characterBounds` 0.7×1.5, `_groundLayer` = Ground, `_platformLayer` = Platform,
`maxSlopeAngle` 45 en el prefab, **80 sobreescrito en la instancia de `TestScene`**.

`ProjectSettings/Physics2DSettings.asset`: `m_AutoSyncTransforms: 0`, `m_QueriesStartInColliders: 1`,
`m_DefaultContactOffset: 0.01`, `m_SimulationMode: 0` (FixedUpdate).

---

## 1 · Inventario de escrituras de posición / velocidad

Todo el movimiento del jugador nace en `Update()` (línea 434) y termina en `MoveCharacter()`.
Hay **cinco** escrituras de `transform.position`, y ninguna fuera de este script
(`RunManager:686/820` teletransporta al entrar en una sección: intencionado, fuera de ámbito).

| # | Sitio | Qué escribe | ¿Pasa por el barrido de `MoveCharacter`? | Capas que consulta |
|---|---|---|---|---|
| A | `MoveCharacter` 1230 | `transform.position += move` (camino rápido) | **es** el barrido, pero sólo comprueba el **punto final** | `_groundLayer` — sólo Ground |
| B | `MoveCharacter` 1242 | `transform.position = positionToMoveTo - solverOffset` | sí (muestreo del trayecto) | `_groundLayer` — sólo Ground |
| C | `MoveCharacter` 1248 | `transform.position += dir.normalized * move.magnitude` | **no** — despenetración a ciegas, sin re-verificar | ninguna |
| D | `ConsumeStepUp` 890 | `transform.position += Vector3.up * lift` | **no** — escritura cruda, sin ninguna comprobación | contactos de Ground **y** Platform mezclados |
| E | `SnapToGround` 1178 | `transform.position += Vector3.down * drop` | **no**, pero `drop` sale de un raycast que ya midió la superficie | Ground o Platform (lo que midiera `ProbeGround`) |

Escrituras de velocidad (todas acaban en `MoveCharacter`; ninguna mueve el transform por su cuenta):

- `CalculateWalk` / `CalculateGravity` / `Jump` / `CalculateJump`.
- `UpdateDash` (1082): sólo fija `_currentHorizontalSpeed = dashSpeed * dir` y anula la vertical.
  **El dash no teletransporta**: su desplazamiento sale por `MoveCharacter` como cualquier otro.
- `ApplyKnockback` (1115) / `UpdateKnockback` (1143) / `CancelKnockback`: igual, sólo velocidad.
  `Knockback.ApplyVelocity` llama a `IKnockbackReceiver.ApplyKnockback`; no toca el transform.
- `BlockAgainstSteepSlope` (915), `ResolveSurface` y `ConsumeStepUp` ponen la vertical a 0.

**Conclusión de la auditoría de rutas**: dash y retroceso *sí* pasan por el barrido. No son
bypasses por sí mismos — lo que falla es el barrido, que tiene tres agujeros (sección 2).

## 1-bis · ¿Ground y Platform comparten código?

| Sistema | Separación |
|---|---|
| Barrido de `MoveCharacter` | Ground puro — OK |
| Sensores laterales / techo (`RunCollisionChecks`) | Ground puro — OK |
| `ResolveSurface` | dos ramas separadas; `groundAngle` (→ `sweepLift`) sólo de Ground — OK |
| `ProbeGround` | dos raycasts separados, **pero** `Consider()` fusiona ambos en un único mejor apoyo y **`_steepBlockDir` se fija desde cualquiera de las dos capas** — MEZCLADO |
| `EvaluateStepContacts` / `ConsumeStepUp` | `int terrain = _groundLayer.value \| _platformLayer.value;` (760) — **explícitamente mezcladas**; `_stepSurfaceY` no guarda de qué capa vino — MEZCLADO |
| `IsWalkableSurface` / `maxSlopeAngle` | umbral compartido por las dos capas — MEZCLADO |

Los tres MEZCLADO son los que hay que partir para que el arreglo sea Ground-only sin tocar el
comportamiento sobre plataformas.

---

## 2 · Mecanismo real de cada fuga (verificado sobre el código)

### Fuga 1 — El barrido sólo valida el **punto de destino** (mecanismo **b**)

```csharp
var hit = Physics2D.OverlapBox(furthestPoint, solverSize, 0, _groundLayer);
if (!hit) { transform.position += move; return; }   // 1227-1231
```

Si la caja **en el destino** no toca Ground, el paso entero se aplica sin mirar el trayecto.
El muestreo del recorrido (el bucle `_freeColliderIterations`) **sólo corre si el destino ya
chocaba**. Geometría fina cruzada en un solo frame no se ve nunca.

Con `dashSpeed 26` a 60 fps el paso es ~0.43 u y la caja mide 0.64 de ancho, así que un muro
vertical fino todavía solapa el destino; pero a 30 fps (0.87 u), en caída libre (`_fallClamp -40`
→ 0.67 u/frame) o en un retroceso con arco, el paso supera el ancho/alto útil de la caja y el
túnel es directo. Es la causa estructural: **no hay barrido, hay un test de punto final**.

### Fuga 2 — `sweepLift` recorta la caja del barrido justo en rampas de Ground (mecanismo **b**, la grave)

```csharp
sweepLift = _rayBuffer * Mathf.Tan(groundAngle * Mathf.Deg2Rad) + groundSkin;   // 1336
sweepLift = Mathf.Min(sweepLift, _characterBounds.size.y * 0.5f - solverSkin);
...
solverSize.y -= sweepLift;                       // 1222
var solverOffset = new Vector3(0f, sweepLift * 0.5f);
```

`tan` diverge. Con `maxSlopeAngle` subido a 80° y el jugador pisando una rampa de Ground de ~80°:
`sweepLift = 0.1·tan(80°)+0.01 ≈ 0.577`. La caja pasa de 1.44 a 0.863 de alto y sube su centro
0.289 → cubre `[pos.y−0.142 , pos.y+0.72]`, cuando el cuerpo va de `pos.y−0.75` a `pos.y+0.75`.

**Los ~0.61 u inferiores del personaje dejan de barrerse.** Cualquier geometría de Ground cuya
cima quede por debajo de `pos.y−0.142` es invisible para el barrido y se atraviesa andando.
A 45° `sweepLift` era ~0.11 y el agujero era despreciable; subir el ángulo a 80° lo abrió.

### Fuga 3 — Rampa más inclinada que `maxSlopeAngle`: el step-up **anula** el bloqueo de pared (mecanismo **c** + **a**)

```csharp
if (!ConsumeStepUp() && ((_currentHorizontalSpeed > 0 && _colRight) ||
                         (_currentHorizontalSpeed < 0 && _colLeft)))
    _currentHorizontalSpeed = 0;                 // 717-720
```

`ConsumeStepUp()` **corto-circuita el bloqueo de pared**. Y su criterio para llamar "escalón" a
algo es exactamente el criterio de "pared" de `EvaluateStepContacts`: cara más inclinada que
`maxSlopeAngle`, del lado del avance, con el contacto por debajo de `pies + stepThresholdHeight`
(0.4). La base de una rampa demasiado inclinada cumple las tres → se clasifica como escalón →
`transform.position += Vector3.up * lift` (**sin ninguna comprobación de colisión**, entrada D) y
la velocidad horizontal **no** se anula. `OnCollisionStay2D` lo vuelve a reportar en cada paso de
física, así que el ciclo se repite: sube y avanza, sube y avanza — se trepa/atraviesa la rampa.

Además `_steepBlockDir` (el bloqueo específico de rampas inclinadas) casi nunca llega a activarse:

```csharp
if (hit.distance <= groundedRange && angle > steepestAngle)   // 648
```

sólo lo fija si la cara inclinada está **bajo los pies** a menos de `_detectionRayLength` (0.1).
Una cuña o rampa *por delante* no la ve ningún rayo hacia abajo, así que `_steepBlockDir` = 0 y
`BlockAgainstSteepSlope()` no hace nada. Y los sensores laterales tampoco ayudan:
`RunDetection(_raysLeft/Right, ignoreWalkable: true)` descarta todo lo que sea `≤ maxSlopeAngle`,
es decir, con el umbral a 80° una cara de 79° **no es pared para el sensor lateral**.

### Fuga 4 — La despenetración `i == 1` es un empujón ciego (mecanismo **a**)

```csharp
var dir = transform.position - hit.transform.position;
transform.position += dir.normalized * move.magnitude;    // 1247-1248
```

`hit.transform.position` es el **origen del transform del collider**, no el punto de contacto.
Para el suelo pintado con Tilemap eso es el origen del Tilemap, que puede estar a decenas de
unidades y en cualquier dirección. El empujón resultante es arbitrario, de magnitud
`move.magnitude` (mayor cuanto más rápido se iba: dash y retroceso son los peores casos) y **no se
vuelve a comprobar**. Puede meter al jugador dentro del Ground o sacarlo al otro lado.

### Fuga 5 — `ConsumeStepUp` y `SnapToGround` escriben la posición fuera del barrido (mecanismo **a**)

Entradas D y E. `SnapToGround` es benigna (`drop` sale de una distancia ya medida hacia abajo).
`ConsumeStepUp` no: sube `lift` sin comprobar si arriba hay Ground (techo, saliente), y su máscara
`terrain` mezcla Ground con Platform.

---

## 3 · Por qué "parchear cada caso" no vale

Dash y retroceso no se saltan `MoveCharacter` — **confían** en él. Las fugas 1, 2 y 4 están dentro
del propio resolvedor, así que cualquier sistema futuro que mueva al jugador (un gancho, un
empujón de jefe, una cinta transportadora) hereda el mismo agujero. El arreglo tiene que estar en
el resolvedor y en una red de seguridad posterior, no en `UpdateDash` ni en `UpdateKnockback`.

---

## 4 · Arreglo aplicado (todo en `PlayerMovement.cs`, todo Ground-only)

Interruptor general nuevo: **`groundIsImpassable`** (encendido). Apagándolo, el controlador se
comporta exactamente como antes de este cambio — sirve para comparar sensación.

### 4.1 Barrido real del trayecto — `SweepAgainstGround` / `SweepAxisAgainstGround`

Corre dentro de `MoveCharacter`, después de `ResolveSurface` y **antes** de cualquier escritura de
posición, así que cubre de una sola vez andar, rampa, dash, retroceso y cualquier sistema futuro:
todos acaban en el mismo `move`.

- `Physics2D.BoxCast` eje por eje (horizontal primero, luego vertical) con el mismo filtro
  Ground-only, recortando `move` hasta el punto de contacto. Cierra la **fuga 1** (test de punto
  final) y la parte de dash/retroceso de las **fugas 3 y 4**.
- **Se descarta a propósito** una cara de Ground transitable (`≤ maxSlopeAngle`) cuyo punto de
  contacto esté dentro del alcance del pie de este frame (`feetY + rise`, el mismo `rise` que usa
  `ResolveSurface`): de esas se encarga la subida de pie, y frenarlas aquí clavaría al personaje al
  pie de cada cuesta.
- En el **eje vertical** se descarta toda cara transitable, suba o baje el movimiento. Bajar una
  cuesta es casi todo desplazamiento vertical contra la propia rampa; recortarlo convertiría el
  descenso en un goteo de centímetros por frame. Techos y muros (normal fuera de `maxSlopeAngle`)
  sí recortan.
- Un impacto a distancia 0 (la caja ya nacía dentro) se ignora: no dice nada del trayecto y tomarlo
  congelaría al personaje. Ese caso lo cubre la red de seguridad.
- **Barrido extra de la franja inferior** cuando `sweepLift > 0`: una caja de altura `sweepLift`
  justo por debajo de la caja del resolvedor, sólo en horizontal. Cierra la **fuga 2** — el hueco
  de ~0.58 u (el 40% inferior del cuerpo) que `sweepLift` abría en rampas de Ground a 80°. Dentro
  de esa franja la rampa que se pisa se descarta por su normal, así que sólo puede frenar algo
  demasiado inclinado, que es exactamente lo que se quería detectar.

### 4.2 Rampa de Ground más inclinada que `maxSlopeAngle` → pared

Knob nuevo, **sólo Ground**: `groundStepMinFaceAngle` (85°, rango 45-90).

- `EvaluateStepContacts` ahora distingue la capa del contacto. Una cara de **Ground** entre
  `maxSlopeAngle` y `groundStepMinFaceAngle` deja de ser "escalón" y pasa a ser rampa no trepable:
  arma `_groundSteepDir` y **cancela el step-up de ese frame** (`steepGroundRamp` entra en el
  `return` junto a `blockedAboveThreshold`). Cierra la **fuga 3**.
- Los escalones reales de un tilemap son caras verticales (90°), así que ningún escalón existente
  cambia de comportamiento.
- `BlockAgainstSteepSlope` consume `_groundSteepDir` con el mismo criterio de frescura que el
  step-up (2 pasos de física). Corre **después** de `ConsumeStepUp`, así que también anula el
  avance en el caso en que el step-up se saltaba el bloqueo de pared.
- **`_steepBlockDir` (el de `ProbeGround`) se deja intacto**, mezclando Ground y plataformas como
  hasta ahora: separarlo habría *quitado* un bloqueo que hoy existe para plataformas, y eso es
  cambiar el comportamiento de plataformas. El bloqueo nuevo es aditivo.

### 4.3 Despenetración real en vez del empujón ciego

`MoveCharacter`, rama `i == 1`: fuera el
`transform.position += (transform.position - hit.transform.position).normalized * move.magnitude`.
En su lugar, `ResolveGroundPenetration()`. Cierra la **fuga 4**.

### 4.4 Red de seguridad — `ResolveGroundPenetration()` en `LateUpdate`

Corre después de que todo lo que mueve al jugador haya escrito su posición.

- Sonda: `Physics2D.OverlapBox` con `ContactFilter2D` de **sólo la capa Ground y sin disparadores**
  (el proyecto tiene `Queries Hit Triggers` activado; una zona de disparo puesta en Ground no debe
  expulsar al jugador). Ninguna otra capa entra aquí.
- **La caja de sondeo lleva la base subida `_sweepLift`**, igual que la del barrido. No es opcional:
  apoyado en una rampa de 80°, la esquina baja del collider real penetra ~0.57 u de verdad, y una
  red ingenua expulsaría al jugador de la cuesta en cada frame.
- Salida por la dirección cardinal que libera la caja a menor distancia (arriba primero en caso de
  empate). **No** se usa `Collider2D.Distance`: mide contra el collider real, que en rampa está
  legítimamente penetrado, y devolvería la separación que echa al jugador de la cuesta.
- Tope `groundClampMaxPush` (1.5 u). Como corre cada frame, lo que corrige nunca es más profundo
  que un frame de movimiento, así que el reajuste no se ve; el tope sólo evita un tirón si algo va
  muy mal.
- La corrección se descuenta de `_lastPosition`, para que no aparezca como un pico en `Velocity` y
  falsee el cálculo del ápex del salto.
- Se anula sólo la componente de velocidad que empujaba contra la superficie.

### 4.5 Escrituras de posición que no pasan por el barrido

- `ConsumeStepUp`: antes de subir, `WouldOverlapGround(destino)`. Si la subida dejaría al personaje
  dentro de Ground, **no sube y devuelve `false`**, con lo que el bloqueo de pared normal vuelve a
  aplicarse. Sólo mira Ground; sobre plataformas la subida es idéntica a la de antes.
- `SnapToGround`: sin cambios (su `drop` sale de una distancia ya medida hacia abajo). Sólo se
  etiqueta para el diagnóstico.

### 4.6 Diagnóstico — `debugGroundClamp`

Apagado por defecto, mismo estilo que `debugStepUp`.

- `Debug.LogWarning` cada vez que la red tuvo que corregir, con el empujón aplicado y **qué sistema
  movía al jugador ese frame** (`MoveSource`: Walk / Dash / Knockback / StepUp / GroundSnap /
  Solver) más cuál fue la última escritura de posición, además de apoyado / velocidades / ángulo de
  rampa / `sweepLift` / `dt`.
- Gizmo: caja magenta donde quedó el personaje tras la corrección, más una flecha con el empujón,
  visible ~120 frames.
- **Con los puntos 4.1-4.5 en su sitio esto no debería saltar nunca.** Si salta durante una partida
  de prueba, queda un camino que se salta el barrido y el propio aviso dice cuál.

### 4.7 Lo que NO se ha tocado

Dash (`TryStartDash` / `UpdateDash`), retroceso (`ApplyKnockback` / `UpdateKnockback` /
`Knockback.cs`), plataformas (`_platformLayer`, `PlatformsActive`, `AcceptPlatformHit`,
`TryDropThroughPlatform`, la rama de plataformas de `ResolveSurface` y de `ProbeGround`),
`ProjectOnSlope`, la gravedad, el salto, el pisotón y `_steepBlockDir`. Ninguna consulta nueva
mira la capa de plataformas.

---

## 5 · Segunda pasada: el terreno de los mundos es SpriteShape (polilínea ABIERTA)

Medido, no supuesto. Todos los colliders de terreno de `Assets/Scenes/Worlds/World1/*` son
`EdgeCollider2D` generados por `SpriteShapeController`:

```
m_IsOpenEnded: 1        (la spline es abierta)
m_EdgeRadius: 0         (grosor cero)
m_CompositeOperation: 0 (sin composite)
```

`MainHub` / `TestScene` / `World2` no tienen ninguno: ahí el suelo son `BoxCollider2D`.

**Una polilínea abierta no tiene "dentro".** Eso invalida dos cosas que la primera pasada daba por
buenas:

- El resolvedor original de `MoveCharacter` está construido entero sobre `Physics2D.OverlapBox`.
  Contra un tilemap (cajas rellenas) eso funciona; contra una línea sólo funciona mientras la caja
  *cruza* la línea, y **no puede detectar jamás "el jugador está por debajo del terreno"**.
- La red de seguridad de la primera pasada heredaba esa ceguera: era decorativa en todas las
  secciones de mundo, justo donde hacía falta. Por eso no rescató nada.

### 5.1 Por qué se atravesaba la cuesta (mecanismo **c**, el que quedaba)

La regla de perdón del barrido era: *«ignora una cara transitable cuyo punto de contacto quede por
debajo de `pies + alcance del pie este frame`»*. Con `maxSlopeAngle` alto, **una pared casi
vertical cuenta como transitable**, y su punto de contacto está justo a la altura del pie — así que
se perdonaba sola. Y `ResolveSurface`, que era quien debía subir el pie, no llegaba a resolver el
apoyo en esa columna. Resultado: el barrido no frenaba y nadie subía el pie → se atravesaba.

**Arreglo**: el perdón deja de basarse en una estimación de alcance y pasa a basarse en un hecho.
`ResolveSurface` ahora publica `_surfaceResolvedGround` — «he encontrado apoyo de Ground alcanzable
en la columna de destino». El barrido sólo perdona una cara transitable si esa bandera está puesta,
es decir, si el pie va a terminar realmente encima de ella este frame. Si no, la cuesta es
inalcanzable y **frena como un muro**. Las dos mitades comparten el mismo hecho, así que no pueden
discrepar.

Efecto lateral buscado: en el aire la bandera está baja, así que el eje vertical también frena
contra caras transitables. Eso es el aterrizaje, y de paso cierra el paso a través de un suelo fino
en una caída rápida o en un frame largo.

### 5.2 Red de seguridad v2 — detección de CRUCE

`ValidateGroundContainment()` (en `LateUpdate`) ahora tiene dos mitades, una por forma de collider:

1. **Geometría rellena** (tilemaps, cajas del hub): `ResolveGroundPenetration()`, el solape de la
   primera pasada. Sin cambios.
2. **Geometría de línea abierta** (SpriteShape): `CrossedGround(lastSafe, current)` —
   `Physics2D.Linecast` entre la posición buena anterior y la actual. Si el segmento atraviesa la
   línea, el personaje se ha colado y se le devuelve a la última posición validada. Como se valida
   cada frame, «la última posición buena» es la de hace un frame: el retroceso es invisible.

Detalles que evitan falsos positivos:

- Se mide **entre centros** del personaje, no entre pies: caminando, el centro va media altura por
  encima de la superficie, así que un recorrido normal nunca roza la línea.
- Pasar por encima de una cresta afilada sí puede rozarla, y eso es legítimo: un cruce sólo cuenta
  si además, en el destino, **no hay suelo transitable bajo los pies** (un rayo hacia abajo de
  media altura).
- Un salto de posición mayor que `max(4u, altura × 3)` se trata como teletransporte (cambio de
  sección, respawn): se reancla la referencia y no se valida. Además `CancelKnockback()` — que es
  lo que ya se llama al teletransportar y al morir — invalida la referencia explícitamente.
- La corrección se copia a `_lastPosition` para que no aparezca como un pico en `Velocity`.

`debugGroundClamp` distingue los dos casos en consola: «estaba DENTRO de la capa Ground» (solape)
frente a «ATRAVESÓ la línea de la capa Ground» (cruce).

---

## 6 · La causa real: el collider estaba en la capa Platform

El terreno de la zona que fallaba estaba asignado a **Platform**, no a **Ground**. Las plataformas
son atravesables por diseño (`PlatformsActive`, `AcceptPlatformHit`: sólo existen para el sensor de
suelo cuando el personaje no sube y no las está atravesando), así que el suelo se comportaba
exactamente como se le había pedido. Cambiar la capa a Ground resolvió el atravesamiento por sí
solo.

Lo de las secciones 4 y 5 no era necesario para ESE bug. Se mantiene porque es lo que pedía el
encargo — que Ground sea infranqueable venga el movimiento de donde venga — y porque la sección 5
sí descubrió algo real e independiente: la red de seguridad basada en solape es ciega contra la
polilínea abierta de SpriteShape. `groundIsImpassable` apaga todo el bloque de una vez si alguna
vez estorba.

**Regla práctica que deja esto**: antes de buscar el fallo en el controlador, comprobar en qué capa
está el collider. `Ground` (6) es sólido; `Platform` (8) es atravesable por abajo y de lado.

### 6.1 Frenado al subir cuestas — introducido en la sección 5, corregido

El perdón del barrido llevaba, además de la bandera `_surfaceResolvedGround`, un techo de altura:
sólo se perdonaba un impacto transitable cuyo `point.y` quedara por debajo de los pies de destino.

La caja del barrido tiene la base **por encima** de la línea de los pies — medio `solverSkin` más
`sweepLift` (≈0.11u en una rampa de 40°) — así que su contacto contra la cuesta cae siempre por
encima de esa línea. A velocidad alta el pie subía lo bastante como para que el techo lo tapara; a
poca velocidad no, y entonces el barrido frenaba y ponía la horizontal a 0. Al frame siguiente se
aceleraba desde 0 y volvía a pasar lo mismo: subir una cuesta se convertía en un arrastre.

**Arreglo**: fuera el techo de altura. La bandera es todo el criterio, que es lo que siempre
significó — si `ResolveSurface` ha resuelto apoyo de Ground alcanzable en la columna de destino, el
pie va a acabar encima de esa cara y no es un muro; si no lo ha resuelto, frena. `SweepAxisAgainstGround`
pasa de recibir una altura a recibir un `bool`.

### 6.2 "Mini frenado" al entrar en una cuesta — un resolvedor de más

Síntoma: al pisar una inclinación suave el personaje se paraba un instante y luego seguía.

Causa: había **dos** resolvedores de colisión funcionando a la vez, con cajas distintas.

`sweepLift` se calcula a partir de `groundAngle`, y `groundAngle` sale de los rayos lanzados en la
columna de **destino** del paso. Justo al entrar en una cuesta esos rayos todavía caen sobre el
llano, así que `groundAngle` = 0 y `sweepLift` = 0. Sin ese margen, la esquina delantera-inferior
de la caja del resolvedor original se mete en la inclinación en el punto de destino: el
`Physics2D.OverlapBox` da positivo, el muestreo del trayecto bloquea ya en la primera iteración y
el frame entero se queda sin avanzar. Al frame siguiente los rayos ya alcanzan la rampa, aparece
`sweepLift`, y todo sigue con normalidad.

Es un fallo del resolvedor original, no del barrido: el barrido perdona la cara transitable por su
normal, no por la geometría de la caja.

**Arreglo**: con `groundIsImpassable` encendido, el barrido es la **única** autoridad sobre Ground.
`MoveCharacter` aplica el `move` ya recortado y termina; el `OverlapBox` del punto de destino más
el muestreo sólo se ejecutan con el interruptor apagado. Dos resolvedores con cajas distintas no
pueden estar de acuerdo, y el que mide el trayecto es el que hay que conservar. El caso que el
resolvedor viejo cubría y el barrido no — nacer ya dentro del suelo — lo cubren
`ResolveGroundPenetration` y `CrossedGround` en `LateUpdate`.
