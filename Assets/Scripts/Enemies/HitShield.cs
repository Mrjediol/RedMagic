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
    /// por el que entra el daño, así que ningún arma ni ataque sabe que existe. Con un aura en bucle
    /// mientras dura y un estallido + sonido al romperse; sin prefabs funciona igual, sólo que invisible.
    /// Reaparece entero en cada nacimiento (<c>OnEnable</c>), también si el enemigo viene de un pool.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class HitShield : MonoBehaviour
    {
        [Tooltip("Golpes que aguanta antes de romperse.")]
        [Min(1)] [SerializeField] private int hits = 1;

        [Header("Efectos")]
        [Tooltip("Aura que lo rodea mientras dura (en bucle). Vacío = escudo invisible.")]
        [SerializeField] private GameObject auraPrefab;

        [Tooltip("Estallido al romperse. Vacío = se rompe sin efecto.")]
        [SerializeField] private GameObject breakPrefab;

        [Tooltip("Ancho en unidades de mundo del aura, para que envuelva el cuerpo.")]
        [Min(0.1f)] [SerializeField] private float auraWidth = 3.4f;

        [Tooltip("Ancho en unidades de mundo del estallido al romperse.")]
        [Min(0.1f)] [SerializeField] private float breakWidth = 4f;

        [Tooltip("Desplazamiento del aura y del estallido desde el centro del cuerpo (mundo).")]
        [SerializeField] private Vector2 offset;

        [Header("Sonido")]
        [Tooltip("Suena al aparecer el escudo.")]
        [SerializeField] private SoundCue upSound = new SoundCue();

        [Tooltip("Suena al romperse.")]
        [SerializeField] private SoundCue breakSound = new SoundCue();

        private Health _health;
        private int _remaining;
        private GameObject _aura;

        /// <summary>Quedan golpes por absorber.</summary>
        public bool Active => _remaining > 0;

        private void Awake() => _health = GetComponent<Health>();

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<Health>();
            _remaining = hits;
            _health.HitAbsorber = Absorb;
            _health.Died += OnDied;

            if (auraPrefab != null)
            {
                _aura = PrefabPool.Spawn(auraPrefab, transform.position + (Vector3)offset, Quaternion.identity);
                if (_aura != null) { FitWidth(_aura, auraPrefab, auraWidth); InFrontOfBody(_aura); }
            }

            PlayCue(upSound);
        }

        // Sin emparentar: devolver al pool un hijo que se está desactivando con su padre no se puede.
        private void LateUpdate()
        {
            if (_aura != null) _aura.transform.position = transform.position + (Vector3)offset;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                if (_health.HitAbsorber == (System.Func<Health, float, bool>)Absorb) _health.HitAbsorber = null;
                _health.Died -= OnDied;
            }

            ReleaseAura();
        }

        private bool Absorb(Health health, float amount)
        {
            if (_remaining <= 0) return false;

            _remaining--;
            if (_remaining > 0) return true;

            ReleaseAura();
            if (breakPrefab != null)
                InFrontOfBody(VfxOneShot.SpawnFitWidth(breakPrefab, transform.position + (Vector3)offset, breakWidth));
            PlayCue(breakSound);
            return true;
        }

        private void OnDied() => ReleaseAura();

        private void ReleaseAura()
        {
            if (_aura == null) return;
            PrefabPool.Despawn(_aura);
            _aura = null;
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

        /// <summary>Escala la instancia en uniforme hasta que el dibujo mida <paramref name="width"/> de ancho en mundo.</summary>
        private static void FitWidth(GameObject instance, GameObject prefab, float width)
        {
            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || renderer.sprite == null) return;

            float natural = renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit * Mathf.Abs(renderer.transform.lossyScale.x);
            if (natural <= 0.0001f) return;

            instance.transform.localScale = prefab.transform.localScale * (width / natural);
        }
    }
}
