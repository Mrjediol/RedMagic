# Auditoría del jugador — componentes, código muerto y prefab nuevo (2026-10-11)

Objetivo: rehacer el jugador en un prefab limpio (`Assets/Prefabs/PlayerNuevo.prefab`) sin tocar `Player.prefab`,
probarlo, y después borrar el viejo y todo lo que sólo existía para él (fase 2, §6).

Complementa `docs/movement-audit.md` (rampas, coyote, buffers).

---

## 1. Lo que estaba mal (y explica el "flotar")

1. **Tres cajas distintas para el mismo cuerpo**, ninguna dibujada donde actuaba:

   | Caja | Dónde | Tamaño / centro | Para qué |
   |---|---|---|---|
   | `BoxCollider2D` | raíz | 0.96 × 1.79, offset (0.13, 0.21) | eventos de físicas, golpes de enemigos, step-up |
   | `_characterBounds` (gizmo amarillo) | campo de `PlayerMovement` | 0.98 × 1.68, centro (0.12, 0.15) | sólo el gizmo y alguna comprobación |
   | **la que movía de verdad** | `PlayerMovement` (sensores y barrido) | 0.98 × 1.68 **centrada en el pivote** (ignoraba el centro) | apoyarse, chocar, caer — **no se dibujaba en ningún sitio** |

   En `Ice1` la instancia del jugador además sobrescribe el `BoxCollider2D` (0.52 × 1.20).

   → **Arreglado**: `PlayerMovement` ya no tiene `_characterBounds`; su caja **es** el `BoxCollider2D` (offset + tamaño).
   Una caja, la que se ve en verde al seleccionar al jugador, y se ajusta con *Edit Collider*.

2. **El dibujo saltaba de sitio al darse la vuelta.** `flipX` espeja sobre el pivote del sprite, y el cuerpo no está
   centrado en él (cola a un lado, mochila al otro): mirando a un lado los pies quedaban ~0.25 u fuera de la caja.
   → **Arreglado** en `PlayerAnimator.LateUpdate`: al voltear se refleja también la X del hijo `Sprite`, o sea, se
   voltea alrededor del centro del jugador. El prefab nuevo tiene el `Sprite` colocado con el cuerpo centrado.

3. **La caja era más ancha que los pies** (0.98 vs ~0.65 de pies). Prefab nuevo: **0.60 × 1.74, offset (0, 0.08)** —
   base exactamente en los pies del dibujo (y = −0.79), sin flotar ni hundirse.

---

## 2. Los círculos alrededor del jugador

Todos son gizmos de depuración de componentes distintos, encendidos por defecto en el prefab viejo:

| Lo que se ve | Quién lo dibuja | Prefab nuevo |
|---|---|---|
| 3-5 círculos naranja→rojo grandes | `PlayerAttack` (`drawGizmo`): alcance de cada golpe del combo (radio 2.5 × power) | apagado |
| Círculo rojo | `PlayerParry` (`drawGizmo`): radio de atrapar | apagado |
| Círculo naranja + punto | `RangedAttack` (`drawGizmos`): rango de detección + boca de la bola de fuego | componente quitado |
| Líneas/cajas amarillas y verdes delante + texto "Roca (0.42, 0.01)" | `WeaponUser` → `WeaponShotPreview`: vista previa del arma (proyectil, collider, boca) | sólo al seleccionar el asset del arma (nuevo check `previewWhenPlayerSelected`) |
| Caja amarilla | `PlayerMovement` (`_characterBounds`) | eliminada (la caja es el collider, contorno verde de Unity) |
| Rayos azules | `PlayerMovement`: sensores | sólo seleccionado; los de los pies en **verde** si pisa suelo |

Los checks siguen en cada componente (sección *Depuración*) para encenderlos cuando haga falta.

---

## 3. Componentes del `Player.prefab` viejo, uno a uno

| Componente | Veredicto | Motivo |
|---|---|---|
| `Rigidbody2D` | ✅ queda | Kinematic: sólo para recibir eventos de colisión/trigger |
| `BoxCollider2D` | ✅ queda, **redimensionado** | La única caja (§1) |
| `Health` | ✅ | Vida, muerte, revivir (compartido con enemigos) |
| `Knockback` | ✅ | Retroceso al recibir golpe; lo usa `RunManager` al teletransportar |
| `HitFlash` | ✅ | Parpadeo al recibir daño |
| `PlayerMovement` | ✅ (con código muerto, §5) | Andar, saltar, doble salto, dash, plataformas, escalones, picado |
| `PlayerDashCombat` | ✅ | i-frames del dash, daño de dash (builds) |
| `PlayerAnimator` | ✅ | Parámetros del Animator + volteo centrado (§1) |
| `PlayerVfx` | ✅ | Efectos de salto/doble salto/dash |
| `PlayerAttack` | ✅ | **El** melee: combo de tajos `MeleeSlashFx`, picado, golpe de dash |
| `PlayerParry` | ✅ | Parry (documentado en `combat.md`, ganchos de items) |
| `WeaponUser` | ✅ | **Las** armas: disparo del `WeaponLoadout` (proyectil recto, carga, cometa) |
| `LegendaryPassivePlayerHook` | ✅ | Pasiva legendaria "El Libro Sin Nombre" (revivir / escudo) |
| `SoundEmitter` | ✅ | Sonidos de todos los anteriores |
| **`AbilityUser`** | ❌ **fuera** | Vive en `Scripts/Legacy/Abilities/`: el sistema de habilidades anterior (menú K). Sólo lo usa este prefab |
| **`RangedAttack`** | ❌ **fuera** | La bola de fuego vieja (botón/acción `Fireball`). Ningún otro prefab lo usa; el pipeline de enemigos lo retira (`EnemyFactory.Retire`) |
| Hijo **`GroundCheck`** | ❌ **fuera** | Transform vacío; ningún script lo lee |
| Hijo `Sprite` (`SpriteRenderer` + `Animator`) | ✅ | Dibujo y animación; X reajustada (§1) |

Interactuar no necesita componente en el jugador: los interactuables leen `InteractInput` y detectan al jugador por
la etiqueta `Player` (se conserva).

---

## 4. Prefab nuevo — `Assets/Prefabs/PlayerNuevo.prefab`

- Construido desde cero: raíz + hijo `Sprite`, componentes añadidos uno a uno (los 14 de §3 que quedan). Los valores
  afinados (combo, dash, vida, sonidos, efectos, input…) se copiaron del viejo; las referencias internas apuntan a los
  objetos del nuevo (verificado: 0 referencias al prefab viejo).
- Caja 0.60 × 1.74, offset (0, 0.08). `Sprite` en x = −0.19 (cuerpo centrado mirando a la derecha).
- **`enableSlopes` apagado**: el prefab nuevo ya se comporta como el juego final sin rampas.
- Gizmos de depuración apagados.
- Puesto en `NivelHielo_1000` en lugar del jugador viejo (escena **sin guardar**; Ctrl+Z lo deshace).

Cambios de código de esta tarea que afectan también al viejo (porque es el mismo script):
`PlayerMovement` usa el `BoxCollider2D` como caja (el viejo pasa a usar su collider de 0.96 × 1.79 con offset — y en
`Ice1`, el sobrescrito de 0.52 × 1.20) y `PlayerAnimator` voltea alrededor del centro. El viejo se verá algo distinto:
es esperado, va a desaparecer.

---

## 5. Código muerto / restos de sistemas viejos (borrar en fase 2)

| Dónde | Qué | Por qué es muerto |
|---|---|---|
| `Legacy/Abilities/AbilityUser.cs` (+ `AbilityDefinition`, `AbilityLibrary`, `Editor`, `Definitions`, `Runtime` de Legacy) | Sistema de habilidades viejo | Sólo lo usa `Player.prefab`. Ojo: `Scripts/Abilities/` (no Legacy: `AbilityContext`, `AbilityFx`, `ProjectileSpec`) **sí** lo usan jefes y armas: se queda |
| `Items/WeaponUser.cs` | `_abilityUser`, rama "si hay habilidad equipada el arma cede", textos de convivencia | Sin `AbilityUser` |
| `Hub/ChestLootContainer.cs:111-112` | Busca `AbilityUser` | Idem |
| `Combat/RangedAttack.cs` entero | Bola de fuego | Sin usuarios tras quitarlo del jugador. Limpiar menciones en `Projectile.cs`, `ProjectilePrefabFactory.cs`, `EnemyFactory.Retire` |
| `Input/TouchInput.cs` | `QueueFireball`/`ConsumeFireball` | Sin consumidor |
| `Ui/TouchControlsController.cs` + UXML | Botón táctil de bola de fuego | Sin función — **decisión**: quitarlo o convertirlo en el botón del arma (ver §7) |
| `RedMagicControls.inputactions` | Acción `Fireball` | Idem |
| `Gameplay/PlayerAttack.cs` | `HitStarted` (evento sin suscriptores); `Suppressed` sólo existe para que arma/habilidad silencien la espada | Revisar según §7 |
| `Gameplay/PlayerMovement.cs` | Todo lo de rampas (`movement-audit.md` §2.1); resolvedor viejo con `groundIsImpassable` apagado; API pública sin uso externo: `GroundNormal`, `SlopeAngle`, `IsOnSlope`, `IsDroppingThrough`; `JumpingThisFrame`/`LandingThisFrame` públicos sólo usados dentro | Terreno cuadrado; nadie los lee |
| `Gameplay/PlayerParry.cs`, `Items/WeaponUser.cs` | `IsParrying`, `CooldownRemaining` públicos sólo usados dentro | Pasar a privados |
| `Pipeline/Editor/PlayerPack.cs`, `Fx/Editor/PlayerVfxPack.cs` | Ruta fija `Assets/Prefabs/Player.prefab` | Apuntar al prefab definitivo al hacer el cambio |
| `Levels/Editor/LevelSetup.cs:442` | `GameObject.Find("Player")` | Por nombre (la instancia de escena se llama `Player`, se conserva) |
| Escenas | `Ice1`: overrides del collider del jugador; 15 escenas con `RunManager.playerPrefab` → viejo; 7 con instancia del viejo | Re-apuntar en el cambio |

---

## 6. Fase 2 (cuando el prefab nuevo esté validado)

1. Re-apuntar escenas: `RunManager.playerPrefab` (15 escenas) e instancias (`Ice1`, `CometTest`, `NivelHielo_*`,
   `ParryTest`, `TestMap`) al nuevo, sin overrides de collider.
2. Borrar `Player.prefab`; renombrar `PlayerNuevo.prefab` → `Player.prefab` (el GUID del nuevo se conserva).
3. Borrar lo de §5 y lo de rampas de `movement-audit.md`.
4. Actualizar `combat.md`, `weapons-items.md`, `input.md`.

## 7. Decisión pendiente del usuario

Hoy, con un arma equipada, `WeaponUser` **toma el botón de atacar y silencia la espada**; sin arma, el botón hace el
combo. Se ha mantenido igual en el prefab nuevo. Con la bola de fuego fuera, el botón táctil/acción `Fireball` queda
libre: ¿el arma pasa a ese botón (espada y arma a la vez, cada una en su botón) o se mantiene como está y se borra el
botón de bola de fuego?
