using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Test
{
    /// <summary>
    /// Para escenas de prueba: cura al jugador a tope cuando lleva <see cref="delay"/> segundos sin
    /// recibir daño, así se puede fallar parrys sin acabar muerto y de vuelta al hub. Fallar se sigue
    /// notando (el golpe entra, parpadea y salta el número).
    /// </summary>
    public class TestAutoHeal : MonoBehaviour
    {
        [Tooltip("Segundos sin recibir daño antes de curar a tope.")]
        [Min(0f)] [SerializeField] private float delay = 2f;

        private Health _player;

        private void Update()
        {
            if (_player == null)
            {
                var go = GameObject.FindWithTag(Teams.PlayerTag);
                if (go == null || !go.TryGetComponent(out _player)) return;
            }

            if (_player.IsDead || _player.CurrentHealth >= _player.MaxHealth) return;
            if (Time.time - _player.LastDamagedTime < delay) return;
            _player.Heal(_player.MaxHealth);
        }
    }
}
