using RedMagic.Combat;
using RedMagic.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// "Efectos de interfaz" a pantalla completa, detrás de las piezas del HUD: una viñeta roja en
    /// los bordes (el centro, donde se juega, queda siempre limpio).
    /// <list type="bullet">
    /// <item><b>Golpe</b>: destello en los bordes al encajar daño, más fuerte cuanto mayor el golpe,
    /// que se apaga en ~0,7 s.</item>
    /// <item><b>Poca vida</b> (bajo <see cref="Health.LowHealthThreshold"/>): latido doble muy suave,
    /// como un pulso, que entra y sale con fundido.</item>
    /// </list>
    /// Los dos se activan y se dosifican en Opciones ▸ Interfaz (<see cref="HudSettings"/>). La viñeta
    /// es una textura generada en código (no hay arte que importar).
    /// </summary>
    public class HudScreenFx : VisualElement
    {
        // Techo de opacidad con la intensidad al máximo: el golpe se nota, el latido no estorba.
        private const float HurtMaxAlpha = 0.85f;
        private const float LowMaxAlpha = 0.42f;
        private const float HurtFadeTime = 0.7f;

        private static Texture2D _vignette;

        private readonly VisualElement _hurt, _low;
        private Health _health;
        private float _hurtLevel;
        private float _lowLevel;
        private float _beatTime;

        public HudScreenFx()
        {
            name = "screen-fx";
            pickingMode = PickingMode.Ignore;
            HudStyle.Fill(this);

            _low = Layer("low-health");
            _hurt = Layer("hurt");
        }

        private VisualElement Layer(string layerName)
        {
            var layer = new VisualElement { name = layerName, pickingMode = PickingMode.Ignore };
            HudStyle.Fill(layer);
            layer.style.backgroundImage = new StyleBackground(Vignette);
            layer.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, 0f);
            layer.style.display = DisplayStyle.None;
            Add(layer);
            return layer;
        }

        public void Unbind()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            _health = null;
        }

        public void Tick(float dt)
        {
            var health = HudPlayer.Health;
            if (health != _health)
            {
                Unbind();
                _health = health;
                if (health != null) health.Damaged += OnDamaged;
                _hurtLevel = 0f;
            }

            // --- golpe
            if (_hurtLevel > 0f) _hurtLevel = Mathf.Max(0f, _hurtLevel - dt / HurtFadeTime);
            float hurtAlpha = HudSettings.HurtEffect ? _hurtLevel * _hurtLevel * HurtMaxAlpha * HudSettings.HurtIntensity : 0f;
            Apply(_hurt, hurtAlpha);

            // --- poca vida: entra y sale con fundido, nunca de golpe.
            bool low = health != null && !health.IsDead && health.Normalized <= health.LowHealthThreshold;
            _lowLevel = Mathf.MoveTowards(_lowLevel, low ? 1f : 0f, dt / 0.8f);
            float lowAlpha = 0f;
            if (_lowLevel > 0f && HudSettings.LowHealthEffect)
            {
                _beatTime += dt;
                lowAlpha = _lowLevel * Heartbeat(_beatTime) * LowMaxAlpha * HudSettings.LowHealthIntensity;
            }
            else _beatTime = 0f;
            Apply(_low, lowAlpha);
        }

        private void OnDamaged(float amount)
        {
            if (_health == null || _health.MaxHealth <= 0f) return;

            // Un golpe del 10 % de la vida ya se nota entero; los pequeños, a medias.
            float strength = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(amount / _health.MaxHealth / 0.1f));
            _hurtLevel = Mathf.Max(_hurtLevel, strength);
        }

        private static void Apply(VisualElement layer, float alpha)
        {
            bool visible = alpha > 0.002f;
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (layer.style.display != display) layer.style.display = display;
            if (visible) layer.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>Pulso "pum-pum… pum-pum" cada 1,1 s, entre 0,55 y 1 (nunca se apaga del todo).</summary>
        private static float Heartbeat(float t)
        {
            const float period = 1.1f;
            float phase = t % period;
            float beat = Bump(phase, 0f, 0.14f) + 0.7f * Bump(phase, 0.22f, 0.16f);
            return 0.55f + 0.45f * Mathf.Clamp01(beat);
        }

        private static float Bump(float t, float start, float length)
        {
            float x = (t - start) / length;
            return x is < 0f or > 1f ? 0f : Mathf.Sin(x * Mathf.PI);
        }

        // ---------------------------------------------------------------- textura

        /// <summary>Viñeta roja: transparente en el centro, opaca en los bordes (elíptica, se estira a la pantalla).</summary>
        private static Texture2D Vignette
        {
            get
            {
                if (_vignette != null) return _vignette;

                const int size = 128;
                _vignette = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "HudVignette",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color32[size * size];
                var deep = new Color(0.55f, 0.02f, 0.03f);
                var bright = new Color(0.95f, 0.08f, 0.06f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    // Mezcla de elipse y "cuadrado redondeado": cubre también las esquinas sin comerse el centro.
                    float d = Mathf.Lerp(Mathf.Sqrt(u * u + v * v) / 1.41f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)), 0.55f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, d));
                    var c = Color.Lerp(bright, deep, a);
                    pixels[y * size + x] = new Color(c.r, c.g, c.b, a * a * 0.9f + a * 0.1f);
                }

                _vignette.SetPixels32(pixels);
                _vignette.Apply(false, true);
                return _vignette;
            }
        }
    }
}
