using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Estado de las mejoras permanentes: qué nivel tiene cada nodo del <see cref="UpgradeTree"/>
    /// y la lógica de compra. Singleton persistente que se auto-crea antes de la primera escena,
    /// igual que <see cref="CurrencyManager"/>.
    ///
    /// Los niveles se guardan en <c>PlayerPrefs</c> por id de nodo, así que sobreviven a la muerte
    /// y al cierre del juego (son meta-progresión, como el fragmento de alma y la calavera con los
    /// que se pagan).
    ///
    /// El efecto de las mejoras todavía no se aplica a nada: <see cref="GetBonus"/> está listo
    /// para cuando exista el sistema de stats del jugador. Por ahora esto sólo lleva la cuenta.
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        /// <summary>Moneda con la que se pagan las mejoras del hub.</summary>
        public const Currency Cost = Currency.SoulFragment;

        private const string TreeResourcePath = "UpgradeTree";
        private const string PrefPrefix = "upgrade.";

        /// <summary>(idDeNodo, nivelNuevo). Lo escucha el menú para repintarse.</summary>
        public event Action<string, int> Changed;

        private readonly Dictionary<string, int> _levels = new();
        private UpgradeTree _tree;

        public UpgradeTree Tree => _tree;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[UpgradeManager]");
            go.AddComponent<UpgradeManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _tree = Resources.Load<UpgradeTree>(TreeResourcePath);
            if (_tree == null)
            {
                Debug.LogWarning($"[UpgradeManager] No hay '{TreeResourcePath}' en Resources; el menú " +
                                 "del caldero saldrá vacío. Créalo con Assets > Create > RedMagic > " +
                                 "Upgrade Tree.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ consulta

        public int GetLevel(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return 0;
            if (_levels.TryGetValue(nodeId, out var lvl)) return lvl;

            lvl = PlayerPrefs.GetInt(PrefPrefix + nodeId, 0);
            _levels[nodeId] = lvl;
            return lvl;
        }

        public bool IsMaxed(UpgradeTree.Node node) => node != null && GetLevel(node.id) >= node.maxLevel;

        /// <summary>
        /// True si el nodo (fila, columna) está desbloqueado: la columna 0 siempre lo está; el
        /// resto necesita que la columna anterior de SU MISMA FILA tenga al menos nivel 1.
        /// </summary>
        public bool IsUnlocked(int row, int column)
        {
            if (_tree == null) return false;
            if (column <= 0) return true;

            var previous = _tree.NodeAt(row, column - 1);
            return previous != null && GetLevel(previous.id) >= 1;
        }

        /// <summary>Coste del siguiente nivel del nodo, o 0 si ya está al máximo.</summary>
        public int NextCost(UpgradeTree.Node node)
        {
            if (node == null || IsMaxed(node)) return 0;
            return node.CostForNextLevel(GetLevel(node.id));
        }

        /// <summary>
        /// True si se puede comprar el siguiente nivel ahora mismo: desbloqueado, sin llegar al
        /// máximo y con fragmentos de alma suficientes.
        /// </summary>
        public bool CanBuy(int row, int column)
        {
            if (_tree == null) return false;
            var node = _tree.NodeAt(row, column);
            if (node == null || IsMaxed(node) || !IsUnlocked(row, column)) return false;

            var wallet = CurrencyManager.Instance;
            return wallet != null && wallet.Get(Cost) >= NextCost(node);
        }

        // ------------------------------------------------------------------ compra

        /// <summary>
        /// Sube un nivel el nodo (fila, columna) cobrando el coste en fragmentos de alma. Devuelve
        /// false sin tocar nada si no se cumplen las condiciones (<see cref="CanBuy"/>).
        /// </summary>
        public bool TryBuy(int row, int column)
        {
            if (!CanBuy(row, column)) return false;

            var node = _tree.NodeAt(row, column);
            int cost = NextCost(node);

            if (!CurrencyManager.Instance.TrySpend(Cost, cost)) return false;

            int newLevel = GetLevel(node.id) + 1;
            _levels[node.id] = newLevel;
            PlayerPrefs.SetInt(PrefPrefix + node.id, newLevel);
            PlayerPrefs.Save();

            Changed?.Invoke(node.id, newLevel);
            return true;
        }

        // ------------------------------------------------------------------ efecto (aún sin usar)

        /// <summary>
        /// Bono acumulado de todas las mejoras compradas que tocan <paramref name="statId"/>, como
        /// fracción (0.30 = +30 %). Nadie lo llama todavía; es el punto de enganche para cuando el
        /// jugador tenga stats modificables.
        /// </summary>
        public float GetBonus(string statId)
        {
            if (_tree == null || string.IsNullOrEmpty(statId)) return 0f;

            float total = 0f;
            foreach (var node in _tree.Nodes)
            {
                if (node == null || node.statId != statId) continue;
                total += node.bonusPerLevel * GetLevel(node.id);
            }
            return total;
        }
    }
}
