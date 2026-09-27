using System;
using System.Collections.Generic;
using System.ComponentModel;
using RedMagic.Economy;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Amuleto de oro: cuanto más oro lleva el jugador al sortearse la tienda (al entrar y en cada reroll),
    /// más peso tienen las rarezas altas (<see cref="ShopLuck"/>). La suerte (0..1) es la del escalón más
    /// alto de <see cref="steps"/> alcanzado; con suerte 1 cada rareza pesa ×su multiplicador, y por debajo
    /// en proporción.
    /// </summary>
    [Serializable, DisplayName("Tienda · Suerte según el oro que llevas")]
    public sealed class ShopLuckFromGoldEffect : ItemEffect
    {
        [Serializable]
        public struct Step
        {
            [Tooltip("Oro que hay que llevar.")]
            [Min(0)] public int gold;
            [Tooltip("Suerte a partir de ese oro (0..1).")]
            [Range(0f, 1f)] public float luck;
        }

        public Currency currency = Currency.Gold;

        [Tooltip("Escalones de oro → suerte.")]
        public List<Step> steps = new()
        {
            new Step { gold = 100, luck = 0.25f },
            new Step { gold = 250, luck = 0.5f },
            new Step { gold = 500, luck = 1f },
        };

        [Header("Peso de cada rareza con suerte 1")]
        [Min(0f)] public float commonWeight = 1f;
        [Min(0f)] public float blueWeight = 1.5f;
        [Min(0f)] public float epicWeight = 2.5f;
        [Min(0f)] public float legendaryWeight = 4f;

        public override void OnEquip(ItemEffectContext context) => ShopLuck.Set(context, WeightFor);

        public override void OnUnequip(ItemEffectContext context) => ShopLuck.Remove(context);

        /// <summary>Suerte con el oro de ahora (0..1).</summary>
        public float CurrentLuck()
        {
            int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(currency) : 0;
            float luck = 0f;
            foreach (var step in steps)
                if (gold >= step.gold) luck = Mathf.Max(luck, step.luck);
            return luck;
        }

        private float WeightFor(ItemRarity rarity)
        {
            float full = rarity switch
            {
                ItemRarity.Blue => blueWeight,
                ItemRarity.Epic => epicWeight,
                ItemRarity.Legendary => legendaryWeight,
                _ => commonWeight,
            };
            return Mathf.Lerp(1f, full, CurrentLuck());
        }

        public override string Summary()
        {
            int maxGold = 0;
            foreach (var step in steps) maxGold = Mathf.Max(maxGold, step.gold);
            return Loc.Get("effect.shop_luck_gold", maxGold, blueWeight, epicWeight, legendaryWeight);
        }
    }
}
