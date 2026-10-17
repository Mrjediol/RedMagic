# Personaje nuevo: animaciones y efectos de la pared

Pendiente desde 2026-10-17. El movimiento de pared ya funciona (`PlayerMovement` ▸ WALL, ver `docs/architecture/combat.md`),
pero el personaje actual no tiene animaciones para él y se ve en la pose de caída. Esto es la lista de lo que hará falta
cuando se haga el personaje nuevo.

## Ganchos que ya da `PlayerMovement` (para el animador / los efectos)

- `IsWallSliding` (bool) y `WallSide` (-1 pared a la izquierda, 1 a la derecha): mientras está agarrado mira hacia fuera
  (`Facing = -WallSide`).
- `IsWallStuck` (bool): clavado tras chocar con un dash (0,5 s, no cae).
- Eventos: `WallGrabbed(lado)` al agarrarse, `WallStuck(lado)` al clavarse con el dash, `WallJumped(dirección)` al saltar de
  la pared, `Dashed(dirección)` al hacer un dash (también el de salida de la pared). Sonido del salto de pared: `OnJump`.
- Sacudidas que ya hay (ajustables en el Inspector): al clavarse con el dash y al salir de la pared con el dash.

## Animaciones

1. **Deslizar por la pared** (bucle): pegado de lado, una mano/garra arañando la pared, mirando hacia fuera. Se repite
   mientras `IsWallSliding` y no `IsWallStuck`.
2. **Agarrarse** (transición corta, 2-4 fotogramas): del salto/caída al deslizar.
3. **Clavado tras el dash** (0,5 s): impacto contra la pared, cuerpo aplastado contra ella, sin bajar. Puede ser la de
   agarrarse más exagerada.
4. **Salto de pared**: despegue impulsándose con piernas y brazos hacia el lado contrario (luego enlaza con la de salto
   normal / ápice / caída).
5. **Dash desde la pared**: arranque del dash empujándose de la pared (puede ser la de dash normal con 1-2 fotogramas de
   empuje al principio).

## Efectos (FX)

- **Polvo / chispas de hielo al deslizar**: partículas pequeñas en la mano que roza, en bucle mientras desliza.
- **Impacto al clavarse con el dash**: pequeño estallido contra la pared (y ya hay sacudida).
- **Salida del salto de pared**: soplo de polvo en la pared, hacia fuera.
- **Salida del dash desde la pared**: estela/onda en la pared (ya hay una sacudida pequeña).
- Efectos con el proceso de bocetos (`docs/fx-proceso-bocetos.md`) y como prefabs intercambiables.

## Sonidos (calificarlos en el registro)

- Roce al deslizar (bucle), golpe al clavarse, salto de pared (ahora usa el del salto normal).
