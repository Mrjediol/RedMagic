using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Marca de oro de un enemigo: un aura dorada que late a su alrededor y, si muere marcado, su
    /// botín sale multiplicado (<see cref="DropMultiplierOf"/>, lo lee <c>CurrencyDropper</c>).
    ///
    /// Mismo patrón que <see cref="SlowStatus"/>: no va en ningún prefab, <see cref="Apply"/> lo añade la
    /// primera vez y lo reutiliza. Reaplicar <b>refresca</b> la duración y se queda con el mayor
    /// multiplicador: nunca se apila. Al morir se apaga (el multiplicador vivo en ese instante es el
    /// que cuenta para el botín).
    ///
    /// Se ve de tres formas: un halo dorado detrás del cuerpo, un anillo dorado que late y un tinte
    /// dorado del sprite por la capa de estado de <see cref="HitFlash"/>. Esa capa la comparte con la
    /// ralentización: si el enemigo está además ralentizado, manda el azul (el hielo cambia cómo se
    /// juega, la marca sólo el botín) y el dorado vuelve en cuanto acaba. Halo y anillo son sprites
    /// hijos creados una vez por enemigo. Sus números los pasa quien marca (<c>Items.GoldMark</c>,
    /// desde <c>SynergyTuning</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldMarkStatus : MonoBehaviour
    {
        /// <summary>Segundos finales en los que el aura se desvanece (avisa de que se acaba).</summary>
        private const float FadeOutSeconds = 0.5f;

        private Health _health;
        private HitFlash _flash;
        private SpriteRenderer _aura, _ring;
        private Color _tint = Color.white;
        private bool _tinted;
        private float _remaining;
        private float _multiplier = 1f;
        private float _deathMultiplier = 1f;
        private Color _color = Color.yellow;
        private float _size = 1f;
        private float _pulseSpeed = 1.5f;
        private float _pulseScale = 0.12f;

        public bool IsMarked => _remaining > 0f;
        public float Remaining => Mathf.Max(0f, _remaining);

        /// <summary>
        /// Marca a <paramref name="target"/>. Si ya lo estaba, refresca la duración y se queda con el
        /// mayor multiplicador. Devuelve el estado (null si el objetivo no vale).
        /// </summary>
        /// <param name="dropMultiplier">Botín al morir marcado: 2 = el doble.</param>
        /// <param name="auraSize">Diámetro del aura respecto al tamaño del sprite del enemigo (1 = igual).</param>
        /// <param name="tint">Tinte del cuerpo; su alfa es la mezcla (0 = sin tinte).</param>
        public static GoldMarkStatus Apply(Health target, float duration, float dropMultiplier, Color color,
                                           float auraSize, float pulseSpeed, float pulseScale, Material material,
                                           Color tint)
        {
            if (target == null || target.IsDead || duration <= 0f) return null;
            if (!target.TryGetComponent(out GoldMarkStatus status))
                status = target.gameObject.AddComponent<GoldMarkStatus>();
            status._tint = tint;
            status.Refresh(duration, dropMultiplier, color, auraSize, pulseSpeed, pulseScale, material);
            return status;
        }

        /// <summary>Multiplicador de botín de <paramref name="target"/> (1 si no está marcado).</summary>
        public static float DropMultiplierOf(Health target)
        {
            if (target == null || !target.TryGetComponent(out GoldMarkStatus status)) return 1f;
            if (status.IsMarked) return status._multiplier;
            return target.IsDead ? status._deathMultiplier : 1f;
        }

        public static bool IsMarkedTarget(Health target) =>
            target != null && target.TryGetComponent(out GoldMarkStatus status) && status.IsMarked;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _flash = GetComponent<HitFlash>();
            if (_flash == null && _health != null) _flash = gameObject.AddComponent<HitFlash>();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
            Clear(repaint: false);
        }

        private void Refresh(float duration, float dropMultiplier, Color color, float auraSize,
                             float pulseSpeed, float pulseScale, Material material)
        {
            _multiplier = IsMarked ? Mathf.Max(_multiplier, dropMultiplier) : dropMultiplier;
            _remaining = Mathf.Max(_remaining, duration);
            _color = color;
            _size = auraSize;
            _pulseSpeed = pulseSpeed;
            _pulseScale = pulseScale;

            EnsureAura(material);
            _aura.gameObject.SetActive(true);
            _ring.gameObject.SetActive(true);
            UpdateAura(1f);
        }

        private void Update()
        {
            if (!IsMarked) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                Clear(repaint: true);
                return;
            }

            UpdateAura(Mathf.Clamp01(_remaining / FadeOutSeconds));
        }

        private void OnDied()
        {
            // El botín se reparte en este mismo Died (CurrencyDropper): se guarda el multiplicador vivo
            // para que lo lea aunque este handler corra antes que el suyo.
            _deathMultiplier = IsMarked ? _multiplier : 1f;
            Clear(repaint: false); // al morir manda el Corpse (su tinte y fundido)
        }

        private void Clear(bool repaint)
        {
            _remaining = 0f;
            if (_aura != null) _aura.gameObject.SetActive(false);
            if (_ring != null) _ring.gameObject.SetActive(false);

            // Sólo se limpia el tinte si es nuestro: el de la ralentización no se toca.
            if (_tinted && _flash != null) _flash.SetStatusTint(Color.white, 0f, repaint);
            _tinted = false;
        }

        // ------------------------------------------------------------------ aura

        private void EnsureAura(Material material)
        {
            if (_aura != null) return;

            var body = GetComponentInChildren<SpriteRenderer>();
            _aura = NewChild("GoldMarkAura", ProceduralSprites.Glow, body, material, -1); // detrás: es un halo
            _ring = NewChild("GoldMarkRing", ProceduralSprites.Ring, body, material, 1);  // delante: se lee siempre
        }

        private SpriteRenderer NewChild(string name, Sprite sprite, SpriteRenderer body, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(body != null ? body.transform : transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material != null) renderer.sharedMaterial = material;
            if (body != null)
            {
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + order;
            }
            return renderer;
        }

        private void UpdateAura(float fade)
        {
            if (_aura == null) return;

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f);
            _aura.color = new Color(_color.r, _color.g, _color.b, _color.a * Mathf.Lerp(0.6f, 1f, wave) * fade);
            _ring.color = new Color(_color.r, _color.g, _color.b, _color.a * Mathf.Lerp(0.35f, 0.8f, wave) * fade);

            // Tamaño relativo al cuerpo, en su espacio local (cuelgan del sprite del enemigo). El anillo
            // late al revés que el halo: se lee como un pulso que sale del cuerpo.
            var parent = _aura.transform.parent != null ? _aura.transform.parent.GetComponent<SpriteRenderer>() : null;
            var bodySize = parent != null && parent.sprite != null ? parent.sprite.bounds.size : Vector3.one;
            var center = parent != null && parent.sprite != null ? parent.sprite.bounds.center : Vector3.zero;
            float body = Mathf.Max(bodySize.x, bodySize.y);
            Fit(_aura, body * _size * (1f + _pulseScale * (wave * 2f - 1f)), center);
            Fit(_ring, body * 0.9f * (1f + _pulseScale * (1f - wave * 2f)), center);

            // Tinte dorado del cuerpo, salvo que la ralentización esté usando la capa de estado.
            if (_flash == null) return;
            if (SlowStatus.IsSlowedTarget(_health))
            {
                _tinted = false;
                return;
            }
            _flash.SetStatusTint(new Color(_tint.r, _tint.g, _tint.b, 1f), _tint.a * fade);
            _tinted = true;
        }

        private static void Fit(SpriteRenderer renderer, float diameter, Vector3 center)
        {
            var size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(diameter / size.x, diameter / size.y, 1f);
            renderer.transform.localPosition = center;
        }
    }
}
