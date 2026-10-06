using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Escudo de golpes de cualquier enemigo: absorbe los primeros <see cref="hits"/> golpes que recibe
    /// (no quitan vida, no empujan) y se rompe. Va por <see cref="Health.HitAbsorber"/>, el único sitio
    /// por el que entra el daño, así que ningún arma ni ataque sabe que existe. El escudo es un GameObject hijo del enemigo
    /// (<see cref="shield"/>): se coloca, escala y ordena a mano en el prefab; se enciende al nacer y se apaga
    /// al romperse, con un estallido + sonido.
    /// Reaparece entero en cada nacimiento (<c>OnEnable</c>), también si el enemigo viene de un pool.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class HitShield : MonoBehaviour
    {
        [Tooltip("Golpes que aguanta antes de romperse.")]
        [Min(1)] [SerializeField] private int hits = 1;

        [Header("Efectos")]
        [Tooltip("GameObject hijo del enemigo que ES el escudo (sprite / animación). Colócalo, escálalo y ordénalo " +
                 "en el prefab: se enciende al nacer y se apaga al romperse. Vacío = escudo invisible.")]
        [SerializeField] private GameObject shield;

        [Tooltip("Estallido al romperse (nace donde está el escudo). Vacío = se rompe sin efecto.")]
        [SerializeField] private GameObject breakPrefab;

        [Tooltip("Ancho en unidades de mundo del estallido al romperse.")]
        [Min(0.1f)] [SerializeField] private float breakWidth = 4f;

        [Header("Sonido")]
        [Tooltip("Suena al aparecer el escudo.")]
        [SerializeField] private SoundCue upSound = new SoundCue();

        [Tooltip("Suena al romperse.")]
        [SerializeField] private SoundCue breakSound = new SoundCue();

        private Health _health;
        private int _remaining;
        /// <summary>Quedan golpes por absorber.</summary>
        public bool Active => _remaining > 0;

        private void Awake() => _health = GetComponent<Health>();

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<Health>();
            _remaining = hits;
            _health.HitAbsorber = Absorb;
            _health.Died += OnDied;

            if (shield != null) shield.SetActive(true);

            PlayCue(upSound);
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                if (_health.HitAbsorber == (System.Func<Health, float, bool>)Absorb) _health.HitAbsorber = null;
                _health.Died -= OnDied;
            }

            if (shield != null) shield.SetActive(false);
        }

        private bool Absorb(Health health, float amount)
        {
            if (_remaining <= 0) return false;

            _remaining--;
            if (_remaining > 0) return true;

            Vector3 at = shield != null ? shield.transform.position : transform.position;
            if (shield != null) shield.SetActive(false);
            if (breakPrefab != null)
                InFrontOfBody(VfxOneShot.SpawnFitWidth(breakPrefab, at, breakWidth));
            PlayCue(breakSound);
            return true;
        }

        private void OnDied()
        {
            if (shield != null) shield.SetActive(false);
        }

        private void PlayCue(SoundCue cue)
        {
            if (cue == null || !cue.HasClips || AudioManager.Instance == null) return;
            AudioManager.Instance.Play(cue, transform.position);
        }

        /// <summary>Dibuja el efecto delante de todas las piezas del cuerpo (misma capa, un orden por encima del más alto).</summary>
        private void InFrontOfBody(GameObject fx)
        {
            if (fx == null) return;

            var parts = GetComponentsInChildren<SpriteRenderer>();
            if (parts.Length == 0) return;

            int top = int.MinValue;
            foreach (var part in parts) top = Mathf.Max(top, part.sortingOrder);

            foreach (var r in fx.GetComponentsInChildren<SpriteRenderer>(true))
            {
                r.sortingLayerID = parts[0].sortingLayerID;
                r.sortingOrder = top + 1;
            }
        }
    }
}
