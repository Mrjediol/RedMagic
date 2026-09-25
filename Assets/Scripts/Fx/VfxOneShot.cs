using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Efecto visual de un solo uso (polvo de dash, estela del doble salto, explosión…).
    /// Va <b>pooled</b> por prefab (<see cref="Core.PrefabPool"/>): se saca, reproduce su animación
    /// y vuelve al pool — nunca <c>Instantiate</c>/<c>Destroy</c>.
    ///
    /// El prefab lleva un SpriteRenderer y, si el efecto tiene varios frames, un Animator con el
    /// clip correspondiente. La duración sale de <see cref="lifetime"/>, o del propio clip si se
    /// deja en 0.
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

        private void OnEnable()
        {
            _timer = ResolveLifetime();
            _enforced = false;
            EnforceSinglePass(rewindFlipbooks: true);
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

            _timer -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_timer <= 0f) Core.PrefabPool.Despawn(gameObject);
        }

        /// <summary>
        /// Saca el efecto del pool en <paramref name="position"/>, orientado según
        /// <paramref name="facing"/> (-1 mira a la izquierda). Devuelve null si no hay prefab,
        /// así que se puede llamar sin comprobar nada.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, int facing = 1, Transform parent = null)
        {
            if (prefab == null) return null;

            var instance = Core.PrefabPool.Spawn(prefab, position, Quaternion.identity, parent);
            if (instance == null) return null;

            // El pool reutiliza instancias cuya escala quedó volteada: se parte de la del prefab.
            var scale = prefab.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facing < 0 ? -1f : 1f);
            instance.transform.localScale = scale;

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
