# Legacy — solo referencia

## `TombInteractable` — la tumba del hub

Cómo se entraba a una run hasta ahora: un objeto del hub con un trigger de proximidad, un aviso de
"pulsa E" y su propio manejo de input (Input System + teclas de reserva + la cola táctil), que
llamaba a `RunManager.StartRun(world)`.

Sustituido por un **`SectionExit` con el campo `world` asignado**: salir del hub y pasar de sección
son el mismo gesto — cruzar una puerta — así que ahora son el mismo componente, y es el
`RunManager` quien decide si eso significa empezar la run o avanzar a la siguiente sección. Se gana
que la salida del hub herede lo que ya tenía la de sección (`requireEnemiesDead`, `oneShot`, el
gizmo) y se pierde una segunda forma de interactuar que había que mantener.

## `CauldronInteractable` — el caldero, placeholder del menú de mejoras

Un solo toque abría directamente `UpgradeMenuController`: sin animación real, sin el flujo en dos
pasos. Era el objeto de pruebas mientras no existía el atril del libro mágico. Sustituido por
**`Hub.BookLootContainer`** (sobre `Hub.HubLootContainer`, igual que el cofre y el armario): abrir →
esperar → consultar → cerrar, donde "consultar" es lo que abre el menú de mejoras. No bloquea la
salida del hub — las mejoras son progreso opcional.

## `AbilityChest` — el cofre del hub, versión de un solo toque

Daba el arma en el primer (y único) toque: abrir, esperar `grantDelay`, entregar. Sustituido por
**`Hub.ChestLootContainer`** (sobre `Hub.HubLootContainer`, compartida con el armario) porque el
cofre del hub ahora pide dos toques — abrir y dejar el contenido a la vista, y sólo entregar el
arma en el segundo — y quedarse gastado hasta que se complete una run y se vuelva al hub, para que
el arma inicial sea de verdad obligatoria (`SectionExit.BlockedByMissingWeapon` no deja cruzar la
puerta del hub sin un arma equipada). La lógica de entrega en sí (`Grant`) se portó tal cual a
`ChestLootContainer.OnLoot`; lo único legacy es la forma de disparar en un solo toque.
`AbilityChestEditor` (el desplegable de armas para pruebas) queda aquí al lado, sustituido por
`Hub.EditorTools.ChestLootContainerEditor`.

## Sistema de habilidades

Código del **sistema de habilidades** (22 assets, ahora en `Assets/Resources/Legacy/Abilities/`) y
del sistema de **niveles de arma 1–3** que lo acompañaba. Sustituido por el sistema de items/armas
(`Assets/Scripts/Items/`): el arma es un `WeaponDefinition` y los items (Elemento / Trayectoria /
Forma / pool libre) la modifican a través de `ShotResolver`; los niveles ahora los lleva
`WeaponLevelManager` + el altar `WeaponForgeAltar`/`WeaponForgeMenuController`.

Sigue compilando y los `.meta` se movieron con los scripts y los assets, así que los GUIDs se
conservan. `Player.prefab` sigue llevando el componente `AbilityUser` (inerte mientras nada lo
equipe). `WeaponUpgrade.prefab` (el que suelta el jefe) **se repuntó** de `WeaponUpgradeAltar` al
nuevo `WeaponForgeAltar` — con el viejo, la forja siempre decía "no llevas arma equipada" porque
buscaba un `AbilityUser`, y con el sistema de items nadie lo equipa.

`AbilityLibrary.ResourceFolder` apunta a `Legacy/Abilities` (antes `Abilities`), así que el menú de
pruebas (tecla K) sigue listando las 22 habilidades como referencia funcional en vez de salir
vacío.

**No construir nada nuevo sobre esto**: leerlo como referencia y portar lo que haga falta al
sistema de items.

Se queda fuera de esta carpeta lo que el juego actual sí usa: `AbilityContext` / `AbilityHit`
(filtrado de objetivos y daño, lo reusan las armas y los jefes), `AbilityFx`, `ProjectileSpec` /
`ProjectileFactory` y `Projectile` (los jefes disparan con ellos).
