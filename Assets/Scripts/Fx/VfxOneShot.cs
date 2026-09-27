using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Efecto visual de un solo uso (polvo de dash, estela del doble salto, explosión…).
    /// Va <b>pooled</b> por prefab (<see cref="Core.PrefabPool"/>): se saca, reproduce su animación
    /// y vuelve al pool — nunca <c>Instantiate</c>/<c>Destroy</c>.
    ///
    /// El prefab lleva un SpriteRenderer y, si el efecto tiene varios frames, un Animator con el
    /// clip correspondiente — o <b>sistemas de partículas</b> (Play On Awake y Looping apagados): se
    /// limpian y relanzan en cada uso (<see cref="Restart"/>) y el efecto vuelve al pool cuando el
    /// sistema raíz se para (Stop Action = Callback → <c>OnParticleSystemStopped</c>). La duración de
    /// seguridad sale de <see cref="lifetime"/>, o del clip / de las partículas si se deja en 0.
    ///
    /// <see cref="SpawnFollowing"/> lo pega a un objetivo unos segundos (la estela del dash sigue al
    /// jugador mientras dura; sus partículas, en espacio de mundo, quedan en el camino).
    /// </summary>
    [DisallowMultipleComponent]
    public class VfxOneShot : MonoBehaviour
    {
        [Tooltip("Segundos antes de destruirse. Si es 0 o menos se usa la duración del clip del Animator.")]
        [SerializeField] private float lifetime = 0f;

        [Tooltip("Segundos extra de margen sobre la duración del clip.")]
        [SerializeField] private float extraTime = 0.05f;

        [Tooltip("Usa tiempo sin escalar, para que el efecto no se congele si el juego se pausa.")]
        [SerializeField] private bool unscaledTime = false;

        private float _timer;
        private bool _enforced;
        private ParticleSystem[] _particles;

        // Seguimiento (SpawnFollowing): el efecto se queda en objetivo + offset mientras dure.
        private Transform _follow;
        private Vector3 _followOffset;
        private float _followTimer;

        private void OnEnable()
        {
            _particles ??= GetComponentsInChildren<ParticleSystem>(true);
            _timer = ResolveLifetime();
            _enforced = false;
            _follow = null;
            _started = false; // Spawn lo relanza ya volteado; si alguien lo activa por otra vía, el primer Update
            EnforceSinglePass(rewindFlipbooks: true);
        }

        private bool _started, _restarting;

        /// <summary>
        /// Relanza las partículas desde el instante 0. Lo llama <see cref="Spawn"/> después de fijar la
        /// orientación, para que la primera ráfaga salga ya con el espejo aplicado.
        ///
        /// Stop + Clear + Play, no Clear + Play: <c>Play</c> sobre un sistema que sigue "reproduciéndose"
        /// (una instancia reciclada a medias) no rebobina su tiempo, y el efecto terminaba en el acto sin
        /// enseñar nada.
        /// </summary>
        public void Restart()
        {
            _started = true;
            if (_particles == null || _particles.Length == 0) return;

            _restarting = true; // el Stop de aquí puede avisar de parada: no es el final del efecto
            _particles[0].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particles[0].Play(true);
            _restarting = false;
        }

        /// <summary>
        /// El sistema raíz ha terminado (Stop Action = Callback): de vuelta al pool. Se ignora un aviso que
        /// llega mientras se relanza o cuando el sistema vuelve a estar reproduciéndose — es de una pasada
        /// anterior de esta instancia reciclada, y devolverla al pool apagaba el efecto recién lanzado.
        /// </summary>
        private void OnParticleSystemStopped()
        {
            if (_restarting || !_started) return;
            if (_particles != null && _particles.Length > 0 && _particles[0].IsAlive(true)) return;
            Core.PrefabPool.Despawn(gameObject);
        }

        /// <summary>
        /// Un efecto de un solo uso <b>nunca repite</b>, lo diga o no su prefab: su flipbook o su
        /// máquina de estados se ponen en una sola pasada, clavados en el último frame hasta que el
        /// pool lo apague. Con bucle, el margen <see cref="extraTime"/> (o un frame de más del pool)
        /// enseñaba otra vez el primer dibujo al final. Se aplica en OnEnable y otra vez en el primer
        /// Update, porque el OnEnable del flipbook (que rebobina) puede correr después del nuestro.
        /// </summary>
        private void EnforceSinglePass(bool rewindFlipbooks)
        {
            // El flipbook respeta oneShot en su propio OnEnable, así que basta con hacerlo una vez.
            if (rewindFlipbooks)
                foreach (var flipbook in GetComponentsInChildren<Gameplay.SpriteFlipbook>())
                    flipbook.PlayOnceFromStart();

            // La máquina se resetea en su OnEnable: si corrió después del nuestro, se vuelve a fijar.
            foreach (var machine in GetComponentsInChildren<Pipeline.SpriteStateMachine>())
                if (!machine.IsPlayingOnce) machine.PlayOnce(machine.DefaultState);
        }

        private float ResolveLifetime()
        {
            if (lifetime > 0f) return lifetime;

            // Partículas: lo que tarde en apagarse el sistema más largo (duración + vida máxima).
            if (_particles != null && _particles.Length > 0)
            {
                float longest = 0f;
                foreach (var ps in _particles)
                    longest = Mathf.Max(longest, ps.main.duration + ps.main.startLifetime.constantMax);
                return longest + extraTime;
            }

            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0 && clips[0] != null)
                    return clips[0].length + extraTime;
            }

            return 0.5f;
        }

        private void Update()
        {
            if (!_enforced)
            {
                _enforced = true;
                EnforceSinglePass(rewindFlipbooks: false);
            }

            if (!_started) Restart(); // activado sin pasar por Spawn: arranca igualmente

            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _timer -= dt;
            if (_timer <= 0f) Core.PrefabPool.Despawn(gameObject);
        }

        private void LateUpdate()
        {
            if (_follow == null) return;
            _followTimer -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_followTimer <= 0f || !_follow.gameObject.activeInHierarchy)
            {
                _follow = null;
                return;
            }
            transform.position = _follow.position + _followOffset;
        }

        /// <summary>
        /// Saca el efecto del pool en <paramref name="position"/>, orientado según
        /// <paramref name="facing"/> (-1 mira a la izquierda). Devuelve null si no hay prefab,
        /// así que se puede llamar sin comprobar nada.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, int facing = 1, Transform parent = null,
                                       float scaleMultiplier = 1f)
        {
            if (prefab == null) return null;

            var instance = Core.PrefabPool.Spawn(prefab, position, Quaternion.identity, parent);
            if (instance == null) return null;

            // El pool reutiliza instancias cuya escala quedó volteada: se parte de la del prefab.
            // scaleMultiplier: el efecto sigue el tamaño de quien lo lanza (escala del jugador por escena).
            var scale = prefab.transform.localScale * Mathf.Max(0.01f, scaleMultiplier);
            scale.x = Mathf.Abs(scale.x) * (facing < 0 ? -1f : 1f);
            instance.transform.localScale = scale;

            // Las partículas se lanzan aquí, una vez, ya con el espejo aplicado
            // (con Scaling Mode = Hierarchy, la X negativa invierte forma y velocidades).
            if (instance.TryGetComponent(out VfxOneShot oneShot)) oneShot.Restart();

            return instance;
        }

        /// <summary>
        /// Como <see cref="Spawn"/>, pero el efecto sigue a <paramref name="target"/> (+ <paramref name="offset"/>,
        /// cuya X se invierte con <paramref name="facing"/>) durante <paramref name="followSeconds"/>. Sin
        /// emparentarlo: si el objetivo desaparece, el efecto sigue siendo del pool.
        /// </summary>
        public static GameObject SpawnFollowing(GameObject prefab, Transform target, Vector2 offset, int facing,
                                                float followSeconds, float scaleMultiplier = 1f)
        {
            if (target == null) return null;
            var worldOffset = new Vector3(offset.x * (facing < 0 ? -1f : 1f), offset.y, 0f);
            var instance = Spawn(prefab, target.position + worldOffset, facing, null, scaleMultiplier);
            if (instance != null && instance.TryGetComponent(out VfxOneShot oneShot))
            {
                oneShot._follow = target;
                oneShot._followOffset = worldOffset;
                oneShot._followTimer = followSeconds;
            }
            return instance;
        }

        /// <summary>
        /// Como <see cref="Spawn"/>, pero escalado en uniforme para que el dibujo mida
        /// <paramref name="width"/> unidades de ancho. Para un efecto que tiene que cuadrar con un
        /// área de daño configurable (el radio de un golpe): cambiar el radio en el Inspector no
        /// deja el dibujo más grande o más pequeño que lo que duele. Se mide la celda del sprite,
        /// que en una hoja cortada por el pipeline es la misma en todos los frames.
        /// </summary>
        public static GameObject SpawnFitWidth(GameObject prefab, Vector3 position, float width,
                                               int facing = 1, Transform parent = null)
        {
            var instance = Spawn(prefab, position, facing, parent);
            if (instance == null || width <= 0f) return instance;

            float natural = NaturalWidth(prefab);
            if (natural > 0.0001f) instance.transform.localScale *= width / natural;

            return instance;
        }

        /// <summary>Ancho en mundo de la celda del sprite del prefab, a la escala autorizada en él.</summary>
        private static float NaturalWidth(GameObject prefab)
        {
            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || renderer.sprite == null) return 0f;

            var sprite = renderer.sprite;
            // lossyScale de un asset de prefab = escala relativa a su raíz, que es la que Spawn copia.
            return sprite.rect.width / sprite.pixelsPerUnit * Mathf.Abs(renderer.transform.lossyScale.x);
        }
    }
}
