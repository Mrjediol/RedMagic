using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Números de la progresión del arma en la run (<see cref="WeaponRunProgress"/>), en
    /// <c>Resources/WeaponProgressionConfig.asset</c>. La parte propia de Supervivencia (rondas con evolución, portadores
    /// por ronda) está en su <c>SurvivalConfig</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Armas/Progresión en la run", fileName = "WeaponProgressionConfig")]
    public class WeaponProgressionConfig : ScriptableObject
    {
        [Tooltip("Las armas empiezan cada partida en su forma base (la evolución y las mejoras se ganan jugando). Apagado = la " +
                 "evolución y las mejoras puestas en el asset del arma (como antes, para probar una forma concreta).")]
        public bool startAtBase = true;

        [Header("Elegir")]
        [Tooltip("Cuántas de las evoluciones del arma se ofrecen (al azar) en cada Prisma.")]
        [Min(1)] public int offerEvolutions = 2;
        [Tooltip("Cuántas mejoras se ofrecen (al azar, de las que se pueden conseguir) con cada Núcleo.")]
        [Min(1)] public int offerUpgrades = 2;
        [Tooltip("Fragmentos de alma (moneda de mejoras permanentes) que suelta un portador cuando tus dos armas ya no pueden " +
                 "subir más (evolucionadas y con todas sus mejoras desbloqueadas).")]
        [Min(0)] public int soulsInsteadOfCore = 30;

        [Header("Portadores de núcleo")]
        [Tooltip("Clásico: probabilidad de que un enemigo normal salga de portador (sin contar la facilidad de prueba).")]
        [Range(0f, 1f)] public float carrierChancePerEnemy = 0.012f;
        [Tooltip("FACILIDAD DE PRUEBA: multiplica la probabilidad de portadores (clásico y Supervivencia). 1 = la de verdad.")]
        [Min(0f)] public float testEase = 8f;
        [Tooltip("Segundos que vive un portador antes de escaparse.")]
        [Min(1f)] public float carrierLifetime = 20f;
        [Tooltip("Los últimos segundos parpadea (se va a escapar).")]
        [Min(0f)] public float carrierBlink = 5f;
        [Tooltip("Aspecto y fuerza del portador (una variante de enemigo).")]
        public Enemies.EnemyVariant carrierLook;
        [Tooltip("Shader de las variantes (RedMagic/Sprite Lit FX).")]
        public Shader variantShader;
        [Tooltip("Material aditivo del halo que late detrás del portador (Fx_SpriteAdditive).")]
        public Material glowMaterial;
        [Tooltip("Color del halo y de la columna de luz del portador.")]
        public Color carrierGlow = new Color(1f, 0.92f, 0.55f, 1f);

        [Header("Núcleo (lo que suelta)")]
        public Sprite coreSprite;
        [Min(0.1f)] public float coreSize = 1f;
        [Tooltip("Se va hacia el jugador cuando está a esta distancia.")]
        [Min(0f)] public float coreMagnetRadius = 4f;

        private static WeaponProgressionConfig _instance;

        public static WeaponProgressionConfig Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<WeaponProgressionConfig>("WeaponProgressionConfig");
                if (_instance == null)
                {
                    Debug.LogWarning("[Arsenal] No se carga Resources/WeaponProgressionConfig: se usan valores por defecto (sin aspecto de portador ni icono de núcleo).");
                    _instance = CreateInstance<WeaponProgressionConfig>();
                }
                return _instance;
            }
        }
    }
}
