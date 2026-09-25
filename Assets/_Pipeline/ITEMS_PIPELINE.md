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
| Enfriamiento · Más rápido con ralentizados en pantalla | `SlowedEnemyCooldownEffect` | `cooldownRate` |
| Al matar · Siguiente disparo instantáneo y potenciado | `KillEmpowersNextCastEffect` | `damageMultiplier`, `resetCooldown`, `hudAccent` |
| Proyectil · Sigue tras matar | `PierceOnKillEffect` | `respawnDelay` |
| Al matar N · Explosión de hielo en el siguiente disparo | `KillCounterExplosionEffect` | `killsRequired`, `radius`, `damage`, `slowsTargets`, `explosionPrefab` |
| Golpe · % de vida máxima a ralentizados | `SlowedBonusDamageEffect` | `percentOfMaxHealth`, `maxBonusPerHit` |
| Ralentizar · Rompe armadura mientras dure | `SlowArmorShredEffect` | `armorReduction` |

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

### Ganchos de combate para efectos (código)

| Necesito… | Uso |
|---|---|
| "al golpear" / "al matar" | `PlayerHit.Landed` / `PlayerHit.Killed` (suscribir en `OnEquip`, quitar en `OnUnequip`; delegados en `context.GetState<T>()`) |
| "el siguiente disparo…" | `WeaponUser.Casting` (`CastArgs.DamageScale`, `Origin`, `Facing`) |
| "donde impacte ese disparo" | en `Casting`: `args.OnFirstImpact(point => …)` — salta una vez con el primer impacto (enemigo → centro del cuerpo; terreno / fin de vida / punta del haz si no toca a nadie) |
| tocar el cooldown | `WeaponUser.Current.ResetCooldown()` / `ReduceCooldown(s)`; ritmo: `PlayerStats` `PlayerStat.CooldownRate` |
| daño extra por golpe / expuesto al ralentizar / atravesar al matar | `CombatModifiers.SetHitBonus` / `SetSlowVulnerability` / `SetPierceOnKill` (clave = context) |
| chapa en el HUD | `BuffIndicators.Set(context, icon, text, highlight, accent)` → `UI.ItemBuffHud` la pinta. **Sólo si el jugador tiene algo que decidir** (carga que se acumula, "listo"); un efecto automático no lleva chapa |
| área de hielo | `IceBurst.Detonate(center, radius, damage, prefab, applySlow)` |

- **Todo daño del jugador pasa por `PlayerHit.Deal`** (proyectil, haz, espada, habilidades antiguas).
  Un arma nueva que haga daño debe llamarlo, no `Health.TakeDamage`, o sus golpes no cuentan para
  items ni sinergias.
- Rareza: campo `rarity` del item (Común / Azul / Épico / Legendario), sale coloreada en la pantalla I.

## 4. Ralentización y sinergias implementadas

- `Combat.SlowStatus` — se añade solo al primer ralentizar. Velocidad ×(1−fuerza) (`EnemyBrain`,
  `EnemyController`), tinte azul por la capa de estado de `HitFlash` (más fuerte = más saturado; se
  desvanece al acabar), daño extra por `Health.StatusDamageMultiplier` (aparte de la armadura del jefe).
- Números en **`Assets/Resources/SynergyConfig.asset` ▸ Tuning** (`SynergyTuning`):

| Umbral | Efecto | Dónde |
|---|---|---|
| Hielo 2 | proyectil/haz ralentiza (`slowStrength` 0.4, `slowDuration` 3 s) | `PlayerHit.Deal` |
| Hielo 4 | ralentizados reciben +15% de todo | `PlayerHit.ApplySlow` |
| Hielo 6 | matar ralentizado → estallido pequeño (`Fx_IceExplosion` a radio 1.8) | `SynergyEffectRunner` |
| Rapidez 2 | enfriamiento ×1.5 con ralentizado en pantalla | `SynergyEffectRunner` |
| Reset 2 | −2 s al cooldown por baja | `SynergyEffectRunner` |
| Vampirismo 2 | +3 vida por baja, **repartida entre las motas rojas y aplicada cuando llegan al jugador** (no al matar); rojas en cada baja, verdes sobre el jugador sólo si recupera vida (`Fx.LifeMotes` + `DrainPacket`, pooled) | `SynergyEffectRunner` |

## 5. Set de hielo — `Tools ▸ RedMagic ▸ Items ▸ Set de hielo · Generar` (`IceSetPack`)

| Item | Rareza | Tags | Efecto |
|---|---|---|---|
| Botas Rúnicas | Épico | Hielo + Rapidez | enfriamiento ×1.5 con ralentizado en pantalla |
| Bastón Helado | Legendario | Hielo + Rapidez | al matar: cooldown a 0 y siguiente disparo ×3 (sin chapa: es automático) |
| Capa de Escarcha | Épico | Hielo + Reset | proyectil que mata reaparece en el cuerpo 0.5 s después |
| Yelmo Rúnico | Legendario | Hielo + Vampirismo | 5 bajas → el siguiente disparo explota en hielo donde impacte (chapa 0/5 … ¡LISTO!) |
| Anillo Glacial | Azul | Hielo + Vampirismo | +8% vida máx. del ralentizado por golpe |
| Grimorio Glacial | Azul | Hielo + Reset | ralentizado recibe +30% (−30% armadura) |

- Corta `Assets/Art/VX/IceExplotion.png` → `Assets/Art/VX/IceExplosion/` y monta
  `Assets/Prefabs/Fx/Items/Fx_IceExplosion.prefab` (pooled, flipbook).
- Convierte cada item **sólo si aún no lleva su efecto**; después respeta lo afinado a mano. Nunca toca iconos.
- Icono del Grimorio: `Assets/Art/Icons/Grimorio.png`, constante `GrimoireIconPath` del pack.
- Reescribe el texto de los umbrales implementados con los números actuales del Tuning: tras
  retocar números, relanzar el pack para que la UI diga lo mismo.
