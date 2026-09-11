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
        [Tooltip("Si está activo no recibe daño nunca (útil para pruebas).")]
        [SerializeField] private bool invulnerable;

        [Tooltip("Multiplica TODO el daño que recibe este personaje. 1 = normal, 0.35 = acorazado, " +
                 "2.5 = expuesto. Es un solo número porque el daño entra por un único sitio; quien " +
                 "golpea no tiene que saber nada de armaduras.")]
        [Min(0f)]
        [SerializeField] private float damageMultiplier = 1f;

        [Header("Invulnerabilidad tras el golpe (i-frames)")]
        [Tooltip("Segundos de invulnerabilidad justo después de recibir daño. Evita que dos " +
                 "enemigos, o un contacto y un proyectil, cuenten como dos o tres golpes en el " +
                 "mismo instante. 0 = sin i-frames (cada impacto hace daño).")]
        [Min(0f)]
        [SerializeField] private float invulnerabilityDuration = 0.15f;

        [Tooltip("Usar tiempo sin escalar, para que los i-frames no se congelen con el juego en pausa.")]
        [SerializeField] private bool invulnerabilityUsesUnscaledTime;

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

        /// <summary>
        /// Multiplicador de daño recibido. Lo mueven en caliente los estados de un combate: la
        /// armadura de una fase de jefe, o su ventana de castigo tras un ataque pesado.
        ///
        /// Vive aquí y no en el atacante porque <see cref="Health"/> es el único sitio donde se
        /// aplica daño: así el número que ve el jugador en el popup ya es el real, y ninguna arma,
        /// habilidad o ataque de jefe necesita saber que existen armaduras.
        /// </summary>
        public float DamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>
        /// True si ahora mismo no se puede hacer daño: por la casilla <see cref="Invulnerable"/>
        /// o porque siguen corriendo los i-frames del último golpe.
        /// </summary>
        public bool IsInvulnerable => invulnerable || _invulnerabilityTimer > 0f;

        /// <summary>Segundos de i-frames que quedan (0 si no hay).</summary>
        public float InvulnerabilityRemaining => Mathf.Max(0f, _invulnerabilityTimer);

        /// <summary>(vidaActual, vidaMáxima). Se dispara con cualquier cambio de vida.</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>Cantidad de daño efectivamente recibido.</summary>
        public event Action<float> Damaged;

        /// <summary>
        /// Versión global de <see cref="Damaged"/>: <c>(quién lo recibe, cuánto)</c> para
        /// <b>cualquier</b> <see cref="Health"/>. Un oyente único (números de daño flotantes, feed
        /// de combate…) se suscribe una sola vez y cubre a todos los personajes sin escanear la
        /// escena ni enganchar cada instancia. Se dispara junto a <see cref="Damaged"/>.
        /// </summary>
        public static event Action<Health, float> AnyDamaged;

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

        /// <summary>
        /// Cambio del estado de i-frames (true al empezar, false al acabar). Lo puede escuchar un
        /// parpadeo del sprite para que se vea que el golpe no cuenta.
        /// </summary>
        public event Action<bool> InvulnerabilityChanged;

        private bool _deathRaised;
        private float _invulnerabilityTimer;
        private Knockback _knockback;

        private void Awake()
        {
            _knockback = GetComponent<Knockback>();

            if (maxHealth <= 0f) maxHealth = 1f;
            if (currentHealth <= 0f) currentHealth = maxHealth;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        private void Start()
        {
            // Se avisa en Start para que las barras de vida ya estén suscritas.
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Update()
        {
            if (_invulnerabilityTimer <= 0f) return;

            _invulnerabilityTimer -= invulnerabilityUsesUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            if (_invulnerabilityTimer <= 0f)
            {
                _invulnerabilityTimer = 0f;
                InvulnerabilityChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Aplica daño y arranca los i-frames.
        ///
        /// <b>Devuelve true sólo si el golpe ha entrado de verdad</b> (no estaba muerto, ni
        /// invulnerable, ni era daño 0). Quien ataque debe mirar ese valor antes de aplicar
        /// retroceso o efectos: así un golpe comido por los i-frames tampoco empuja.
        ///
        /// Esta sobrecarga no empuja. Para que el golpe además haga retroceder, usa
        /// <see cref="TakeDamage(float, Vector2, float)"/> pasando de dónde viene el golpe.
        /// </summary>
        public bool TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead || IsInvulnerable) return false;

            // La armadura se aplica aquí, antes de nada más, para que todo lo que viene después
            // —la vida, los eventos, el número flotante— hable del daño que de verdad ha entrado.
            // Con multiplicador 0 el golpe no entra, así que tampoco empuja ni gasta i-frames.
            amount *= damageMultiplier;
            if (amount <= 0f) return false;

            currentHealth = Mathf.Max(0f, currentHealth - amount);

            if (invulnerabilityDuration > 0f)
            {
                _invulnerabilityTimer = invulnerabilityDuration;
                InvulnerabilityChanged?.Invoke(true);
            }

            Damaged?.Invoke(amount);
            AnyDamaged?.Invoke(this, amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);
            PlaySfx(hurtSfxId);

            if (currentHealth <= 0f) Die();
            return true;
        }

        /// <summary>
        /// Aplica daño y, si el golpe entra, el retroceso configurado en el <see cref="Knockback"/>
        /// de este mismo objeto, alejándose de <paramref name="sourcePosition"/> (la posición del
        /// atacante, del proyectil o del cuerpo con el que se ha chocado).
        ///
        /// El empujón se decide aquí, y no en cada atacante, por dos razones: el golpe absorbido
        /// por los i-frames tampoco empuja sin que nadie tenga que acordarse de comprobarlo, y la
        /// fuerza la afina la víctima en su propio prefab. <paramref name="knockbackMultiplier"/>
        /// es lo único que aporta el atacante: 1 = el empujón normal de la víctima, 0 = ninguno.
        ///
        /// Sin componente <see cref="Knockback"/> se comporta exactamente igual que
        /// <see cref="TakeDamage(float)"/>, así que es seguro llamarla contra cualquier cosa.
        /// </summary>
        public bool TakeDamage(float amount, Vector2 sourcePosition, float knockbackMultiplier = 1f)
        {
            if (!TakeDamage(amount)) return false;

            // Al morir no se empuja: el cadáver apaga su física (y su control, si es el jugador),
            // así que el empujón no se vería y sí dejaría un temporizador corriendo.
            if (!IsDead && _knockback != null) _knockback.ApplyFrom(sourcePosition, knockbackMultiplier);
            return true;
        }

        /// <summary>
        /// Cambia la vida máxima en caliente. Hace falta para lo que se construye en código y se
        /// reutiliza desde un pool (las anclas de un jefe, por ejemplo): el prefab no puede traer
        /// el número puesto porque no hay prefab.
        /// </summary>
        public void SetMaxHealth(float value, bool healToFull)
        {
            maxHealth = Mathf.Max(1f, value);
            if (healToFull) currentHealth = maxHealth;
            else currentHealth = Mathf.Min(currentHealth, maxHealth);

            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Duración de los i-frames en caliente. Lo usa <c>EnemyStats</c>, que centraliza en un
        /// solo componente todo lo que se afina de un enemigo y lo reparte a los compartidos.
        /// </summary>
        public void SetInvulnerabilityDuration(float value) => invulnerabilityDuration = Mathf.Max(0f, value);

        /// <summary>Ids de sonido en caliente. Vacío = sin sonido, como el valor por defecto.</summary>
        public void SetSfx(string hurt, string death)
        {
            hurtSfxId = hurt ?? string.Empty;
            deathSfxId = death ?? string.Empty;
        }

        /// <summary>
        /// Resta vida sin que sea un golpe: sin i-frames, sin sonido y sin <see cref="Damaged"/> (que
        /// dispara la animación de daño y el parpadeo). Para pérdidas continuas — un item maldito,
        /// un veneno. Sí sale el número flotante (<see cref="AnyDamaged"/>). Con
        /// <paramref name="canKill"/> false nunca baja de 1.
        /// </summary>
        public bool Drain(float amount, bool canKill = true)
        {
            if (amount <= 0f || IsDead) return false;

            float floor = canKill ? 0f : Mathf.Min(1f, currentHealth);
            float next = Mathf.Max(floor, currentHealth - amount);
            float lost = currentHealth - next;
            if (lost <= 0f) return false;

            currentHealth = next;
            AnyDamaged?.Invoke(this, lost);
            HealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f) Die();
            return true;
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

            if (_invulnerabilityTimer > 0f)
            {
                _invulnerabilityTimer = 0f;
                InvulnerabilityChanged?.Invoke(false);
            }
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
