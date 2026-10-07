using System.Collections.Generic;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Una barra de vida de enemigo: relleno con estela al recibir daño y destello, la vida exacta
    /// opcional, el escudo en segmentos encima y, si el jugador los ve, una tira por cooldown debajo
    /// (<see cref="IEnemyCooldownSource"/>: se llena al recargar y parpadea mientras ataca).
    /// Tres tamaños (<see cref="Size"/>) para la barra flotante, la de foco y la lista.
    /// Se reutiliza: <see cref="Bind"/> la apunta a otro enemigo sin reconstruirla.
    /// </summary>
    public class EnemyBarView : VisualElement
    {
        public enum Size { Overhead, List, Focus }

        private const float GhostHold = 0.35f;
        private const int MaxShieldPips = 12;

        private readonly float _barHeight, _stripHeight;
        private readonly VisualElement _ghost, _fill, _flash, _shieldRow, _strips;
        private readonly Label _label;
        private readonly List<(VisualElement track, VisualElement fill)> _stripViews = new();

        private HudEnemies.Info _info;
        private float _fillValue = 1f, _ghostValue = 1f, _ghostHold, _flashValue;
        private float _lastHealth = -1f;
        private string _text = "";
        private int _pips = -1;

        public HudEnemies.Info Info => _info;

        /// <summary>0..1 de opacidad, para entrar y salir con fundido (la lleva quien la gestiona).</summary>
        public float Fade = 1f;

        public EnemyBarView(Size size)
        {
            pickingMode = PickingMode.Ignore;
            float width, font, shieldHeight;
            switch (size)
            {
                case Size.Overhead: width = 170f; _barHeight = 20f; font = 15f; shieldHeight = 7f; _stripHeight = 6f; break;
                case Size.Focus: width = 760f; _barHeight = 36f; font = 24f; shieldHeight = 12f; _stripHeight = 10f; break;
                default: width = 380f; _barHeight = 28f; font = 20f; shieldHeight = 9f; _stripHeight = 8f; break;
            }

            style.width = width;
            style.flexDirection = FlexDirection.Column;

            var frame = new VisualElement { name = "frame", pickingMode = PickingMode.Ignore };
            frame.style.height = _barHeight;
            frame.style.backgroundColor = HudStyle.FrameDark;
            HudStyle.Border(frame, 2f, HudStyle.Frame);
            HudStyle.Radius(frame, _barHeight * 0.3f);
            frame.style.paddingLeft = 2;
            frame.style.paddingRight = 2;
            frame.style.paddingTop = 2;
            frame.style.paddingBottom = 2;
            Add(frame);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.flexGrow = 1;
            track.style.backgroundColor = HudStyle.HealthTrack;
            track.style.overflow = Overflow.Hidden;
            HudStyle.Radius(track, _barHeight * 0.22f);
            frame.Add(track);

            _ghost = Bar(HudStyle.HealthGhost);
            _fill = Bar(HudStyle.EnemyFill);
            track.Add(_ghost);
            track.Add(_fill);
            var shine = HudStyle.Layer("shine", new Color(1f, 1f, 1f, 0.16f));
            shine.style.bottom = Length.Percent(58f);
            track.Add(shine);
            _flash = HudStyle.Layer("flash", new Color(1f, 1f, 1f, 0f));
            track.Add(_flash);

            _label = HudStyle.OutlinedLabel("value", font);
            HudStyle.Fill(_label);
            frame.Add(_label);

            // Escudo: segmentos flotando encima (no cambian el alto de la pieza).
            _shieldRow = new VisualElement { name = "shield", pickingMode = PickingMode.Ignore };
            _shieldRow.style.position = Position.Absolute;
            _shieldRow.style.left = 0;
            _shieldRow.style.right = 0;
            _shieldRow.style.bottom = Length.Percent(100f);
            _shieldRow.style.height = shieldHeight;
            _shieldRow.style.marginBottom = 3;
            _shieldRow.style.flexDirection = FlexDirection.Row;
            _shieldRow.style.display = DisplayStyle.None;
            frame.Add(_shieldRow);

            _strips = new VisualElement { name = "cooldowns", pickingMode = PickingMode.Ignore };
            _strips.style.flexDirection = FlexDirection.Column;
            Add(_strips);

            // Arte (HudSkin): aro hueco de enemigo delante del relleno.
            var skin = HudSkin.Current;
            if (skin != null) HudStyle.SkinBar(frame, track, skin.enemyFrame, skin.enemyRim, _barHeight);
        }

        private static VisualElement Bar(Color color)
        {
            var bar = HudStyle.Layer("bar", color);
            bar.style.right = StyleKeyword.Auto;
            bar.style.width = Length.Percent(100f);
            return bar;
        }

        public void Bind(HudEnemies.Info info)
        {
            if (info == _info) return;
            _info = info;
            _lastHealth = -1f;
            _pips = -1;
            if (info == null) return;
            _fillValue = _ghostValue = info.Health.Normalized;
            _ghostHold = 0f;
            _flashValue = 0f;
        }

        /// <summary>Barra de muestra para colocarla en Opciones (sin enemigo).</summary>
        public void ShowSample(float fraction, bool numbers, bool cooldowns)
        {
            Bind(null);
            _fill.style.width = _ghost.style.width = Length.Percent(fraction * 100f);
            SetText(numbers ? $"{Mathf.RoundToInt(fraction * 80f)} / 80" : "");
            _shieldRow.style.display = DisplayStyle.None;
            SyncStrips(cooldowns ? 1 : 0);
            if (cooldowns) PaintStrip(0, EnemyCooldownKind.Attack, new CooldownState { Remaining = 0.6f, Duration = 1.8f });
            style.opacity = 1f;
        }

        public void Tick(float dt, bool numbers, bool cooldowns)
        {
            style.opacity = Fade;
            if (_info == null || _info.Health == null) return;

            var health = _info.Health;
            float target = health.Normalized;

            if (_lastHealth >= 0f && health.CurrentHealth < _lastHealth - 0.001f)
            {
                _ghostValue = Mathf.Max(_ghostValue, _fillValue);
                _ghostHold = GhostHold;
                _flashValue = 1f;
            }
            _lastHealth = health.CurrentHealth;

            _fillValue = target < _fillValue ? target : HudStyle.Smooth(_fillValue, target, 8f, dt);
            if (_ghostHold > 0f) _ghostHold -= dt;
            else if (_ghostValue > _fillValue) _ghostValue = Mathf.Max(_fillValue, HudStyle.Smooth(_ghostValue, _fillValue - 0.02f, 5f, dt));
            if (_ghostValue < _fillValue) _ghostValue = _fillValue;

            _fill.style.width = Length.Percent(_fillValue * 100f);
            _ghost.style.width = Length.Percent(_ghostValue * 100f);

            if (_flashValue > 0f)
            {
                _flashValue = Mathf.Max(0f, _flashValue - dt * 5f);
                _flash.style.backgroundColor = new Color(1f, 1f, 1f, _flashValue * 0.6f);
            }

            SetText(numbers ? $"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}" : "");
            UpdateShield();
            UpdateCooldowns(cooldowns);
        }

        private void SetText(string text)
        {
            if (text == _text) return;
            _text = text;
            _label.text = text;
        }

        private void UpdateShield()
        {
            var (current, max) = _info.Shield();
            bool show = current > 0f && max > 0f && max <= MaxShieldPips;
            _shieldRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            int count = Mathf.RoundToInt(max);
            if (count != _pips)
            {
                _pips = count;
                _shieldRow.Clear();
                for (int i = 0; i < count; i++)
                {
                    var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                    pip.style.flexGrow = 1;
                    pip.style.marginRight = i < count - 1 ? 3 : 0;
                    HudStyle.Radius(pip, 4f);
                    HudStyle.Border(pip, 1f, HudStyle.FrameDark);
                    _shieldRow.Add(pip);
                }
            }

            int index = 0;
            foreach (var pip in _shieldRow.Children())
                pip.style.backgroundColor = index++ < current ? HudStyle.Shield : HudStyle.ShieldEmpty;
        }

        // ---------------------------------------------------------------- cooldowns

        private void UpdateCooldowns(bool show)
        {
            int count = show ? _info.Cooldowns.Count : 0;
            SyncStrips(count);
            for (int i = 0; i < count; i++)
            {
                var source = _info.Cooldowns[i];
                if (source is Behaviour b && (b == null || !b.isActiveAndEnabled))
                {
                    _stripViews[i].track.style.display = DisplayStyle.None;
                    continue;
                }
                _stripViews[i].track.style.display = DisplayStyle.Flex;
                PaintStrip(i, source.Kind, source.Cooldown);
            }
        }

        private void SyncStrips(int count)
        {
            while (_stripViews.Count < count)
            {
                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.style.height = _stripHeight;
                track.style.marginTop = 3;
                track.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
                track.style.overflow = Overflow.Hidden;
                HudStyle.Radius(track, _stripHeight * 0.5f);
                var fill = HudStyle.Layer("fill", Color.white);
                fill.style.right = StyleKeyword.Auto;
                track.Add(fill);
                _strips.Add(track);
                _stripViews.Add((track, fill));
            }

            for (int i = 0; i < _stripViews.Count; i++)
                _stripViews[i].track.style.display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Recargando: se llena apagada. Lista: llena y viva. Atacando: parpadea en blanco-rojo.</summary>
        private void PaintStrip(int index, EnemyCooldownKind kind, CooldownState state)
        {
            var (_, fill) = _stripViews[index];
            var accent = kind == EnemyCooldownKind.Shield ? HudStyle.Shield : HudStyle.Accent(CooldownSlot.Attack);

            if (state.Charge > 0f)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 28f);
                fill.style.width = Length.Percent(100f);
                fill.style.backgroundColor = Color.Lerp(new Color(1f, 0.25f, 0.2f), Color.white, blink);
                return;
            }

            float progress = 1f - state.Fraction;
            fill.style.width = Length.Percent(progress * 100f);
            fill.style.backgroundColor = state.Ready ? accent : new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 0.9f);
        }
    }
}
