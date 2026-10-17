using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Peso de los golpes que recibe el jugador: un hit-stop y una sacudida de cámara que crecen con la parte de la vida
    /// máxima que se lleva el golpe. Un rasguño (≈1 %) apenas se nota; a partir de <see cref="maxAtFraction"/> (50 %) es el
    /// máximo. Escucha <see cref="Health.Damaged"/> (el daño ya con reducciones), así sirve para cualquier fuente: enemigos,
    /// proyectiles, pinchos…
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class PlayerHurtFeedback : MonoBehaviour
    {
        [Header("Escala")]
        [Tooltip("Fracción de la vida máxima a la que el efecto es el mínimo (0,01 = 1 %).")]
        [Range(0f, 0.2f)] [SerializeField] private float minAtFraction = 0.01f;
        [Tooltip("Fracción de la vida máxima a la que el efecto llega al máximo (0,5 = 50 %).")]
        [Range(0.05f, 1f)] [SerializeField] private float maxAtFraction = 0.5f;
        [Tooltip("Forma de la subida: 1 = lineal, >1 = los golpes pequeños se notan menos y los grandes más.")]
        [Range(0.3f, 3f)] [SerializeField] private float curve = 0.8f;

        [Header("Hit-stop (segundos reales)")]
        [Min(0f)] [SerializeField] private float hitStopMin = 0.06f;
        [Min(0f)] [SerializeField] private float hitStopMax = 0.25f;
        [Tooltip("Velocidad del juego durante la congelación (0 = parado del todo).")]
        [Range(0f, 1f)] [SerializeField] private float hitStopTimeScale = 0.03f;

        [Header("Sacudida de cámara")]
        [Min(0f)] [SerializeField] private float shakeAmplitudeMin = 0.1f;
        [Min(0f)] [SerializeField] private float shakeAmplitudeMax = 0.6f;
        [Min(0f)] [SerializeField] private float shakeDurationMin = 0.1f;
        [Min(0f)] [SerializeField] private float shakeDurationMax = 0.4f;

        private Health _health;

        private void Awake() => _health = GetComponent<Health>();

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(float amount)
        {
            if (_health == null || _health.MaxHealth <= 0f || amount <= 0f) return;
            float t = Mathf.Pow(Mathf.InverseLerp(minAtFraction, maxAtFraction, amount / _health.MaxHealth), curve);
            HitStop.Play(Mathf.Lerp(hitStopMin, hitStopMax, t), hitStopTimeScale);
            CameraFollow.ShakeAll(Mathf.Lerp(shakeAmplitudeMin, shakeAmplitudeMax, t), Mathf.Lerp(shakeDurationMin, shakeDurationMax, t));
        }
    }
}
