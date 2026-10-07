using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// La animación de un cooldown dibujada en código (Painter2D), encima de un botón redondo:
    /// <list type="bullet">
    /// <item>sombra en forma de tarta que se retira en el sentido de las agujas del reloj,</item>
    /// <item>anillo de progreso del color de acento, con la punta más brillante,</item>
    /// <item>anillo de carga por fuera (armas que se mantienen),</item>
    /// <item>al quedar lista: onda que se expande y destello.</item>
    /// </list>
    /// Es una capa transparente que llena a su padre: la usan <see cref="HudCooldownSlot"/> (HUD de
    /// PC/mando) y los botones táctiles, así que las dos se ven igual. Quien la tiene llama a
    /// <see cref="Tick"/> cada frame con el estado de la fuente.
    /// </summary>
    public class CooldownDial : VisualElement
    {
        private const float ReadyFlashTime = 0.42f;

        /// <summary>Con cooldowns más cortos no se lanza la onda de "listo" (el ataque básico spamea).</summary>
        public float ReadyFlashMinDuration = 0.7f;

        public Color Accent = HudStyle.Gold;

        /// <summary>Grosor del anillo de progreso, en píxeles de referencia.</summary>
        public float RingWidth = 5f;

        private CooldownState _state;
        private float _flash = 1f;       // 0 = acaba de quedar lista, 1 = sin destello
        private bool _wasReady = true;

        /// <summary>Se dispara al pasar de "en cooldown" a "lista" (para el rebote del icono).</summary>
        public event System.Action BecameReady;

        /// <summary>Se dispara al gastarse (el cooldown arranca de nuevo).</summary>
        public event System.Action Used;

        public bool Ready => _state.Ready;
        public CooldownState State => _state;

        public CooldownDial()
        {
            pickingMode = PickingMode.Ignore;
            HudStyle.Fill(this);
            generateVisualContent += Draw;
        }

        public void Tick(CooldownState state, float dt)
        {
            bool ready = state.Ready;
            if (state.Remaining > _state.Remaining + 0.05f) Used?.Invoke();

            if (ready && !_wasReady)
            {
                if (_state.Duration >= ReadyFlashMinDuration) _flash = 0f;
                BecameReady?.Invoke();
            }

            _wasReady = ready;
            _state = state;
            if (_flash < 1f) _flash = Mathf.Min(1f, _flash + dt / ReadyFlashTime);
            MarkDirtyRepaint();
        }

        /// <summary>Vuelve al estado "lista" sin animar (al cambiar de jugador o de fuente).</summary>
        public void ResetState()
        {
            _state = default;
            _wasReady = true;
            _flash = 1f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 1f) return;

            var center = rect.center;
            var p = ctx.painter2D;

            // Sombra: lo que falta del cooldown (o todo, si está bloqueada).
            float fraction = _state.Blocked ? 1f : _state.Fraction;
            if (fraction > 0.001f)
            {
                p.fillColor = HudStyle.CooldownShade;
                if (fraction >= 0.999f)
                {
                    p.BeginPath();
                    p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                }
                else
                {
                    float start = -90f + (1f - fraction) * 360f;
                    p.BeginPath();
                    p.MoveTo(center);
                    p.Arc(center, radius, Angle.Degrees(start), Angle.Degrees(270f));
                    p.ClosePath();
                    p.Fill();
                }
            }

            float ringRadius = radius - RingWidth * 0.5f;

            // Anillo de progreso: lo ya recargado, con la punta brillante.
            if (!_state.Blocked && fraction > 0.001f && fraction < 0.999f)
            {
                float end = -90f + (1f - fraction) * 360f;
                p.lineWidth = RingWidth;
                p.lineCap = LineCap.Round;
                p.strokeColor = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
                p.BeginPath();
                p.Arc(center, ringRadius, Angle.Degrees(-90f), Angle.Degrees(end));
                p.Stroke();

                float rad = end * Mathf.Deg2Rad;
                var tip = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * ringRadius;
                p.fillColor = Color.Lerp(Accent, Color.white, 0.6f);
                p.BeginPath();
                p.Arc(tip, RingWidth * 0.95f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            // Carga (mantener el botón): anillo por fuera.
            if (_state.Charge > 0.001f)
            {
                float chargeRadius = radius + RingWidth * 1.3f;
                p.lineWidth = RingWidth * 1.2f;
                p.lineCap = LineCap.Round;
                p.strokeColor = Color.Lerp(Accent, Color.white, _state.Charge >= 0.999f ? 0.7f : 0.2f);
                p.BeginPath();
                p.Arc(center, chargeRadius, Angle.Degrees(-90f), Angle.Degrees(-90f + _state.Charge * 360f));
                p.Stroke();
            }

            // "Lista": onda que sale y un destello que se apaga.
            if (_flash < 1f)
            {
                float t = _flash;
                float ease = 1f - (1f - t) * (1f - t);
                p.lineWidth = Mathf.Lerp(RingWidth * 1.6f, 1f, t);
                p.strokeColor = new Color(Accent.r, Accent.g, Accent.b, (1f - t) * 0.95f);
                p.BeginPath();
                p.Arc(center, radius * (1f + 0.45f * ease), Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();

                p.fillColor = new Color(1f, 1f, 1f, (1f - t) * (1f - t) * 0.45f);
                p.BeginPath();
                p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }
    }
}
