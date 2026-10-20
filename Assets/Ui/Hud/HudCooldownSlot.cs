using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Casilla redonda del HUD para una acción con cooldown (<see cref="ICooldownSource"/>): fondo,
    /// icono (el de la fuente, o un glifo vectorial por defecto), el <see cref="CooldownDial"/> y los
    /// segundos que faltan en el centro (sólo si el cooldown es largo, para no parpadear números).
    ///
    /// Se esconde sola si nadie ocupa la casilla (p. ej. Parry hasta que exista). Si la fuente tiene hueco de reserva
    /// (<see cref="IReserveCooldownSource"/>: la segunda arma) lleva pegada la <see cref="HudReserveBadge"/>.
    /// </summary>
    public class HudCooldownSlot : VisualElement
    {
        /// <summary>Por debajo de esto no se escriben los segundos: el barrido ya lo dice todo.</summary>
        private const float MinDurationForNumber = 1.5f;

        private readonly CooldownSlot _slot;
        private readonly VisualElement _body;
        private readonly Image _icon;
        private readonly SlotGlyph _glyph;
        private readonly CooldownDial _dial;
        private readonly Label _seconds;
        private readonly Image _ring;              // aro del HudSkin (null sin arte)
        private readonly HudSkin _skin;
        private readonly HudReserveBadge _reserve;   // sólo la casilla del arma
        private float _lastSwap = float.NaN;

        private ICooldownSource _source;
        private float _popT = 1f;
        private float _popPeak = 1f;
        private string _secondsText = "";

        public CooldownSlot Slot => _slot;

        public HudCooldownSlot(CooldownSlot slot)
        {
            _slot = slot;
            name = $"cooldown-{slot}";
            pickingMode = PickingMode.Ignore;
            style.width = HudStyle.SlotSize;
            style.height = HudStyle.SlotSize;
            style.marginRight = HudStyle.SlotGap;
            style.flexShrink = 0;

            _body = new VisualElement { name = "body", pickingMode = PickingMode.Ignore };
            HudStyle.Fill(_body);
            _body.style.backgroundColor = HudStyle.PanelBg;
            HudStyle.Radius(_body, HudStyle.SlotSize * 0.5f);
            HudStyle.Border(_body, 3f, HudStyle.Frame);
            Add(_body);

            _icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            Inset(_icon, 0.2f);
            _body.Add(_icon);

            _glyph = new SlotGlyph(slot);
            Inset(_glyph, 0.24f);
            _body.Add(_glyph);

            _dial = new CooldownDial { Accent = HudStyle.Accent(slot) };
            _dial.BecameReady += () => Pop(1.16f);
            _dial.Used += () => Pop(0.86f);
            Add(_dial);

            // Arte (HudSkin): aro de piedra delante (apagado / encendido) y el barrido por dentro de él.
            _skin = HudSkin.Current;
            if (_skin != null && _skin.abilityRing.IsSet)
            {
                HudStyle.Border(_body, 0f, Color.clear);
                Inset(_body, _skin.abilityRingInset * 0.5f);
                Inset(_dial, _skin.abilityRingInset);
                _ring = new Image { name = "ring", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                _ring.sprite = _skin.abilityRing.normal;
                Inset(_ring, -_skin.abilityRingOverflow);
                Add(_ring);
            }

            _seconds = HudStyle.OutlinedLabel("seconds", HudStyle.SlotFont);
            HudStyle.Fill(_seconds);
            Add(_seconds);

            if (slot == CooldownSlot.Weapon)
            {
                _reserve = new HudReserveBadge();
                _reserve.style.left = HudStyle.SlotSize * 0.72f;
                _reserve.style.top = HudStyle.SlotSize * 0.48f;
                _reserve.style.display = DisplayStyle.None;
                Add(_reserve);
            }

            SetIcon(ResolveIcon(null));
        }

        /// <summary>El icono de la fuente (el del arma), si no el del skin, si no ninguno (glifo vectorial).</summary>
        private Sprite ResolveIcon(ICooldownSource source)
        {
            var own = source?.CooldownIcon;
            if (own != null) return own;
            return _skin != null ? _skin.IconFor(_slot) : null;
        }

        private static void Inset(VisualElement e, float fraction)
        {
            HudStyle.Fill(e);
            e.style.left = Length.Percent(fraction * 100f);
            e.style.right = Length.Percent(fraction * 100f);
            e.style.top = Length.Percent(fraction * 100f);
            e.style.bottom = Length.Percent(fraction * 100f);
        }

        /// <summary>Lee la fuente del jugador actual y anima. Devuelve false si la casilla no la usa nadie.</summary>
        public bool Tick(float dt)
        {
            var source = HudPlayer.Cooldown(_slot);
            if (source != _source)
            {
                _source = source;
                _dial.ResetState();
                SetIcon(ResolveIcon(source));
            }

            if (source == null) return false;

            // El icono del arma cambia al cambiar de arma sin cambiar de fuente.
            var icon = ResolveIcon(source);
            if (icon != _icon.sprite) SetIcon(icon);

            var state = source.Cooldown;
            _dial.Tick(state, dt);

            if (_reserve != null)
            {
                var reserve = source as IReserveCooldownSource;
                _reserve.Tick(reserve, dt);
                float swap = reserve != null ? reserve.LastSwapTime : float.NaN;
                if (!float.IsNaN(_lastSwap) && swap != _lastSwap) { Pop(1.2f); _reserve.Pop(); }
                _lastSwap = swap;
            }

            bool ready = state.Ready;
            var tint = ready ? Color.white : new Color(0.55f, 0.52f, 0.5f, state.Blocked ? 0.5f : 0.85f);
            _icon.tintColor = tint;
            _glyph.Tint = ready ? HudStyle.Accent(_slot) : tint * HudStyle.Accent(_slot);

            if (_ring != null)
            {
                var ringSprite = _skin.abilityRing.Get(ready);
                if (_ring.sprite != ringSprite) _ring.sprite = ringSprite;
                _ring.tintColor = state.Blocked ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : Color.white;
            }
            else HudStyle.BorderColor(_body, ready ? HudStyle.Frame : new Color(0.3f, 0.23f, 0.17f, 0.9f));

            string text = !state.Blocked && state.Remaining > 0f && state.Duration >= MinDurationForNumber
                ? (state.Remaining >= 10f ? Mathf.CeilToInt(state.Remaining).ToString() : state.Remaining.ToString("0.0"))
                : "";
            if (text != _secondsText)
            {
                _secondsText = text;
                _seconds.text = text;
            }

            if (_popT < 1f)
            {
                _popT = Mathf.Min(1f, _popT + dt / 0.28f);
                float s = HudStyle.Punch(_popT, _popPeak);
                style.scale = new Scale(new Vector3(s, s, 1f));   // la casilla entera: cuerpo, aro y barrido
            }

            return true;
        }

        private void Pop(float peak)
        {
            _popPeak = peak;
            _popT = 0f;
        }

        private void SetIcon(Sprite sprite)
        {
            _icon.sprite = sprite;
            _icon.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            _glyph.style.display = sprite != null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ---------------------------------------------------------------- glifo por defecto

        /// <summary>
        /// Icono vectorial de reserva para cada casilla (espada, dash, parry) hasta que haya arte.
        /// </summary>
        private class SlotGlyph : VisualElement
        {
            private readonly CooldownSlot _slot;
            private Color _tint = Color.white;

            public Color Tint
            {
                set
                {
                    if (value == _tint) return;
                    _tint = value;
                    MarkDirtyRepaint();
                }
            }

            public SlotGlyph(CooldownSlot slot)
            {
                _slot = slot;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width < 2f) return;

                var p = ctx.painter2D;
                p.strokeColor = _tint;
                p.fillColor = _tint;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                float w = r.width;
                Vector2 P(float x, float y) => new(r.x + x * w, r.y + y * r.height);

                switch (_slot)
                {
                    case CooldownSlot.Attack:
                        // Espada en diagonal: hoja, guarda y pomo.
                        p.lineWidth = w * 0.13f;
                        p.BeginPath();
                        p.MoveTo(P(0.30f, 0.70f));
                        p.LineTo(P(0.92f, 0.08f));
                        p.Stroke();
                        p.lineWidth = w * 0.11f;
                        p.BeginPath();
                        p.MoveTo(P(0.14f, 0.56f));
                        p.LineTo(P(0.44f, 0.86f));
                        p.Stroke();
                        p.BeginPath();
                        p.MoveTo(P(0.24f, 0.76f));
                        p.LineTo(P(0.10f, 0.90f));
                        p.Stroke();
                        break;

                    case CooldownSlot.Dash:
                        // Dos chevrones hacia la derecha.
                        p.lineWidth = w * 0.13f;
                        for (int i = 0; i < 2; i++)
                        {
                            float x = 0.12f + i * 0.36f;
                            p.BeginPath();
                            p.MoveTo(P(x, 0.16f));
                            p.LineTo(P(x + 0.32f, 0.5f));
                            p.LineTo(P(x, 0.84f));
                            p.Stroke();
                        }
                        break;

                    default:
                        // Escudo.
                        p.lineWidth = w * 0.11f;
                        p.BeginPath();
                        p.MoveTo(P(0.5f, 0.06f));
                        p.LineTo(P(0.88f, 0.2f));
                        p.BezierCurveTo(P(0.88f, 0.6f), P(0.7f, 0.82f), P(0.5f, 0.95f));
                        p.BezierCurveTo(P(0.3f, 0.82f), P(0.12f, 0.6f), P(0.12f, 0.2f));
                        p.ClosePath();
                        p.Stroke();
                        break;
                }
            }
        }
    }
}
