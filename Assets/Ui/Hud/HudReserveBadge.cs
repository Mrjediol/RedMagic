using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Insignia del hueco de reserva (<see cref="IReserveCooldownSource"/>), pegada abajo a la derecha de la casilla del
    /// arma: icono de la otra arma, su recarga y la tecla de cambiar (Tab con teclado, ▲ con mando; en táctil la fila de
    /// casillas no se ve). Al cambiar de arma da un golpe para que se vea el intercambio.
    /// </summary>
    public class HudReserveBadge : VisualElement
    {
        public const float Size = 60f;

        private readonly VisualElement _body;
        private readonly Image _icon;
        private readonly CooldownDial _dial;
        private readonly Label _key;
        private readonly PadGlyph _pad;
        private float _popT = 1f;

        public HudReserveBadge()
        {
            name = "reserve-weapon";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.width = Size;
            style.height = Size;

            _body = new VisualElement { name = "body", pickingMode = PickingMode.Ignore };
            HudStyle.Fill(_body);
            _body.style.backgroundColor = HudStyle.PanelBg;
            HudStyle.Radius(_body, Size * 0.5f);
            HudStyle.Border(_body, 3f, HudStyle.Frame);
            Add(_body);

            _icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            HudStyle.Fill(_icon);
            _icon.style.left = _icon.style.right = _icon.style.top = _icon.style.bottom = Size * 0.14f;
            _body.Add(_icon);

            _dial = new CooldownDial { Accent = HudStyle.Accent(CooldownSlot.Weapon), RingWidth = 4f };
            Add(_dial);

            // Tecla de cambiar, como una chapita sobre el borde de arriba a la derecha.
            var cap = new VisualElement { name = "swap-key", pickingMode = PickingMode.Ignore };
            cap.style.position = Position.Absolute;
            cap.style.right = -Size * 0.3f;
            cap.style.top = -Size * 0.24f;
            cap.style.minWidth = 30f;
            cap.style.height = 26f;
            cap.style.paddingLeft = cap.style.paddingRight = 5f;
            cap.style.justifyContent = Justify.Center;
            cap.style.alignItems = Align.Center;
            cap.style.backgroundColor = HudStyle.FrameDark;
            HudStyle.Border(cap, 2f, HudStyle.Frame);
            HudStyle.Radius(cap, 6f);
            Add(cap);

            _key = HudStyle.OutlinedLabel("key", 17f);
            _key.text = "Tab";   // nombre de tecla, como en HudControlsLegend: sin clave de idioma
            cap.Add(_key);
            _pad = new PadGlyph();
            cap.Add(_pad);
        }

        /// <summary>Lee la reserva y anima. Se esconde si no hay nada en reserva.</summary>
        public void Tick(IReserveCooldownSource source, float dt)
        {
            bool show = source != null && source.HasReserve;
            var display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (style.display != display) style.display = display;
            if (!show) { if (!_dial.Ready) _dial.ResetState(); return; }

            var sprite = source.ReserveIcon;
            if (_icon.sprite != sprite) _icon.sprite = sprite;

            var state = source.ReserveCooldown;
            _dial.Tick(state, dt);
            _icon.tintColor = state.Ready ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.55f, 0.52f, 0.5f, 0.8f);

            bool pad = InputDeviceManager.GamepadActive;
            _key.style.display = pad ? DisplayStyle.None : DisplayStyle.Flex;
            _pad.style.display = pad ? DisplayStyle.Flex : DisplayStyle.None;

            if (_popT < 1f)
            {
                _popT = Mathf.Min(1f, _popT + dt / 0.3f);
                float s = HudStyle.Punch(_popT, 1.3f);
                _body.style.scale = new Scale(new Vector3(s, s, 1f));
            }
        }

        public void Pop() => _popT = 0f;

        /// <summary>El triángulo del botón norte del mando (acción SwapWeapon).</summary>
        private class PadGlyph : VisualElement
        {
            public PadGlyph()
            {
                pickingMode = PickingMode.Ignore;
                style.width = 18f;
                style.height = 16f;
                generateVisualContent += ctx =>
                {
                    var r = contentRect;
                    var p = ctx.painter2D;
                    p.strokeColor = new Color(0.45f, 0.95f, 0.8f, 1f);
                    p.lineWidth = 2.6f;
                    p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    p.MoveTo(new Vector2(r.center.x, r.yMin + 1.5f));
                    p.LineTo(new Vector2(r.xMax - 1.5f, r.yMax - 1.5f));
                    p.LineTo(new Vector2(r.xMin + 1.5f, r.yMax - 1.5f));
                    p.ClosePath();
                    p.Stroke();
                };
            }
        }
    }
}
