using System;
using UnityEngine;

namespace RedMagic.Audio
{
    /// <summary>Abrir y cerrar un menú.</summary>
    [Serializable]
    public class MenuSounds
    {
        public SoundCue open = new SoundCue { priority = SoundPriority.Critical };
        public SoundCue close = new SoundCue { priority = SoundPriority.Critical };
    }

    /// <summary>
    /// Los sonidos que no son de un objeto sino del juego: UI, menús, flujo de la run, economía,
    /// estados. Vive en el prefab del AudioManager (<c>Resources/AudioManager.prefab</c>), el único
    /// sitio que existe una sola vez en todo el juego — ponerlos en un objeto de escena (RunManager,
    /// SectionClearTracker…) los repetiría en cada escena.
    ///
    /// Se usa con <see cref="Play"/>: <c>SystemSounds.Play(s => s.runStart)</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioManager))]
    public class SystemSounds : MonoBehaviour
    {
        [Header("UI")]
        public SoundCue uiHover = new SoundCue { priority = SoundPriority.Critical };
        [Tooltip("Cambio de foco con mando/teclado.")]
        public SoundCue uiFocus = new SoundCue { priority = SoundPriority.Critical };
        public SoundCue uiClick = new SoundCue { priority = SoundPriority.Critical };
        public SoundCue uiBack = new SoundCue { priority = SoundPriority.Critical };
        [Tooltip("Acción rechazada en un menú (sin fondos, bloqueado, al máximo).")]
        public SoundCue uiDeny = new SoundCue { priority = SoundPriority.Critical };

        [Header("Música")]
        [Tooltip("Suena en bucle mientras se está en el menú principal. Vacío = sin música.")]
        [MusicSlot]
        public AudioClip menuMusic;

        [Header("Tienda")]
        [Tooltip("Compra hecha (sólo si se cobra y se equipa).")]
        public SoundCue shopBuy = new SoundCue();
        [Tooltip("Interacción rechazada (sin oro, sin rerolls, huecos llenos).")]
        public SoundCue shopDeny = new SoundCue();
        public SoundCue shopReroll = new SoundCue();

        [Header("Menús")]
        public MenuSounds mainMenu = new MenuSounds();
        public MenuSounds pauseMenu = new MenuSounds();
        public MenuSounds optionsMenu = new MenuSounds();
        public MenuSounds itemMenu = new MenuSounds();
        public MenuSounds upgradeMenu = new MenuSounds();
        public MenuSounds mirrorMenu = new MenuSounds();
        public MenuSounds weaponForgeMenu = new MenuSounds();
        public MenuSounds weaponChoiceMenu = new MenuSounds();

        [Header("Acciones de menú")]
        [Tooltip("Mejora permanente comprada (libro / árbol de mejoras).")]
        public SoundCue upgradeBought = new SoundCue();
        [Tooltip("Arma subida de nivel en la forja.")]
        public SoundCue weaponLevelUp = new SoundCue();
        [Tooltip("Pasiva legendaria mejorada en el espejo.")]
        public SoundCue mirrorPassiveUpgraded = new SoundCue();

        [Header("Run")]
        [Tooltip("Se cruza la puerta del hub y empieza la run.")]
        public SoundCue runStart = new SoundCue();
        [Tooltip("Llega una sección nueva (tras el fundido).")]
        public SoundCue sectionEnter = new SoundCue();
        [Tooltip("Empieza una oleada del WaveManager.")]
        public SoundCue waveStart = new SoundCue();
        [Tooltip("Aparece un enemigo de una oleada (en su punto de aparición).")]
        public SoundCue enemySpawn = new SoundCue { positional = true };
        [Tooltip("Ya no queda ningún enemigo: la salida se abre.")]
        public SoundCue sectionClear = new SoundCue();
        [Tooltip("Llega la tienda entre secciones.")]
        public SoundCue shopEnter = new SoundCue();
        [Tooltip("El jugador muere y la run termina.")]
        public SoundCue runOver = new SoundCue { priority = SoundPriority.High };
        [Tooltip("Se completan todos los mundos de la run.")]
        public SoundCue runComplete = new SoundCue { priority = SoundPriority.High };
        [Tooltip("Aparece el altar de recompensa del jefe.")]
        public SoundCue bossRewardAppear = new SoundCue();
        [Tooltip("Una puerta no deja pasar (quedan enemigos, o sin arma en el hub).")]
        public SoundCue doorLocked = new SoundCue();

        [Header("Economía")]
        public SoundCue goldGained = new SoundCue();
        public SoundCue diamondGained = new SoundCue();
        public SoundCue soulFragmentGained = new SoundCue();
        public SoundCue skullGained = new SoundCue();

        [Header("Enemigo genérico (se usa cuando un enemigo no tiene su propio sonido)")]
        [Tooltip("Enemigo recibe daño.")]
        public SoundCue enemyHurt = new SoundCue { positional = true };
        [Tooltip("Enemigo muere.")]
        public SoundCue enemyDeath = new SoundCue { positional = true, priority = SoundPriority.High };
        [Tooltip("Paso / aleteo de un enemigo que se desplaza.")]
        public SoundCue enemyMove = new SoundCue { positional = true, priority = SoundPriority.Low };
        [Tooltip("Enemigo ataca (frame en que sale el golpe o el disparo).")]
        public SoundCue enemyAttack = new SoundCue { positional = true };

        [Header("Build y estados")]
        [Tooltip("Una sinergia alcanza un umbral (2 / 4 / 6).")]
        public SoundCue synergyTierReached = new SoundCue { priority = SoundPriority.High };
        [Tooltip("Un enemigo queda ralentizado (no al refrescar).")]
        public SoundCue slowApplied = new SoundCue { priority = SoundPriority.Low };
        [Tooltip("Un enemigo recibe la Marca de Oro (no al refrescar).")]
        public SoundCue goldMarkApplied = new SoundCue { priority = SoundPriority.Low };

        private static SystemSounds s_current;

        /// <summary>El del AudioManager activo, o null si aún no hay.</summary>
        public static SystemSounds Current
        {
            get
            {
                if (s_current == null && AudioManager.Instance != null)
                    s_current = AudioManager.Instance.GetComponent<SystemSounds>();
                return s_current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => s_current = null;

        /// <summary>
        /// Cue genérica de enemigo para <paramref name="trigger"/>, o null si ese momento no tiene
        /// genérico. La usa <see cref="SoundEmitter"/> cuando un enemigo no tiene su propio clip.
        /// </summary>
        public SoundCue EnemyFallback(SoundTrigger trigger) => trigger switch
        {
            SoundTrigger.OnHit => enemyHurt,
            SoundTrigger.OnDeath => enemyDeath,
            SoundTrigger.OnMove => enemyMove,
            SoundTrigger.OnAttack => enemyAttack,
            _ => null,
        };

        /// <summary>Momentos que tienen sonido genérico de enemigo.</summary>
        public static bool HasEnemyFallback(SoundTrigger trigger) =>
            trigger is SoundTrigger.OnHit or SoundTrigger.OnDeath or SoundTrigger.OnMove or SoundTrigger.OnAttack;

        /// <summary>Suena la cue que elige <paramref name="pick"/> (2D).</summary>
        public static void Play(Func<SystemSounds, SoundCue> pick)
        {
            var sounds = Current;
            if (sounds != null) AudioManager.Instance.Play(pick(sounds));
        }

        /// <summary>Suena la cue que elige <paramref name="pick"/> en un punto (posicional si la cue lo es).</summary>
        public static void PlayAt(Func<SystemSounds, SoundCue> pick, Vector3 position)
        {
            var sounds = Current;
            if (sounds != null) AudioManager.Instance.Play(pick(sounds), position);
        }
    }
}
