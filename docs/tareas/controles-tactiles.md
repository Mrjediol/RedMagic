# Controles táctiles por gestos — refinar

**Objetivo:** controles de móvil sin botones de dirección que se fallen al pulsar: andar arrastrando, disparar
tocando la pantalla, botones solo para lo imprescindible. El usuario lo va a probar en su móvil (RedMagic 11 Pro,
~1116×2480 en horizontal) y hay que pulirlo con lo que diga.

Arquitectura: `docs/architecture/input.md` (gestos, botones leídos de los toques) y `docs/architecture/ui.md`
(colocar botones con `HudLayoutEditor`).

## Hecho (2026-10-15)
- **Opción** Opciones ▸ Interfaz ▸ *Controles táctiles por gestos* (`HudSettings.GestureControls`). El esquema
  clásico sigue disponible.
- **Zona de movimiento** abajo a la izquierda (ancho 0,336 × alto 0,375 de la pantalla, guardado en
  `Resources/MenusUI.prefab`): joystick flotante, ±1 al pasar la deadzone, arrastrar hacia abajo atraviesa plataformas.
  Lógica en `Assets/Test/TouchGestures/TouchGestureInput.cs`.
- **Dash por re-toque**: soltar andando y volver a tocar cerca y enseguida (`retapWindow` 0,25 s, `retapRadius` 200 px)
  = dash y sigue andando hacia el mismo lado. También hay botón de dash.
- **Magia**: tocar fuera de la zona mantiene el arma (`TouchInput.WeaponHeld`); con carga, al soltar dispara si está
  completa. Apunta al dedo (`TouchInput.AimScreen` → `ProjectileAim.MouseWorld`). El cometa ya respeta
  BaseShot ▸ Aim Mode.
- **Botones** (dash · ataque · salto abajo, parry encima del salto, USAR encima del ataque): se leen de los toques
  directamente (`Assets/Ui/TouchTapButtons.cs`, EnhancedTouch), no con eventos de `Button`, que perdían o retrasaban
  pulsaciones. El salto corta al soltar (`TouchInput.QueueJumpRelease`), como en teclado/mando. USAR: toque =
  interactuar, mantener = reciclar.
- **Colocar botones**: pausa ▸ Opciones ▸ Interfaz ▸ *Colocar botones táctiles* / *Botones por defecto*
  (`TouchControlsController.BeginLayoutEdit`, posiciones en `HudSettings` por `TouchElement`).
- La pausa se sondea en `TouchControlsController.Update`: el `GameStateManager` vive en la escena y nace después que
  `MenusUI`, así que el evento `PausedChanged` nunca llegaba.

## Dónde se ajusta
Prefab `Assets/Resources/MenusUI.prefab` ▸ hijo **TouchControlsUI** ▸ componente **Touch Controls Controller**:
- **Gestures**: Move Zone Width/Height, Show Move Zone, Deadzone, Stick Radius, Swipe Down Drops Through,
  Drop Threshold, Retap Window, Retap Radius.
- **Button Hit Padding**: margen de toque alrededor de los botones.
En Play se ve en DontDestroyOnLoad ▸ MenusUI ▸ TouchControlsUI (lo cambiado ahí se pierde al salir de Play).
**Ojo**: cambiar el valor por defecto en el C# no llega al prefab ya cargado; escribirlo en el prefab
(`PrefabUtility.LoadPrefabContents` → `SaveAsPrefabAsset`) y comprobar el YAML.

## Pendiente (pasos en todo.json)
1. Prueba en el móvil real: multitáctil (andar + disparar, andar + saltar), dash por re-toque (¿falsos positivos?),
   tamaño de la zona, tamaño y sitio de los botones, sensación del salto variable.
2. Ajustar valores con lo que diga el usuario y guardarlos en el prefab.
3. Error `Screen position out of view frustum (screen pos inf, -inf)` desde `UIElementsRuntimeUtilityNative.UpdatePanels`:
   no sale de nuestro código; probablemente lo provoca el Simulator al soltar un toque. Confirmar si sale en el móvil.
4. Esquema clásico: decidir si se queda, se mejora o se retira (sus botones de dirección y "abajo" siguen con eventos de UI).
5. Cuando esté validado, mover `TouchGestureInput` de `Assets/Test/` a `Assets/Ui/`.

## Decisiones
- Sin doble toque genérico para el dash: chocaba con tocar para disparar. Por eso el dash es por re-toque en la zona de
  movimiento + botón.
- Movimiento digital (a tope al pasar la deadzone), no analógico: lo pidió el usuario ("camine rápidamente").
- Solo se recolocan los botones del esquema por gestos.
