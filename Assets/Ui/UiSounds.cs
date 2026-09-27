using RedMagic.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Sonidos de UI Toolkit. <see cref="Bind"/> una vez por documento (en su OnEnable / al construir
    /// la UI) y todo botón del árbol suena solo:
    /// <list type="bullet">
    /// <item>hover con ratón/dedo → <c>uiHover</c>; cambio de foco con mando/teclado → <c>uiFocus</c>;</item>
    /// <item>clic o submit → <c>uiClick</c> (<c>uiBack</c> con <see cref="BackClass"/>; nada con
    /// <see cref="NoClickClass"/>).</item>
    /// </list>
    /// Suena cualquier <see cref="Button"/> y cualquier elemento con la clase <see cref="SoundClass"/>
    /// (cartas, filas…); <see cref="MuteClass"/> silencia un elemento y lo que cuelga de él.
    ///
    /// Reglas: el foco sólo suena si lo movió la navegación (NavigationMoveEvent), así el foco
    /// inicial que pone un menú al abrirse es mudo; y el mismo elemento no suena dos veces seguidas
    /// aunque le lleguen hover y foco a la vez. Los clips son los de <see cref="AudioManager"/>
    /// (<see cref="SystemSounds"/>: UI Hover / Focus / Click / Back / Deny). Atrás/cerrar y rechazos los pide cada menú con
    /// <see cref="Back"/> / <see cref="Deny"/>, porque no pasan por un botón.
    /// </summary>
    public static class UiSounds
    {
        public const string SoundClass = "rm-sfx";
        public const string MuteClass = "rm-nosfx";

        /// <summary>Botón de cerrar/volver: al pulsarlo suena <c>uiBack</c> en vez de <c>uiClick</c>.</summary>
        public const string BackClass = "rm-sfx-back";

        /// <summary>Suena el hover pero no el clic: el menú lo pide sólo si la acción sale bien
        /// (comprar, mejorar). Un intento fallido queda en silencio, como antes.</summary>
        public const string NoClickClass = "rm-sfx-noclick";
        private const string BoundClass = "rm-sfx-bound";

        // Mismo elemento dentro de esta ventana = no vuelve a sonar (hover + foco juntos).
        private const float RepeatWindow = 0.25f;

        public static void Hover() => SystemSounds.Play(s => s.uiHover);
        public static void Focus() => SystemSounds.Play(s => s.uiFocus);
        public static void Click() => SystemSounds.Play(s => s.uiClick);
        public static void Back() => SystemSounds.Play(s => s.uiBack);
        public static void Deny() => SystemSounds.Play(s => s.uiDeny);

        /// <summary>Engancha los sonidos a todo el árbol de <paramref name="root"/>. Idempotente.</summary>
        public static void Bind(VisualElement root)
        {
            if (root == null || root.ClassListContains(BoundClass)) return;
            root.AddToClassList(BoundClass);

            var state = new BindState(root);
            root.RegisterCallback<PointerOverEvent>(state.OnPointerOver, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(state.OnPointerLeave);
            root.RegisterCallback<NavigationMoveEvent>(state.OnNavigationMove, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusInEvent>(state.OnFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(state.OnClick, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(state.OnSubmit, TrickleDown.TrickleDown);
        }

        private sealed class BindState
        {
            private readonly VisualElement _root;
            private VisualElement _hovered;
            private VisualElement _lastSounded;
            private float _lastSoundedAt = float.NegativeInfinity;
            private int _navigationFrame = -1;

            public BindState(VisualElement root) => _root = root;

            public void OnPointerOver(PointerOverEvent evt)
            {
                var element = Resolve(evt.target as VisualElement);
                if (element == _hovered) return;     // moverse entre hijos del mismo botón
                _hovered = element;
                if (element != null) SoundOnce(element, focus: false);
            }

            public void OnPointerLeave(PointerLeaveEvent evt) => _hovered = null;

            public void OnNavigationMove(NavigationMoveEvent evt) => _navigationFrame = Time.frameCount;

            public void OnFocusIn(FocusInEvent evt)
            {
                if (_navigationFrame != Time.frameCount) return;   // foco puesto por código o por clic
                var element = Resolve(evt.target as VisualElement);
                if (element != null) SoundOnce(element, focus: true);
            }

            public void OnClick(ClickEvent evt) => Press(evt.target as VisualElement);

            public void OnSubmit(NavigationSubmitEvent evt) => Press(evt.target as VisualElement);

            private void Press(VisualElement target)
            {
                var element = Resolve(target);
                if (element == null || element.ClassListContains(NoClickClass)) return;
                if (element.ClassListContains(BackClass)) Back();
                else Click();
            }

            // Hover y foco comparten la guarda: el mismo elemento no suena dos veces seguidas.
            private void SoundOnce(VisualElement element, bool focus)
            {
                float now = Time.unscaledTime;
                if (element == _lastSounded && now - _lastSoundedAt < RepeatWindow) return;
                _lastSounded = element;
                _lastSoundedAt = now;
                if (focus) Focus(); else Hover();
            }

            // El botón (o elemento marcado) que contiene a target, o null si no hay / está mudo / desactivado.
            private VisualElement Resolve(VisualElement target)
            {
                for (var e = target; e != null; e = e.parent)
                {
                    if (e.ClassListContains(MuteClass)) return null;
                    if (e is Button || e.ClassListContains(SoundClass))
                        return e.enabledInHierarchy ? e : null;
                    if (e == _root) return null;
                }
                return null;
            }
        }
    }
}
