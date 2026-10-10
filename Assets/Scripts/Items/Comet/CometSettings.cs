using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Qué hace el cometa al encontrarse con enemigos. Sólo uno a la vez.</summary>
    public enum CometBehaviour
    {
        /// <summary>Recto e impacta en el primer enemigo.</summary>
        Base,
        /// <summary>Se pega al enemigo, apunta al siguiente cercano y salta (hasta <see cref="CometSettings.chainCount"/> saltos).</summary>
        Chain,
        /// <summary>Atraviesa <see cref="CometSettings.pierceCount"/> enemigos e impacta en el siguiente.</summary>
        Pierce,
        /// <summary>Fija al enemigo más cercano al cursor al disparar y lo persigue.</summary>
        Homing,
    }

    /// <summary>Números de una versión del cometa (base o upgrade), relativos a los stats del arma.</summary>
    [System.Serializable]
    public class CometStats
    {
        [Tooltip("Multiplica el daño base del arma.")]
        [Min(0f)] public float damageMultiplier = 1f;

        [Tooltip("Multiplica la velocidad del disparo (Base Shot ▸ Speed).")]
        [Min(0.1f)] public float speedMultiplier = 1f;

        [Tooltip("Escala del cometa (cabeza, cola y radio de golpe).")]
        [Min(0.1f)] public float sizeScale = 1f;

        [Tooltip("Multiplica el empuje del arma.")]
        [Min(0f)] public float knockbackMultiplier = 1f;

        [Tooltip("Temblor de cámara del impacto final (unidades de mundo). Los impactos intermedios usan una parte.")]
        [Min(0f)] public float shakeAmplitude = 0.08f;

        [Min(0f)] public float shakeDuration = 0.16f;

        public CometStats Clone() => (CometStats)MemberwiseClone();
    }

    /// <summary>
    /// Ajustes del arma «Cometa» (<see cref="ShotDelivery.Comet"/>): el comportamiento elegido, las
    /// opciones generales y los números de la versión base y la de upgrade. Vive en el
    /// <see cref="WeaponDefinition"/>; el menú DEV del juego (<see cref="CometDevMenu"/>) trabaja sobre una
    /// copia en memoria (<see cref="CometRuntime"/>) para no tocar el asset al probar.
    /// </summary>
    [System.Serializable]
    public class CometSettings
    {
        [Header("Comportamiento (sólo uno)")]
        public CometBehaviour behaviour = CometBehaviour.Base;

        [Tooltip("Ataque en cadena: saltos máximos tras el primer enemigo, mientras haya otro dentro del alcance.")]
        [Min(1)] public int chainCount = 3;

        [Tooltip("Ataque en cadena: distancia máxima (unidades) para saltar al siguiente enemigo.")]
        [Min(0.5f)] public float chainRange = 7f;

        [Tooltip("Ataque en cadena: segundos que el cometa se queda pegado antes de salir hacia el siguiente.")]
        [Min(0.02f)] public float chainStickTime = 0.2f;

        [Tooltip("Perforación: enemigos que atraviesa. 1 = atraviesa al primero e impacta en el segundo.")]
        [Min(1)] public int pierceCount = 1;

        [Tooltip("Homing: radio alrededor del cursor en el que busca al enemigo que fija.")]
        [Min(0.5f)] public float homingCursorRadius = 8f;

        [Tooltip("Homing: grados por segundo que puede girar persiguiendo.")]
        [Min(0f)] public float homingTurnRate = 320f;

        [Header("General")]
        [Tooltip("Mejora: el cometa sale hacia el cursor. Apagado (base) = recto hacia donde mira el jugador.")]
        public bool aimAtCursor;

        [Tooltip("Segundos desde que se suelta el botón hasta que sale el cometa (el orbe ya está cargado; " +
                 "las demás armas esperan al gesto, WeaponUser ▸ Release Delay).")]
        [Min(0f)] public float fireDelay = 0.06f;

        [Tooltip("Impact frames: al impactar, un instante con el fondo oscuro y el enemigo en blanco. Primera vez que se prueban.")]
        public bool impactFrames = true;

        [Tooltip("Segundos (reales) que dura cada impact frame.")]
        [Min(0.01f)] public float impactFrameDuration = 0.06f;

        [Tooltip("Versión upgrade: más potente (onda de choque, cola oscura, carga e impacto mayores) con los números de Upgrade Stats.")]
        public bool upgraded;

        [Header("Números")]
        public CometStats baseStats = new CometStats();

        public CometStats upgradeStats = new CometStats
        {
            damageMultiplier = 2.5f, speedMultiplier = 1.1f, sizeScale = 1.45f,
            knockbackMultiplier = 1.6f, shakeAmplitude = 0.16f, shakeDuration = 0.2f,
        };

        [Header("Arte")]
        [Tooltip("Material de los efectos dibujados por código (Fx_VertexColor). Asignado = el shader entra en la build.")]
        public Material fxMaterial;

        [Tooltip("Material de silueta plana para los impact frames (Fx_SpriteSilhouette).")]
        public Material silhouetteMaterial;

        [Tooltip("Unidades de mundo por \"px\" del boceto (docs/bocetos/ataque-cometa-estados.html).")]
        [Min(0.001f)] public float pixel = 0.016f;

        [Header("Sonido del cometa")]
        [Tooltip("Al terminar de cargar (estado «listo»).")]
        public SoundCue readySound = new SoundCue();

        [Tooltip("Bucle mientras se mantiene cargado, hasta soltar.")]
        public SoundCue holdLoopSound = new SoundCue();

        [Tooltip("Perforación: al entrar en un enemigo.")]
        public SoundCue pierceEntrySound = new SoundCue { positional = true };

        [Tooltip("Perforación: al salir por el otro lado.")]
        public SoundCue pierceExitSound = new SoundCue { positional = true };

        [Tooltip("Cadena: llega, impacta y se queda pegado.")]
        public SoundCue chainStickSound = new SoundCue { positional = true };

        [Tooltip("Cadena: sale con fuerza hacia el siguiente.")]
        public SoundCue chainReleaseSound = new SoundCue { positional = true };

        public CometStats Stats => upgraded ? upgradeStats : baseStats;

        /// <summary>Copia independiente (los stats también), para editarla sin tocar el asset.</summary>
        public CometSettings Clone()
        {
            var copy = (CometSettings)MemberwiseClone();
            copy.baseStats = baseStats != null ? baseStats.Clone() : new CometStats();
            copy.upgradeStats = upgradeStats != null ? upgradeStats.Clone() : new CometStats();
            return copy;
        }
    }

    /// <summary>
    /// Los ajustes del cometa que se usan de verdad al disparar: los del asset, o la copia que haya
    /// tocado el menú DEV durante el Play (no se guarda en el asset y se olvida al salir de Play).
    /// </summary>
    public static class CometRuntime
    {
        private static WeaponDefinition _weapon;
        private static CometSettings _override;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _weapon = null;
            _override = null;
        }

        /// <summary>Ajustes efectivos de <paramref name="weapon"/>.</summary>
        public static CometSettings For(WeaponDefinition weapon)
        {
            if (weapon == null) return null;
            return weapon == _weapon && _override != null ? _override : weapon.Comet;
        }

        /// <summary>La copia editable de <paramref name="weapon"/> (se crea la primera vez a partir del asset).</summary>
        public static CometSettings Editable(WeaponDefinition weapon)
        {
            if (weapon == null) return null;
            if (weapon != _weapon || _override == null)
            {
                _weapon = weapon;
                _override = (weapon.Comet ?? new CometSettings()).Clone();
            }
            return _override;
        }

        public static bool HasOverride(WeaponDefinition weapon) => weapon != null && weapon == _weapon && _override != null;

        /// <summary>Vuelve a los valores del asset.</summary>
        public static void Clear()
        {
            _weapon = null;
            _override = null;
        }
    }
}
