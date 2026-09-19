using System;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.Run;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Economy
{
    /// <summary>
    /// La tienda que <see cref="RunManager"/> coloca en una sección al azar de cada mundo. El
    /// jugador se acerca, pulsa interactuar y se abre <see cref="ShopMenuController"/>.
    ///
    /// <b>Sólo se puede usar con la sección despejada</b>: mientras quede algún enemigo vivo
    /// (<see cref="SectionClearTracker"/>) el caldero no responde, igual que la salida.
    ///
    /// El escaparate se sortea una vez por tienda (con la semilla de la run, así que es
    /// reproducible) y se recuerda entre aperturas: lo comprado no vuelve a aparecer.
    ///
    /// Los artículos son items reales del sistema de builds (<see cref="RedMagic.Items.ItemLibrary"/>):
    /// 1 de Elemento + 1 de Trayectoria + 1 de Forma + varios de pool libre. Comprar equipa el item
    /// en el <see cref="RedMagic.Items.WeaponLoadout"/> — de eso se encarga el menú de tienda.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class ShopInteractable : MonoBehaviour
    {
        private const string ConfigResourcePath = "ShopConfig";

        [Header("Detección")]
        [Tooltip("Etiqueta del objeto que puede usar la tienda.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("La tienda no se abre hasta haber matado a todos los enemigos de la sección.")]
        [SerializeField] private bool requireEnemiesDead = true;

        [Header("Aviso en pantalla")]
        [Tooltip("Objeto que se enciende cuando el jugador está a rango y la tienda está abierta al " +
                 "público. Opcional.")]
        [SerializeField] private GameObject prompt;

        [Header("Sonido")]
        [SerializeField] private string openSfxId = "SFX_ButtonClick";

        private readonly List<ShopStockEntry> _stock = new();
        private ShopConfig _config;
        private bool _rolled;
        private bool _playerInRange;
        private PlayerAnimator _playerAnimator;

        /// <summary>Artículos que quedan por comprar en esta tienda.</summary>
        public IReadOnlyList<ShopStockEntry> Stock
        {
            get
            {
                EnsureStock();
                return _stock;
            }
        }

        /// <summary>True si hay enemigos vivos y por eso la tienda no atiende.</summary>
        public bool BlockedByEnemies =>
            requireEnemiesDead && SectionClearTracker.Instance != null &&
            !SectionClearTracker.Instance.IsCleared;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake() => SetPromptVisible(false);

        private void OnDisable()
        {
            _playerInRange = false;
            SetPromptVisible(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;

            // El prompt sigue el estado de la sección: aparece en cuanto cae el último enemigo.
            SetPromptVisible(!BlockedByEnemies);

            if (!GameStateManager.CanPlayerAct) return;
            if (BlockedByEnemies) return;

            if (InteractPressed()) Open();
        }

        private static bool InteractPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
                return true;

            return TouchInput.ConsumeInteract();
        }

        /// <summary>Abre el escaparate. Público para poder llamarlo desde un botón de UI.</summary>
        public void Open()
        {
            if (ShopMenuController.Instance == null)
            {
                Debug.LogWarning("[ShopInteractable] No hay ShopMenuController; ¿falta su " +
                                 "PanelSettings en Resources?", this);
                return;
            }

            if (_playerAnimator != null) _playerAnimator.TriggerInteract();

            EnsureStock();

            if (!string.IsNullOrWhiteSpace(openSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(openSfxId);

            SetPromptVisible(false);
            ShopMenuController.Instance.Open(this);
        }

        /// <summary>Quita el artículo del escaparate tras comprarlo (sólo se puede comprar una vez).</summary>
        public void MarkSold(ShopStockEntry entry)
        {
            if (entry != null) _stock.Remove(entry);
        }

        // El escaparate se sortea una sola vez, con la semilla de la run + la posición de la
        // tienda, para que sea reproducible y distinto entre tiendas del mismo mundo.
        private void EnsureStock()
        {
            if (_rolled) return;
            _rolled = true;

            _config = Resources.Load<ShopConfig>(ConfigResourcePath);
            if (_config == null)
            {
                Debug.LogWarning($"[ShopInteractable] No hay '{ConfigResourcePath}' en Resources: la " +
                                 "tienda saldrá vacía.", this);
                return;
            }

            int seed = RunManager.Instance != null ? RunManager.Instance.RunSeed : Environment.TickCount;
            seed ^= Mathf.RoundToInt(transform.position.x * 73856093f);

            foreach (var entry in _config.RollStock(seed))
                if (entry != null && entry.Item != null) _stock.Add(entry);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInRange = true;
            _playerAnimator = other.GetComponentInParent<PlayerAnimator>();
            SetPromptVisible(!BlockedByEnemies);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInRange = false;
            _playerAnimator = null;
            SetPromptVisible(false);
        }

        private void SetPromptVisible(bool visible)
        {
            if (prompt != null && prompt.activeSelf != visible) prompt.SetActive(visible);
        }

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<Collider2D>();
            if (col == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.5f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
