using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Economy;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Portador de núcleo: un enemigo cualquiera convertido en raro que, si muere, suelta un <see cref="CorePickup"/> — una
    /// subida de nivel del arma — y si no, se escapa a los <see cref="WeaponProgressionConfig.carrierLifetime"/> s (los
    /// últimos <see cref="WeaponProgressionConfig.carrierBlink"/> parpadea). Es lo que mete habilidad en la suerte.
    ///
    /// Tiene que reconocerse de un vistazo y desde lejos: aspecto propio (<see cref="WeaponProgressionConfig.carrierLook"/>),
    /// un halo que late detrás, una columna de luz hacia arriba (se ve aunque esté lejos) y el icono de lo que suelta
    /// flotando encima — el núcleo, o el alma si tus armas ya no pueden subir más (<see cref="WeaponRunProgress.NothingLeft"/>).
    /// Fuera de pantalla, <see cref="ArsenalProgressUi"/> pone una flecha en el borde (<see cref="Active"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class CoreCarrier : MonoBehaviour
    {
        private static readonly List<CoreCarrier> ActiveList = new List<CoreCarrier>();
        private static Sprite _glow;

        /// <summary>Portadores vivos ahora (para la flecha del borde de la pantalla).</summary>
        public static IReadOnlyList<CoreCarrier> Active => ActiveList;

        private Health _health;
        private Collider2D _collider;
        private SpriteRenderer[] _renderers;
        private SpriteRenderer _halo, _beam, _icon;
        private float _age, _iconCheck;
        private bool _done, _souls;

        /// <summary>Lo que soltaría ahora: almas (sin nada que subir) o núcleo.</summary>
        public bool DropsSouls => _souls;
        public Vector2 Center => _collider != null ? (Vector2)_collider.bounds.center : (Vector2)transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveList.Clear();

        /// <summary>Convierte <paramref name="enemy"/> en portador (aspecto, fuerza, marcas, aviso).</summary>
        public static CoreCarrier Make(GameObject enemy)
        {
            if (enemy == null || enemy.GetComponent<CoreCarrier>() != null) return null;
            var cfg = WeaponProgressionConfig.Instance;
            if (cfg.carrierLook != null) cfg.carrierLook.Apply(enemy, cfg.variantShader);
            var c = enemy.AddComponent<CoreCarrier>();
            ArsenalProgressUi.Toast("progress.carrier_appeared");
            ArsenalSounds.Play("star_mark", c.Center);
            return c;
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _collider = GetComponent<Collider2D>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (_health != null) _health.Died += OnDied;
            _souls = WeaponRunProgress.NothingLeft();
            BuildMarks();
            ActiveList.Add(this);
        }

        private void OnDestroy()
        {
            ActiveList.Remove(this);
            if (_health != null) _health.Died -= OnDied;
            DestroyMarks();
        }

        // ------------------------------------------------------------------ marcas

        private void BuildMarks()
        {
            var cfg = WeaponProgressionConfig.Instance;
            _halo = Mark("CarrierHalo", Glow(), cfg.glowMaterial, 1);
            _beam = Mark("CarrierBeam", Glow(), cfg.glowMaterial, 1);
            _icon = Mark("CarrierIcon", IconSprite(), null, 70);
        }

        private SpriteRenderer Mark(string name, Sprite sprite, Material material, int order)
        {
            // Sueltas en el mundo (no hijas): una torreta girada no gira la columna de luz.
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = "Characters";
            sr.sortingOrder = order;
            return sr;
        }

        private void DestroyMarks()
        {
            if (_halo != null) Destroy(_halo.gameObject);
            if (_beam != null) Destroy(_beam.gameObject);
            if (_icon != null) Destroy(_icon.gameObject);
        }

        private Sprite IconSprite()
        {
            if (!_souls) return WeaponProgressionConfig.Instance.coreSprite;
            var visual = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config?.VisualFor(Currency.SoulFragment) : null;
            return visual != null ? visual.icon : WeaponProgressionConfig.Instance.coreSprite;
        }

        /// <summary>Mancha radial blanca (se tiñe con el color del halo); una para todos, hecha en código.</summary>
        private static Sprite Glow()
        {
            if (_glow != null) return _glow;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            _glow.hideFlags = HideFlags.DontSave;
            return _glow;
        }

        private void LateUpdate()
        {
            if (_done || _halo == null) return;
            var cfg = WeaponProgressionConfig.Instance;
            Vector2 c = Center;
            float size = _collider != null ? Mathf.Max(_collider.bounds.size.x, _collider.bounds.size.y) : 1.5f;
            float t = Time.time;
            bool visible = _renderers.Length == 0 || _renderers[0] == null || _renderers[0].enabled;

            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            _halo.transform.position = c;
            _halo.transform.localScale = Vector3.one * size * (2.2f + 0.35f * pulse);
            _halo.color = new Color(cfg.carrierGlow.r, cfg.carrierGlow.g, cfg.carrierGlow.b, (0.55f + 0.35f * pulse) * (visible ? 1f : 0.4f));

            const float beamHeight = 16f;
            _beam.transform.position = c + Vector2.up * (beamHeight * 0.5f);
            _beam.transform.localScale = new Vector3(size * (0.7f + 0.15f * pulse), beamHeight, 1f);
            _beam.color = new Color(cfg.carrierGlow.r, cfg.carrierGlow.g, cfg.carrierGlow.b, 0.3f + 0.1f * pulse);

            float top = _collider != null ? _collider.bounds.max.y : c.y + 1f;
            _icon.transform.position = new Vector2(c.x, top + 0.9f + 0.15f * Mathf.Sin(t * 3f));
            _icon.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 15f);
            if (_icon.sprite != null)
                _icon.transform.localScale = Vector3.one * (0.9f / Mathf.Max(0.01f, _icon.sprite.bounds.size.y)) * (1f + 0.12f * pulse);

            // Lo que suelta puede cambiar (otro núcleo ganado, un arma al máximo): el icono lo dice.
            if ((_iconCheck -= Time.deltaTime) <= 0f)
            {
                _iconCheck = 1f;
                bool souls = WeaponRunProgress.NothingLeft();
                if (souls != _souls) { _souls = souls; _icon.sprite = IconSprite(); }
            }
        }

        private void Update()
        {
            if (_done || _health == null || _health.IsDead) return;
            var cfg = WeaponProgressionConfig.Instance;
            _age += Time.deltaTime;
            float left = cfg.carrierLifetime - _age;

            if (left <= cfg.carrierBlink)
            {
                bool on = Mathf.Repeat(Time.time * Mathf.Lerp(10f, 4f, Mathf.Clamp01(left / Mathf.Max(0.01f, cfg.carrierBlink))), 1f) < 0.6f;
                foreach (var r in _renderers) if (r != null) r.enabled = on;
            }
            if (left > 0f) return;

            // Se escapa: sin morir, así que tampoco suelta botín.
            _done = true;
            ArsenalProgressUi.Toast("progress.carrier_escaped");
            ArsenalReactFx.Steam(Center);   // se esfuma
            Destroy(gameObject);
        }

        private void OnDied()
        {
            if (_done) return;
            _done = true;
            foreach (var r in _renderers) if (r != null) r.enabled = true;
            DestroyMarks();
            if (WeaponRunProgress.NothingLeft()) CorePickup.SpawnSouls(Center, WeaponProgressionConfig.Instance.soulsInsteadOfCore);
            else CorePickup.Spawn(Center);
        }
    }
}
