using UnityEngine;

namespace RedMagic.Hub
{
    /// <summary>
    /// El armario del hub: mismo flujo en dos toques que <see cref="ChestLootContainer"/> (abrir →
    /// esperar → recoger → cerrar) y el mismo reseteo al volver de una run, pero **no** bloquea la
    /// puerta del hub — es botín opcional, no el arma inicial.
    ///
    /// La recompensa real (3 items) todavía no existe: <see cref="OnLoot"/> es un hueco a propósito
    /// para esa lógica futura. No se ha dejado vacío del todo — ya reproduce la animación de abrir
    /// y cerrar y avisa por consola qué tocaría dar — para que enchufar la recompensa de verdad sea
    /// sólo escribir el cuerpo de este método, sin tocar la mecánica de interacción.
    /// </summary>
    public class WardrobeLootContainer : HubLootContainer
    {
        protected override string OpenPromptKey => "prompt.wardrobe_open";
        protected override string PickupPromptKey => "prompt.wardrobe_pickup";
        protected override bool BlocksHubExitUntilLooted => false; // opcional: nunca bloquea la salida

        protected override void OnLoot()
        {
            // TODO: implementar la lógica real. Debe conceder 3 items (de qué pool, si pueden
            // repetirse, si hay algún filtro por rareza — sin decidir todavía) a través del mismo
            // camino que usa la tienda (RedMagic.Items.ItemLibrary + WeaponLoadout.Instance.Inventory
            // .TryEquip), y enseñar RewardPopupUi.Show(...) una vez por item, como hace el cofre.
            Debug.Log("[WardrobeLootContainer] TODO: implementar la recompensa real (3 items). " +
                      "Por ahora el armario sólo se abre y se cierra.", this);
        }
    }
}
