using System.Collections.Generic;
using RedMagic.Gameplay;
using RedMagic.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Las barras de vida de los enemigos con los que se pelea (<see cref="HudEnemies"/>), en el modo
    /// que el jugador elija en Opciones ▸ Interfaz (<see cref="HudSettings.EnemyHealth"/>):
    /// <list type="bullet">
    /// <item><b>Flotante</b>: una barra pequeña sobre la cabeza de cada uno (capa <see cref="Overhead"/>,
    /// sigue a la cámara; no se recoloca).</item>
    /// <item><b>Foco</b>: una barra grande, la del último golpeado o el más cercano (pieza
    /// <see cref="Focus"/>, recolocable; de fábrica arriba al centro, bajo la del jefe).</item>
    /// <item><b>Lista</b>: una barra por enemigo, del más cercano al más lejano, hasta
    /// <see cref="MaxListed"/> y "+N" (pieza <see cref="List"/>, recolocable; de fábrica a la derecha).</item>
    /// </list>
    /// Las vistas se reutilizan (nada se crea por golpe) y entran y salen con fundido.
    /// </summary>
    public class HudEnemyBars
    {
        public const int MaxListed = 6;
        private const float FadeTime = 0.3f;

        /// <summary>Capa a pantalla completa de las barras flotantes.</summary>
        public readonly VisualElement Overhead;

        /// <summary>Pieza recolocable del modo foco.</summary>
        public readonly VisualElement Focus;

        /// <summary>Pieza recolocable del modo lista.</summary>
        public readonly VisualElement List;

        private readonly EnemyBarView _focusBar;
        private readonly Label _listMore;
        private readonly VisualElement _listRows;
        private readonly List<EnemyBarView> _listViews = new();
        private readonly List<EnemyBarView> _overheadViews = new();
        private readonly Dictionary<HudEnemies.Info, EnemyBarView> _overheadByEnemy = new();
        private readonly HashSet<HudEnemies.Info> _seen = new();
        private float _focusFade;
        private Camera _camera;

        public HudEnemyBars()
        {
            Overhead = new VisualElement { name = "enemy-overhead", pickingMode = PickingMode.Ignore };
            HudStyle.Fill(Overhead);

            Focus = new VisualElement { name = "enemy-focus", pickingMode = PickingMode.Ignore };
            _focusBar = new EnemyBarView(EnemyBarView.Size.Focus);
            Focus.Add(_focusBar);

            List = new VisualElement { name = "enemy-list", pickingMode = PickingMode.Ignore };
            List.style.alignItems = Align.FlexEnd;
            _listRows = new VisualElement { pickingMode = PickingMode.Ignore };
            List.Add(_listRows);
            for (int i = 0; i < MaxListed; i++)
            {
                var view = new EnemyBarView(EnemyBarView.Size.List);
                view.style.marginBottom = 16;
                view.style.display = DisplayStyle.None;
                _listRows.Add(view);
                _listViews.Add(view);
            }
            _listMore = HudStyle.OutlinedLabel("more", 22f);
            _listMore.style.color = HudStyle.Gold;
            List.Add(_listMore);
        }

        /// <summary>
        /// Anima el modo activo y devuelve qué piezas se ven (GameHud decide el display de las piezas
        /// recolocables, que comparte con el editor). <paramref name="editing"/> = colocando el HUD:
        /// las piezas del modo activo se enseñan con barras de muestra.
        /// </summary>
        public (bool focus, bool list) Tick(float dt, bool editing)
        {
            var mode = HudSettings.EnemyHealth;
            bool numbers = HudSettings.EnemyHealthNumbers;
            bool cooldowns = HudSettings.EnemyCooldownsDebug || EnemyCooldownReveal.Granted;

            TickOverhead(dt, mode == EnemyHealthMode.Overhead && !editing, numbers, cooldowns);

            if (editing)
            {
                if (mode == EnemyHealthMode.Focus) _focusBar.ShowSample(0.65f, numbers, cooldowns);
                if (mode == EnemyHealthMode.List)
                {
                    for (int i = 0; i < _listViews.Count; i++)
                    {
                        bool sample = i < 3;
                        _listViews[i].style.display = sample ? DisplayStyle.Flex : DisplayStyle.None;
                        if (sample) _listViews[i].ShowSample(1f - i * 0.25f, numbers, cooldowns);
                    }
                    _listMore.text = "";
                }
                return (mode == EnemyHealthMode.Focus, mode == EnemyHealthMode.List);
            }

            bool focus = mode == EnemyHealthMode.Focus && TickFocus(dt, numbers, cooldowns);
            bool list = mode == EnemyHealthMode.List && TickList(dt, numbers, cooldowns);
            return (focus, list);
        }

        // ---------------------------------------------------------------- foco

        private bool TickFocus(float dt, bool numbers, bool cooldowns)
        {
            var target = HudEnemies.Focus;
            if (target != null) _focusBar.Bind(target);

            // Sin nadie: la última barra se apaga con fundido (no desaparece de golpe al matar).
            _focusFade = Mathf.MoveTowards(_focusFade, target != null ? 1f : 0f, dt / FadeTime);
            _focusBar.Fade = _focusFade;
            _focusBar.Tick(dt, numbers, cooldowns);
            return _focusFade > 0f;
        }

        // ---------------------------------------------------------------- lista

        private bool TickList(float dt, bool numbers, bool cooldowns)
        {
            var engaged = HudEnemies.InCombat;
            int shown = Mathf.Min(engaged.Count, MaxListed);

            for (int i = 0; i < _listViews.Count; i++)
            {
                var view = _listViews[i];
                bool used = i < shown;
                view.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;

                // La fila sigue al enemigo, no a la posición: si cambia el orden, no se mezclan las estelas.
                view.Bind(engaged[i]);
                view.Fade = 1f;
                view.Tick(dt, numbers, cooldowns);
            }

            int extra = engaged.Count - shown;
            _listMore.text = extra > 0 ? $"+{extra}" : "";
            return shown > 0;
        }

        // ---------------------------------------------------------------- flotantes

        private void TickOverhead(float dt, bool active, bool numbers, bool cooldowns)
        {
            Overhead.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active)
            {
                foreach (var view in _overheadViews) Release(view);
                _overheadByEnemy.Clear();
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            var panel = Overhead.panel;
            if (_camera == null || panel == null) return;

            _seen.Clear();
            foreach (var info in HudEnemies.InCombat)
            {
                _seen.Add(info);
                if (!_overheadByEnemy.TryGetValue(info, out var view))
                {
                    view = Acquire();
                    view.Bind(info);
                    view.Fade = 0f;
                    _overheadByEnemy[info] = view;
                }
                view.Fade = Mathf.MoveTowards(view.Fade, 1f, dt / FadeTime);
                Place(view, info, panel);
                view.Tick(dt, numbers, cooldowns);
            }

            // Los que ya no están en combate (o han muerto) se apagan y vuelven a la reserva.
            _releasing.Clear();
            foreach (var (info, view) in _overheadByEnemy)
            {
                if (_seen.Contains(info)) continue;
                view.Fade = Mathf.MoveTowards(view.Fade, 0f, dt / FadeTime);
                if (info.Health != null && info.Health.isActiveAndEnabled) Place(view, info, panel);
                view.Tick(dt, numbers, cooldowns);
                if (view.Fade <= 0f) _releasing.Add(info);
            }
            foreach (var info in _releasing)
            {
                Release(_overheadByEnemy[info]);
                _overheadByEnemy.Remove(info);
            }
        }

        private readonly List<HudEnemies.Info> _releasing = new();

        private void Place(EnemyBarView view, HudEnemies.Info info, IPanel panel)
        {
            var screen = _camera.WorldToScreenPoint(info.HeadPoint);
            bool onScreen = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            view.style.visibility = onScreen ? Visibility.Visible : Visibility.Hidden;
            if (!onScreen) return;

            var panelPoint = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            var local = Overhead.WorldToLocal(panelPoint);
            float width = view.resolvedStyle.width;
            float height = view.resolvedStyle.height;
            if (float.IsNaN(width)) width = 0f;
            if (float.IsNaN(height)) height = 0f;
            view.style.left = local.x - width * 0.5f;
            view.style.top = local.y - height;
        }

        private EnemyBarView Acquire()
        {
            foreach (var view in _overheadViews)
                if (view.style.display == DisplayStyle.None)
                {
                    view.style.display = DisplayStyle.Flex;
                    return view;
                }

            var created = new EnemyBarView(EnemyBarView.Size.Overhead);
            created.style.position = Position.Absolute;
            _overheadViews.Add(created);
            Overhead.Add(created);
            return created;
        }

        private static void Release(EnemyBarView view)
        {
            view.Bind(null);
            view.style.display = DisplayStyle.None;
        }
    }
}
