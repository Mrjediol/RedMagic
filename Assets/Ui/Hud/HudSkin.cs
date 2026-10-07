using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.UI
{
    /// <summary>
    /// El arte del HUD (<c>Resources/HudSkin.asset</c>), cableado por <c>HudUiPack</c> desde
    /// <c>Assets/Ui/HudSprites/</c>. Sólo arte fijo: lo que se mueve (relleno, barrido de cooldown,
    /// destellos) lo sigue pintando el código, dentro de estos marcos.
    ///
    /// Cada pieza es opcional: la que falte se queda con el aspecto vectorial de <see cref="HudStyle"/>,
    /// así que borrar el asset sólo quita el arte, nunca rompe el HUD. Los números (escalas, márgenes)
    /// se ajustan en el Inspector y sobreviven a volver a procesar el kit.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/UI/HUD Skin", fileName = "HudSkin")]
    public class HudSkin : ScriptableObject
    {
        public const string ResourcePath = "HudSkin";

        [Header("Barra de vida del jugador (marco hueco, va DELANTE del relleno)")]
        public UiFrame healthFrame = new();

        [Tooltip("Grosor del aro respecto al alto de la barra: el relleno se mete esto por dentro para " +
                 "quedar bajo el aro, no fuera de él.")]
        [Range(0f, 0.4f)] public float healthRim = 0.1f;

        [Header("Barras de enemigo (las tres: flotante, foco y lista)")]
        public UiFrame enemyFrame = new();

        [Range(0f, 0.4f)] public float enemyRim = 0.1f;

        [Header("Casillas de habilidad")]
        [Tooltip("normal = aro apagado (recargando), hover = aro encendido (lista).")]
        public UiStateSprites abilityRing = new();

        [Tooltip("Grosor del aro respecto al diámetro: el barrido del cooldown se dibuja por dentro.")]
        [Range(0f, 0.3f)] public float abilityRingInset = 0.08f;

        [Tooltip("Cuánto se sale el sprite del aro de la casilla por cada lado (fracción del lado): los dos estados " +
                 "reservan sitio para el halo del encendido, así que el aro en sí es más pequeño que el sprite.")]
        [Range(0f, 0.3f)] public float abilityRingOverflow = 0.01f;

        public Sprite iconAttack;
        public Sprite iconDash;
        public Sprite iconParry;

        [Header("Monedas")]
        public UiFrame currencyPlaque = new();

        [Header("Chuleta de controles")]
        public UiFrame keyCap = new();

        /// <summary>Icono de una casilla, o null si no hay arte para ella.</summary>
        public Sprite IconFor(CooldownSlot slot) => slot switch
        {
            CooldownSlot.Attack => iconAttack,
            CooldownSlot.Dash => iconDash,
            CooldownSlot.Parry => iconParry,
            _ => null,
        };

        private static HudSkin _current;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            _loaded = false;
        }

        /// <summary>El skin de <c>Resources</c>, o null si no existe (aspecto vectorial).</summary>
        public static HudSkin Current
        {
            get
            {
                if (_loaded) return _current;
                _loaded = true;
                _current = Resources.Load<HudSkin>(ResourcePath);
                return _current;
            }
        }
    }
}
