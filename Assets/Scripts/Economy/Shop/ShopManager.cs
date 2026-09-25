using System;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.Hub;
using RedMagic.Items;
using RedMagic.Run;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Economy
{
    /// <summary>
    /// La tienda de la escena Shop. Llena cada punto de <see cref="itemSpawnPoints"/> (los altares)
    /// con un item del pool global sorteado por <see cref="ShopConfig.RollStock"/> — rareza por peso
    /// según el mundo, sin repetidos, precio = base × multiplicador del mundo.
    ///
    /// <see cref="RunManager"/> llama a <see cref="Stock"/> con el mundo en curso al cargar la
    /// escena; abierta suelta (probando la escena) se llena sola con el mundo 1.
    ///
    /// Con el jugador cerca de un altar enseña <see cref="ShopItemPanel"/> (nombre, rareza, efecto,
    /// sinergias antes → después) e Interactuar compra: oro (<see cref="CurrencyManager.TrySpend"/>)
    /// → inventario (<see cref="WeaponInventory.TryEquip"/>) → altar vacío.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopManager : MonoBehaviour
    {
        [Tooltip("Un punto por altar. Cada uno recibe un item (si el pool da para tantos).")]
        [SerializeField] private List<Transform> itemSpawnPoints = new();

        [Header("Aspecto")]
        [Min(0.1f)] [SerializeField] private float iconSize = 1.6f;
        [Tooltip("Dónde va el precio respecto al icono (unidades, negativo = debajo).")]
        [SerializeField] private float priceOffset = -1.25f;
        [Min(0f)] [SerializeField] private float bobAmplitude = 0.12f;
        [Min(0f)] [SerializeField] private float bobSpeed = 2.2f;

        [Header("Cercanía")]
        [Tooltip("Distancia horizontal máxima jugador ↔ altar para poder comprar.")]
        [Min(0.1f)] [SerializeField] private float interactRangeX = 1.4f;
        [Tooltip("Distancia vertical máxima (el icono flota sobre el altar, el jugador está en el suelo).")]
        [Min(0.1f)] [SerializeField] private float interactRangeY = 4f;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";

        public IReadOnlyList<Transform> SpawnPoints => itemSpawnPoints;

        private readonly List<ShopAltar> _altars = new();
        private InputAction _interactAction;
        private ShopAltar _focused;
        private bool _stocked;
        private Transform _player;
        private PlayerAnimator _playerAnimator;

        private void Awake()
        {
            if (inputActions != null)
                _interactAction = inputActions.FindActionMap(actionMapName, false)?.FindAction(actionName, false);
        }

        private void OnEnable()
        {
            _interactAction?.Enable();
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed += OnCurrencyChanged;
        }

        private void OnDisable()
        {
            _interactAction?.Disable();
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            SetFocus(null);
        }

        private void Start()
        {
            // En una run, RunManager llama a Stock justo después de cargar; suelta, se llena sola.
            bool runWillStock = RunManager.Instance != null && RunManager.Instance.Phase == RunPhase.Shop;
            if (!_stocked && !runWillStock) Stock(1, Environment.TickCount);
        }

        // ------------------------------------------------------------------ surtido

        /// <param name="world">Número de mundo (desde 1): escala rarezas y precios.</param>
        public void Stock(int world, int seed)
        {
            _stocked = true;
            SetFocus(null);

            var config = ShopConfig.Instance;
            var stock = config.RollStock(itemSpawnPoints.Count, world, new System.Random(seed));
            var coin = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config?.VisualFor(Currency.Gold)?.icon : null;
            var sortingRef = FindPlayer() != null ? _player.gameObject : null;

            _altars.Clear();
            for (int i = 0; i < itemSpawnPoints.Count; i++)
            {
                var point = itemSpawnPoints[i];
                if (point == null) continue;

                if (!point.TryGetComponent(out ShopAltar altar)) altar = point.gameObject.AddComponent<ShopAltar>();
                _altars.Add(altar);

                if (i < stock.Count)
                    altar.Show(stock[i], iconSize, priceOffset, bobAmplitude, bobSpeed, coin, sortingRef);
                else
                    altar.Clear();
            }

            RefreshAffordability();
            Debug.Log($"[ShopManager] Tienda del mundo {world}: {stock.Count} item(s).", this);
        }

        // ------------------------------------------------------------------ cercanía y compra

        private void Update()
        {
            if (FindPlayer() == null) { SetFocus(null); return; }

            SetFocus(Nearest());

            if (_focused == null || !GameStateManager.CanPlayerAct) return;
            if (InteractInput.Pressed(_interactAction)) TryBuy(_focused);
        }

        private ShopAltar Nearest()
        {
            ShopAltar best = null;
            float bestDx = float.MaxValue;
            Vector3 p = _player.position;

            foreach (var altar in _altars)
            {
                if (altar == null || !altar.HasItem) continue;
                float dx = Mathf.Abs(altar.ItemPosition.x - p.x);
                float dy = Mathf.Abs(altar.ItemPosition.y - p.y);
                if (dx > interactRangeX || dy > interactRangeY || dx >= bestDx) continue;
                best = altar;
                bestDx = dx;
            }

            return best;
        }

        private void SetFocus(ShopAltar altar)
        {
            if (altar == _focused) return;
            if (_focused != null) _focused.SetFocused(false);
            _focused = altar;
            if (_focused != null) _focused.SetFocused(true);

            if (_focused == null) ShopItemPanel.Hide(this);
            else ShopItemPanel.Show(this, _focused.Entry);
        }

        private void TryBuy(ShopAltar altar)
        {
            var entry = altar.Entry;
            var wallet = CurrencyManager.Instance;
            var inventory = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            if (entry.Item == null || wallet == null || inventory == null) return;

            if (_playerAnimator != null) _playerAnimator.TriggerInteract();

            if (wallet.Get(Currency.Gold) < entry.Price)
            {
                altar.Deny();
                AudioManager.Instance?.PlaySFX("SFX_ButtonClick");
                return;
            }

            // Comprobar antes de cobrar: un item de pool libre con los 6 huecos llenos no cabe.
            if (entry.Item is FreePoolItemDefinition && inventory.FreeSlotsFull)
            {
                altar.Deny();
                RewardPopupUi.Show(entry.Item.Icon, "Huecos libres llenos", entry.Item.DisplayName, entry.Item.Accent);
                return;
            }

            int goldBefore = wallet.Get(Currency.Gold);
            if (!wallet.TrySpend(Currency.Gold, entry.Price)) { altar.Deny(); return; }

            if (!inventory.TryEquip(entry.Item))
            {
                wallet.Add(Currency.Gold, entry.Price); // no debería pasar tras la comprobación; devolver
                altar.Deny();
                return;
            }

            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");
            RewardPopupUi.Show(entry.Item.Icon, "Comprado", entry.Item.DisplayName, ItemRarities.ColorOf(entry.Item.Rarity));

            // Efecto de compra (el altar queda vacío ya; el juego sigue) + el oro del HUD bajando.
            altar.PlayPurchase(_player);
            var fx = ShopFxConfig.Current;
            CurrencyHud.PlaySpend(Currency.Gold, goldBefore, wallet.Get(Currency.Gold),
                                  fx.goldTickDuration, fx.goldShakeAmplitude, fx.goldShakeDuration);
            SetFocus(null);
        }

        private void OnCurrencyChanged(Currency currency, int amount)
        {
            if (currency != Currency.Gold) return;
            RefreshAffordability();
            if (_focused != null) ShopItemPanel.Show(this, _focused.Entry);
        }

        private void RefreshAffordability()
        {
            int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Gold) : 0;
            foreach (var altar in _altars)
                if (altar != null && altar.HasItem) altar.SetAffordable(gold >= altar.Entry.Price);
        }

        private Transform FindPlayer()
        {
            if (_player != null && _player.gameObject.activeInHierarchy) return _player;

            var go = RunManager.Instance != null && RunManager.Instance.Player != null
                ? RunManager.Instance.Player
                : GameObject.FindWithTag("Player");
            _player = go != null ? go.transform : null;
            _playerAnimator = go != null ? go.GetComponentInChildren<PlayerAnimator>() : null;
            return _player;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            for (int i = 0; i < itemSpawnPoints.Count; i++)
            {
                var p = itemSpawnPoints[i];
                if (p == null) continue;
                Gizmos.DrawWireCube(p.position, Vector3.one * iconSize);
                UnityEditor.Handles.Label(p.position + Vector3.up * (iconSize * 0.7f), $"Altar {i + 1}");
            }
        }
#endif
    }
}
