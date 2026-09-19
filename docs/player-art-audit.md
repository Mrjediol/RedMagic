# Auditoría — arte y animación del jugador (paso 1, antes de tocar nada)

Fecha: 2026-09-18. Estado leído de disco (`Assets/`), no del Editor abierto.

## 1. Piezas

| Pieza | Ruta |
|---|---|
| Prefab | `Assets/Prefabs/Player.prefab` |
| AnimatorController | `Assets/Art/Characters/Player/Player.controller` (guid `e90f4eaf…`) |
| Clips | `Assets/Art/Characters/Player/Anim/Player_*.anim` (12) |
| Sprites viejos | `Assets/Art/Characters/Player/Legacy/` (8 PNG hoja) |
| Sprites nuevos | `Assets/Art/Characters/Player/Animations/<Estado>/frame_###.png` (9 carpetas, 46 PNG) |
| Sobrante | `Assets/Art/Characters/Player/New player/Player/manifest.json` |
| Receta vieja | `Assets/Art/Characters/Player/Player.sheet.asset` (apunta a `Assets/Sprites/player_sprite.jpeg`) |
| Pack | `Assets/Scripts/Pipeline/Editor/PlayerPack.cs` — **roto a propósito**: su `SourceController` vive en `Assets/Dragon Warrior Files/`, borrado en la limpieza de vendors |

Jerarquía del prefab: raíz `Player` → hijos `GroundCheck` y `Sprite`.
El **Animator está en el hijo `Sprite`**, junto al `SpriteRenderer` (no en la raíz como en los
enemigos generados), con `localScale 1.1971833`.

## 2. Hallazgo principal — los clips NO usan el arte nuevo

Los 12 `.anim` siguen apuntando **todos** a los PNG de `Legacy/`. En disco, del arte nuevo sólo se
usa **un** sprite: `Animations/Idle/frame_000.png` (`4c0ac70c…`), en el `SpriteRenderer` del prefab.

`git status` lo confirma: el único archivo modificado es `Assets/Prefabs/Player.prefab`.
Los `.anim` tienen fecha del 10 de septiembre (el commit `66a431a`), sin tocar.

**Conclusión: el reemplazo de keyframes hecho a mano está sin guardar en el Editor abierto (PID
3856, puerto 7800), o se perdió.** Lo que hay en disco sigue siendo el personaje viejo en
movimiento y el nuevo sólo en el frame estático del prefab.

Mapa clip → sprite actual:

| Clip | Sprites |
|---|---|
| `Player_Idle`, `Player_Crouch` | `Legacy/Player_Idle.png` |
| `Player_Walk` | `Legacy/Player_Walk.png` |
| `Player_Jump`, `Player_Fall` | `Legacy/Player_Jump.png` |
| `Player_Attack`, `Player_JumpAttack`, `Player_CrouchAttack` | `Legacy/Player_Attack.png` |
| `Player_Dash` | `Legacy/Player_Dash.png` |
| `Player_DoubleJump` | `Legacy/Player_DoubleJump.png` |
| `Player_Hurt` | `Legacy/Player_Hurt.png` |
| `Player_Die` | `Legacy/Player_Die.png` |

Nada más en el proyecto (escenas, prefabs, materiales, recetas) referencia `Legacy/`.
**Sólo esos 12 clips lo atan.** Reescritos, `Legacy/` queda libre.

## 3. Ajustes de importación del arte nuevo — inconsistentes y mal

Comunes y correctos: PPU 100, `filterMode 1` (Bilinear), compresión por defecto, mesh Tight.

Problemas:

1. **`spriteMode: 2` (Multiple) en 46 PNG de un solo frame.** Cada uno se auto-troceó en
   sub-sprites con recorte ajustado. Deberían ser **Single**.
2. **Frames partidos en varios sub-sprites** por ruido del auto-slice:
   `Attack/frame_004` (2), `Dash/frame_001` (2), `Jump/frame_001` (2 — uno es una mancha de 8×10 px),
   `Walk/frame_001` (2), `Walk/frame_002` (2). En esos frames el clip puede acabar cogiendo el trozo
   equivocado.
3. **`alignment: 0` (Center) sobre un recorte ajustado distinto en cada frame** → el pivote se mueve
   frame a frame y el personaje baila. Es peor en `Death` (alto 146 → 70 px) y `Dash`.
4. **Los lienzos tampoco son uniformes** dentro de una animación (p. ej. `Walk`: 132×156 → 114×153).
   Con pivote Center eso desplaza al personaje; con pivote **BottomCenter** los pies se quedan
   quietos, que es la convención del resto del proyecto (`AnchorMode.BottomCenter`).

## 4. AnimatorController — 12 estados, Base Layer, default `Idle`

Estados: `Idle` (default), `Walk`, `Jump`, `Fall`, `Crouch`, `Attack`, `JumpAttack`,
`CrouchAttack`, `Hurt`, `Die`, `DoubleJump`, `Dash`.

Transiciones desde AnyState (7):

| Destino | Condiciones | Exit time |
|---|---|---|
| `Attack` | `Attack` ∧ `Grounded` ∧ ¬`Crouching` ∧ ¬`Dead` | no |
| `CrouchAttack` | `Attack` ∧ `Grounded` ∧ `Crouching` ∧ ¬`Dead` | no |
| `JumpAttack` | `Attack` ∧ ¬`Grounded` ∧ ¬`Dead` | no |
| `Hurt` | `Hurt` ∧ ¬`Dead` | no |
| `Die` | `Dead` | no |
| `Dash` | `Dashing` ∧ ¬`Dead` | no |
| `DoubleJump` | `DoubleJump` ∧ ¬`Dead` | no |

Patrón de disparo puntual (el que pide el paso 3 para `Interact`): **AnyState → estado por trigger,
sin exit time**, y **estado → `Idle` con `hasExitTime = 1`, `exitTime = 1`**. Así están `Hurt`,
`Attack` y `CrouchAttack`.

Transiciones locomotoras: `Idle↔Walk` por `Speed`/`Grounded`; `Idle`/`Walk` → `Jump` (¬`Grounded` ∧
`VSpeed > 0.01`) o `Fall` (¬`Grounded` ∧ `VSpeed < 0.01`); `Jump`→`Fall` (`VSpeed < 0.01`) y
`Jump`/`Fall`→`Idle` (`Grounded`); `Dash`→`Fall` (¬`Dashing`); `DoubleJump`/`JumpAttack`→`Fall` por
tiempo. `Die` no tiene salida (correcto).

Parámetros (22): `Speed`, `VSpeed` (Float); `Grounded`, `Crouching`, `Dead`, `Moving`, `Dashing`
(Bool); `Attack`, `Hurt`, `DoubleJump` (Trigger); y 13 `*Speed` (Float, default 1) — uno por estado.
`Moving` no lo escribe nadie.

No hay estado `Interact` ni parámetro para él.

## 5. Sistema de interacción — existe, y es la tecla E

`InteractPressed()` está **copiado en cuatro sitios**, todos con la misma receta (acción `Interact`
del `InputActionAsset` + `E`/`Enter` + `buttonNorth` de mando + `TouchInput.ConsumeInteract()`):

- `Assets/Scripts/Hub/HubLootContainer.cs` (base de cofre / armario / libro)
- `Assets/Scripts/Hub/AnvilInteractable.cs`
- `Assets/Scripts/Hub/MirrorInteractable.cs`
- `Assets/Scripts/Economy/ShopInteractable.cs`

Todos acaban llamando a un `Interact()` público. Ése es el punto de enganche del trigger.

## 6. Referencias a "crouch" en todo el proyecto (12 archivos)

| Archivo | Qué |
|---|---|
| `Gameplay/PlayerMovement.cs` | `allowCrouch`, `IsCrouching`, `FrameInput.CrouchHeld`, la rama de freno en `CalculateWalk`, y 5 reseteos (`IsCrouching = false`) |
| `Gameplay/PlayerAnimator.cs` | `CrouchingKey` + 2 escrituras del bool |
| `Gameplay/PlayerAttack.cs` | `crouchYOffset`, `GetHitboxCenter()` lo aplica si `IsCrouching` |
| `Input/TouchInput.cs` | `public static bool Crouch` + reset |
| `Ui/TouchControlsController.cs` | `_crouchButton` + 4 callbacks + 4 handlers |
| `Ui/TouchControls.uxml` | `<Button name="crouchButton" text="AGACHAR">` |
| `Ui/TouchControls.uss` | `.touch-button--crouch` y `:active` |
| `Prefabs/Player.prefab` | `allowCrouch: 1`, `crouchYOffset: -0.35` |
| `Settings/InputSystem_Actions.inputactions` | acción `Crouch` → `<Keyboard>/c` y `<Gamepad>/buttonEast` |
| `Art/…/Player.controller` | estados `Crouch` y `CrouchAttack`, parámetros `Crouching`/`CrouchSpeed`/`CrouchAttackSpeed`, 6 transiciones |
| `Art/…/Anim/Player_Crouch.anim`, `Player_CrouchAttack.anim` | los dos clips |
| `Pipeline/Editor/PlayerPack.cs` | dos `DerivedClip` (`Crouch`, `CrouchAttack`) |

Notas:

- **`InputSystem_Actions.inputactions` no lo usa nadie** (es el asset de muestra de Unity; su guid no
  aparece en ningún prefab ni escena). El jugador usa `RedMagicControls.inputactions`, que **no tiene
  acción Crouch**. O sea: la tecla `C` hoy no hace nada. Se limpia igual.
- **Cuidado con `TryDropThroughPlatform()`**: atravesar plataformas es *abajo + salto*, y lee
  `_input.Y < -0.5f || _input.CrouchHeld`. En teclado sobra con `_input.Y` (el compuesto Move tiene
  `S`/flecha abajo), pero en táctil el botón AGACHAR era la **única** fuente de "abajo". Quitar
  crouch no debe llevarse por delante el drop-through: el botón táctil se reetiqueta a **BAJAR** y
  `TouchInput.Crouch` pasa a `TouchInput.Down`.
- Ni `ControlsLegendHud` ni ningún enemigo/IA consultan `IsCrouching`. No hay detección que lo mire.

## 7. Resolución (mismo día, tras el paso 1)

El hallazgo del §2 cambiaba el trabajo real: no había keyframes nuevos que "limpiar", había que
**construirlos**. Se hizo con una herramienta nueva y reutilizable en vez de a mano — ver
`Assets/Scripts/Pipeline/Editor/PlayerFrameImporter.cs`
(`Tools > RedMagic > Pipeline > Packs > Player · Reconstruir clips desde carpetas de frames`):

- Corrige el import de los 46 PNG de `Animations/*/frame_###.png` (Single, no Multiple; pivote
  BottomCenter; PPU 100; Bilinear) y reescribe los 12 `.anim` **en su sitio** (mismo GUID, el
  AnimatorController no se recablea).
- `Interact` es nuevo: clip creado desde `Animations/Interact/`, estado añadido, cableado
  AnyState→Interact (trigger `Interact`, sin exit time) → Fall (exitTime=1) — el mismo patrón que
  `Hurt`/`Attack`. `PlayerAnimator.TriggerInteract()` es el punto de enganche; lo llaman los cuatro
  interactuables (`HubLootContainer`, `AnvilInteractable`, `MirrorInteractable`,
  `ShopInteractable`) al pulsar interactuar.
- `Crouch`/`CrouchAttack` eliminados del controller (estados, parámetros `Crouching`/
  `CrouchSpeed`/`CrouchAttackSpeed`, condición sobrante en AnyState→Attack) vía la misma API de
  `UnityEditor.Animations`, sin tocar YAML a mano.
- Código: `PlayerMovement` (quitado `IsCrouching`/`allowCrouch`/rama de frenado; `CrouchHeld` →
  `DownHeld`, sigue alimentando el atravesar-plataformas), `PlayerAnimator` (quitado
  `CrouchingKey`, añadido `TriggerInteract`), `PlayerAttack` (quitado `crouchYOffset`),
  `TouchInput.Crouch` → `TouchInput.Down`, botón táctil "AGACHAR" → "ABAJO"
  (`touch-button--crouch` → `--down`), acción `Crouch` quitada de `InputSystem_Actions.inputactions`
  (no la usaba nadie — el jugador corre sobre `RedMagicControls.inputactions`, que nunca tuvo esa
  acción). `Player.prefab` reguardado para soltar los campos serializados obsoletos.
- `Legacy/` confirmado sin referencias — verificado por segunda vez tras la reescritura de los 12
  clips. **Borrado a mano por el usuario, no por esta sesión.**
- Carpeta huérfana `Assets/Art/Characters/Player/New player/` (sólo contenía un `manifest.json`
  suelto, restos de una importación abortada) borrada.
- `PlayerPack.cs` y `Player.sheet.asset` (el pipeline viejo, basado en lámina única, deliberadamente
  roto desde la limpieza de vendors) actualizados para no seguir generando `Crouch`/`CrouchAttack`
  si alguna vez se repara y se vuelve a ejecutar.

Verificado con el Editor conectado: recompila sin errores/warnings nuevos, el controller no tiene
transiciones colgando (11 estados, todos con `motion` válido, 7 transiciones AnyState, todas con
destino), consola sin errores.
