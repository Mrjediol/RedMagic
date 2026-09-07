using System;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Fx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Run
{
    /// <summary>
    /// Lleva la cuenta de los enemigos vivos de la sección cargada y avisa cuando cae el último.
    /// Es lo que sostiene la mecánica de "mata a todo antes de seguir": la salida
    /// (<see cref="SectionExit"/>) y la tienda sólo se activan con <see cref="IsCleared"/> en true.
    ///
    /// Singleton persistente colocado a mano en MainHub (con respaldo automático si falta), para
    /// poder ajustar el efecto y el sonido de "sección despejada" en el Inspector.
    ///
    /// <b>Qué cuenta como enemigo:</b> cualquier <see cref="Health"/> que esté dentro de la escena
    /// de la sección. El jugador de la run no cuenta porque es un objeto persistente
    /// (DontDestroyOnLoad) y por tanto no vive en esa escena — la exclusión sale gratis.
    /// </summary>
    [DisallowMultipleComponent]
    public class SectionClearTracker : MonoBehaviour
    {
        public static SectionClearTracker Instance { get; private set; }

        [Header("Aviso de sección despejada")]
        [Tooltip("Efecto que aparece en cada SectionExit al morir el último enemigo. Se puede " +
                 "reutilizar la explosión de la bola de fuego (VFX_Explosion).")]
        [SerializeField] private GameObject clearEffectPrefab;

        [Tooltip("Escala del efecto en la salida (la explosión de la bola de fuego es pequeña).")]
        [Min(0.1f)]
        [SerializeField] private float clearEffectScale = 2f;

        [Tooltip("id de sonido del AudioManager al despejar la sección. Vacío = sin sonido.")]
        [SerializeField] private string clearSfxId = "SFX_Fireball";

        /// <summary>True si no queda ningún enemigo vivo en la sección actual.</summary>
        public bool IsCleared { get; private set; } = true;

        /// <summary>Enemigos que quedan vivos en la sección actual.</summary>
        public int RemainingEnemies { get; private set; }

        /// <summary>Se dispara una vez, al morir el último enemigo de la sección.</summary>
        public event Action Cleared;

        /// <summary>Se dispara con cada cambio del contador (para HUD/contadores futuros).</summary>
        public event Action<int> RemainingChanged;

        private readonly List<Health> _tracked = new();
        private Scene _trackedScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            new GameObject("[SectionClearTracker]").AddComponent<SectionClearTracker>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Untrack();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ seguimiento

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Track(scene);

        /// <summary>
        /// Empieza a vigilar los enemigos de <paramref name="scene"/>. Lo llama cada carga de
        /// escena; <see cref="RunManager"/> también puede forzarlo tras spawnear contenido extra.
        /// </summary>
        public void Track(Scene scene)
        {
            Untrack();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                SetCleared(true, 0);
                return;
            }

            _trackedScene = scene;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var health in root.GetComponentsInChildren<Health>(true))
                {
                    if (health == null || health.IsDead) continue;
                    _tracked.Add(health);
                    health.Died += OnEnemyDied;
                }
            }

            SetCleared(_tracked.Count == 0, _tracked.Count);
        }

        private void Untrack()
        {
            foreach (var health in _tracked)
                if (health != null) health.Died -= OnEnemyDied;

            _tracked.Clear();
            _trackedScene = default;
        }

        private void OnEnemyDied()
        {
            // Se recuenta en vez de descontar: así un enemigo destruido sin morir (o spawneado
            // después) no descuadra el contador.
            int alive = 0;
            foreach (var health in _tracked)
                if (health != null && !health.IsDead) alive++;

            bool nowCleared = alive == 0;
            bool wasCleared = IsCleared;

            SetCleared(nowCleared, alive);

            if (nowCleared && !wasCleared) OnSectionCleared();
        }

        private void SetCleared(bool cleared, int remaining)
        {
            IsCleared = cleared;

            if (RemainingEnemies == remaining) return;
            RemainingEnemies = remaining;
            RemainingChanged?.Invoke(remaining);
        }

        // ------------------------------------------------------------------ aviso al jugador

        /// <summary>
        /// Feedback de "ya puedes salir": explosión y sonido en cada salida de la sección, que es
        /// justo donde el jugador tiene que mirar ahora.
        /// </summary>
        private void OnSectionCleared()
        {
            if (!string.IsNullOrWhiteSpace(clearSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(clearSfxId);

            if (clearEffectPrefab != null && _trackedScene.IsValid() && _trackedScene.isLoaded)
            {
                foreach (var root in _trackedScene.GetRootGameObjects())
                {
                    foreach (var exit in root.GetComponentsInChildren<SectionExit>(true))
                    {
                        var vfx = VfxOneShot.Spawn(clearEffectPrefab, exit.transform.position);
                        if (vfx != null) vfx.transform.localScale *= clearEffectScale;
                    }
                }
            }

            Cleared?.Invoke();
        }
    }
}
