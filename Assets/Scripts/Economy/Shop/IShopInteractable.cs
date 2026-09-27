using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Algo de la tienda con lo que el jugador interactúa igual que con un altar: misma cercanía,
    /// mismo cartel, misma tecla y el mismo "no llega" (<see cref="Deny"/>). Lo recorre
    /// <see cref="ShopManager"/>; lo implementan <see cref="ShopAltar"/> y <see cref="ShopRerollAltar"/>.
    /// </summary>
    public interface IShopInteractable
    {
        /// <summary>Punto con el que se mide la distancia al jugador.</summary>
        Vector3 ItemPosition { get; }

        /// <summary>Se puede enfocar ahora mismo (tiene algo y no está animándose).</summary>
        bool CanInteract { get; }

        void SetFocused(bool focused);

        /// <summary>Respuesta visual a una interacción rechazada (sin oro / sin rerolls).</summary>
        void Deny();
    }
}
