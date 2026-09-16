# UI art pipeline — de un kit de imágenes a una pantalla con arte

Referencia para vestir una pantalla de UI (UI Toolkit construida en código) con un kit de arte
nuevo. **No hace falta leer el código**: estos pasos bastan. Primer caso: la pantalla de items.

---

## 0. Resumen en 30 segundos

```
Assets/Ui/<Kit>/*.jpeg|png            imágenes tal como llegan (fondo liso), no se tocan
   │   <Kit>.uikit.asset  (UiArtKitRecipe)   ← qué imagen es qué, rejillas, modo de estirado
   ▼   UiArtKitProcessor
Assets/Art/UI/<Kit>/<prefix><Pieza>.png     sin fondo, recortadas, un PNG por estado
   ▼   <Kit>Pack.cs  (cablea referencias tipadas)
Assets/Resources/<Pantalla>Skin.asset       ← lo que lee el controlador de la pantalla
```

Menú: **Tools ▸ RedMagic ▸ UI ▸ Items · Procesar kit de arte**
CLI: `unity command run_script --file Assets/Scripts/Pipeline/Editor/ItemsUiPack.cs --entry RedMagic.Pipeline.EditorTools.ItemsUiPack.Run`

## 1. Qué hace el procesador (`UiArtKitProcessor`)

1. **Quita el fondo liso** con el mismo criterio que `SheetSlicer`: el color de fondo es el tono
   más repetido del marco de la imagen; es fondo lo que se parece a él (`backgroundTolerance`) y
   está conectado con el borde. `keyEnclosed` quita también un hueco grande encerrado (el interior
   blanco de un marco "con espacio para icono"; `minEnclosedArea` protege brillos pequeños).
2. **Borde suave** (`softEdge` px): junto al fondo, el color se des-mezcla del fondo ("color a
   alfa") — los halos de hover se desvanecen en vez de cortarse. `softOpaqueAt` hace opaco lo que
   ya es claramente arte (sin él, la piedra apagada junto a un halo sale translúcida). Junto a un
   hueco encerrado la franja se limita a 4 px.
3. **Separa estados**: `columns × rows` celdas, nombradas por `cellNames` (por filas, arriba-izq.
   primero). Todas las celdas salen del mismo tamaño y **centradas en su propio contenido**, así
   normal/hover no saltan al intercambiarse. Piezas en archivos distintos pero dibujadas en el
   mismo sitio (normal y hover sueltos) comparten `trimGroup` ≠ 0 → recorte idéntico al pixel.
4. **Marcos estirables** (`slice = Quad`): un 9-slice normal estira el centro de cada lado, que es
   justo donde estos marcos llevan su adorno. Quad parte el marco en 4 cuadrantes (`_TL/_TR/_BL/_BR`),
   cada uno un 9-slice; lo único que se estira es una tira de 2 px que el procesador elige sola en
   la columna/fila **más lisa** de `stretchSearch` (fracción desde cada borde). El log imprime las
   tiras elegidas. Además emite el **panel interior** como 5.º sprite `_Fill`, que en runtime se
   pinta encima de los cuadrantes: sin él, el panel del centro sale de estirar las tiras y se ve
   partido en 4 rectángulos. Su rectángulo es `interior` en la receta, **medido una vez** en px de
   la imagen fuente (x, y, ancho, alto), justo dentro del bisel interior. Para medirlo, dibuja una
   rejilla cada 20 px sobre la fuente y léelo — la detección automática confundía el bisel con la
   talla del marco y se descartó.
5. Importa como Sprite para UI: sin mipmaps, **malla FullRect** (UI Toolkit la necesita para el
   9-slice), bordes escritos en el sprite. GUIDs estables entre regeneraciones.

## 2. En runtime (`Assets/Ui/UiFrame.cs`)

- `UiFrame` = 4 cuadrantes + `maxScale` (px de UI por px de sprite en las partes fijas) +
  `padding`. `frame.Dress(element)` pone el marco detrás del contenido (capa con
  `PickingMode.Ignore`) y el relleno; la escala se reduce sola si el elemento es pequeño.
  `UiFrame.Tint(element, color)` tiñe (resaltados de estado).
- `UiStateSprites` = par normal/hover para piezas que no se estiran (slots, botones).
- Hover: callbacks `PointerEnter/Leave` + `Focus/Blur` (mando/teclado) que cambian el sprite, igual
  que el resto de menús construidos en código — no hay USS.
- **Fallback**: cualquier pieza sin sprite pinta el aspecto plano de `MenuStyle`. Sin skin, la
  pantalla funciona igual.

## 3. Pantalla de items (`ItemsUiPack` → `Resources/ItemMenuSkin.asset`)

| Fuente (`Assets/Ui/ItemsUi/`) | Pieza | Uso |
|---|---|---|
| `ExteriorBorde` | `Window` (quad) | marco de toda la ventana (sin cartel de título); la X se sienta en la esquina |
| `LefftAndRightSection…` | `SidePanel` (quad) | columnas SINERGIAS y DESCRIPCIÓN |
| `MiddleSectionForItems` | `MiddlePanel` (quad) | columna EQUIPO |
| `ForTitles…` | `TitleBar` (quad) | detrás de cada título |
| `Gemini_Generated_Image_…` (marco apaisado sin cristales) | `Row` (quad) | filas de sinergia |
| `Empty Items Slots, with hover` (2×2) | `Slot_Empty` / `_Hover`, `Slot_Filled` / `_Hover` | columna izq. = normal, der. = hover; fila 1 = vacío, fila 2 = equipado |
| `ItemSlotWithScapeForitemIcon` + `ItemSlotHover…` | `IconSlot` / `_Hover` | slot hueco cuando el item/arma tiene icono (se ve por el hueco) |
| `ItenSlot` | `WeaponSlot` | slot del arma sin icono (hover = `IconSlot_Hover`) |
| `CloseButtonWithHover` (2×1) | `Close` / `_Hover` | botón X |

Los **sprites** del skin se reescriben en cada pasada; los **números** (escalas, rellenos, tamaños,
tintes) sólo al crearlo — ajústalos en el Inspector del asset y sobreviven.
**Tools ▸ RedMagic ▸ UI ▸ Items · Procesar kit RESETEANDO números del skin**
(`ItemsUiPack.RunResettingSkin`) es la vuelta explícita a los valores por defecto.

Presupuesto de alto: 4 filas de slots + textos tienen que caber también en un móvil 19.5:9
(≈810 px de alto virtual con la escala 1600×900 / match 0.5). Con `slotFrameSize` 64 caben justas;
si lo subes, comprueba el móvil — si no caben, el marco no se aplasta (`flexShrink 0`), se desborda.

## 3-bis. Menús de pantalla completa (`MenusUiPack` → `Resources/MenuSkin.asset`)

Principal, pausa y opciones. A diferencia de la de items, **esas tres pantallas son UXML+USS**, no
construidas en código — da igual: `UiFrame.Dress` vale para cualquier `VisualElement`. Lo que las
viste es `Assets/Ui/MenuSkinDresser.cs`, compartido por los tres controladores (cada uno llama a su
`DressWithSkin()` en `OnEnable`), y **es idempotente**: marca lo ya vestido con la clase
`rm-skinned`, así que un `OnEnable` repetido no duplica capas ni callbacks.

| Fuente (`Assets/Ui/UiSprites/`) | Pieza | Uso |
|---|---|---|
| `Container` | `Panel` (quad) | cuerpo del menú de opciones |
| `VerticalContainer` | `PanelTall` (quad) | cuerpo del menú de pausa |
| `TitleContainer` | `TitleBar` (quad) | placa detrás de RED MAGIC / PAUSA / OPCIONES |
| `Unpresedbutton` / `HoverButton` / `PressedButton` | `Button` / `_Hover` / `_Pressed` (quad) | los tres estados de un botón de menú |
| `CloseButtonWithHover` (2×1) | `Close` / `_Hover` | botón redondo (libre) |
| `checkBox` (2×2) | `Check_Off` / `_On` / `_Off_Hover` / `_On_Hover` | casillas de silenciar |
| `Sample` | `Window` (quad) | marco cuadrado, libre |

Dos cosas que este kit obliga a hacer bien y que valen para cualquier otro:

- **Los tres estados del botón son el mismo dibujo**, así que comparten `trimGroup` *y*
  `interior`. Si cada uno se midiera solo, el halo del hover encogería su relleno y el botón daría
  un salto al pasar el ratón. Se comprueba en el log: los tres tienen que imprimir los mismos
  márgenes de interior.
- **El relleno de cada marco tiene que ser mayor que el grosor de su piedra** (ver la tabla de
  problemas). Los `interior` de este kit se midieron rellenando el panel liso desde el centro con
  tolerancia de color y quedándose con su caja; la comprobación de que la medida es buena es que
  los márgenes salen **simétricos** (izq≈der, arriba≈abajo).

## 3-ter. Retocar los menús ya vestidos — lo que hay que saber

Las cuatro pantallas vestidas con `MenuSkin` y **dónde se toca cada cosa**:

| Pantalla | Construida en | Se viste en | Piezas que usa |
|---|---|---|---|
| Principal | `MainMenu.uxml` + `.uss` | `MainMenuController.DressWithSkin()` | `titleBar` (placa de RED MAGIC), `button*` |
| Pausa | `PauseMenu.uxml` + `.uss` | `PauseMenuController.DressWithSkin()` | `panelTall`, `titleBar`, `button*` |
| Opciones | `OptionsMenu.uxml` + `.uss` | `OptionsMenuController.DressWithSkin()` | `panel`, `titleBar`, `button*`, `checkOn`/`checkOff` |
| Mejoras permanentes | **código** (`MenuStyle`) | dentro de `UpgradeMenuController.BuildUi/BuildCell/RefreshCell` | `panel`, `titleBar`, `card`, `closeButton` |

**Qué mando mueve qué:**

- **Grosor de la piedra de un marco** → `maxScale` de ese `UiFrame` en el skin. Más bajo = piedra
  más fina y más hueco por dentro. Es el mando a tocar cuando el relleno necesario se come el panel.
- **Aire entre el texto y la piedra** → `padding` de ese `UiFrame`. **Tiene que ser mayor que la
  piedra**: `piedra = fill.rect.x * escala`, con
  `escala = min(maxScale, ancho/2/(bordeIzq+bordeDer+1), alto/2/(bordeSup+bordeInf+1))`.
- **Tamaño de las placas de título** → `titleBarHeight` / `titleBarMaxWidth` (pausa y opciones),
  `mainTitleHeight` / `mainTitleMaxWidth` (principal) y la constante `SkinnedTitleHeight` dentro de
  `UpgradeMenuController` (ahí la letra es de 34 px, la mitad). El **ancho** de la placa se ajusta
  solo al texto; esos números sólo la limitan.
- **Tamaño de botón** → lo sigue mandando el USS (`.menu-button`), no el skin: es maqueta de
  pantalla, no arte. El marco se adapta a lo que mida el botón.
- **Casillas** → `checkBoxSize`. **X de cerrar** → `closeButtonSize`.
- **Estado de una celda de mejora** → `cardLockedTint` / `cardBuyableTint` / `cardMaxedTint`. Con
  arte, el estado va en el **tinte del marco**, no en el color de fondo: el fondo queda por detrás
  de la piedra y no se ve (ver `UpgradeMenuController.PaintCell`).

Los **sprites** del skin se reescriben en cada pasada del pack; los **números** sólo al crearlo. Si
tocas números a mano en el Inspector, sobreviven a un `Procesar kit de arte` normal — y
*Procesar kit RESETEANDO números del skin* es la vuelta explícita a los de `MenusUiPack`. **Si
cambias un número que quieras conservar, cámbialo también en el pack**, o el primer reseteo se lo
lleva.

**Presupuesto de alto (referencia 900 px).** El panel de mejoras es el más justo: placa 96 + 22 de
cabecera + 3 filas × (180 + 14) + pista 33 + relleno del panel 102 ≈ **835**. Si subes el alto de la
celda o de la placa, comprueba que sigue cabiendo: el panel va centrado, así que lo que sobra se
recorta por arriba y por abajo sin avisar.

**Cómo comprobarlo.** No hay forma de renderizar estos paneles desde el editor (ver la tabla de
problemas): hay que entrar en Play y usar `capture_game_view --source screen`. Lo que **sí** se
puede comprobar sin jugar, y conviene hacerlo antes, es que `padding ≥ piedra` para el tamaño real
de cada elemento — con la fórmula de arriba, en un `unity command eval`.

## 4. Vestir otra pantalla con otro kit

1. Deja las imágenes en `Assets/Ui/<Kit>/`.
2. Copia `ItemsUiPack.cs` → `<Kit>Pack.cs`; cambia las rutas y la lista de `UiArtPiece`
   (rejillas y `cellNames` para estados, `slice = Quad` para marcos, `softEdge` 48-64 si hay halo,
   `trimGroup` para estados en archivos distintos).
3. Crea un `<Pantalla>Skin : ScriptableObject` con `UiFrame` / `UiStateSprites` y que el pack lo
   rellene. En el controlador: `frame.Dress(el)` donde antes había fondo+borde, y el par
   normal/hover en los callbacks de hover/foco; deja el camino de `MenuStyle` como fallback.
4. Ejecuta el pack. Si un marco se ve mal estirado, ajusta `stretchSearch` de esa pieza en la
   receta (`<Kit>.uikit.asset`) y vuelve a ejecutar.

## 5. Cuando algo sale mal

| Síntoma | Causa / arreglo |
|---|---|
| PNG del tamaño de la imagen entera, fondo sin quitar | fondo no liso o `backgroundTolerance` baja; mira el color de fondo que imprime el log |
| Piedra translúcida junto a un halo | baja `softOpaqueAt` o `softEdge` de esa pieza |
| Halo cortado a tijera | sube `softEdge` (48-64) |
| Adorno central aplastado/alargado | la pieza no está en `Quad`, o `stretchSearch` incluye el adorno |
| Panel interior partido en rectángulos / con rayas | falta el `_Fill`: mide `interior` de esa pieza en la receta (mira la línea "interior" del log) y vuelve a ejecutar el pack |
| Un segundo marco dibujado dentro del panel | `interior` mide más que el panel liso (incluye talla del marco): redúcelo hasta dentro del bisel |
| Esquinas del relleno interior tapando el bisel | ajusta `fillCornerRadius` de ese marco en el skin |
| Normal y hover no coinciden | mismo archivo → misma rejilla; archivos distintos → mismo `trimGroup` |
| Skin con sprites vacíos tras la primera importación | vuelve a ejecutar el pack (sub-sprites recién cortados) |
| Capturas sin la UI | `screenshot` y `capture_game_view --source camera` no pintan paneles de UI Toolkit. Lo que sí funciona: **`capture_game_view --source screen`, y sólo en Play Mode** (lee el backbuffer ya compuesto). Renderizar un `PanelSettings` clonado a una `RenderTexture` **no vale en modo edición**: el panel fuera de pantalla no se dibuja sin runtime y la textura sale en blanco |
| **El texto se mete dentro del bisel** | el `padding` del marco es menor que el grosor de su piedra. La piedra mide `fill.rect.x * escala` px de pantalla (y lo propio arriba/abajo), donde la escala es la que calcula `UiFrame.Fit` para el tamaño real del elemento: `min(maxScale, w/2/(bordeIzq+bordeDer+1), h/2/(bordeSup+bordeInf+1))`. **Calcúlalo, no lo estimes**, y deja 12-15 px de aire por encima. Si el relleno necesario se come el panel, baja `maxScale` (piedra más fina) en vez de subir el relleno |
| Texto de un `Button` o `Label` tapado por el marco | UI Toolkit dibuja los hijos **por encima** del texto propio del elemento, y el marco es un hijo. El texto tiene que ser otro hijo: los títulos se **envuelven** en una placa y los botones pasan su `text` a un `Label` hijo (lo hace `MenuSkinDresser`) |
