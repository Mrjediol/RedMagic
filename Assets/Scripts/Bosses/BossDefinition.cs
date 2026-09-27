using System;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Una fase del combate: qué ataques hay en la baraja, a qué ritmo salen y cómo pegan.
    ///
    /// Las fases se leen <b>en orden</b> y una entra cuando la vida normalizada del jefe baja de
    /// <see cref="startsAtHealth"/>. La primera debe estar a 1.
    /// </summary>
    [Serializable]
    public class BossPhase
    {
        [Tooltip("Nombre de la fase (sólo para el Inspector y los logs).")]
        public string displayName = "Fase";

        [Tooltip("Vida normalizada a partir de la cual manda esta fase. La primera fase va a 1.")]
        [Range(0f, 1f)]
        public float startsAtHealth = 1f;

        [Tooltip("Baraja de ataques de la fase. Se sortean por peso, sin repetir de inmediato.")]
        public BossAttack[] attacks = Array.Empty<BossAttack>();

        [Tooltip("Espera mínima y máxima entre ataques, además de la recuperación de cada uno.")]
        public Vector2 pauseBetweenAttacks = new Vector2(0.9f, 1.6f);

        [Tooltip("Multiplica el daño de todos los ataques de la fase.")]
        [Min(0.01f)]
        public float damageScale = 1f;

        [Tooltip("Multiplica el daño que RECIBE el jefe durante la fase. 1 = normal; por debajo " +
                 "de 1 va acorazado y sólo se le hace daño de verdad en las ventanas de castigo " +
                 "que abren sus ataques (BossAttack.vulnerableSeconds).")]
        [Min(0f)]
        public float damageTakenMultiplier = 1f;

        [Tooltip("Multiplica el ritmo: 1.3 = todos los avisos, oleadas y recuperaciones un 30% más " +
                 "rápidos. Es lo que convierte la fase 2 en la misma baraja pero agobiante.")]
        [Min(0.1f)]
        public float speedScale = 1f;

        [Tooltip("Color de la fase: tiñe al jefe, sus avisos y sus proyectiles.")]
        public Color accent = new Color(0.55f, 0.9f, 0.4f, 1f);

        [Header("Entrada en la fase")]
        [Tooltip("Segundos de transición al entrar. El jefe es invulnerable y no ataca: es el " +
                 "'rugido' que avisa de que la cosa cambia.")]
        [Min(0f)]
        public float transitionSeconds = 1.8f;

        [Tooltip("Sacudida de cámara de la transición.")]
        [Min(0f)]
        public float transitionShake = 0.5f;

        [Tooltip("id de sonido del AudioManager al entrar en la fase. Vacío = sin sonido.")]
        public string transitionSfxId;

        [Tooltip("Efecto de un solo uso (pooled, con VfxOneShot) que estalla a los pies del jefe al " +
                 "entrar en la fase. Vacío = sólo aura, sacudida y sonido.")]
        public GameObject transitionFx;

        [Tooltip("Ataque que el jefe lanza UNA sola vez al acabar la transición (tras la " +
                 "invulnerabilidad y la sacudida), antes de volver a su baraja. No entra en el " +
                 "sorteo. Vacío = la fase empieza directamente con la baraja.")]
        public BossAttack openingAttack;

        [Header("Frenesí")]
        [Tooltip("Por debajo de esta vida normalizada la fase acelera aún más. 0 = nunca.")]
        [Range(0f, 1f)]
        public float frenzyBelowHealth;

        [Tooltip("Ritmo extra mientras dura el frenesí (se multiplica sobre speedScale).")]
        [Min(1f)]
        public float frenzySpeedScale = 1.35f;
    }

    /// <summary>
    /// Un jefe entero como asset: su nombre, sus fases y sus barajas de ataques.
    ///
    /// Va en <c>Assets/Resources/Bosses/</c> por convención con el resto del contenido del
    /// proyecto (habilidades, armas, objetos), aunque nada lo carga por nombre: el prefab del jefe
    /// referencia el asset directamente en su <see cref="BossController"/>.
    ///
    /// Un jefe nuevo es un asset nuevo: no hay lista ni registro que tocar, y ningún código
    /// menciona a ninguno en concreto.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Boss Definition", fileName = "Boss_Nuevo")]
    public class BossDefinition : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Prefijo de sus textos en los ficheros de idioma (<prefijo>.name / .title / .description). " +
                 "Lo rellena Tools ▸ RedMagic ▸ Localización ▸ Sincronizar textos de assets.")]
        [SerializeField] private string textKey;

        [SerializeField] private string displayName = "Jefe";

        [Tooltip("Epíteto que sale bajo el nombre en la barra de vida.")]
        [SerializeField] private string title;

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Fases")]
        [Tooltip("En orden, de más vida a menos. La primera debe empezar en 1.")]
        [SerializeField] private BossPhase[] phases = Array.Empty<BossPhase>();

        public string DisplayName => Loc.ForAsset(textKey, "name", string.IsNullOrWhiteSpace(displayName) ? name : displayName);
        public string Title => Loc.ForAsset(textKey, "title", title);
        public string Description => Loc.ForAsset(textKey, "description", description);
        public BossPhase[] Phases => phases;
        public int PhaseCount => phases != null ? phases.Length : 0;

        public BossPhase GetPhase(int index)
        {
            if (phases == null || phases.Length == 0) return null;
            return phases[Mathf.Clamp(index, 0, phases.Length - 1)];
        }

        /// <summary>
        /// Índice de la fase que corresponde a <paramref name="normalizedHealth"/>. Las fases nunca
        /// van hacia atrás (curarse no devuelve al jefe a la fase anterior), de eso se encarga el
        /// controlador quedándose siempre con el índice mayor.
        /// </summary>
        public int PhaseIndexFor(float normalizedHealth)
        {
            if (phases == null || phases.Length == 0) return 0;

            int index = 0;
            for (int i = 0; i < phases.Length; i++)
                if (normalizedHealth <= phases[i].startsAtHealth) index = i;

            return index;
        }

        private void OnValidate()
        {
            if (phases == null || phases.Length == 0) return;

            // La primera fase tiene que cubrir al jefe a vida llena o el combate empezaría sin
            // ninguna fase activa y el jefe se quedaría plantado sin atacar.
            phases[0].startsAtHealth = 1f;

            // Una fase a 0 es un jefe al que no se le puede quitar vida: nunca es lo que alguien
            // quiere, y como los elementos de array nacen a ceros es un fallo fácil de colar sin
            // enterarse. Se repara y se avisa en vez de dejar un combate imposible de ganar.
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i] == null || phases[i].damageTakenMultiplier > 0f) continue;

                phases[i].damageTakenMultiplier = 1f;
                Debug.LogWarning($"[Boss] '{name}': la fase '{phases[i].displayName}' recibía 0 de " +
                                 $"daño (invencible). Se ha puesto a 1.", this);
            }
        }
    }
}
