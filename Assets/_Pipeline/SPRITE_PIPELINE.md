# Sprite pipeline — de una lámina a un enemigo jugable

Este documento es la referencia completa. **Si estás leyendo esto para importar una lámina nueva,
no hace falta que mires el código**: los pasos de abajo bastan.

Regla que gobierna todo el sistema: *lo que se repite se automatiza*. Añadir contenido debe ser
escribir lo que ese contenido tiene de particular; todo lo compartido — vida, retroceso, parpadeo
al golpe, cadáver, monedas, collider, cuerpo físico — lo pone la herramienta.

---

## 0. Resumen en 30 segundos

```
Lámina (PNG/JPG)
   │   SpriteSheetRecipe.asset   ← la rejilla y el mapeo fila→estado, una sola vez
   ▼
Assets/Art/Characters/<Nombre>/<Nombre>_<Estado>.png     hojas limpias, ya cortadas
   ▼
   ├─ Anim/<Nombre>_<Estado>.anim  +  <Nombre>.controller   (runtime = Animator)
   └─ estados dentro de un SpriteStateMachine              (runtime = Flipbook)
   ▼
   EnemyRecipe.asset  →  Assets/Prefab/Enemies/Enemy_<Nombre>.prefab
```

Menú: **Tools ▸ RedMagic ▸ Pipeline**.

---

## 1. Estructura de carpetas

| Ruta | Qué contiene |
|---|---|
| `Assets/Sprites/` | Láminas tal como llegan del artista. No se tocan nunca. |
| `Assets/Art/Characters/<Nombre>/` | Todo lo generado de ese personaje. |
| `Assets/Art/Characters/<Nombre>/<Nombre>.sheet.asset` | La receta de la lámina. **Es la fuente de verdad del corte.** |
| `Assets/Art/Characters/<Nombre>/<Nombre>.enemy.asset` | La ficha del enemigo. **Es la fuente de verdad de sus números.** |
| `Assets/Art/Characters/<Nombre>/<Nombre>_<Estado>.png` | Hoja limpia de un estado, cortada en sprites. |
| `Assets/Art/Characters/<Nombre>/Anim/` | Los `.anim` generados. |
| `Assets/Art/Characters/<Nombre>/<Nombre>.controller` | El AnimatorController generado. |
| `Assets/Prefab/Enemies/Enemy_<Nombre>.prefab` | El prefab final. |
| `Assets/Scripts/Pipeline/Editor/<Nombre>Pack.cs` | El pack: escribe las dos recetas y lanza el pipeline. Opcional pero recomendado. |

**Un personaje = una carpeta.** Igual que los FX de jefe (`Assets/Prefab/Fx/Bosses/<Jefe>/`), un
repaso de arte de un personaje es una carpeta, y ningún personaje lee los assets de otro.

## 2. Convención de nombres

| Cosa | Patrón | Ejemplo |
|---|---|---|
| Sprite | `<Personaje>_<Estado>_<##>` | `TreeWalk_Attack_03` |
| Hoja de estado | `<Personaje>_<Estado>.png` | `TreeWalk_Attack.png` |
| Clip | `<Personaje>_<Estado>.anim` | `TreeWalk_Attack.anim` |
| Controller | `<Personaje>.controller` | `TreeWalk.controller` |
| Prefab | `Enemy_<Nombre>.prefab` | `Enemy_TreeWalk.prefab` |

Los índices empiezan en `00` y llevan dos cifras siempre.

### Estados que el generador cablea solo

`Idle` · `Walk` · `Attack` · `Hurt` · `Death` · `Wake`

(`Wake` sólo se usa con `sleepsUntilDetected`, ver §6-sexies: `Idle →(trigger Wake)→ Wake →(fin)→ Walk`.)

Cualquier otro nombre se crea igualmente como estado y como clip, pero **sin transiciones**: hay
que cablearlo a mano en la ventana del Animator, o dispararlo por código.

Los parámetros del Animator son los mismos que ya usa `PlayerAnimator`, a propósito, para que el
proyecto tenga un solo vocabulario:

| Parámetro | Tipo | Lo escribe |
|---|---|---|
| `Speed` | Float | `EnemyAnimator`, desde la velocidad horizontal del `Rigidbody2D` |
| `Attack` | Trigger | `EnemyAnimator` al acercarse al objetivo, o `EnemyAnimator.TriggerAttack()` |
| `Hurt` | Trigger | evento `Health.Damaged` |
| `Dead` | Bool | evento `Health.Died` |

---

## 3. Formato de lámina esperado

- Una **fila por estado**, un **frame por columna**. El número de frames puede variar por fila.
- Se admiten rótulos escritos en la imagen (`IDLE`, `ATTACK`…): el corte los descarta.
- Las filas se declaran en la receta **de arriba abajo**, en el mismo orden que en la imagen.

### Alfa

**Un PNG con alfa real siempre da mejor resultado.** El corte conserva los bordes suaves tal cual.

Si la lámina no tiene alfa (un JPG, o un PNG con el damero de transparencia pintado encima), deja
`Key Background` encendido: se deduce el color de fondo del marco de la imagen y se recorta. Es un
recorte **duro** — sin semitransparencias — así que el borde queda algo más seco que con alfa real.
Funciona bien y es lo que se ha usado con `TreeWalk.jpg`.

Si quedan restos del fondo, sube `Background Tolerance`. Si se come parte del arte, bájala.

---

## 4. Procedimiento

### 4.1 Camino rápido (ventana)

1. **Tools ▸ RedMagic ▸ Pipeline ▸ 1 · Ventana de pipeline**
2. Crea la receta: *Assets ▸ Create ▸ RedMagic ▸ Pipeline ▸ Sprite Sheet Recipe*.
   Asigna `sheet`, escribe `characterName`, describe las filas (estado, nº de frames, fps, loop).
3. Arrastra la receta a la ventana y pulsa **Cortar hoja + generar animación**.
4. Mira las hojas generadas. Si algún frame sale mal, ajusta la receta y repite: es idempotente.
5. Crea la ficha: *Assets ▸ Create ▸ RedMagic ▸ Pipeline ▸ Enemy Recipe*, apúntala a la receta de
   arriba y rellena vida, velocidades y daño.
6. Pulsa **Cortar + generar prefab de enemigo**.

### 4.2 Camino reproducible (pack) — el recomendado

Copia `Assets/Scripts/Pipeline/Editor/TreeWalkPack.cs`, cambia los datos y el nombre del menú.
Un pack no hace trabajo: escribe las dos recetas y llama a `SpritePipeline.RunEnemy`. Ventaja: la
importación queda en git y se puede relanzar entera cuando llegue una versión retocada de la lámina.

Desde la CLI, sin abrir la interfaz:

```bash
unity command run_script --file Assets/Scripts/Pipeline/Editor/TreeWalkPack.cs \
                         --entry RedMagic.Pipeline.EditorTools.TreeWalkPack.Run
```

### 4.3 Sustituir un placeholder

Para cambiar el arte de un prefab que ya existe y ya funciona (el círculo rojo de un proyectil, la
caja gris de un enemigo):

1. Corta la lámina (paso 4.1, hasta el punto 4).
2. En la ventana, sección **3 · Sustituir un placeholder**: arrastra el prefab, ajusta la escala y
   pulsa **Vestir**.

Cambia únicamente el `SpriteRenderer` (sprite + color a blanco) y engancha el Animator o el
flipbook. **Colliders, scripts, Rigidbody, tamaños y referencias no se tocan.** Si algo no gusta,
`git checkout` del prefab y la lógica ni se entera.

### 4.4 Comandos de menú

| Menú | Qué hace |
|---|---|
| `Pipeline ▸ 1 · Ventana de pipeline` | Todo el proceso en una pantalla |
| `Pipeline ▸ 2 · Cortar hoja + animar` | Con una `SpriteSheetRecipe` seleccionada en el Project |
| `Pipeline ▸ 2b · Diagnosticar bandas y manchas` | Imprime el recuadro y el área de cada mancha, banda por banda. Es con lo que se ajustan `groupSlack` y `propBlobs` |
| `Pipeline ▸ 3 · Generar enemigo` | Con una `EnemyRecipe` seleccionada |
| `Pipeline ▸ 4 · Auditar contenido` | Lista los prefabs con `Health` a los que les falta algo |
| `Pipeline ▸ 5 · Auditar y reparar` | Añade lo que falte |
| `Pipeline ▸ 8 · Auditar animaciones terminales` / `9 · … y reparar` | Muerte/impacto en bucle (clips, controllers, estados de `SpriteStateMachine`, flipbooks de `VfxOneShot`). Ver §5-bis |
| `Pipeline ▸ Packs ▸ …` | Un pack por personaje |

---

## 5. Animator o Flipbook

Se elige en la receta, campo `runtime`. **No es una preferencia, depende de si el objeto pasa por
pool:**

| | `Animator` | `Flipbook` |
|---|---|---|
| Para | enemigos, jefes | proyectiles, FX, cualquier cosa en `PrefabPool` |
| Genera | `.anim` + `.controller` | estados dentro de un `SpriteStateMachine` |
| Transiciones | sí, en la ventana del Animator | no: se llama a `Play("Estado")` |

Un objeto en pool **no puede** llevar Animator: `PrefabPool` no tiene gancho de reinicio por
instancia, así que una instancia reutilizada volvería a la vida a mitad de su animación de muerte.
`SpriteStateMachine` rebobina en `OnEnable`, que es el único momento de reinicio que hay.

Para FX de una sola animación sigue existiendo `Gameplay.SpriteFlipbook`, más simple.
`SpriteStateMachine` es su versión con varios estados.

## 5-bis. Muerte e impacto nunca repiten — `Pipeline.AnimStates`

Una animación **terminal** (nombre con `Death`/`Impact`/`Explo`/`Muerte`/`Despawn`, o exactamente
`Die`/`Dead`/`End`/`Destroy`) se reproduce una vez y se queda en su último dibujo hasta que el objeto
muere o vuelve al pool. El exportador web marca `loop: true` por defecto y eso hacía que el primer
dibujo reapareciera al final (cadáver ~1 s visible, "Impact" esperando a acabar). La regla vive en
**un sitio** (`AnimStates.IsTerminal` / `Loops`) y se aplica en tres capas:

- **Importadores** (`AnimClipBuilder`, `EnemyImporter`, `FxPrefabBuilder`, `BossBodyBuilder`,
  `PlayerFrameImporter`): el bucle de un estado terminal se escribe siempre a `false`. Un VFX de
  `FxPrefabBuilder` nunca repite.
- **Runtime**, por si queda un asset viejo: `SpriteStateMachine` no repite ni vuelve al estado por
  defecto tras un terminal (`PlayOnce` para forzarlo; `Projectile` lo usa en "Impact",
  `EnemyAnimation` en la muerte); `VfxOneShot` pone en una sola pasada su flipbook / máquina;
  `EnemyAnimation` clava el Animator en el último frame si el clip de muerte viniera en bucle.
- **Auditoría** `Pipeline ▸ 8/9`: relánzala tras importar contenido nuevo.

Un estado terminal nuevo con otro nombre: añade el término en `AnimStates`, no un parche por objeto.

---

## 6. Qué pone la fábrica de enemigos sin que se lo pidas

En la raíz del prefab: `Rigidbody2D` (dinámico, rotación congelada, sin gravedad si vuela),
`BoxCollider2D`, `Health`, `Knockback`, `HitFlash`, `Corpse`, `CurrencyDropper`,
`EnemyController`, `EnemyAnimator`.
En el hijo `Sprite`: `SpriteRenderer` y el `Animator` (o el `SpriteStateMachine`).

Esto no es comodidad, es corregir un fallo que era silencioso: `Health` funciona perfectamente sin
`Knockback` y sin `HitFlash`. El enemigo recibe daño y muere, pero no parpadea ni sale despedido —
y eso, jugando, se lee como que el juego no registra los impactos. No hay error en consola.

Detalles que la fábrica decide sola:

- **Collider**: si `colliderSize` es `(0,0)` se deduce del sprite de reposo — 55% del ancho (para
  que la copa del árbol no choque con las paredes) y 90% del alto.
- **Origen a los pies**: el pivote de los sprites es `BottomCenter`, así que el collider sube desde
  el origen. Colocar un enemigo es dejarlo sobre el terreno.
- **`invulnerabilityDuration` a 0**: los i-frames en un enemigo se tragan las armas multigolpe (una
  escopeta de 5 perdigones acertaría uno). El aturdimiento es trabajo del retroceso.

**La ficha manda sobre el prefab**: regenerar reescribe los números desde la receta. Los ajustes se
hacen en el asset, no abriendo el prefab. Lo que la fábrica no gestiona (un componente añadido a
mano) no se borra nunca.

---

## 6-bis. Ataque a distancia: el objeto suelto se convierte en proyectil solo

Cuando la fila de ataque dibuja algo separado del personaje — la piedra que el ogro ya ha soltado,
el aguijón que la abeja acaba de disparar — el corte lo detecta como un grupo de más. Declarando
en la receta **sólo las poses del personaje**, `SheetSlicer` retira ese grupo de la cuenta de
frames y lo exporta aparte, centrado y sin fondo, como `<Personaje>_<Fila>_Prop.png`.

```csharp
// La fila del Ogro tiene 5 dibujos, pero el quinto es SÓLO la piedra volando.
new SheetRow { state = "Attack", frames = 4, fps = 12f, loop = false, releaseFrame = 3 },
```

**Cuenta las poses del personaje, no los dibujos de la fila.** Es el único número que hay que
mirar dos veces al escribir un pack.

El objeto retirado **no entra en los límites del frame**, y eso importa por dos motivos medidos en
la lámina del Ogro:

- La celda es uniforme para toda la fila. Si el proyectil se fundiera con su vecino, la celda
  pasaba de ~140 px a **385** y el resto de frames se rellenaban de aire, dejando al personaje
  diminuto y descentrado.
- El juego ya lanza ahí un proyectil de verdad — ese mismo sprite. Si además quedara pintado en el
  dibujo, se verían dos.

Para convertirlo en proyectil, en la `EnemyRecipe` basta con que el arquetipo dispare y con decir
de qué fila sale el prop:

```csharp
t.archetype = EnemyArchetype.Ranged;      // o Static + staticAttack = AttackKind.Ranged
recipe.projectilePropState = "Attack";    // la fila cuyo prop se usa
recipe.projectileScale = 1f;

t.projectile.speed = 9f;
t.projectile.lifetime = 3.5f;
t.projectile.size = new Vector2(0.45f, 0.45f);
t.projectile.muzzleOffset = new Vector2(0.7f, 1f);   // a la altura de la mano
t.aimAtTarget = true;   // un tiro plano falla en cuanto hay desnivel
```

`EnemyFactory` entonces construye (o actualiza)
`Assets/Prefab/Fx/Enemies/<Nombre>/Fx_<Nombre>_Projectile.prefab` vía
**`ProjectilePrefabFactory`** y lo mete en `tuning.projectile.prefab`. Sin `FxPlaceholderStyle`: el
sprite ya es arte real, sale del propio personaje, así que el spawner sólo posiciona — no tiñe ni
redimensiona. El disparo va por `ProjectileFactory`, o sea **pooled** como todo lo demás.

Quién dispara y cuándo lo decide `EnemyAttack` en el `OnAttackRelease` que el pipeline planta en el
frame de `releaseFrame`; no hay que tocar nada más.

**La perilla del proyectil es `Enemy_<X>.prefab ▸ EnemyStats ▸ Tuning ▸ projectile`**, no el
componente `Projectile` del prefab del proyectil. `ProjectileFactory.Spawn` llama a `Configure` con
los valores del spec en **cada disparo**, así que los campos serializados de `Fx_<X>_Projectile`
(velocidad, vida, daño, homing…) se pisan siempre y editarlos ahí no hace nada. El tamaño visual sí
se toca en ese prefab porque es la escala del `SpriteRenderer`, que el spawn no toca. El pipeline
copia ahora el spec sobre el componente `Projectile` del prefab al generar, sólo para que lo que se
ve ahí sea la verdad — pero se sigue editando en el `Tuning`.

**Si la fila no dibuja nada suelto** y el arquetipo dispara igualmente, sale un aviso y el
proyectil se construye en código (una forma teñida). Funciona, pero se ve a placeholder.

---

---

## 6-ter. Las dos perillas del corte automático: `groupSlack` y `propBlobs`

El corte automático agrupa manchas por solapamiento horizontal. Dos cosas lo rompen, y cada una
tiene su perilla en la `SheetRow`. **Antes de tocarlas, mide**: *Tools ▸ RedMagic ▸ Pipeline ▸
`2b · Diagnosticar bandas y manchas`* con la receta seleccionada imprime, banda por banda, el
recuadro y el área de cada mancha. Los huecos entre dibujos se leen ahí; probar valores a ciegas
cuesta una regeneración por intento.

**`groupSlack`** — multiplica el margen con el que dos manchas se dan por del mismo frame (1 = el
de siempre, ~1/12 de la altura del personaje). Bájalo cuando dos poses casi se toquen: la cola de
un caballo al galope llega al hocico del siguiente y el margen por defecto (13 px) se come huecos
reales de 3-5 px, fundiendo cinco frames en uno. Medido en `Lobo`, la fila de andar tiene huecos de
3 px: `groupSlack = 0.2f` los respeta.

```csharp
new SheetRow { state = "Walk", frames = 5, fps = 12f, loop = true, groupSlack = 0.15f },
```

**`propBlobs`** — cuántos dibujos de la fila **no** son poses del personaje, sino el proyectil ya
lanzado. Se apartan por la derecha (la lámina siempre los dibuja después de la última pose) y
**antes** de agrupar. Es lo que `frames` por sí solo no arregla: si el orbe está pegado al hocico y
se solapa en X con el personaje, para cuando la cuenta se reconcilia ya se ha fundido en la celda
del ataque — medido en `Lobo`, la celda pasaba de 192 a 272 px y la última pose salía descentrada.

```csharp
// 6 dibujos: 4 poses del lobo + el orbe escupido, dibujado dos veces.
new SheetRow { state = "Attack", frames = 4, fps = 12f, loop = false, releaseFrame = 3, propBlobs = 2 },
```

Con varios props se exportan como `<Personaje>_<Fila>_Prop0.png`, `…_Prop1.png`, y **el primero
(el más a la derecha, o sea el más "en vuelo") es el que se convierte en proyectil**.

### Qué elegir para una fila que sale mal

| Lo que pasa | Perilla |
|---|---|
| Dos poses fundidas en un frame, celda mucho más ancha, huecos reales pequeños | `groupSlack` bajo (0.15–0.5) |
| Dos poses fundidas porque el FX de una invade a la otra (polvo, estallido) y no hay hueco | `evenSplit = true` |
| El proyectil suelto cuenta como frame, o estira la celda del ataque | `propBlobs` |
| Ninguna heurística acierta | `frameRects` (§12) |

`evenSplit` reparte la banda en columnas iguales, así que **exige rejilla**: si los dibujos no están
igual de espaciados, cada columna se lleva un trozo del vecino (se ve como una astilla de cola
flotando al borde del frame). Cuando hay hueco real entre dibujos, `groupSlack` da un corte exacto
y es preferible.

## 6-quater. Los rótulos ya no llegan al PNG

Las láminas del proyecto traen `IDLE` / `WALK` / `ATTACK` escrito dentro de la propia fila. El
corte los quita en dos pasos, sin que haya que configurar nada:

- Todo lo que **no cae en ninguna banda** se borra de la máscara antes de emitir. `Emit` copia
  píxeles del original dentro de un margen alrededor del frame, así que sin esto la parte baja del
  rótulo de la fila de arriba acababa pintada encima del personaje (media palabra `HURT` sobre el
  caballo).
- Dentro de la banda, se borra la mancha corta **pegada al techo de la franja**. Medido en las
  láminas del proyecto, las letras cuelgan del borde superior y el arte suelto (hojas, chispas,
  polvo) nunca llega tan arriba, porque el techo lo marca el propio personaje. La regla anterior
  —"mancha colgada por encima del personaje más alto"— sólo acertaba cuando el rótulo estaba
  dibujado más alto que la crin, y en las demás filas la palabra salía pintada en el primer frame.

## 6-quinquies. Voladores y estáticos en el aire

El nombre del archivo de la lámina ya dice el arquetipo. La traducción es directa salvo un caso:

| Lámina | `archetype` | Extra |
|---|---|---|
| `…-meele-movimiento-suelo` | `Melee` | — |
| `…-distancia-movimiento-suelo` | `Ranged` | `personalSpace` > 0 para que retroceda |
| `…-meele-movimiento-aire` | `FlyingMelee` | — |
| `…-distancia-movimiento-aire` | `FlyingRanged` | — |
| `…-distancia-statico-suelo` | `Static` + `staticAttack = Ranged` | — |
| `…-distancia-statica-aire` | `Static` + `staticAttack = Ranged` | **`gravityScale = 0`** |

El caso raro es el último: los arquetipos `Flying*` son los que **vuelan persiguiendo**. Una
torreta en el aire no persigue, así que no es `Flying*` — lo único que necesita del vuelo es no
caerse, y eso es `tuning.gravityScale = 0`, que `EnemyStats.Apply` vuelca al `Rigidbody2D`. Se
queda a la altura a la que la dejes en la escena.

**Todo lo que flota lleva `anchor = AnchorMode.Center` en la receta de la lámina**, no
`BottomCenter`: un bicho que vuela no tiene pies sobre los que apoyarse, y con el pivote abajo el
aguijón (o la cola, o lo que cuelgue) haría de suelo. `EnemyFactory` lee ese anchor y **centra el
collider** en consecuencia; dar por hecho `BottomCenter` dejaba el collider medio cuerpo por encima
del dibujo, o sea disparos que atraviesan al bicho y golpes que impactan en el aire.

**Sólo los que se mueven Y disparan huyen** (`EnemyTuning.Retreats`). Un melé persigue y pega, y un
`Static` no se mueve: en esos dos, `personalSpace` se ignora y su círculo ni siquiera se dibuja en
los gizmos, aunque el valor siga guardado de un cambio de arquetipo anterior.

## 6-sexies. Dormidos y kamikazes — `MurcielagoPack`

Dos casillas del `EnemyTuning`, independientes entre sí y del arquetipo:

| Casilla | Qué hace | Qué pide a la lámina |
|---|---|---|
| `sleepsUntilDetected` | Empieza quieto en `Idle`, que pasa a ser el clip de **dormir**. Al entrar el objetivo en `detectionRange` — o al recibir un golpe — reproduce `Wake` quieto (no se interrumpe) y luego persigue. Si se rinde, **vuelve a su posición inicial** y se duerme; nunca se queda en `Idle` a mitad de camino. Sólo arquetipos que se mueven. | Una fila `Wake`, `loop = false`. |
| `selfDestruct` | El ataque es explotar: al llegar a `attackRange` reproduce `Attack` (la mecha) y en su `releaseFrame` hace `attackDamage` en un círculo de `explosionRadius` y muere (`Health.Die`). Si lo matan antes, no hace daño. `EnemyStats` apaga el tinte del `Corpse`. Un `Static` así es una mina. | `Attack` = la mecha (corta, `releaseFrame` = último frame); `Death` = la explosión. |

Con `rootedWhileAttacking = false` el enemigo **sigue persiguiendo** durante el clip de ataque: un
kamikaze no debe poder esquivarse con un paso atrás mientras arde la mecha.

El murciélago además enseña dos perillas del corte sobre la misma lámina: la fila del picado
dibuja un estallido de velocidad en medio de las poses, que se salta con `frameRects` (los
recuadros se leen del Sprite Editor), y los dos anillos de la explosión se tocan, así que esa fila
va con `evenSplit`. La fila de vuelo normal (`Fly`) no la usa nadie: se declara porque el corte
automático necesita contar las 6 franjas.

## 7. Cuando algo sale mal

| Síntoma | Causa y arreglo |
|---|---|
| «se han detectado N filas» y N ≠ las declaradas | Las franjas de contenido no se separan. Pasa a `sliceMode = Grid` con `columns`. |
| Sale un frame de más | Un objeto suelto dentro del frame (un proyectil lanzado). Declara `frames` en la fila: el corte retira el grupo más pequeño hasta cuadrar y lo exporta como prop. Si además el objeto se **solapa** con el personaje y la celda del ataque sale mucho más ancha, usa `propBlobs` (§6-ter). |
| Sale un frame de menos, con celda mucho más ancha | Dos frames se pisan en horizontal. Si hay hueco real entre los dibujos (aunque sea de 3 px), baja `groupSlack` — corte exacto. Si de verdad se pisan (polvo de un pisotón, un estallido), `evenSplit = true`: parte la franja en `frames` columnas iguales. Lo usa `Gorila` en su fila de ataque; `Caballo` y `Lobo` usan `groupSlack`. |
| Fondo pegado a los frames | `Key Background` apagado en una lámina sin alfa, o `Background Tolerance` muy baja. |
| Se come parte del arte | `Background Tolerance` muy alta, o el arte tiene el mismo tono que el fondo. |
| El personaje da saltos al animar | El ancla no es estable. Con `AnchorMode.BottomCenter` se ancla a los pies; si el arte no apoya siempre igual, prueba `Center`. |
| El enemigo flota o se hunde | Escala del sprite (`spriteScale`) frente al `colliderSize`. Deja `colliderSize` en `(0,0)` para que se deduzca. |
| El prefab sale sin arte | Se generó antes de cortar la lámina. Relanza el pack. |
| Un clip sale vacío (1 s, 60 fps, sin eventos) tras generar | Es el bug de la primera importación: los sub-sprites recién cortados no estaban listos en el tick en que se construyó el clip. `SpritePipeline.RunSheet` ahora construye los clips **dos veces** con un `Refresh` en medio para curarlo solo; si aun así aparece, relanza el pack una vez más. |
| El enemigo se queda en Idle y nunca ataca/persigue | Casi siempre no hay ningún objeto con la etiqueta `Player` en la escena que se está probando — `MainHub` no tiene jugador propio hasta que `RunManager.StartRun` lo instancia; probar el prefab ahí sin pasar por el flujo normal (menú → hub → run) deja a `EnemyController`/`EnemyAnimator` sin nada que perseguir. No es un fallo del pipeline. |
| Un enemigo colocado en una escena se quedó en Idle aunque hay jugador y el prefab funciona en uno nuevo | La instancia de la escena es de una versión **anterior** del prefab (de antes de corregir algo en el generador) y algo en ella quedó desincronizado — visto una vez con `EnemyAnimator.animator` en null pese a que una instancia nueva del mismo prefab lo resuelve bien en `Awake`. El arreglo es borrar esa instancia y volver a arrastrar el prefab actual, no perseguir la causa exacta. |
| `ranged` activo pero no aparece ningún proyectil | El log de generación dice si encontró el prop suelto. Si no lo encontró: la fila no tiene ningún objeto separado del personaje (revisa la lámina), o se generó el prefab **antes** de cortar/re-cortar la hoja — relanza `SpritePipeline.RunEnemy`, que corta primero. |

Comprobar que un cambio compila, sin abrir el editor a mano:

```bash
unity command recompile
unity command recompile_status   # errors: [] = limpio
```

---

## 8. Ejemplo trabajado: TreeWalk

`Assets/Sprites/TreeWalk.jpg`, 1264×1264, 4 filas (IDLE / ATTACK / HURT / DEATH) × 5 frames, con
los rótulos escritos en la imagen y **sin canal alfa**.

Lo que hizo el pipeline sin intervención:

1. Dedujo el fondo (blanco ~249) del marco y lo recortó.
2. Encontró 8 franjas de contenido — 4 de personajes y 4 de rótulos — y se quedó con las 4 más
   altas, que son las de los personajes.
3. Segmentó cada banda en manchas conexas y las agrupó por solapamiento horizontal, de modo que las
   hojas que salen despedidas en HURT viajan con su frame en vez de contar como frames.
4. En ATTACK detectó 6 grupos: la piedra que el bicho lanza en el cuarto frame va suelta y
   separada de la mano. Como la receta declara 5, absorbió el grupo más pequeño en su vecino más
   cercano — y además lo exportó aparte, centrado, como `TreeWalk_Attack_Prop.png`.
5. Empaquetó cada estado en celdas uniformes ancladas a los pies, con el pivote en el ancla.
6. Generó 4 clips, el controller con sus transiciones, y `Enemy_TreeWalk.prefab` con los nueve
   componentes compartidos, collider `0.81 × 1.52` deducido del arte y sprite a escala `0.7`.
7. Con `recipe.ranged = true`, construyó `Fx_TreeWalk_Projectile.prefab` a partir de esa piedra y
   añadió un `RangedAttack` en `AutoDetect`: TreeWalk ahora tira la piedra de verdad cada 1.8s
   cuando el jugador entra en su rango, con la animación de "Attack" cuadrada al soltarla.

Coste de la siguiente lámina: copiar `TreeWalkPack.cs`, cambiar los datos, ejecutar.

---

## 9. Rótulos pintados en la lámina — `cropLeft` y compañía

Muchas láminas traen los nombres de las filas (`IDLE`, `WALK`, `SALTAR`…) escritos en una columna
a la izquierda, o una paleta de colores en un margen. Eso **es contenido** para el corte: se lleva
un frame por delante y, peor, su color entra en la lista de tonos que se toman por fondo.

La receta lo resuelve con cuatro enteros: `cropLeft` / `cropRight` / `cropTop` / `cropBottom`,
en píxeles y **como se ve la imagen** (arriba es arriba). Lo que quede fuera se ignora antes de
deducir el fondo, no después.

Mídelo, no lo estimes: `PlayerPack.Diagnose` (**Tools ▸ RedMagic ▸ Pipeline ▸ Packs ▸ Player ·
Diagnosticar lámina**) imprime el porcentaje de píxeles oscuros por columna y dice dónde acaba el
rótulo. Copia ese patrón en el pack del personaje nuevo si su lámina trae rótulos.

En modo `Grid` la rejilla se reparte **sobre el recorte**, no sobre la lámina entera, así que un
recorte a la izquierda ya no desplaza todas las celdas.

## 10. Estados que la lámina no dibuja — `derivedClips`

Un controller cableado casi siempre tiene más estados que filas la lámina: hay salto pero no
caída, hay ataque pero no ataque agachado. Si se dejan, esos estados **siguen con el arte
anterior** y el personaje cambia de aspecto a mitad de partida.

`SpriteSheetRecipe.derivedClips` los monta con un trozo de otra fila:

```csharp
new DerivedClip { state = "Fall", fromState = "Jump", firstFrame = 3, frameCount = 1, loop = true }
```

`frameCount = 0` significa «hasta el final de la fila». Se generan como un `.anim` normal en
`Anim/`, así que `BuildController` los coloca en su estado igual que a los demás.

**`reverse = true`** recorre ese tramo al revés (del último frame al primero). Es para un cierre
que reutiliza los mismos dibujos de una apertura pero en sentido contrario, sin que la lámina
tenga que dibujar la secuencia dos veces — aunque en la práctica, si la fila ya dibuja el ciclo
completo abrir+cerrar (medido en `Maibhubitems.png`: cerrado → grieta → brillo pico → brillo
atenuado → grieta → cerrado, los 6-7 frames de un tirón), el cierre sale de los últimos frames de
la propia fila **en el mismo orden**, sin `reverse` — mide con `2b` antes de asumir que hace falta.

## 11. Cambiar el arte del jugador

El jugador **no** pasa por `EnemyFactory`: su prefab lleva el control, el ataque, el inventario y
el audio, y eso no se toca. Su pack (`Assets/Scripts/Pipeline/Editor/PlayerPack.cs`,
**Tools ▸ RedMagic ▸ Pipeline ▸ Packs ▸ Player**) hace sólo tres cosas:

1. Corta la lámina en `Assets/Art/Characters/Player/`.
2. **Copia** el controller una vez (`DragonWarrior.controller` → `Player.controller`) y deja que
   `AnimClipBuilder` le reescriba los clips *en su sitio*. Las transiciones afinadas a mano y los
   parámetros que escribe `PlayerAnimator` (`Speed`, `VSpeed`, `Grounded`, `Crouching`, `Attack`,
   `Hurt`, `Dead`) quedan intactos: por eso cambiar el arte no cambia cómo se juega.
3. Apunta `Player.prefab` al controller nuevo y escala el hijo `Sprite` para que el personaje mida
   lo mismo que antes — la escala sale del `bounds` del sprite real, no de un número a ojo.

Los estados de la fila se llaman **igual que los del controller** (`Idle`, `Walk`, `Jump`,
`Attack`, `Hurt`, `Die`…). Eso es lo que hace que reescribir los clips baste.

`attackEvents = false` en la receta: `OnAttackRelease` / `OnAttackFinished` los escucha
`EnemyAnimation`, que el jugador no lleva. Sin apagarlo, Unity avisa en cada ataque.

Para una lámina nueva del jugador: sustituye el JPEG, borra `Player.sheet.asset` si cambian las
filas, y vuelve a lanzar el pack.

## 12. Cuando ninguna heurística acierta una fila — `frameRects`

`SheetRow.frameRects` es la salida de emergencia: recuadros de frame puestos a mano, en píxeles de
la lámina (`y = 0` abajo, igual que en el Sprite Editor de Unity). Si hay alguno, mandan sobre la
detección automática y sobre `evenSplit`.

Lo demás del corte **no** cambia: se sigue quitando el fondo, empaquetando en celdas uniformes y
poniendo el pivote en el mismo punto del personaje en todos los frames. Lo que se pone a mano es
sólo *dónde empieza y acaba cada frame*.

Cómo se sacan los números sin contar píxeles: abre la lámina en el Sprite Editor, corta esa fila a
mano, y lee el `rect` de cada sprite resultante. `PlayerPack.AdoptHandCutAttack` hace justo eso con
los cinco `attack1..5.asset` de `Assets/Sprites/New folder/` y los **copia** a la receta — una vez
copiados la receta es autosuficiente y los sprites sueltos se pueden borrar.

Fue necesario en la fila del ataque del jugador: el aura de energía crece tanto que invade los
frames vecinos, y el reparto uniforme cortaba la última pose por la mitad.

## 13. Arte pintado sobre fondo de color — `softEdge` y `fillHoles`

Las láminas pintadas sobre un fondo liso de color (`TreeBoss.png`, `BossAttack.png`: verde
oscuro, sin alfa) rompen el recorte duro de dos maneras. Cada una tiene su perilla en la receta,
las dos sólo con `keyBackground`:

| Lo que pasa | Perilla | Valor típico |
|---|---|---|
| El halo de un brillo sale cortado a tijera, con un cerco del color del fondo | `softEdge` = ancho (px) de la franja del contorno donde la **luz añadida** sobre el fondo pasa a ser alfa, y se le quita el fondo mezclado del color. Lo **más oscuro** que el fondo (tinta, sombra) se queda opaco, así que la silueta no pierde el perfil. Sobre un fondo claro no cambia nada. | 10 (cuerpo), 16 (FX) |
| El personaje sale **lleno de agujeritos**: las sombras del interior tienen el tono del fondo y el recorte las perfora (el borde suave además los agranda) | `fillHoles` = área máxima (px) de un hueco de «fondo» totalmente rodeado de personaje que se rellena. Los huecos grandes de verdad (entre brazo y cuerpo) se respetan. | 200 (cuerpo), 80 (FX) |

`0` en las dos = el comportamiento de siempre, así que las láminas anteriores no cambian.

Una lámina con filas que necesitan **anclas distintas** (orbes centrados, cosas que salen del
suelo con el pivote abajo) se corta con **dos recetas sobre la misma imagen**, cada una con
`cropTop` / `cropBottom` para quedarse con sus filas. Es lo que hace `TreeBossPack` con
`BossAttack.png` (`TreeBossOrb` arriba, `TreeBossRoot` abajo; la línea se mide con
`TreeBoss · Diagnosticar láminas`).

## 14. Jefe animado — gestos con `BossAnimator` (`TreeBossPack`)

Un jefe con lámina propia pasa por **el mismo pipeline que un enemigo** para el cuerpo
(`SpritePipeline.RunSheet`, `runtime = Animator`), pero no por `EnemyFactory`: su prefab lo monta
su pack (`Assets/Scripts/Bosses/Editor/<Jefe>Pack.cs`), igual que los demás jefes.

- **Una fila por gesto**, con el nombre que usarán los ataques (`Charge`, `Slam`, `Summon`…) y
  `releaseFrame` = el dibujo en el que sale el golpe. `AnimClipBuilder` planta
  `OnAttackRelease` / `OnAttackFinished` en **cualquier fila con `releaseFrame >= 0`**, no sólo en
  `Attack`. Los gestos quedan como estados sueltos del controller; `Idle`, `Hurt` y `Death` se
  cablean solos.
- En el prefab: `Animator` **en la raíz** + `BossAnimator` (recibe los eventos) + `BossController`.
- En cada `BossAttack`, el campo **`gesture`** = nombre del estado. `BossAnimator` acelera o frena el
  clip para que el frame de suelta caiga justo al acabar `telegraph`, y el ataque arranca en ese
  evento. `telegraph` sigue siendo el único mando de tiempo; la fase 2 lo acorta (`speedScale`) y
  con él acelera el gesto.
- `Hurt` es el tambaleo: se reproduce al cambiar de fase y, con `flinchOnHit`, al recibir golpes
  en reposo (con enfriamiento). `Death` entra por el bool `Dead`.
- Los FX del jefe (`Assets/Prefab/Fx/Bosses/<Jefe>/`) son `SpriteFlipbook` + `VfxOneShot`
  (runtime `Flipbook`: van por pool). Los que salen del suelo con un área de daño se escalan al
  radio del golpe (`GroundSlamAttack.fitFxToRadius` → `VfxOneShot.SpawnFitWidth`).

Para un gesto nuevo: fila nueva en la lámina y en la receta, relanzar el pack, poner su nombre en
el `gesture` del ataque. Para un ataque nuevo: otro asset en la baraja de la fase.
