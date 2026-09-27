using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.Hub;
using RedMagic.Items;
using RedMagic.Localization;
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
    ///
    /// <see cref="rerollPoint"/> es el altar del reroll (<see cref="ShopRerollAltar"/>), que se usa
    /// igual que un altar: gasta un <see cref="RunRerolls"/> y cambia los items de todos los altares
    /// (sin repetir los que estaban a la vista si el pool da, nunca lo ya comprado en esta visita),
    /// con salida/entrada escalonada de izquierda a derecha. Sin rerolls, el mismo "no llega" que
    /// un item sin oro.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopManager : MonoBehaviour
    {
        /// <summary>La tienda de la escena cargada, si hay una (el HUD enseña los rerolls sólo entonces).</summary>
        public static ShopManager Current { get; private set; }

        /// <summary>Al surtir la tienda al entrar (una vez por visita, antes de poder usarla): Anillo de oro.</summary>
        public static event Action<ShopManager> Entered;

        [Tooltip("Un punto por altar. Cada uno recibe un item (si el pool da para tantos).")]
        [SerializeField] private List<Transform> itemSpawnPoints = new();

        [Tooltip("El altar del reroll (el objeto 'Reroll' de la escena, con su SpriteRenderer). Vacío = sin reroll.")]
        [SerializeField] private Transform rerollPoint;

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

        [Header("Sonido")]
        [Tooltip("Compra hecha (sólo si se cobra y se equipa).")]
        [SerializeField] private SoundCue buySound = new SoundCue();
        [Tooltip("Interacción rechazada (sin oro, sin rerolls).")]
        [SerializeField] private SoundCue denySound = new SoundCue();
        [SerializeField] private SoundCue rerollSound = new SoundCue();

        public IReadOnlyList<Transform> SpawnPoints => itemSpawnPoints;

        private readonly List<ShopAltar> _altars = new();         // ordenados de izquierda a derecha
        private readonly HashSet<ItemDefinition> _purchased = new(); // comprados en esta visita
        private ShopRerollAltar _reroll;
        private InputAction _interactAction;
        private IShopInteractable _focused;
        private bool _stocked, _rerolling;
        private int _world = 1;
        private System.Random _rng;
        private Sprite _coin;
        private Transform _player;
        private PlayerAnimator _playerAnimator;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            Entered = null;
        }

        private void Awake()
        {
            if (inputActions != null)
                _interactAction = inputActions.FindActionMap(actionMapName, false)?.FindAction(actionName, false);

            if (rerollPoint != null && !rerollPoint.TryGetComponent(out _reroll))
                _reroll = rerollPoint.gameObject.AddComponent<ShopRerollAltar>();
            if (_reroll != null) _reroll.Setup(priceOffset);
        }

        private void OnEnable()
        {
            Current = this;
            _interactAction?.Enable();
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed += OnCurrencyChanged;
            RunRerolls.Changed += OnRerollsChanged;
            OnRerollsChanged(RunRerolls.Count);
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            _interactAction?.Disable();
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            RunRerolls.Changed -= OnRerollsChanged;
            SetFocus(null);
        }

        private void Start()
        {
            // En una run, RunManager llama a Stock justo después de cargar; suelta, se llena sola
            // (y trae los rerolls de salida, para poder probar el reroll).
            bool runWillStock = RunManager.Instance != null && RunManager.Instance.Phase == RunPhase.Shop;
            if (!_stocked && !runWillStock)
            {
                if (RunManager.Instance == null || !RunManager.Instance.RunInProgress)
                    RunRerolls.Set(ShopConfig.Instance.startingRerolls);
                Stock(1, Environment.TickCount);
            }
        }

        // ------------------------------------------------------------------ surtido

        /// <param name="world">Número de mundo (desde 1): escala rarezas y precios.</param>
        public void Stock(int world, int seed)
        {
            _stocked = true;
            _world = world;
            _rng = new System.Random(seed);
            _purchased.Clear();
            SetFocus(null);

            _coin = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config?.VisualFor(Currency.Gold)?.icon : null;

            _altars.Clear();
            foreach (var point in itemSpawnPoints)
            {
                if (point == null) continue;
                if (!point.TryGetComponent(out ShopAltar altar)) altar = point.gameObject.AddComponent<ShopAltar>();
                _altars.Add(altar);
            }
            _altars.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            var stock = ShopConfig.Instance.RollStock(_altars.Count, world, _rng);
            for (int i = 0; i < _altars.Count; i++)
                Fill(_altars[i], stock, i);

            RefreshAffordability();
            Debug.Log($"[ShopManager] Tienda del mundo {world}: {stock.Count} item(s).", this);
            Entered?.Invoke(this);
        }

        private void Fill(ShopAltar altar, List<ShopStockEntry> stock, int index)
        {
            if (index < stock.Count)
                altar.Show(stock[index], iconSize, priceOffset, bobAmplitude, bobSpeed, _coin, SortingReference());
            else
                altar.Clear();
        }

        // ------------------------------------------------------------------ cercanía y compra

        private void Update()
        {
            if (_rerolling || FindPlayer() == null) { SetFocus(null); return; }

            SetFocus(Nearest());

            if (_focused == null || !GameStateManager.CanPlayerAct) return;
            if (!InteractInput.Pressed(_interactAction)) return;

            if (_focused is ShopAltar altar) TryBuy(altar);
            else if (_focused == (IShopInteractable)_reroll) TryReroll();
        }

        private IShopInteractable Nearest()
        {
            IShopInteractable best = null;
            float bestDx = float.MaxValue;

            foreach (var altar in _altars) Consider(altar, ref best, ref bestDx);
            if (_reroll != null) Consider(_reroll, ref best, ref bestDx);

            return best;
        }

        private void Consider(IShopInteractable candidate, ref IShopInteractable best, ref float bestDx)
        {
            if (candidate == null || !candidate.CanInteract) return;
            Vector3 p = _player.position;
            float dx = Mathf.Abs(candidate.ItemPosition.x - p.x);
            float dy = Mathf.Abs(candidate.ItemPosition.y - p.y);
            if (dx > interactRangeX || dy > interactRangeY || dx >= bestDx) return;
            best = candidate;
            bestDx = dx;
        }

        private void SetFocus(IShopInteractable target)
        {
            if (target == _focused) return;
            _focused?.SetFocused(false);
            _focused = target;
            _focused?.SetFocused(true);
            ShowPanel();
        }

        private void ShowPanel()
        {
            if (_focused is ShopAltar altar) ShopItemPanel.Show(this, altar.Entry);
            else if (_focused != null) ShopItemPanel.ShowReroll(this, RunRerolls.Count, _altars.Count);
            else ShopItemPanel.Hide(this);
        }

        /// <summary>El "no llega" de la tienda (sin oro, sin rerolls): una sola respuesta para todo.</summary>
        private void Deny(IShopInteractable target)
        {
            target.Deny();
            AudioManager.Instance?.Play(denySound);
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
                Deny(altar);
                return;
            }

            // Comprobar antes de cobrar: un item de pool libre con los 6 huecos llenos no cabe.
            if (entry.Item is FreePoolItemDefinition && inventory.FreeSlotsFull)
            {
                altar.Deny();
                RewardPopupUi.Show(entry.Item.Icon, Loc.Get("shop.free_slots_full"), entry.Item.DisplayName, entry.Item.Accent);
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

            _purchased.Add(entry.Item);
            AudioManager.Instance?.Play(buySound);
            RewardPopupUi.Show(entry.Item.Icon, Loc.Get("shop.bought"), entry.Item.DisplayName, ItemRarities.ColorOf(entry.Item.Rarity));

            // Efecto de compra (el altar queda vacío ya; el juego sigue) + el oro del HUD bajando.
            altar.PlayPurchase(_player);
            var fx = ShopFxConfig.Current;
            CurrencyHud.PlaySpend(Currency.Gold, goldBefore, wallet.Get(Currency.Gold),
                                  fx.goldTickDuration, fx.goldShakeAmplitude, fx.goldShakeDuration);
            SetFocus(null);
        }

        // ------------------------------------------------------------------ reroll

        private void TryReroll()
        {
            if (_playerAnimator != null) _playerAnimator.TriggerInteract();

            if (!RunRerolls.TrySpend())
            {
                Deny(_reroll);
                return;
            }

            StartCoroutine(RerollRoutine());
        }

        private IEnumerator RerollRoutine()
        {
            _rerolling = true;
            SetFocus(null);

            var fx = ShopFxConfig.Current;
            AudioManager.Instance?.Play(rerollSound);
            _reroll.PlayTrigger(SortingReference());

            // Un icono recién comprado que aún vuela al jugador termina su viaje antes de tocar su altar.
            while (_altars.Exists(a => a != null && a.Purchasing)) yield return null;

            // Lo que estaba a la vista se evita (si el pool da); lo comprado en esta visita, nunca.
            var displayed = new HashSet<ItemDefinition>();
            foreach (var altar in _altars)
                if (altar != null && altar.HasItem) displayed.Add(altar.Entry.Item);

            // 1. Salida escalonada de izquierda a derecha.
            for (int i = 0; i < _altars.Count; i++)
                _altars[i].PlayExit(i * fx.rerollStagger, fx.rerollExitDuration);
            yield return new WaitForSeconds(Mathf.Max(0, _altars.Count - 1) * fx.rerollStagger + fx.rerollExitDuration);

            // 2. Surtido nuevo con la misma tabla de rarezas/precios; entrada escalonada con rebote.
            var stock = ShopConfig.Instance.RollStock(_altars.Count, _world, _rng ??= new System.Random(), _purchased, displayed);
            for (int i = 0; i < _altars.Count; i++)
            {
                Fill(_altars[i], stock, i);
                _altars[i].PlayEnter(i * fx.rerollStagger, fx.rerollEnterDuration, fx.rerollEnterOvershoot);
            }
            RefreshAffordability();
            yield return new WaitForSeconds(Mathf.Max(0, _altars.Count - 1) * fx.rerollStagger + fx.rerollEnterDuration);

            _rerolling = false;
        }

        // ------------------------------------------------------------------ refrescos

        private void OnCurrencyChanged(Currency currency, int amount)
        {
            if (currency != Currency.Gold) return;
            RefreshAffordability();
            if (_focused is ShopAltar) ShowPanel();
        }

        private void OnRerollsChanged(int count)
        {
            if (_reroll != null) _reroll.SetAffordable(count > 0);
            if (_focused != null && _focused == (IShopInteractable)_reroll) ShowPanel();
        }

        private void RefreshAffordability()
        {
            int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Gold) : 0;
            foreach (var altar in _altars)
                if (altar != null && altar.HasItem) altar.SetAffordable(gold >= altar.Entry.Price);
        }

        private GameObject SortingReference() => FindPlayer() != null ? _player.gameObject : null;

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

            if (rerollPoint != null)
            {
                Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
                Gizmos.DrawWireCube(rerollPoint.position, Vector3.one * iconSize);
                UnityEditor.Handles.Label(rerollPoint.position + Vector3.up * (iconSize * 0.7f), "Reroll");
            }
        }
#endif
    }
}
