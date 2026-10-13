# Validar en el móvil / mando: salas, tienda, menús (sesión 2026-10-13)

Construido y compilando, **no probado en un dispositivo** (el usuario hacía build a `E:\Builds`). Probar y anotar fallos:

1. **Salas** (`3 CombatRoomsTest`, Mundo 1): se cierra a las 2 celdas de entrar (`LevelRoom.entryMargin`), oleadas al entrar, *terminar sala* abre las
   puertas y suelta item, *terminar nivel* deja salir con una sala opcional sin limpiar.
2. **Tienda** (World1 `shopAfterSections = [1]`, una entre los 2 niveles): comprar con inventario lleno (pantalla de elegir hueco), el item
   sustituido cae al suelo, equipar con E / L1 / USAR, **mantener F / L1 / USAR 0,8 s = reciclar 30 %**. Subir `CurrencyConfig` (drops) para probar.
3. **Menús persistentes** (`Resources/MenusUI.prefab`: pausa, opciones, controles táctiles): pausa en todos los niveles y en la tienda; nada de eso en
   el menú principal. Opciones ▸ *Límite de FPS* (por defecto la frecuencia de la pantalla: debería dar 144 en el Red Magic 11 Pro; si sigue en 60, es el
   Game Space del móvil) y *Mostrar FPS*.
4. Cuadro magenta al inicio de `NivelHielo_1000` (arreglado metiendo los shaders propios en *Always Included Shaders*) y energía de las puertas
   fina (shader nuevo): confirmar en el móvil.
5. **Mando**: la pantalla de elegir hueco (`ItemMenuController.OpenReplace`) **no está preparada para navegar con mando** (sólo ratón/tocar). Arreglar.
6. **Auditar claves** (localización) = 0 faltantes: se añadieron `touch.interact/weapon`, `items.replace_title`, `item.recycled`, `ground.*`, `legend.attack/weapon`, `options.fps_*`, `options.show_fps` (es/en).
7. Quitar la tecla **P** temporal de `PauseMenuController` (marcada TEMPORAL) y el script de una sola vez `Assets/Scripts/Editor/MenusUiMigration.cs` (ya migrado).
8. Cambios de movimiento ya fusionados (jugador y enemigos sin rampas ni subida automática): confirmar que no hay tirones al aterrizar ni enemigos que resbalen.

Controles táctiles: **se rehacen cuando el player esté acabado** (decisión del usuario); ahora sólo mando.
