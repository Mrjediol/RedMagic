using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Qué pasa con el cuerpo al morir: se queda un momento tintado en el suelo, se desvanece y
    /// desaparece.
    ///
    /// El instante de cadáver existe para que la muerte se lea (un enemigo que se esfuma en el
    /// frame del golpe parece un fallo), pero dejarlo para siempre ensucia la escena y llega a
    /// tapar cosas — la recompensa que suelta el jefe, sin ir más lejos.
    ///
    /// Va junto al <see cref="Health"/>. Los colliders y la física los apaga quien controle al
    /// personaje (<c>EnemyController</c> al morir); aquí sólo se toca el aspecto y la destrucción,
    /// para que un enemigo con otra IA, o sin ninguna, se limpie igual.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class Corpse : MonoBehaviour
    {
        [Header("Tiempos")]
        [Tooltip("Segundos que el cadáver se queda quieto y visible antes de empezar a irse.")]
        [Min(0f)]
        [SerializeField] private float linger = 0.6f;

        [Tooltip("Segundos que tarda en desvanecerse del todo.")]
        [Min(0f)]
        [SerializeField] private float fadeDuration = 0.35f;

        [Header("Aspecto")]
        [Tooltip("Color al que se tiñe el cuerpo nada más morir. El alfa cuenta: es la opacidad " +
                 "del cadáver antes de empezar a desvanecerse.")]
        [SerializeField] private Color corpseTint = new Color(0.35f, 0.1f, 0.12f, 0.75f);

        [Tooltip("Destruir el objeto al terminar. Apágalo si el cadáver debe quedarse (por " +
                 "ejemplo, si algo va a reanimarlo).")]
        [SerializeField] private bool destroyWhenDone = true;

        private Health _health;
        private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
        private bool _dying;

        /// <summary>Segundos totales desde la muerte hasta que el objeto desaparece.</summary>
        public float TotalDuration => linger + fadeDuration;

        private void Awake()
        {
            _health = GetComponent<Health>();
            GetComponentsInChildren(true, _sprites);
        }

        private void OnEnable() => _health.Died += OnDied;

        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied()
        {
            if (_dying) return;
            _dying = true;

            // El parpadeo de i-frames pinta el sprite por su cuenta; si siguiera vivo pelearía con
            // el desvanecido y el cadáver daría tirones de opacidad.
            var flash = GetComponent<HitFlash>();
            if (flash != null) flash.enabled = false;

            StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            Tint();

            if (linger > 0f) yield return new WaitForSeconds(linger);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(corpseTint.a * (1f - Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }

            SetAlpha(0f);

            if (destroyWhenDone) Destroy(gameObject);
        }

        private void Tint()
        {
            foreach (var sprite in _sprites)
                if (sprite != null) sprite.color = corpseTint;
        }

        private void SetAlpha(float alpha)
        {
            foreach (var sprite in _sprites)
            {
                if (sprite == null) continue;

                var color = sprite.color;
                color.a = alpha;
                sprite.color = color;
            }
        }
    }
}
