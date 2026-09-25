using System.Collections;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Fx;
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

        [Tooltip("Segundos desde que se lanzó antes de que pueda volver a salir en el sorteo. Se " +
                 "suma a 'cooldownInAttacks' (tienen que cumplirse los dos). 0 = sin espera por tiempo.")]
        [Min(0f)]
        [SerializeField] private float cooldownSeconds;

        [Header("Fases")]
        [Tooltip("Fase (1 = la primera) a partir de la cual este ataque puede salir en el sorteo. " +
                 "Por debajo nunca se elige, aunque esté en la baraja de esa fase. 1 = siempre.")]
        [Min(1)]
        [SerializeField] private int minPhase = 1;

        [Header("Daño")]
        [Min(0f)]
        [SerializeField] private float damage = 18f;

        [Tooltip("Cuánto empuja: multiplica el retroceso configurado en el Knockback de la víctima.")]
        [Min(0f)]
        [SerializeField] private float knockbackMultiplier = 1f;

        [Header("Castigo")]
        [Tooltip("Segundos que el jefe queda EXPUESTO al acabar este ataque, durante su " +
                 "recuperación. 0 = no abre ventana.\n\n" +
                 "Es lo que convierte 'esquiva y espera' en 'esquiva y castiga': con un jefe " +
                 "acorazado (la armadura de la fase), pegarle fuera de la ventana casi no vale, " +
                 "así que el ataque más peligroso pasa a ser también la oportunidad.")]
        [Min(0f)]
        [SerializeField] private float vulnerableSeconds;

        [Tooltip("Cuánto multiplica el daño recibido durante la ventana, por encima de la " +
                 "armadura de la fase.")]
        [Min(1f)]
        [SerializeField] private float vulnerableMultiplier = 2.5f;

        [Header("Gesto")]
        [Tooltip("Estado del Animator del jefe que hace de aviso de este ataque ('Charge', 'Slam'…). " +
                 "Vacío = sin gesto: el aviso es sólo el aura y las marcas del ataque.\n\n" +
                 "Con gesto (y un BossAnimator en el jefe) el cuerpo ES el aviso: el clip se acelera " +
                 "o frena para que su frame de suelta — el evento OnAttackRelease que el pipeline " +
                 "planta en el 'releaseFrame' de esa fila — caiga justo al acabar 'telegraph', y el " +
                 "ataque arranca en ese frame exacto. 'telegraph' sigue siendo el único mando de " +
                 "tiempo, así que la fase 2, al acortarlo con speedScale, acelera también el gesto.")]
        [SerializeField] private string gesture;

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
        /// <summary>
        /// Segundos de aviso. Virtual para el arquetipo que expone su tiempo con otro nombre
        /// (<see cref="QuakeSlamAttack"/>: "segundos hasta el impacto").
        /// </summary>
        public virtual float Telegraph => telegraph;
        public float Recovery => recovery;
        public float Weight => weight;
        public int CooldownInAttacks => cooldownInAttacks;
        public float CooldownSeconds => cooldownSeconds;
        public int MinPhase => minPhase;

        /// <summary>Si puede salir en el sorteo de la fase <paramref name="phaseIndex"/> (0 = la primera).</summary>
        public bool AvailableInPhase(int phaseIndex) => phaseIndex >= minPhase - 1;
        public float VulnerableSeconds => vulnerableSeconds;
        public float VulnerableMultiplier => vulnerableMultiplier;
        public string Gesture => gesture;

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
            EmitWarn(ctx, position, size, color, duration, 0f, 1.15f);
        }

        /// <summary>
        /// Pinta el aviso: por el prefab de aviso del jefe (<see cref="BossController.WarnPrefab"/>,
        /// editable para meter arte real) si lo tiene, o por el cuadrado pooled de
        /// <see cref="AbilityFx.Flash"/> si no.
        /// </summary>
        private static void EmitWarn(in BossContext ctx, Vector2 position, Vector2 size, Color color,
                                     float duration, float rotationDegrees, float growTo)
        {
            var prefab = ctx.Boss != null ? ctx.Boss.WarnPrefab : null;
            float d = Mathf.Max(0.05f, duration);

            if (prefab != null)
                FxTelegraph.Spawn(prefab, position, size, color, d, rotationDegrees, growTo, ctx.Ability.Caster);
            else
                AbilityFx.Flash(ctx.FxSprite, position, size, color, d, rotationDegrees, growTo, ctx.Ability.Caster);
        }

        /// <summary>
        /// Marca de aviso <b>girada</b> y con intensidad regulable. Hace falta para los avisos que
        /// no son cajas rectas (el filo de una guadaña) y para poder pintar la misma marca fuerte o
        /// tenue — una recta de salida bien visible y el resto del recorrido apenas insinuado.
        /// </summary>
        protected void Warn(in BossContext ctx, Vector2 position, Vector2 size, float duration,
                            float rotationDegrees, float strength)
        {
            var color = ctx.Accent;
            color.a = 0.55f * Mathf.Clamp01(strength);
            EmitWarn(ctx, position, size, color, duration, rotationDegrees, 1f);
        }

        /// <summary>
        /// Aviso de <b>área en el suelo</b>: un círculo (elipse vista de lado) centrado en
        /// <paramref name="groundPoint"/> que cubre <paramref name="radius"/>. Sale del
        /// <see cref="BossController.WarnCirclePrefab"/> del jefe (arte propio, escalado al diámetro);
        /// sin él, una franja plana del mismo ancho pegada al suelo.
        /// </summary>
        protected void WarnCircle(in BossContext ctx, Vector2 groundPoint, float radius, float duration,
                                  float strength = 1f)
        {
            var color = ctx.Accent;
            color.a = 0.55f * Mathf.Clamp01(strength);
            float d = Mathf.Max(0.05f, duration);
            float diameter = Mathf.Max(0.1f, radius * 2f);

            var prefab = ctx.Boss != null ? ctx.Boss.WarnCirclePrefab : null;
            if (prefab != null)
            {
                FxTelegraph.Spawn(prefab, groundPoint, new Vector2(diameter, radius), color, d, 0f, 1f,
                                  ctx.Ability.Caster);
                return;
            }

            EmitWarn(ctx, groundPoint + Vector2.up * 0.2f, new Vector2(diameter, 0.4f), color, d, 0f, 1f);
        }

        /// <summary>
        /// Aviso de <b>trayectoria</b>: una flecha desde <paramref name="from"/> en
        /// <paramref name="direction"/> de <paramref name="length"/> unidades (una embestida, una
        /// carga). Sale del <see cref="BossController.WarnArrowPrefab"/> del jefe (arte mirando +X,
        /// escalado al largo); sin él, una barra girada del mismo largo.
        /// </summary>
        protected void WarnArrow(in BossContext ctx, Vector2 from, Vector2 direction, float length,
                                 float width, float duration, float strength = 1f)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = new Vector2(ctx.Facing, 0f);
            direction.Normalize();

            var color = ctx.Accent;
            color.a = 0.55f * Mathf.Clamp01(strength);
            float d = Mathf.Max(0.05f, duration);
            float len = Mathf.Max(0.1f, length);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector2 center = from + direction * (len * 0.5f);

            var prefab = ctx.Boss != null ? ctx.Boss.WarnArrowPrefab : null;
            if (prefab != null)
            {
                FxTelegraph.Spawn(prefab, center, new Vector2(len, width), color, d, angle, 1f, ctx.Ability.Caster);
                return;
            }

            EmitWarn(ctx, center, new Vector2(len, Mathf.Max(0.1f, width)), color, d, angle, 1f);
        }

        /// <summary>
        /// Marca en un color propio en vez del de la fase. Sólo para lo que significa lo contrario
        /// que el resto de avisos: un <b>sitio seguro</b> no puede pintarse del mismo color que lo
        /// que hace daño.
        /// </summary>
        protected static void Mark(in BossContext ctx, Vector2 position, Vector2 size, Color color,
                                   float duration, float growTo = 1f)
        {
            EmitWarn(ctx, position, size, color, duration, 0f, growTo);
        }
    }
}
