# Guía de herramientas web (Tools ▸ Web)

Referencia rápida para cuando vuelves después de meses y no te acuerdas de nada. Todo lo que
aparece bajo **Tools ▸ Web** en el editor de Unity es el lado "importador" de una app web que vive
fuera de Unity, en `Web/RedMagicWeb/`. El flujo siempre es: trabajas en el navegador → exportas un
`.json` o `.zip` → lo importas aquí con uno de estos menús.

Todo lo demás bajo **Tools ▸ RedMagic** (Camera, Jugador, Hub, Boss, FX, Items, UI, Pipeline,
Ability Starter Pack, World Scene Generator) es herramienta interna de Unity, no depende de la web,
y se queda donde está — en particular **Pipeline no es parte de esto**: corta láminas de sprites y
genera enemigos/jefes a partir de `SpriteSheetRecipe`/`EnemyRecipe` (assets de Unity que eliges en
el Inspector), nunca lee nada exportado por la web.

## Cómo levantar la web app

No hay build ni servidor propio — son módulos ES planos. Sirve la carpeta con cualquier servidor
estático y abre la URL que imprima:

```
npx serve Web/RedMagicWeb
# o
python -m http.server --directory Web/RedMagicWeb 8000
```

Abrir `index.html` con doble clic (`file://...`) NO funciona: los navegadores bloquean los módulos
ES por CORS en ese caso (verás un banner de aviso en la propia página si pasa esto).

---

## Tools ▸ Web ▸ Enemy Importer ▸ Build Enemy From Folder

**Qué hace**: recorta los sprites + genera un `AnimationClip` por animación + un
`AnimatorController` + el `SpriteSheetRecipe` "de compatibilidad" que `EnemyConfigImporter`/
`EnemyFactory.Generate` necesitan para construir el prefab jugable de verdad (`EnemyStats`/
`EnemyBrain`/`EnemyAttack`, ver el siguiente menú) — y, si el enemigo dispara algo, el mismo
tratamiento + un prefab de proyectil, a partir de la carpeta que produce la pestaña **Sprites** de
la web. **Ya NO genera un prefab de enemigo propio**: una versión anterior escribía uno
sólo-sprite, sin ningún componente de gameplay, pensado para un flujo manual ya retirado — se quitó
porque nada lo leía nunca (`EnemyFactory.Generate` sólo necesita el `SpriteSheetRecipe` + los
PNG/controller en disco, nunca ese prefab).

**Web app**: pestaña 🍄 *Sprites* — botón "⬇ Descargar .zip" (o "Exportar zip" desde una entrada
guardada en la Biblioteca).

**Entrada esperada**: descomprime el `.zip` DENTRO de `Assets/`, por ejemplo en
`Assets/Art/EnemyImports/RawImport/MushroomWarrior/`, y selecciona esa carpeta cuando el menú te la
pida. Dentro debe estar `manifest.json` más una carpeta de PNGs por animación (formato que fija
`Web/RedMagicWeb/modules/export-manifest.js`).

**Qué produce**:
- `Assets/Art/EnemyImports/<Nombre>/Animations/*.anim`
- `Assets/Art/EnemyImports/<Nombre>/<Nombre>Controller.controller`
- `Assets/Art/EnemyImports/<Nombre>/<Nombre>.sheet.asset` (un `SpriteSheetRecipe`)
- `Assets/Prefabs/Projectiles/<NombreProyectil>/...` (clip + controller + prefab) si el enemigo
  dispara algo
- El prefab jugable de verdad sale del siguiente menú (*Import Config...*), apuntando su `art` a
  este `.sheet.asset` — no de aquí directamente.

**Caveats**:
- Cada frame del `AnimationClip` apunta DIRECTAMENTE al PNG de entrada — **no copia los frames a
  otro sitio**. Si borras la carpeta de entrada después de importar, el enemigo se queda sin
  sprites (invisible en Play), aunque el clip conserve el número de frames. Deja esa carpeta donde
  está para siempre, no la trates como material de staging.
- Recomendado tener instalado el paquete "Newtonsoft Json" (`com.unity.nuget.newtonsoft-json`)
  para un parseo robusto del manifest; sin él usa un parser manual de respaldo.

---

## Tools ▸ Web ▸ Import Config...

Cuatro entradas, todas leen el `EnemyConfig`/`ProjectileSpec`/`BossDefinition` JSON que arma la
pestaña **Enemy Creator** de la web. Comparten un único punto de entrada
(`ConfigImportRunner`/`ConfigImportWindow`) que detecta automáticamente qué tipo de config es por
su forma (o respeta el tipo si lo fuerzas desde la ventana).

**Web app**: pestaña 🧩 *Enemy Creator* — botón "⬇ Exportar solo config (.json)".

### Importar archivo...
Importa un config JSON suelto sin tocar el `EnemyStats`/tuning ya afinado a mano en el prefab
existente (si el prefab ya existe, solo actualiza lo que el JSON trae explícitamente).

### Importar archivo (RESETEANDO tuning de enemigo)...
Igual, pero si el JSON es un `EnemyConfig` **sobrescribe** el `EnemyStats` completo del prefab con
los valores del JSON — pide confirmación antes, porque cualquier ajuste manual hecho en el
Inspector se pierde.

### Ventana de importación
Abre una ventana (`ConfigImportWindow`) para elegir el archivo y el tipo de config a mano en vez de
usar la detección automática — útil si el JSON es ambiguo o quieres forzar el tipo.

### Importar enemigo completo (sprites + config)...
**Web app**: pestaña 🧩 *Enemy Creator* con un enemigo enlazado a la Biblioteca — botón "⬇ Exportar
todo junto (.zip)" (o "Exportar combinado" desde la pestaña 📚 Biblioteca).

Una tercera vía de import: el `.zip` combinado trae el mismo `manifest.json` + carpetas de PNG que
lee *Enemy Importer* arriba, más un `enemy-config.json` en la raíz. Descomprime, corre el import de
sprites tal cual (mismo resultado que *Build Enemy From Folder*), y con el `SpriteSheetRecipe` recién
creado corre el import de config tal cual — el campo `art` del config se sobrescribe con la ruta
real del recipe que se acaba de crear, así que un `enemyName` que no coincida entre el manifest y el
config falla alto en vez de producir un enemigo con el arte equivocado.

**Qué produce**: lo mismo que *Enemy Importer* arriba, más el prefab jugable real (vía
`EnemyConfigImporter`/`EnemyFactory.Generate`, en `Assets/Prefabs/Enemies/Enemy_<Nombre>.prefab`)
con el `EnemyStats`/tuning ya aplicado desde el config — un enemigo listo para jugar en un solo
paso.

**Caveats**: mismo aviso que arriba sobre no borrar la carpeta de entrada
(`Assets/Art/EnemyImports/<Nombre>/`) después de importar.

---

## Tools ▸ Web ▸ Map Tracer

Los tres importan el/los JSON que produce la pestaña 🗺️ **Map Tracer** de la web (piezas +
instancias + líneas de colisión). Ninguno trae los PNG en el JSON — resuelven cada sprite por
nombre/archivo contra lo que YA esté importado en `Assets/` (`FindSprite`, busca por nombre exacto
sin extensión), así que **importa primero las imágenes de las piezas como Sprites normales en
Unity, con nombres únicos en todo el proyecto**, antes de correr cualquiera de estos tres.

### Import Map JSON as Prefab
Importa UN mapa compuesto: coloca cada pieza (fondo/plataforma/borde) en su posición/escala tal
cual se dejó en el lienzo de la web, y genera los `EdgeCollider2D` de las líneas trazadas sobre las
capas `Ground`/`Platform`.

**Web app**: pestaña 🗺️ *Map Tracer* — botón "⬇ Map JSON + colisiones" (formato `RedMagicMap/1`).

**Qué produce**: un único prefab con tres contenedores (`Background`/`Platforms`/`Border`) más un
contenedor `Collisions` con los `EdgeCollider2D`. Tú eliges la ruta del prefab al importar.

**Caveats**:
- Las layers `Ground` y `Platform` deben existir en Project Settings ▸ Tags and Layers o el import
  se rechaza.
- El PPU (pixels-per-unit) de los colliders se lee del PPU real del primer sprite de tipo
  borde/plataforma que resuelva en ese mapa — si un mismo mapa mezcla piezas de borde a PPU
  distintos, todas las líneas de ese tipo comparten el PPU de la primera pieza encontrada (limitación
  heredada del formato de export, que no guarda a qué pieza pertenece cada punto).

### Import Map JSONs (Batch)...
Igual que el anterior pero para varios mapas de una vez: elige una CARPETA (Unity no tiene selector
nativo de múltiples archivos) y importa cada `.json` que encuentre directamente dentro de ella —
sin preguntar por archivo. Cada prefab se guarda como `<carpeta>/<nombreDelJson>.prefab`, junto al
JSON del que vino.

**Qué produce**: igual que arriba, uno por JSON, con un único diálogo resumen al final (creados,
fallidos, sprites totales no encontrados) en vez de un diálogo por mapa.

### Import Map Pieces as Prefabs...
Importa piezas SUELTAS (no un mapa compuesto) — un prefab independiente por pieza, con su propio
`SpriteRenderer` y, si la pieza es de tipo borde/plataforma, sus propios `EdgeCollider2D` en espacio
local (no relativos a ninguna instancia colocada en un mapa).

**Web app**: pestaña 🗺️ *Map Tracer* ▸ Biblioteca — "⬇ Exportar" en una pieza individual, o
"⬇ Exportar piezas seleccionadas" con varias piezas marcadas (formato `RedMagicMapPieces/1`). Esta
es la única vía que produce este formato — no existe un modo "exportar las piezas usadas en el
mapa actual", solo piezas guardadas en la Biblioteca de la web.

**Qué produce**: pide UNA carpeta para todo el lote (`SaveFolderPanel`) y guarda ahí un prefab por
pieza, con orden de sorting según el tipo (fondo=0, plataforma=10, borde=20 — igual que en el
importador de mapas). Un único diálogo resumen (piezas creadas, sprites no encontrados).

**Caveats**: mismas layers `Ground`/`Platform` requeridas; mismo requisito de nombres de sprite
únicos en el proyecto.

---

## Tools ▸ Web ▸ Biblioteca

**Qué hace**: una ventana acoplable (`Tools ▸ Web ▸ Biblioteca`, `Assets/Editor/WebLibraryWindow.cs`)
para navegar y borrar de forma segura lo que estos importadores (y el pipeline de enemigos/jefes)
han ido dejando en el proyecto — sin ella, limpiar un enemigo de prueba significa acordarte a mano
de las 2-4 carpetas/prefabs que le corresponden y no dejarte ninguna suelta. No lee nada de la web
directamente; es un panel sobre lo que YA está importado en `Assets/`.

**Layout**: lista de categorías a la izquierda (Enemies / Bosses / MiniBosses / Escenas / Mapas /
Player), grid de tarjetas a la derecha para la categoría activa — cada tarjeta con vista previa,
nombre, sus propios botones de acción y un par de botones ↑/↓ para reordenarla (ver "Orden manual"
abajo). Cambiar de categoría no cierra la ventana, igual que los favoritos del Project window.
"🔄 Actualizar" fuerza un re-escaneo (el resto del tiempo usa una caché en memoria, no re-escanea el
proyecto en cada frame). Es una `EditorWindow` normal (`GetWindow<WebLibraryWindow>()`), así que
Unity recuerda dónde la acoplaste entre sesiones como cualquier otro panel — no hace falta código
extra para eso.

**Orden manual persistente**: cada categoría recuerda el orden en que dejaste sus tarjetas, entre
sesiones del Editor — los botones ↑/↓ de una tarjeta la mueven una posición y guardan el resultado
en `Library/WebLibraryWindowOrder.json` (dentro de `Library/`, no de `Assets/`: es una preferencia
de esta máquina/instalación del Editor, no datos del proyecto — nunca se versiona ni se comparte con
el equipo, igual que el resto de lo que Unity guarda en `Library/`). Un ítem nuevo que no está
todavía en el orden guardado aparece al final, en el orden que devolvió el escaneo.

**Por qué "Eliminar" puede revisar hasta 2 rutas por enemigo — historial**: hasta la reestructura de
carpetas (ver `docs/folder-restructure-audit.md`), dos importadores distintos escribían un prefab
con forma de enemigo en dos carpetas hermanas reales y pobladas: `Assets/Prefab/Enemies/
Enemy_<Nombre>.prefab` (pipeline/`EnemyFactory`/`EnemyConfigImporter` — el prefab de verdad, con
`EnemyStats`/`EnemyBrain`/`EnemyAttack`, listo para jugar) y `Assets/Prefabs/Enemies/<Nombre>.prefab`
(el `EnemyImporter.cs` standalone — "Build Enemy From Folder" —, sin prefijo; sólo sprites/Animator,
sin ningún componente de gameplay). El segundo era un artefacto INTERMEDIO camino al primero (el
propio `EnemyConfigImporter` lee los sprites que ese paso deja para construir el prefab real) — no
algo pensado como entrada de biblioteca. La categoría **Enemies** ya lo excluía por completo desde
antes de la reestructura; en la reestructura, ese paso de escritura se quitó del todo de
`EnemyImporter.cs`, la carpeta plural (ya sólo basura de pruebas) se borró, y la carpeta real se
renombró para reusar ese mismo nombre plural ahora libre — así que hoy sólo existe UNA carpeta de
prefabs de enemigo, `Assets/Prefabs/Enemies/` (plural), y es la que escanea esta categoría. Lo que
sí sigue revisando por `<Nombre>` junto al prefab real son sus datos de sprites, que pueden vivir en
`Assets/Art/Characters/<Nombre>/` (camino pipeline) y/o `Assets/Art/EnemyImports/<Nombre>/` (camino
`EnemyImporter.cs`, renombrado desde `Assets/Enemies/<Nombre>/` en la misma reestructura — el
prefab real puede seguir apuntando ahí si vino de "Importar enemigo completo"). "Eliminar enemigo"
lista las rutas que realmente existen (hasta 2: el prefab más esa carpeta), pide confirmación
explícita, y borra cada una con `AssetDatabase.MoveAssetToTrash` (recuperable desde la papelera del
sistema operativo, nunca un borrado duro) — si alguna ruta ya no existe, se omite y se reporta al
final en vez de abortar todo el borrado. Los proyectiles (`Assets/Prefabs/Projectiles/<Nombre>/`)
**no** se borran con el enemigo a propósito: están indexados por nombre de proyectil, no de
enemigo, y nada impide que dos enemigos compartan uno — bórralos a mano si quedan huérfanos.

**Por categoría**:

- **Enemies** — un card por `<Nombre>`, desde `Assets/Prefabs/Enemies/Enemy_<Nombre>.prefab` (la
  única carpeta de prefabs de enemigo que existe hoy — ver arriba). "Abrir prefab" entra en Prefab
  Mode sobre ese prefab real para tocar `EnemyStats`/tuning directamente en el Inspector. "Eliminar
  enemigo" como se describe arriba.
- **Bosses** — un card por `BossDefinition` en `Assets/Resources/Bosses/` (`Boss_<Nombre>.asset`),
  con el `Boss_<Nombre>.prefab` de `Assets/Prefabs/Enemies/` como vista previa si existe. Sólo
  "Abrir definición"/"Abrir prefab" — **sin acción de borrado**: los `BossAttack_*.asset` de un
  jefe viven sueltos en la misma carpeta plana sin subcarpeta propia (a diferencia de los
  personajes), así que no hay un conjunto de rutas seguro y completo que enumerar todavía; bórralos
  a mano.
- **MiniBosses** — vacío siempre por ahora: el audit confirmó, por grep exhaustivo, que no existe
  ningún tipo/convención de asset para minijefes en el proyecto. Estado vacío explícito en vez de
  fingir que hay algo que listar.
  ("¿por qué mostrar una categoría vacía?" — para que quien la abra sepa que se comprobó y no que
  se olvidó comprobar.)
- **Escenas** — un card por cada `t:Scene` dentro de `Assets/Scenes/` (no el proyecto entero — deja
  fuera escenas de prueba, de paquetes de assets vendorizados, etc. que no son escenas jugables
  reales). "Abrir escena" respeta el flujo normal de Unity: si la escena actual tiene cambios sin
  guardar, pregunta antes de cambiar (`EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo`).
- **Mapas** — sin carpeta fija que barrer (`MapImporter` guarda donde el usuario apunte al
  exportar), así que se detectan por FORMA: cualquier prefab con un hijo llamado exactamente
  `Collisions` — la huella que `MapImporter.ImportOneMap` siempre deja. "Eliminar mapa" también
  borra el `.json` homónimo en la misma carpeta si Unity lo tiene indexado como asset (la
  convención de nombre 1:1 carpeta/JSON que usa el importador por lotes — ver *Import Map JSONs
  (Batch)...* arriba).
- **Player** — una sola tarjeta fija (`Assets/Prefabs/Player.prefab`) si existe. Sólo "Abrir prefab":
  a diferencia de todo lo demás, el jugador no tiene convención por nombre (`PlayerPack.cs`
  re-viste ese único prefab in situ), así que no hay nada que desambiguar ni una acción de borrado
  segura que ofrecer aquí — es el único prefab de jugador del juego.
