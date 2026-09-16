using RedMagic.UI;
using UnityEngine;

namespace RedMagic.Hub
{
    /// <summary>
    /// El atril del libro mágico: mismo flujo en dos toques que <see cref="ChestLootContainer"/>/
    /// <see cref="WardrobeLootContainer"/> (abrir → esperar → usar → cerrar) y el mismo reseteo al
    /// volver de una run, pero lo que "recoge" el segundo toque no es un objeto — es abrir
    /// <see cref="UpgradeMenuController"/> (el menú de mejoras permanentes). Sustituye al viejo
    /// caldero placeholder (<c>Legacy.CauldronInteractable</c>), que sólo necesitaba un toque
    /// porque no tenía animación de verdad.
    ///
    /// No bloquea la salida del hub: las mejoras son progreso opcional, no el arma inicial.
    /// </summary>
    public class BookLootContainer : HubLootContainer
    {
        protected override string OpenPromptText => "Pulsa [Interactuar] para abrir el libro";
        protected override string PickupPromptText => "Pulsa [Interactuar] para consultar el libro";
        protected override bool BlocksHubExitUntilLooted => false;

        protected override void OnLoot()
        {
            if (UpgradeMenuController.Instance == null)
            {
                Debug.LogWarning("[BookLootContainer] No hay UpgradeMenuController; ¿falta su " +
                                 "PanelSettings en Resources?", this);
                return;
            }

            UpgradeMenuController.Instance.Open();
        }
    }
}
