using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Suelta moneda al morir. Se pone en cualquier cosa con <see cref="Health"/> (enemigos,
    /// jefes) y escucha su evento <c>Died</c> — igual que <c>RunManager</c> escucha la muerte del
    /// jugador, sin que <see cref="Health"/> sepa nada de economía.
    ///
    /// El <see cref="tier"/> elige qué fila de la tabla de drops del <c>CurrencyConfig</c> se usa;
    /// las cantidades concretas se balancean ahí, no aquí.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class CurrencyDropper : MonoBehaviour
    {
        [Tooltip("Categoría a efectos de botín: Basic (común), Elite o Boss. Los rangos de cada " +
                 "una se editan en el asset CurrencyConfig de Resources.")]
        [SerializeField] private EnemyTier tier = EnemyTier.Basic;

        private Health _health;
        private bool _dropped;

        private void Awake() => _health = GetComponent<Health>();

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void OnDied()
        {
            // Health.Die() es idempotente pero curarse y volver a morir no debería duplicar botín.
            if (_dropped) return;
            _dropped = true;

            // Marca de oro: el marcado suelta su botín multiplicado (la marca aún está puesta: se quita
            // después de este Died).
            CurrencyManager.Instance?.GrantDrops(tier, GoldMarkStatus.DropMultiplierOf(_health));
        }
    }
}
