# Items — crear, dar icono y comportamiento

Todo lo de un item vive **en su asset** (`Assets/Resources/Items/*.asset`): nombre, descripción,
icono, color, tags de sinergia y **efectos con sus valores**. Nada se engancha en otro sitio: la
pantalla de items (tecla I), la tienda y los ciclos de prueba los encuentran solos.

---

## 1. Crear un item

1. Duplica un `Item_*.asset` de `Assets/Resources/Items/` (o *Create ▸ RedMagic ▸ Items ▸ Free Pool Item*).
2. En su Inspector: **Ficha** (nombre, descripción, icono, acento), **Tags de sinergia**
   (pool libre: 1 elemental + 1 universal; avisa en consola si no), **Comportamiento**.
3. Listo: aparece al ciclar un slot libre en la pantalla de items (clic en el slot) y en la tienda.

## 2. Comportamiento — lista `effects`

- `+` añade una entrada; el **desplegable** de la derecha elige el tipo de efecto y debajo salen
  sus valores. Varios efectos se combinan. `(ninguno)` vacía la entrada.
- Efectos disponibles (`Assets/Scripts/Items/Effects/`):

| Desplegable | Clase | Valores |
|---|---|---|
| Jugador · Multiplicar estadística | `PlayerStatMultiplierEffect` | `stat` (Velocidad / Distancia de dash / Altura de salto), `multiplier` |
| Jugador · Perder vida por segundo | `DrainHealthEffect` | `amount`, `interval`, `canKill` |
| Economía · Dar moneda por segundo | `GrantCurrencyEffect` | `currency`, `amount`, `interval` |

- Se aplican **sólo mientras el item está equipado** (`ItemEffectRunner`, dentro de
  `WeaponLoadout`): al quitarlo, se deshacen. Vaciar el inventario al acabar la run los quita todos.
- Los multiplicadores de estadística van a `Gameplay.PlayerStats` (una tabla, clave = cada
  equipado); `PlayerMovement` lee el producto. Varios items sobre lo mismo se multiplican. Se
  re-aplican cada frame: retocar el valor en Play se nota al momento.

### Un tipo de efecto nuevo (código)

```csharp
[Serializable, DisplayName("Grupo · Qué hace")]
public sealed class MiEfecto : ItemEffect
{
    public float valor = 1f;                       // se edita en el Inspector del item
    public override void OnEquip(ItemEffectContext c) { /* aplicar, con c como clave */ }
    public override void OnUnequip(ItemEffectContext c) { /* deshacer */ }
    public override void Tick(ItemEffectContext c, float dt) { /* c.Every(1f, dt, ...) */ }
    public override string Summary() => $"...";    // línea en la descripción de la UI
}
```

- El estado por equipado (temporizadores) va en el `ItemEffectContext`, **nunca** en campos del
  efecto: la instancia es dato del asset y el mismo item puede estar en dos slots.
- Aparece solo en el desplegable (`[SubclassPicker]` + `TypeCache`).

## 3. Iconos

- Imágenes tal como llegan (fondo blanco) en `Assets/Icon/<Nombre>.jpeg`.
- **Tools ▸ RedMagic ▸ Items ▸ Iconos · Procesar e instalar** (`ItemIconsPack`): quita el fondo con
  el mismo procesador que el arte de UI (`UiArtKitProcessor`, ver `UI_ART_PIPELINE.md`), deja
  `Assets/Art/UI/ItemIcons/Icon_<Nombre>.png` y lo asigna a los items del pack **que no tengan icono**.
- Un icono nuevo: añade una pieza en `Assets/Art/UI/ItemIcons/ItemIcons.uikit.asset` (Inspector:
  nombre + imagen fuente, `softEdge` 48 si tiene brillo), ejecuta el menú y asígnalo en el item.
- **Tools ▸ RedMagic ▸ Items ▸ Catálogo de items (iconos)**: todos los items y armas con su icono
  editable (cambia el asset del item, no una copia) y botón *Editar* para abrir el item.
- La pantalla de items pinta el icono dentro del slot hueco; la tienda, en la carta. Sin icono se
  ve la inicial del nombre.

## 4. Items de prueba (hielo)

Creados por `ItemIconsPack` (sólo si faltan), para comprobar el sistema — exagerados a propósito:

| Item | Efecto |
|---|---|
| Botas Rúnicas | velocidad ×3 |
| Capa de Escarcha | dash ×3 de largo |
| Yelmo Rúnico | salto ×3 de alto |
| Bastón Helado | −1 de vida por segundo (no mata) |
| Anillo Glacial | +1 diamante por segundo |

Prueba: Play → I → clic en un slot libre hasta el item → cierra y juega. Clic otra vez (o vacíalo)
y el efecto se va.
