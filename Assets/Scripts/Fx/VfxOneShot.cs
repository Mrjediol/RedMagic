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

        private void OnEnable()
        {
            _timer = ResolveLifetime();
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
    }
}
