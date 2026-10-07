using RedMagic.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Barra de vida (y escudo) del jugador en el HUD. Capas, de atrás adelante:
    /// <list type="bullet">
    /// <item><b>estela</b>: al recibir daño se queda un instante en el valor anterior y luego baja
    /// suave, para que se lea cuánto ha quitado el golpe;</item>
    /// <item><b>curación</b>: al curar salta en verde al valor nuevo y el relleno sube detrás;</item>
    /// <item><b>relleno</b> con un brillo arriba; con poca vida late;</item>
    /// <item><b>destello</b> blanco y temblor al encajar un golpe.</item>
    /// </list>
    /// Encima, si hay escudo (<see cref="RedMagic.Gameplay.IShieldSource"/>), una barra fina: en
    /// segmentos si son pocos golpes, continua si son puntos. Todo se anima en <see cref="Tick"/>.
    /// </summary>
    public class HudHealthBar : VisualElement
    {

        private const float GhostHold = 0.45f;
        private const int MaxShieldPips = 12;

        private readonly VisualElement _frame;
        private readonly HudStyle.BarLayer _ghost, _heal, _fill, _flash;
        private readonly VisualElement _rimLayer;   // aro del skin (null = marco vectorial)
        private readonly Label _label;
        private readonly VisualElement _shieldRow, _shieldBar, _shieldFill;

        private Health _health;
        private float _fillValue = 1f, _ghostValue = 1f, _healValue = 1f;
        private float _ghostHold;
        private float _flashValue, _shake;
        private float _shieldShown = -1f, _shieldMaxShown = -1f;
        private float _shieldFlash;
        private string _text = "";

        public HudHealthBar()
        {
            name = "health";
            pickingMode = PickingMode.Ignore;
            style.width = HudStyle.HealthWidth;
            style.flexDirection = FlexDirection.Column;

            // --- escudo: flotando justo encima de la barra, sin cambiar el tamaño de la pieza (que
            // el jugador coloca a mano: aparecer un escudo no debe mover nada).
            _shieldRow = new VisualElement { name = "shield", pickingMode = PickingMode.Ignore };
            _shieldRow.style.position = Position.Absolute;
            _shieldRow.style.left = 0;
            _shieldRow.style.right = 0;
            _shieldRow.style.bottom = Length.Percent(100f);
            _shieldRow.style.height = HudStyle.ShieldHeight;
            _shieldRow.style.marginBottom = 5;
            _shieldRow.style.flexDirection = FlexDirection.Row;
            _shieldRow.style.display = DisplayStyle.None;
            Add(_shieldRow);

            _shieldBar = new VisualElement { pickingMode = PickingMode.Ignore };
            _shieldBar.style.flexGrow = 1;
            _shieldBar.style.backgroundColor = HudStyle.ShieldEmpty;
            HudStyle.Radius(_shieldBar, HudStyle.ShieldHeight * 0.5f);
            HudStyle.Border(_shieldBar, 2f, HudStyle.FrameDark);
            _shieldBar.style.overflow = Overflow.Hidden;
            _shieldFill = HudStyle.Layer("fill", HudStyle.Shield);
            _shieldFill.style.right = StyleKeyword.Auto;
            _shieldBar.Add(_shieldFill);

            // --- vida
            _frame = new VisualElement { name = "frame", pickingMode = PickingMode.Ignore };
            _frame.style.height = HudStyle.HealthHeight;
            _frame.style.backgroundColor = HudStyle.FrameDark;
            HudStyle.Radius(_frame, 10f);
            HudStyle.Border(_frame, 3f, HudStyle.Frame);
            _frame.style.paddingLeft = 3;
            _frame.style.paddingRight = 3;
            _frame.style.paddingTop = 3;
            _frame.style.paddingBottom = 3;
            Add(_frame);

            var track = new VisualElement { name = "track", pickingMode = PickingMode.Ignore };
            track.style.flexGrow = 1;
            track.style.backgroundColor = HudStyle.HealthTrack;
            track.style.overflow = Overflow.Hidden;
            HudStyle.Radius(track, 6f);
            _frame.Add(track);

            // Arte (HudSkin): aro hueco delante y relleno con su silueta. Sin skin, el marco vectorial.
            var skin = HudSkin.Current;
            var art = skin != null && HudStyle.PrepareSkinnedBar(_frame, track, skin.healthFrame, skin.healthRim, HudStyle.HealthHeight)
                ? skin.healthFrame : null;

            if (art != null && art.HasMask)
                track.Add(HudStyle.BarLayer.Create("track-bg", HudStyle.HealthTrack, art, track).Root);

            _ghost = HudStyle.BarLayer.Create("ghost", HudStyle.HealthGhost, art, track);
            _heal = HudStyle.BarLayer.Create("heal", HudStyle.HealthHeal, art, track);
            _fill = HudStyle.BarLayer.Create("fill", HudStyle.HealthFill, art, track);
            track.Add(_ghost.Root);
            track.Add(_heal.Root);
            track.Add(_fill.Root);

            // Brillo: franja clara arriba, da volumen sin textura.
            track.Add(HudStyle.BarLayer.Create("shine", new Color(1f, 1f, 1f, 0.16f), art, track, 0.42f).Root);
            if (art == null || !art.HasMask)
            {
                var shade = HudStyle.Layer("shade", new Color(0f, 0f, 0f, 0.18f));
                shade.style.top = Length.Percent(70f);
                track.Add(shade);
            }

            _flash = HudStyle.BarLayer.Create("flash", new Color(1f, 1f, 1f, 0f), art, track);
            track.Add(_flash.Root);

            _label = HudStyle.OutlinedLabel("value", HudStyle.HealthFont);
            HudStyle.Fill(_label);
            _frame.Add(_label);

            if (art != null) _rimLayer = art.Overlay(_frame);
        }

        /// <summary>Lee la vida y el escudo del jugador actual y anima. Devuelve false si no hay jugador.</summary>
        public bool Tick(float dt)
        {
            var health = HudPlayer.Health;
            if (health != _health)
            {
                _health = health;
                if (health != null) Snap(health.Normalized);
            }

            if (health == null) return false;

            UpdateHealth(health, dt);
            UpdateShield(dt);
            return true;
        }

        /// <summary>Barra llena de muestra, para colocarla en Opciones cuando no hay jugador (menú principal).</summary>
        public void ShowPreview()
        {
            _health = null;
            Snap(1f);
            _fill.SetFraction(1f);
            _heal.SetFraction(1f);
            _ghost.SetFraction(1f);
            _text = "100 / 100";
            _label.text = _text;
            _shieldRow.style.display = DisplayStyle.None;
        }

        private void Snap(float value)
        {
            _fillValue = _ghostValue = _healValue = value;
            _ghostHold = 0f;
            _flashValue = 0f;
            _shake = 0f;
        }

        private void UpdateHealth(Health health, float dt)
        {
            float target = health.Normalized;

            if (target < _healValue - 0.0001f)
            {
                // Golpe: el relleno cae ya, la estela se queda donde estaba un momento.
                _ghostValue = Mathf.Max(_ghostValue, _fillValue);
                _fillValue = target;
                _healValue = target;
                _ghostHold = GhostHold;
                _flashValue = 1f;
                _shake = 1f;
            }
            else if (target > _healValue + 0.0001f)
            {
                // Curación: el verde salta al valor nuevo y el relleno lo alcanza.
                _healValue = target;
            }

            if (_fillValue < _healValue) _fillValue = Mathf.Min(_healValue, HudStyle.Smooth(_fillValue, _healValue + 0.02f, 7f, dt));

            if (_ghostHold > 0f) _ghostHold -= dt;
            else if (_ghostValue > _fillValue)
                _ghostValue = Mathf.Max(_fillValue, HudStyle.Smooth(_ghostValue, _fillValue - 0.02f, 5f, dt));
            if (_ghostValue < _fillValue) _ghostValue = _fillValue;

            _fill.SetFraction(_fillValue);
            _heal.SetFraction(_healValue);
            _ghost.SetFraction(_ghostValue);

            // Poca vida: el relleno y el marco laten.
            bool low = target > 0f && target <= health.LowHealthThreshold;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f) : 0f;
            _fill.SetColor(low ? Color.Lerp(HudStyle.HealthFill, HudStyle.HealthFillLow, pulse) : HudStyle.HealthFill);
            if (_rimLayer != null) UiFrame.TintLayer(_rimLayer, low ? Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f, 1f), pulse) : Color.white);
            else HudStyle.BorderColor(_frame, low ? Color.Lerp(HudStyle.Frame, new Color(1f, 0.3f, 0.25f, 1f), pulse) : HudStyle.Frame);

            if (_flashValue > 0f)
            {
                _flashValue = Mathf.Max(0f, _flashValue - dt * 5f);
                _flash.SetColor(new Color(1f, 1f, 1f, _flashValue * 0.6f));
            }

            if (_shake > 0f)
            {
                _shake = Mathf.Max(0f, _shake - dt * 4f);
                float offset = Mathf.Sin(Time.unscaledTime * 70f) * 7f * _shake * _shake;
                style.translate = new Translate(offset, 0f);
            }

            string text = $"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}";
            if (text != _text)
            {
                _text = text;
                _label.text = text;
            }
        }

        private void UpdateShield(float dt)
        {
            var (current, max) = HudPlayer.Shield();
            bool show = max > 0f && current > 0f;
            _shieldRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
            {
                _shieldShown = _shieldMaxShown = -1f;
                return;
            }

            bool pips = max <= MaxShieldPips && Mathf.Approximately(max, Mathf.Round(max));
            if (!Mathf.Approximately(max, _shieldMaxShown))
            {
                _shieldMaxShown = max;
                _shieldShown = -1f;
                _shieldRow.Clear();
                if (pips)
                {
                    for (int i = 0; i < Mathf.RoundToInt(max); i++)
                    {
                        var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                        pip.style.flexGrow = 1;
                        pip.style.marginRight = i < max - 1 ? 5 : 0;
                        HudStyle.Radius(pip, HudStyle.ShieldHeight * 0.5f);
                        HudStyle.Border(pip, 2f, HudStyle.FrameDark);
                        _shieldRow.Add(pip);
                    }
                }
                else _shieldRow.Add(_shieldBar);
            }

            if (current < _shieldShown) _shieldFlash = 1f;
            _shieldShown = current;
            _shieldFlash = Mathf.Max(0f, _shieldFlash - dt * 3f);

            var on = Color.Lerp(HudStyle.Shield, Color.white, _shieldFlash * 0.6f);
            if (pips)
            {
                int i = 0;
                foreach (var pip in _shieldRow.Children())
                {
                    pip.style.backgroundColor = i < current ? on : HudStyle.ShieldEmpty;
                    i++;
                }
            }
            else
            {
                _shieldFill.style.width = Length.Percent(Mathf.Clamp01(current / max) * 100f);
                _shieldFill.style.backgroundColor = on;
            }
        }
    }
}
