using System;
using System.ComponentModel;
using RedMagic.Economy;
using RedMagic.Localization;
using RedMagic.Run;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Anillo de oro: rerolls extra cada vez que se entra en una tienda. Dos anillos, el doble.</summary>
    [Serializable, DisplayName("Tienda · Rerolls al entrar")]
    public sealed class RerollsOnShopEntryEffect : ItemEffect
    {
        [Tooltip("Rerolls que suma al entrar en cada tienda.")]
        [Min(1)] public int rerolls = 1;

        private sealed class State
        {
            public Action<ShopManager> OnEntered;
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.OnEntered = _ => RunRerolls.Add(rerolls);
            ShopManager.Entered += state.OnEntered;
        }

        public override void OnUnequip(ItemEffectContext context) =>
            ShopManager.Entered -= context.GetState<State>().OnEntered;

        public override string Summary() =>
            Loc.Get(rerolls == 1 ? "effect.rerolls_on_shop_one" : "effect.rerolls_on_shop_many", rerolls);
    }
}
