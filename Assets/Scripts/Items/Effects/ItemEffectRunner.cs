using System;
using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Aplica los <see cref="ItemEffect"/> de lo que hay equipado. Escucha a
    /// <see cref="WeaponInventory"/> (como <see cref="SynergyTracker"/>): al equipar, cada efecto del
    /// item recibe <c>OnEquip</c> con un contexto propio de ese slot; al quitar, <c>OnUnequip</c>; y
    /// mientras tanto <c>Tick</c> cada frame. Vaciar el inventario al acabar la run quita todo solo.
    ///
    /// Vive dentro de <see cref="WeaponLoadout"/>, que le da el Tick.
    /// </summary>
    public sealed class ItemEffectRunner : IDisposable
    {
        private readonly WeaponInventory _inventory;
        private readonly Dictionary<InventorySlot, List<(ItemEffect effect, ItemEffectContext context)>> _active = new();

        private static Health _playerHealth;

        /// <summary>
        /// La vida del jugador activo (el de la escena en el hub, el persistente en la run). Se busca
        /// por la tag "Player" y se vuelve a buscar si el que había ya no está activo.
        /// </summary>
        public static Health PlayerHealth
        {
            get
            {
                if (_playerHealth != null && _playerHealth.isActiveAndEnabled) return _playerHealth;
                var go = GameObject.FindWithTag("Player");
                _playerHealth = go != null ? go.GetComponent<Health>() : null;
                return _playerHealth;
            }
        }

        public ItemEffectRunner(WeaponInventory inventory)
        {
            _inventory = inventory;
            _inventory.ItemEquipped += OnEquipped;
            _inventory.ItemUnequipped += OnUnequipped;

            foreach (var (slot, item) in _inventory.EquippedItems()) OnEquipped(slot, item);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            foreach (var list in _active.Values)
            foreach (var (effect, context) in list)
                effect.Tick(context, deltaTime);
        }

        public void Dispose()
        {
            _inventory.ItemEquipped -= OnEquipped;
            _inventory.ItemUnequipped -= OnUnequipped;

            foreach (var list in _active.Values)
            foreach (var (effect, context) in list)
                effect.OnUnequip(context);
            _active.Clear();
        }

        private void OnEquipped(InventorySlot slot, ItemDefinition item)
        {
            if (item == null || item.Effects.Count == 0) return;

            var list = new List<(ItemEffect, ItemEffectContext)>(item.Effects.Count);
            foreach (var effect in item.Effects)
            {
                if (effect == null) continue;
                var context = new ItemEffectContext(item);
                effect.OnEquip(context);
                list.Add((effect, context));
            }

            _active[slot] = list;
        }

        private void OnUnequipped(InventorySlot slot, ItemDefinition item)
        {
            if (!_active.Remove(slot, out var list)) return;
            foreach (var (effect, context) in list) effect.OnUnequip(context);
        }
    }
}
