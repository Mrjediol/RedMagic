using System;
using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Componente de vida reutilizable (jugador, enemigos, objetos destructibles).
    /// Dispara eventos al cambiar la vida, al recibir daño y al morir, y lanza los SFX
    /// correspondientes a través de <see cref="AudioManager"/> por id de sonido.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour
    {
        [Header("Vida")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [Tooltip("Si está activo no recibe daño (útil para pruebas).")]
        [SerializeField] private bool invulnerable;

        [Header("SFX — ids de sonido del AudioManager")]
        [Tooltip("id del sonido al recibir daño. Déjalo vacío para no sonar.")]
        [SerializeField] private string hurtSfxId;
        [Tooltip("id del sonido al morir. Déjalo vacío para no sonar.")]
        [SerializeField] private string deathSfxId;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;

        /// <summary>Vida entre 0 y 1, para barras de vida.</summary>
        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        public bool IsDead => currentHealth <= 0f;

        public bool Invulnerable
        {
            get => invulnerable;
            set => invulnerable = value;
        }

        /// <summary>(vidaActual, vidaMáxima). Se dispara con cualquier cambio de vida.</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>Cantidad de daño efectivamente recibido.</summary>
        public event Action<float> Damaged;

        /// <summary>Se dispara una sola vez, al llegar la vida a 0.</summary>
        public event Action Died;

        /// <summary>
        /// Se dispara al llamar a <see cref="ResetHealth"/> mientras se estaba muerto. Es el
        /// complemento de <see cref="Died"/>: un componente que se desactiva permanentemente al
        /// morir (por ejemplo <c>PlayerMovement</c> apagando el control, o <c>PlayerAnimator</c>
        /// dejando el parámetro Dead a true) se suscribe también a este evento para deshacerlo,
        /// en vez de comprobar <see cref="IsDead"/> a cada frame.
        /// </summary>
        public event Action Revived;

        private bool _deathRaised;

        private void Awake()
        {
            if (maxHealth <= 0f) maxHealth = 1f;
            if (currentHealth <= 0f) currentHealth = maxHealth;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        private void Start()
        {
            // Se avisa en Start para que las barras de vida ya estén suscritas.
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead || invulnerable) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);

            Damaged?.Invoke(amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);
            PlaySfx(hurtSfxId);

            if (currentHealth <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>Mata al personaje inmediatamente. Es idempotente.</summary>
        public void Die()
        {
            if (_deathRaised) return;
            _deathRaised = true;

            currentHealth = 0f;
            HealthChanged?.Invoke(currentHealth, maxHealth);
            PlaySfx(deathSfxId);
            Died?.Invoke();
        }

        /// <summary>Resetea la vida al máximo (respawn).</summary>
        public void ResetHealth()
        {
            bool wasDead = _deathRaised;

            _deathRaised = false;
            currentHealth = maxHealth;
            HealthChanged?.Invoke(currentHealth, maxHealth);

            // Sólo si de verdad venía de estar muerto: entrar al hub sano ya sin haber muerto no
            // debería disparar Revived de la nada.
            if (wasDead) Revived?.Invoke();
        }

        private static void PlaySfx(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(id);
        }
    }
}
