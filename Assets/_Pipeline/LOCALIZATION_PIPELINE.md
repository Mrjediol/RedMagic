# Localización y ajustes de pantalla

Cómo funcionan el idioma (es / en / …) y el modo de pantalla (PC 16:9 / Móvil 20:9), y los pasos
exactos para añadir texto, un idioma o un modo. Código: `Assets/Scripts/Localization/`,
`Assets/Scripts/Settings/`. Todo lo configurable está en `Assets/Resources/GameSettings.asset`.

## 1. Idioma

### Ficheros

Un fichero por idioma en `Assets/Resources/Localization/<código>.txt` (`es.txt`, `en.txt`). El
código es el nombre del fichero. Formato:

```
# comentario
@name = English              ← cómo sale en el selector de Opciones
@system = English            ← SystemLanguage que lo elige en el primer arranque (opcional)
@culture = en-US             ← formato de números de los huecos {0:0.#} (1.5 / 1,5)
shop.buy_hint = Press [Interact] to buy · {0} gold
```

- `{0}`, `{1:0.##}`… son huecos de `string.Format` (mismo número y orden en todos los idiomas).
- `\n` es salto de línea. Lo que va tras el primer `=` es el valor.
- Idioma por defecto = `GameSettings ▸ defaultLanguage` (`es`). Es también la **reserva**: si a
  `en` le falta una clave se enseña la de `es`; si falta en todos, la propia clave (y un aviso).
- Elección del jugador → `PlayerPrefs["settings.language"]`. Sin nada guardado: idioma del
  dispositivo si hay fichero con ese `@system` (`useDeviceLanguageOnFirstLaunch`), si no, el de
  por defecto.

### Mostrar un texto

| Dónde | Cómo |
|---|---|
| Código | `Loc.Get("clave")`, `Loc.Get("clave", arg0, arg1)` |
| UXML | `text="#clave"`, `label="#clave"` (Toggle), `tooltip="#clave"` + `LocalizedUi.BindTree(root)` en el `OnEnable` del controlador, **antes** de `DressWithSkin()` |
| Menú hecho en código, rótulo fijo | `LocalizedUi.Bind(label, "clave")` |
| Menú hecho en código, rótulo compuesto | `LocalizedUi.Bind(label, () => label.text = …)` |
| uGUI `Text` / `TMP_Text` en escena/prefab | componente `LocalizedText` con su clave |
| Texto que se rellena al abrir un menú | basta con `Loc.Get` al rellenar (el menú está cerrado mientras se cambia el idioma) |
| Cartel de interacción | `InteractionPromptUi.ShowKey(this, "prompt.x")` |

`Loc.Changed` salta al cambiar de idioma; `LocalizedText`, `LocalizedUi` y los paneles que ya lo
escuchan (tienda, cartel, barra de jefe, opciones) se repintan al momento, sin recargar escena.

### Textos de assets (items, armas, pasivas, jefes, mejoras, sinergias)

El asset guarda su texto original y un **`textKey`** (prefijo). Las propiedades `DisplayName`,
`Description`, `Title`… devuelven `<textKey>.name` / `.description` / `.title` / `.upgrade` del
idioma activo, o el texto del asset si no hay traducción. Nodos de `UpgradeTree`:
`upgrade.<id>.title/.description`. Tramos de `SynergyConfig`: `synergy.<tag>.tier<n>`.

**Añadir un item / arma / pasiva / jefe nuevo:**
1. Crear el asset con su texto (en español) como siempre.
2. `Tools ▸ RedMagic ▸ Localización ▸ Sincronizar textos de assets` — le pone `textKey` (del nombre
   visible, p. ej. `item.anillo_glacial`) y añade sus claves a `es.txt`. Nunca sobrescribe.
3. Traducir las claves nuevas en `en.txt` (la auditoría que corre al final las lista).

**Regla del proyecto: ningún texto que vea el jugador va escrito a mano en el código.** Todo texto
nuevo — nombres, descripciones, tooltips, rótulos, carteles — lleva clave y entrada en **todos** los
ficheros de idioma (hoy `es` y `en`), escribiendo la traducción si sólo se tiene un idioma.

### Añadir un texto de UI nuevo

1. Escribir `Loc.Get("zona.algo")` / `#zona.algo` en el código o UXML.
2. Añadir `zona.algo = …` a **todos** los `.txt`.
3. `Tools ▸ RedMagic ▸ Localización ▸ Auditar claves` → 0 ausencias.

### Añadir un idioma

1. Copiar `en.txt` a `<código>.txt`, cambiar `@name` / `@system`, traducir.
2. Nada más: el selector de Opciones lo lista solo. Auditar claves para ver huecos.

### Auditoría

`Auditar claves` recoge las claves que usa el juego (líneas de código con `Loc.`, `LocalizedUi.Bind`,
`ShowKey`, `PromptKey`…, UXML `#clave`, assets, `GameSettings`) y lista, por idioma, las que faltan.
Una clave compuesta en tiempo de ejecución (`$"tag.{x}"`) no se detecta: usar literales (un
`switch`) — así lo hacen `BuildTags`, `ItemRarities`, `Currencies`.

## 2. Modo de pantalla

`GameSettings ▸ resolutionPresets` (label = clave de idioma, width, height, aspect a enseñar).
Añadir un modo = otra fila + su clave. `defaultPresetDesktop` / `defaultPresetMobile` sin nada
guardado. Elección → `PlayerPrefs["settings.resolutionPreset"]`, aplicada en `BeforeSceneLoad`.

Aplicar (`DisplaySettings`):
- **PC ventana**: `Screen.SetResolution` al tamaño del modo (encogido para caber en el monitor).
- **PC pantalla completa**: resolución nativa + bandas (forzar otra proporción la estiraría).
- **Móvil**: superficie nativa (otra proporción la deformaría) + bandas si hace falta.
- **Editor**: la ventana Game manda; bandas para ver el encuadre del modo.

Las bandas (`DisplayLetterbox`, persistente) recortan `Camera.rect` de todas las cámaras al
rectángulo del modo (una cámara de fondo limpia las bandas con `letterboxColor`) y, con
`letterboxUi`, meten los paneles de UI Toolkit en ese rectángulo (relleno en el `visualTree` del
panel). `CameraFollow` lee `camera.aspect` cada frame, así que su encuadre y sus límites de BG
siguen al modo sin tocarlo. Orientación: sólo horizontal (PlayerSettings + `Screen.autorotate*`).
Tras añadir fondos, `Tools ▸ RedMagic ▸ Camera ▸ Auditar encuadre` comprueba 16:9 y 20:9.
