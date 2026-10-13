using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Fx;
using RedMagic.Gameplay;
using RedMagic.Hub;
using RedMagic.Localization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Items
{
    /// <summary>
    /// Un item tirado en el suelo. Si <c>autoCollect</c> y cabe, se equipa al tocarlo (mismas reglas que la tienda: un
    /// modificador reemplaza su hueco, uno de pool libre va al primer hueco vacío). Si no (no cabe, o es un item que
    /// acaba de salir del inventario al sustituirlo — ésos nunca se recogen solos, o rebotarían de un hueco a otro), con
    /// el jugador encima sale el cartel y:
    /// <list type="bullet">
    /// <item><b>Interactuar</b> (E / L1 / tocar el botón) lo equipa; con los 6 huecos libres llenos abre la pantalla de
    /// items para elegir cuál sustituir. Lo sustituido cae al suelo.</item>
    /// <item><b>Mantener</b> (F / L1 / el botón táctil) <see cref="RecycleSeconds"/> lo recicla por
    /// <c>ShopConfig.recycleFraction</c> de su precio en oro.</item>
    /// </list>
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

        /// <summary>Segundos manteniendo pulsado para reciclar.</summary>
        public const float RecycleSeconds = 0.8f;

        private static Pool<ItemPickup> _pool;

        private ItemDefinition _item;
        private SpriteRenderer _icon, _glow;
        private Vector3 _basePosition;
        private float _phase;
        private bool _autoCollect;
        private float _nearUntil;       // el jugador sigue dentro del trigger (lo renueva OnTriggerStay)
        private float _hold;            // segundos manteniendo reciclar
        private bool _holdCanSwap;      // la pulsación en curso es de L1 / táctil: soltar pronto = equipar
        private bool _prompting;

        /// <summary>El item del suelo que atiende la entrada ahora mismo (con varios a la vez, sólo uno).</summary>
        private static ItemPickup _focus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            _pool = null;
            _focus = null;
        }

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

        /// <param name="autoCollect">False = no se equipa al tocarlo (lo que sale del inventario al sustituirlo).</param>
        public static ItemPickup Spawn(ItemDefinition item, Vector3 position, bool autoCollect = true)
        {
            if (item == null) return null;

            _pool ??= new Pool<ItemPickup>(Build, prewarm: 2);
            var pickup = _pool.Get();

            pickup.Begin(item, SnapToGround(position), autoCollect);
            return pickup;
        }

        /// <summary>
        /// Lo que sale del inventario al sustituirlo: cae delante del jugador y no se recoge solo (se cambia o se
        /// recicla a propósito).
        /// </summary>
        public static void DropReplaced(ItemDefinition item, Transform player)
        {
            if (item == null) return;
            Vector3 at = player != null ? player.position : Vector3.zero;
            float facing = 1f;
            var movement = player != null ? player.GetComponent<PlayerMovement>() : null;
            if (movement != null) facing = movement.Facing;
            Spawn(item, at + new Vector3(facing * 1.3f, 0.5f, 0f), autoCollect: false);
        }

        /// <summary>Oro que da reciclar <paramref name="item"/> en el mundo actual.</summary>
        public static int RecycleValue(ItemDefinition item)
        {
            int world = Run.RunManager.Instance != null ? Run.RunManager.Instance.CurrentWorldNumber : 1;
            var config = ShopConfig.Instance;
            return Mathf.Max(1, Mathf.RoundToInt(config.PriceFor(item, world) * config.recycleFraction));
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

        private void Begin(ItemDefinition item, Vector3 position, bool autoCollect)
        {
            _item = item;
            _autoCollect = autoCollect;
            _basePosition = position;
            _phase = Random.value * 10f;
            _nearUntil = 0f;
            _hold = 0f;
            _holdCanSwap = false;
            _prompting = false;
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

        public void OnReturnedToPool()
        {
            HidePrompt();
            if (_focus == this) _focus = null;
            _item = null;
        }

        private void Update()
        {
            float t = Time.time + _phase;
            float shake = _hold > 0f ? Mathf.Sin(Time.time * 60f) * 0.04f * (_hold / RecycleSeconds) : 0f;
            transform.position = _basePosition + new Vector3(shake, Mathf.Sin(t * 2.5f) * 0.12f, 0f);
            _glow.transform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);

            UpdateInteraction();
        }

        private void OnTriggerEnter2D(Collider2D other) => OnPlayerTouch(other);
        private void OnTriggerStay2D(Collider2D other) => OnPlayerTouch(other);

        private void OnPlayerTouch(Collider2D other)
        {
            if (_item == null || !other.CompareTag("Player")) return;
            _nearUntil = Time.time + 0.2f;

            var inventory = Inventory;
            if (!_autoCollect || inventory == null || !Fits(inventory, _item)) return;
            var previous = inventory.Occupant(_item);
            if (!inventory.TryEquip(_item)) return;
            var player = other.transform;
            Collected();
            DropReplaced(previous, player);
        }

        // ------------------------------------------------------------------ cambiar / reciclar

        private static WeaponInventory Inventory => WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;

        /// <summary>¿Entra sin sacar a nadie? Un modificador siempre entra (sustituye su hueco: eso sí saca al que hubiera).</summary>
        private static bool Fits(WeaponInventory inventory, ItemDefinition item) =>
            !(item is FreePoolItemDefinition) || !inventory.FreeSlotsFull;

        private void UpdateInteraction()
        {
            bool near = _item != null && Time.time < _nearUntil && GameStateManager.CanPlayerAct;
            if (!near)
            {
                _hold = 0f;
                HidePrompt();
                if (_focus == this) _focus = null;
                return;
            }

            if (_focus != null && _focus != this && _focus._item != null && Time.time < _focus._nearUntil) return;
            _focus = this;
            TouchInput.RequestInteractButton();

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool fHeld = keyboard != null && keyboard.fKey.isPressed;
            bool swapHeld = (gamepad != null && gamepad.leftShoulder.isPressed) || TouchInput.InteractHeld;
            bool tap = keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
            TouchInput.ConsumeInteract();   // el botón táctil se lee por su estado (mantener), no por la pulsación

            if (fHeld || swapHeld)
            {
                if (_hold <= 0f) _holdCanSwap = swapHeld && !fHeld;
                _hold += Time.deltaTime;
                if (_hold >= RecycleSeconds) { Recycle(); return; }
            }
            else
            {
                // L1 / táctil soltados antes de reciclar = pulsación corta = equipar
                if (_hold > 0f && _holdCanSwap) tap = true;
                _hold = 0f;
            }

            ShowPrompt();
            if (tap) Swap();
        }

        /// <summary>Equipa el item; si hace falta sacar uno, lo elige el jugador (huecos libres) o sale el del hueco.</summary>
        private void Swap()
        {
            var inventory = Inventory;
            if (inventory == null) return;
            var item = _item;

            if (item is FreePoolItemDefinition free && inventory.FreeSlotsFull)
            {
                var menu = UI.ItemMenuController.Instance;
                if (menu == null) return;
                HidePrompt();
                menu.OpenReplace(free, index =>
                {
                    if (_item != free) return;   // ya no está (reciclado o vuelto al pool)
                    var player = PlayerTransform();
                    var old = inventory.EquipFree(index, free);
                    Collected();
                    ItemPickup.DropReplaced(old, player);
                });
                return;
            }

            var previous = inventory.Occupant(item);
            if (!inventory.TryEquip(item)) return;
            var who = PlayerTransform();
            Collected();
            DropReplaced(previous, who);
        }

        private void Recycle()
        {
            int gold = RecycleValue(_item);
            CurrencyManager.Instance?.Add(Currency.Gold, gold);
            Audio.SystemSounds.Play(s => s.shopBuy);
            AbilityFx.Flash(ProceduralSprites.Glow, transform.position, Vector2.one * 2.5f,
                            new Color(1f, 0.82f, 0.3f), 0.4f, 0f, 1.8f, gameObject);
            RewardPopupUi.Show(_item.Icon, Loc.Get("item.recycled", gold), _item.DisplayName, new Color(1f, 0.82f, 0.3f));
            _pool?.Release(this);
        }

        private void ShowPrompt()
        {
            string key = InputDeviceManager.TouchActive ? "prompt.ground_item.touch"
                       : InputDeviceManager.GamepadActive ? "prompt.ground_item.pad"
                       : "prompt.ground_item.kb";
            string text = Loc.Get(key, RecycleValue(_item));
            if (_hold > 0f) text += $"  {Mathf.RoundToInt(_hold / RecycleSeconds * 100f)}%";
            InteractionPromptUi.Show(this, text);
            _prompting = true;
        }

        private void HidePrompt()
        {
            if (!_prompting) return;
            _prompting = false;
            InteractionPromptUi.Hide(this);
        }

        private static Transform PlayerTransform()
        {
            var go = Run.RunManager.Instance != null && Run.RunManager.Instance.Player != null
                ? Run.RunManager.Instance.Player
                : GameObject.FindWithTag("Player");
            return go != null ? go.transform : null;
        }

        /// <summary>Recogido: sonido de botín, destello, aviso y de vuelta al pool.</summary>
        private void Collected()
        {
            // Suena como el OnLoot de quien lo recoge (el jugador, vía su WeaponUser).
            if (WeaponUser.Current != null) WeaponUser.Current.NotifyItemPickedUp();
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
