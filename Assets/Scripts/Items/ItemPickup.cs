using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Fx;
using RedMagic.Gameplay;
using RedMagic.Hub;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Un item gratis tirado en el suelo: el jugador lo toca y se equipa (mismas reglas que la
    /// tienda — un modificador reemplaza su hueco, uno de pool libre va al primer hueco vacío). Si
    /// los 6 huecos libres están llenos se queda en el suelo y avisa.
    ///
    /// <see cref="Drop"/> es la entrada ("soltar N items aquí"); <see cref="RandomItem"/> elige
    /// uno de <see cref="ItemLibrary"/>, con rareza mínima opcional. Pooled
    /// (<see cref="Pool{T}"/>): construido en código una vez, <see cref="PoolRunner"/> recoge los
    /// que queden al cambiar de escena.
    /// </summary>
    public sealed class ItemPickup : MonoBehaviour, IPooled
    {
        private const float IconSize = 0.9f;
        private const float HoverHeight = 0.9f;
        private const float Spacing = 1.4f;

        private static Pool<ItemPickup> _pool;

        private ItemDefinition _item;
        private SpriteRenderer _icon, _glow;
        private Vector3 _basePosition;
        private float _phase;
        private float _warnCooldown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _pool = null;

        // ------------------------------------------------------------------ API

        /// <summary>
        /// Suelta <paramref name="items"/> en fila centrada en <paramref name="position"/>, apoyados
        /// sobre el suelo que haya debajo. Viven en el pool, que los recoge
        /// al cambiar de escena.
        /// </summary>
        public static void Drop(IReadOnlyList<ItemDefinition> items, Vector3 position)
        {
            if (items == null || items.Count == 0) return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                float x = position.x + (i - (items.Count - 1) * 0.5f) * Spacing;
                Spawn(items[i], new Vector3(x, position.y, 0f));
            }
        }

        public static ItemPickup Spawn(ItemDefinition item, Vector3 position)
        {
            if (item == null) return null;

            _pool ??= new Pool<ItemPickup>(Build, prewarm: 2);
            var pickup = _pool.Get();

            pickup.Begin(item, SnapToGround(position));
            return pickup;
        }

        /// <summary>
        /// Item al azar de todo el catálogo. Con <paramref name="minRarity"/> sólo de esa rareza o
        /// más; si no hay ninguno, el de rareza más alta disponible.
        /// </summary>
        public static ItemDefinition RandomItem(ItemRarity minRarity = ItemRarity.Common)
        {
            var all = new List<ItemDefinition>(ItemLibrary.All);
            if (all.Count == 0) return null;

            var pool = all.FindAll(i => i.Rarity >= minRarity);
            if (pool.Count == 0)
            {
                var best = ItemRarity.Common;
                foreach (var i in all) if (i.Rarity > best) best = i.Rarity;
                pool = all.FindAll(i => i.Rarity == best);
            }

            return pool[Random.Range(0, pool.Count)];
        }

        // ------------------------------------------------------------------ vida

        private static ItemPickup Build()
        {
            var go = new GameObject("ItemPickup");
            var pickup = go.AddComponent<ItemPickup>();

            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            pickup._glow = glow.AddComponent<SpriteRenderer>();
            pickup._glow.sprite = ProceduralSprites.Glow;

            var icon = new GameObject("Icon");
            icon.transform.SetParent(go.transform, false);
            pickup._icon = icon.AddComponent<SpriteRenderer>();

            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;

            return pickup;
        }

        private void Begin(ItemDefinition item, Vector3 position)
        {
            _item = item;
            _basePosition = position;
            _phase = Random.value * 10f;
            _warnCooldown = 0f;
            transform.position = position;

            var rarity = ItemRarities.ColorOf(item.Rarity);

            _icon.sprite = item.Icon != null ? item.Icon : AbilityFx.DefaultSprite;
            _icon.color = item.Icon != null ? Color.white : item.Accent;
            AbilityFx.Resize(_icon.transform, _icon, Vector2.one * IconSize);

            _glow.color = new Color(rarity.r, rarity.g, rarity.b, 0.75f);
            AbilityFx.Resize(_glow.transform, _glow, Vector2.one * IconSize * 2.2f);

            var reference = Run.RunManager.Instance != null ? Run.RunManager.Instance.Player : null;
            AbilityFx.CopySorting(_glow, reference, 1);
            AbilityFx.CopySorting(_icon, reference, 2);
        }

        public void OnReturnedToPool() => _item = null;

        private void Update()
        {
            float t = Time.time + _phase;
            transform.position = _basePosition + Vector3.up * (Mathf.Sin(t * 2.5f) * 0.12f);
            _glow.transform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
            if (_warnCooldown > 0f) _warnCooldown -= Time.deltaTime;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
        private void OnTriggerStay2D(Collider2D other) { if (_warnCooldown <= 0f) TryCollect(other); }

        private void TryCollect(Collider2D other)
        {
            if (_item == null || !other.CompareTag("Player")) return;

            var inventory = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            if (inventory == null) return;

            if (!inventory.TryEquip(_item))
            {
                // Sólo pasa con un item de pool libre y los 6 huecos llenos: se queda aquí.
                _warnCooldown = 3f;
                RewardPopupUi.Show(_item.Icon, Loc.Get("shop.free_slots_full"), _item.DisplayName, _item.Accent);
                return;
            }

            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");
            AbilityFx.Flash(ProceduralSprites.Glow, transform.position, Vector2.one * 2.5f,
                            ItemRarities.ColorOf(_item.Rarity), 0.4f, 0f, 1.8f, gameObject);
            RewardPopupUi.Show(_item.Icon, Loc.Get("reward.item"), _item.DisplayName,
                               ItemRarities.ColorOf(_item.Rarity));

            _pool?.Release(this);
        }

        /// <summary>Se apoya sobre el suelo (Ground/Platform) que haya debajo, a altura de recogida.</summary>
        private static Vector3 SnapToGround(Vector3 position)
        {
            var hit = Physics2D.Raycast((Vector2)position + Vector2.up * 0.5f, Vector2.down, 30f,
                                        GroundMotion.TerrainMask);
            if (hit.collider == null) return new Vector3(position.x, position.y, 0f);
            return new Vector3(position.x, hit.point.y + HoverHeight, 0f);
        }
    }
}
