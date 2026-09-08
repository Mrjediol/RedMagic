# Legacy — solo referencia

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
