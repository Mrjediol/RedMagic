using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Realimentación visual del golpe: tiñe el sprite al recibir daño y lo hace parpadear
    /// mientras corren los i-frames.
    ///
    /// Sin esto, los i-frames se sienten como un fallo del juego: el jugador ve al enemigo
    /// encima suyo sin perder vida y no entiende por qué. El parpadeo es la convención que dice
    /// "ahora mismo no te pueden dar".
    ///
    /// Escucha los eventos de <see cref="Health"/> (<c>Damaged</c> y <c>InvulnerabilityChanged</c>)
    /// en vez de comprobar el estado cada frame, y sirve igual para el jugador y para los enemigos.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        [Header("Tinte del impacto")]
        [Tooltip("Color al que se tiñe el sprite justo al recibir el golpe.")]
        [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.35f, 1f);
        [Tooltip("Segundos que dura ese tinte.")]
        [Min(0f)]
        [SerializeField] private float flashDuration = 0.1f;

        [Header("Parpadeo durante los i-frames")]
        [Tooltip("Parpadear mientras el personaje es invulnerable tras el golpe.")]
        [SerializeField] private bool blinkWhileInvulnerable = true;
        [Tooltip("Segundos de cada mitad del parpadeo (visible / atenuado).")]
        [Min(0.01f)]
        [SerializeField] private float blinkInterval = 0.06f;
        [Tooltip("Opacidad de la fase atenuada del parpadeo.")]
        [Range(0f, 1f)]
        [SerializeField] private float blinkAlpha = 0.35f;

        private Health _health;
        private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
        private readonly List<Color> _originalColors = new List<Color>();

        private float _flashTimer;
        private float _blinkTimer;
        private bool _blinking;
        private bool _dimmed;

        // Capa de estado (p. ej. el azul de la ralentización): el color "de reposo" pasa a ser el
        // original multiplicado por este tinte, y el destello y el parpadeo se pintan encima.
        private Color _statusTint = Color.white;
        private float _statusWeight;

        private void Awake()
        {
            _health = GetComponent<Health>();

            GetComponentsInChildren(true, _sprites);
            foreach (var sprite in _sprites) _originalColors.Add(sprite.color);
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.InvulnerabilityChanged += OnInvulnerabilityChanged;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.InvulnerabilityChanged -= OnInvulnerabilityChanged;
            Restore();
        }

        private void OnDamaged(float amount) => _flashTimer = flashDuration;

        private void OnInvulnerabilityChanged(bool invulnerable)
        {
            _blinking = invulnerable && blinkWhileInvulnerable;

            if (_blinking)
            {
                _blinkTimer = blinkInterval;
                _dimmed = true;
            }
            else
            {
                _dimmed = false;
                if (_flashTimer <= 0f) Restore();
            }
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && !_blinking) Restore();
            }

            if (_blinking)
            {
                _blinkTimer -= Time.deltaTime;
                if (_blinkTimer <= 0f)
                {
                    _blinkTimer = blinkInterval;
                    _dimmed = !_dimmed;
                }
            }

            Paint();
        }

        /// <summary>
        /// Pinta el estado actual. El tinte del impacto manda sobre el parpadeo, y la opacidad se
        /// aplica encima: así el primer instante del golpe se ve rojo aunque ya esté parpadeando.
        /// </summary>
        private void Paint()
        {
            if (_flashTimer <= 0f && !_blinking) return;

            for (int i = 0; i < _sprites.Count; i++)
            {
                var sprite = _sprites[i];
                if (sprite == null) continue;

                var color = _flashTimer > 0f ? flashColor : BaseColor(i);
                color.a = _originalColors[i].a * (_dimmed ? blinkAlpha : 1f);
                sprite.color = color;
            }
        }

        /// <summary>
        /// Tinte de estado persistente (ralentización, veneno…): multiplica el color original por
        /// <paramref name="tint"/> con mezcla <paramref name="weight"/> (0 = sin tinte). El destello
        /// del golpe y el parpadeo se siguen pintando encima y, al acabar, vuelven a este color en
        /// vez de al original. Con <paramref name="repaint"/> false sólo se guarda (lo usa quien no
        /// quiere tocar el sprite en ese momento, p. ej. al morir, cuando manda el Corpse).
        /// </summary>
        public void SetStatusTint(Color tint, float weight, bool repaint = true)
        {
            weight = Mathf.Clamp01(weight);
            if (Mathf.Approximately(weight, _statusWeight) && tint == _statusTint) return;

            _statusTint = tint;
            _statusWeight = weight;

            if (repaint && _flashTimer <= 0f && !_blinking) Restore();
        }

        /// <summary>Color de reposo del sprite <paramref name="i"/>: el original, teñido por el estado.</summary>
        private Color BaseColor(int i)
        {
            var original = _originalColors[i];
            if (_statusWeight <= 0f) return original;

            var tint = Color.Lerp(Color.white, _statusTint, _statusWeight);
            return new Color(original.r * tint.r, original.g * tint.g, original.b * tint.b, original.a);
        }

        private void Restore()
        {
            for (int i = 0; i < _sprites.Count && i < _originalColors.Count; i++)
                if (_sprites[i] != null) _sprites[i].color = BaseColor(i);
        }
    }
}
