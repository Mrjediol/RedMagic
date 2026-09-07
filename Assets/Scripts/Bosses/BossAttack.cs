using System.Collections;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Un patrón de ataque de jefe, como <b>asset</b>. Misma idea que
    /// <see cref="AbilityDefinition"/>: el asset son números y el código sólo describe la
    /// <i>forma</i> del ataque, así que un jefe nuevo (o una variante de fase 2) es duplicar un
    /// asset y cambiar valores — escribir C# sólo hace falta para un arquetipo nuevo.
    ///
    /// El ciclo que ejecuta <see cref="BossController"/> por ataque es siempre el mismo y es lo que
    /// hace el combate legible: <b>aviso → ataque → recuperación</b>.
    ///  - <see cref="Telegraph"/>: el jefe se tiñe con <see cref="Accent"/> y el ataque puede pintar
    ///    su propio aviso (marcas en el suelo, líneas). Nada hace daño todavía.
    ///  - <see cref="Run"/>: el ataque en sí.
    ///  - <see cref="Recovery"/>: el jefe se queda quieto. Es la ventana de daño del jugador, y por
    ///    eso los ataques más devastadores llevan la recuperación más larga.
    /// </summary>
    public abstract class BossAttack : ScriptableObject
    {
        [Header("Identidad")]
        [SerializeField] private string displayName = "Ataque";

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Tooltip("Color del aviso y de los efectos de este ataque.")]
        [SerializeField] private Color accent = new Color(0.55f, 0.9f, 0.4f, 1f);

        [Header("Ritmo")]
        [Tooltip("Segundos de aviso antes de que el ataque haga daño. Es lo que lo hace esquivable: " +
                 "súbelo si el patrón es difícil de leer, bájalo para agobiar.")]
        [Min(0f)]
        [SerializeField] private float telegraph = 0.8f;

        [Tooltip("Segundos que el jefe se queda quieto al terminar. Es la ventana de daño del " +
                 "jugador: cuanto más brutal el ataque, más larga.")]
        [Min(0f)]
        [SerializeField] private float recovery = 1.1f;

        [Tooltip("Peso en el sorteo del siguiente ataque dentro de la fase.")]
        [Min(0.01f)]
        [SerializeField] private float weight = 1f;

        [Tooltip("Ataques que tienen que pasar antes de que este pueda repetirse. 1 = nunca dos " +
                 "seguidos. Evita que el sorteo encadene tres veces el mismo patrón.")]
        [Min(0)]
        [SerializeField] private int cooldownInAttacks = 1;

        [Header("Daño")]
        [Min(0f)]
        [SerializeField] private float damage = 18f;

        [Tooltip("Cuánto empuja: multiplica el retroceso configurado en el Knockback de la víctima.")]
        [Min(0f)]
        [SerializeField] private float knockbackMultiplier = 1f;

        [Header("Presencia")]
        [Tooltip("id de sonido del AudioManager al lanzar el ataque. Vacío = sin sonido.")]
        [SerializeField] private string sfxId;

        [Tooltip("Sacudida de cámara al lanzar el ataque. 0 = ninguna.")]
        [Min(0f)]
        [SerializeField] private float shakeAmplitude;

        [Min(0f)]
        [SerializeField] private float shakeDuration = 0.25f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Color Accent => accent;
        public float Telegraph => telegraph;
        public float Recovery => recovery;
        public float Weight => weight;
        public int CooldownInAttacks => cooldownInAttacks;

        protected float Damage => damage;
        protected float KnockbackMultiplier => knockbackMultiplier;

        /// <summary>
        /// Ejecuta el ataque. Se lanza como corrutina en el <see cref="BossController"/>, así que
        /// puede durar varios segundos y encadenar oleadas; el controlador la corta en seco si el
        /// jefe muere o cambia de fase a mitad.
        /// </summary>
        public abstract IEnumerator Run(BossContext ctx);

        /// <summary>
        /// Aviso propio del ataque (marcas en el suelo, línea de barrido). Se llama al empezar el
        /// telegrafiado y no debe hacer daño. El tinte del jefe lo pone el controlador.
        /// </summary>
        public virtual void OnTelegraph(BossContext ctx)
        {
        }

        /// <summary>Resumen de una línea para el Inspector y para depurar.</summary>
        public virtual string ShortStats() => $"{damage:0} dmg · aviso {telegraph:0.00}s · recup. {recovery:0.00}s";

        // ------------------------------------------------------------------ utilidades

        /// <summary>Daño ya escalado por la fase. Úsalo al pasar daño a un proyectil.</summary>
        protected float ScaledDamage(in BossContext ctx) => damage * ctx.Ability.DamageScale;

        /// <summary>Sonido del ataque y sacudida de cámara, si están configurados.</summary>
        protected void Impact()
        {
            if (!string.IsNullOrWhiteSpace(sfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(sfxId);

            if (shakeAmplitude > 0f) CameraFollow.ShakeAll(shakeAmplitude, shakeDuration);
        }

        /// <summary>
        /// Marca de aviso en el suelo (o donde se le diga): un destello que crece y se apaga justo
        /// cuando el ataque entra. Va por el pool de <see cref="AbilityFx.Flash"/>.
        /// </summary>
        protected void Warn(in BossContext ctx, Vector2 position, Vector2 size, float duration)
        {
            var color = ctx.Accent;
            color.a = 0.55f;
            AbilityFx.Flash(ctx.FxSprite, position, size, color, Mathf.Max(0.05f, duration), 0f, 1.15f,
                            ctx.Ability.Caster);
        }
    }
}
