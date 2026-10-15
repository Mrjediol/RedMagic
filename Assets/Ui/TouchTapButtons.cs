using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UIElements;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace RedMagic.UI
{
    /// <summary>
    /// Botones de acción táctiles (saltar, atacar, dash, arma, parry) leídos <b>directamente de los toques</b> del
    /// Input System cada frame, no con los eventos de <see cref="Button"/> de UI Toolkit: su manipulador de clic
    /// captura el puntero y retrasaba o perdía pulsaciones. Aquí un dedo que cae dentro del botón (más
    /// <see cref="HitPadding"/>) lo pulsa en ese mismo frame, aunque toque y suelte en el mismo frame
    /// (EnhancedTouch lo reporta como Began). Cada dedo es independiente: saltar y atacar a la vez funciona.
    /// El buffer de entrada lo ponen los consumidores (<c>PlayerMovement</c>, <c>PlayerAttack</c>).
    /// </summary>
    public class TouchTapButtons
    {
        /// <summary>Clase USS mientras un dedo mantiene el botón (el aspecto de :active).</summary>
        public const string PressedClass = "touch-button--pressed";

        /// <summary>Margen extra (px del panel) alrededor de cada botón para que un toque al borde cuente.</summary>
        public float HitPadding = 24f;

        private readonly List<Entry> _buttons = new();
        private readonly Dictionary<int, Entry> _held = new();
        private readonly HashSet<int> _alive = new();
        private bool _enabled;

        public void Add(VisualElement button, Action press, Action release = null)
        {
            if (button != null && press != null) _buttons.Add(new Entry(button, press, release));
        }

        public void Enable()
        {
            if (_enabled) return;
            EnhancedTouchSupport.Enable();   // con contador de referencias: no pisa a otros usuarios
            _enabled = true;
        }

        public void Disable()
        {
            Release();
            _buttons.Clear();
            if (!_enabled) return;
            EnhancedTouchSupport.Disable();
            _enabled = false;
        }

        /// <summary>Suelta todos los dedos (pausa, cambio de esquema).</summary>
        public void Release()
        {
            foreach (var entry in _held.Values)
            {
                entry.Button.RemoveFromClassList(PressedClass);
                entry.Release?.Invoke();
            }
            _held.Clear();
        }

        public void Tick(IPanel panel)
        {
            if (!_enabled || panel == null) return;

            _alive.Clear();
            foreach (var touch in ETouch.activeTouches)
            {
                int id = touch.touchId;
                var phase = touch.phase;

                if (phase == ETouchPhase.Began && !_held.ContainsKey(id))
                {
                    var hit = Hit(panel, touch.screenPosition);
                    if (hit != null)
                    {
                        hit.Press();
                        _held[id] = hit;
                        hit.Button.AddToClassList(PressedClass);
                    }
                }

                if (phase == ETouchPhase.Ended || phase == ETouchPhase.Canceled) Drop(id);
                else _alive.Add(id);
            }

            // Un dedo que desaparece sin Ended (pérdida de foco, cambio de dispositivo) también suelta.
            if (_held.Count > _alive.Count)
            {
                var stale = new List<int>();
                foreach (var id in _held.Keys) if (!_alive.Contains(id)) stale.Add(id);
                foreach (var id in stale) Drop(id);
            }
        }

        // Soltar el dedo: avisa (salto corto) y apaga el aspecto pulsado si ningún otro dedo mantiene el botón.
        private void Drop(int id)
        {
            if (!_held.TryGetValue(id, out var entry)) return;
            _held.Remove(id);
            entry.Release?.Invoke();
            if (!_held.ContainsValue(entry)) entry.Button.RemoveFromClassList(PressedClass);
        }

        private Entry Hit(IPanel panel, Vector2 screen)
        {
            // ScreenToPanel espera el origen arriba a la izquierda.
            Vector2 p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));

            Entry best = null;
            float bestDistance = float.MaxValue;
            foreach (var entry in _buttons)
            {
                if (!IsShown(entry.Button)) continue;
                Rect r = entry.Button.worldBound;
                if (float.IsNaN(r.width) || r.width <= 0f) continue;

                var padded = new Rect(r.x - HitPadding, r.y - HitPadding, r.width + HitPadding * 2f, r.height + HitPadding * 2f);
                if (!padded.Contains(p)) continue;

                // Con el margen dos botones pueden solaparse: gana el centro más cercano.
                float distance = (r.center - p).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = entry;
            }
            return best;
        }

        private sealed class Entry
        {
            public readonly VisualElement Button;
            public readonly Action Press;
            public readonly Action Release;

            public Entry(VisualElement button, Action press, Action release)
            {
                Button = button;
                Press = press;
                Release = release;
            }
        }

        private static bool IsShown(VisualElement element)
        {
            if (element.panel == null) return false;
            for (var e = element; e != null; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
                if (e.resolvedStyle.visibility == Visibility.Hidden) return false;
            }
            return true;
        }
    }
}
