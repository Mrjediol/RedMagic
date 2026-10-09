# Ataque melee — pendiente

Depende del player nuevo (por partes): cuando exista, seguramente cambie cómo funciona todo esto.

## Estado actual (hecho, 2026-10-09)
- Tajo procedural por capas `Fx.MeleeSlashFx` + shader `RedMagic/Melee Slash Layer`; variantes A–D en
  `Assets/Prefabs/Fx/Player/Slash/` (elegida C · Tormenta con chispas; A, Ext1 Rombos y Ext5 Sin guardadas).
- `Gameplay.PlayerAttack`: combo de 5 golpes con 3 desbloqueados (items `ComboHitsEffect` suman), golpe de dash
  que no rompe el combo, parry acertado → siguiente golpe = remate, empuje casi nulo (0.08–0.2, solo visual).
  La zona de daño es el propio tajo (`MeleeSlashFx.Covers`). Valores por defecto: *Tools ▸ RedMagic ▸ FX ▸ Tajo
  melee · Combo del jugador* (`Gameplay/Editor/PlayerComboSetup`). Detalle: `docs/architecture/combat.md`.
- Problema abierto: los tajos grandes se salen de la pantalla por abajo en el suelo. Recortarlos contra el suelo
  se probó y quedó fatal (descartado); hay que buscar otra solución.

## Idea general (del usuario, por diseñar)
- **Combo de suelo estándar** = el actual, sólo al pulsar ataque sin dirección.
- **Impulso al atacar**: cada golpe mueve un poco al player (como impulsarse), sin cortar el flujo de movimiento rápido.
- **Arriba + ataque**: ataque hacia arriba-delante (arriba-derecha / arriba-izquierda según hacia dónde mira).
- **Abajo + ataque**: ataque hacia abajo, para cuando estás encima de los enemigos.
- **En el aire: un solo ataque** que te lleva al suelo golpeando a los enemigos por el camino; al caer deja un
  área de impacto.
- **Golpe 3 del combo**: el personaje da un saltito y golpea hacia abajo con un golpe pequeño.
- **Intercalar**: los ataques direccionales se pueden meter dentro del combo sin romperlo, p. ej. 1 · 2 · arriba ·
  4 · 5 — saltándote el 3 (salto, más lento) si sabes que no vas a dar.
- **Todo mejorable con builds**: cada ataque con su set para hacerse más grande / poderoso (p. ej. el de caída
  con área de impacto mayor).

## Notas de implementación
- Los golpes ya son datos (`PlayerAttack.Hit`): los direccionales pueden ser golpes sueltos como `dashHit` que
  no tocan el índice del combo (así se intercalan gratis).
- El impulso y el saltito necesitan un gancho en `PlayerMovement` (impulso que no anule el control).
