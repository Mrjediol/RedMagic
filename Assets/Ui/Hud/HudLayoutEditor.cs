using System;
using System.Collections.Generic;
using RedMagic.Localization;
using RedMagic.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Modo "colocar HUD" (Opciones ▸ Interfaz ▸ Colocar elementos): una cuadrícula sobre la
    /// pantalla y un asa encima de cada pieza (<see cref="HudElement"/>) que se arrastra con ratón o
    /// dedo. Al arrastrar, el borde o el centro de la pieza que quede más cerca de una línea se pega a
    /// ella — así centrar algo es soltarlo cerca de la línea dorada del medio. Al soltar se guarda en
    /// <see cref="HudSettings"/>; "Restablecer" vuelve todo a su sitio de fábrica.
    ///
    /// Sólo dibuja y arrastra: dónde está cada pieza lo aplica <see cref="GameHud"/>.
    /// </summary>
    public class HudLayoutEditor : VisualElement
    {
        /// <summary>Divisiones de la cuadrícula. Pares, para que el centro de pantalla sea una línea.</summary>
        public const int Columns = 48;
        public const int Rows = 24;

        private sealed class Handle
        {
            public HudElement Element;
            public VisualElement Target;
            public VisualElement Box;
        }

        private readonly VisualElement _host;
        private readonly Func<HudElement, Rect, HudAnchor> _toAnchor;
        private readonly List<Handle> _handles = new();
        private readonly VisualElement _grid;
        private readonly VisualElement _toolbar;

        private Handle _dragging;
        private Vector2 _dragStartPointer;
        private Vector2 _dragStartPos;
        private Vector2 _placed;                  // dónde se ha dejado la pieza (resolvedStyle aún no lo sabe este frame)
        private Vector2 _guide = new(-1f, -1f);   // líneas de la cuadrícula a las que se ha pegado, para resaltarlas

        /// <summary>El jugador ha pulsado "Listo" (o Escape).</summary>
        public event Action Finished;

        /// <summary>Pieza que se está arrastrando ahora (GameHud no la recoloca mientras tanto).</summary>
        public VisualElement Dragging => _dragging?.Target;

        public HudLayoutEditor(VisualElement host, Func<HudElement, Rect, HudAnchor> toAnchor)
        {
            _host = host;
            _toAnchor = toAnchor;
            name = "hud-layout-editor";
            pickingMode = PickingMode.Position;   // tapa el juego y los menús de detrás mientras se edita
            HudStyle.Fill(this);
            style.display = DisplayStyle.None;

            _grid = new VisualElement { name = "grid", pickingMode = PickingMode.Ignore };
            HudStyle.Fill(_grid);
            _grid.generateVisualContent += DrawGrid;
            Add(_grid);

            _toolbar = BuildToolbar();
            Add(_toolbar);
        }

        public void Register(HudElement element, VisualElement target, string labelKey)
        {
            var box = new VisualElement { name = $"handle-{element}", pickingMode = PickingMode.Position };
            box.style.position = Position.Absolute;
            box.style.backgroundColor = new Color(1f, 0.82f, 0.4f, 0.10f);
            HudStyle.Border(box, 3f, new Color(1f, 0.82f, 0.4f, 0.8f));
            HudStyle.Radius(box, 8f);

            var label = HudStyle.OutlinedLabel("label", 22f);
            LocalizedUi.Bind(label, labelKey);
            label.style.position = Position.Absolute;
            label.style.left = 0;
            label.style.bottom = Length.Percent(100f);
            label.style.marginBottom = 4;
            label.style.color = HudStyle.Gold;
            box.Add(label);

            var handle = new Handle { Element = element, Target = target, Box = box };
            box.RegisterCallback<PointerDownEvent>(e => BeginDrag(handle, e));
            box.RegisterCallback<PointerMoveEvent>(e => Drag(handle, e));
            box.RegisterCallback<PointerUpEvent>(e => EndDrag(handle, e));
            box.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag(handle));
            box.RegisterCallback<PointerEnterEvent>(_ => Highlight(handle, true));
            box.RegisterCallback<PointerLeaveEvent>(_ => Highlight(handle, false));

            Add(box);
            _handles.Add(handle);
        }

        public bool IsOpen => style.display == DisplayStyle.Flex;

        public void Open()
        {
            style.display = DisplayStyle.Flex;
            _guide = new Vector2(-1f, -1f);
            _grid.MarkDirtyRepaint();
        }

        public void Close()
        {
            if (_dragging != null) CancelDrag(_dragging);
            style.display = DisplayStyle.None;
        }

        /// <summary>Lo llama GameHud cada frame mientras está abierto: las asas siguen a sus piezas.</summary>
        public void Tick()
        {
            foreach (var handle in _handles)
            {
                bool shown = handle.Target.resolvedStyle.display == DisplayStyle.Flex;
                handle.Box.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;

                var r = handle.Target.layout;
                const float pad = 6f;
                handle.Box.style.left = r.x - pad;
                handle.Box.style.top = r.y - pad;
                handle.Box.style.width = r.width + pad * 2f;
                handle.Box.style.height = r.height + pad * 2f;
            }
        }

        // ---------------------------------------------------------------- arrastre

        private void BeginDrag(Handle handle, PointerDownEvent evt)
        {
            if (_dragging != null) return;
            _dragging = handle;
            _dragStartPointer = evt.position;
            var r = handle.Target.layout;
            _dragStartPos = new Vector2(r.x, r.y);
            _placed = _dragStartPos;
            handle.Box.CapturePointer(evt.pointerId);
            Highlight(handle, true);
            UiSounds.Click();
            evt.StopPropagation();
        }

        private void Drag(Handle handle, PointerMoveEvent evt)
        {
            if (_dragging != handle || !handle.Box.HasPointerCapture(evt.pointerId)) return;

            var area = _host.layout;
            var r = handle.Target.layout;
            var raw = _dragStartPos + ((Vector2)evt.position - _dragStartPointer);

            float cellX = area.width / Columns, cellY = area.height / Rows;
            float x = Snap(raw.x, r.width, cellX, out float guideX);
            float y = Snap(raw.y, r.height, cellY, out float guideY);
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, area.width - r.width));
            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, area.height - r.height));

            handle.Target.style.left = x;
            handle.Target.style.top = y;
            _placed = new Vector2(x, y);
            _guide = new Vector2(guideX, guideY);
            _grid.MarkDirtyRepaint();
        }

        private void EndDrag(Handle handle, PointerUpEvent evt)
        {
            if (_dragging != handle) return;
            handle.Box.ReleasePointer(evt.pointerId);
            Commit(handle);
        }

        private void CancelDrag(Handle handle)
        {
            if (_dragging != handle) return;
            Commit(handle);
        }

        private void Commit(Handle handle)
        {
            _dragging = null;
            var r = handle.Target.layout;
            var placed = new Rect(_placed.x, _placed.y, r.width, r.height);
            HudSettings.SetAnchor(handle.Element, _toAnchor(handle.Element, placed));
            _guide = new Vector2(-1f, -1f);
            _grid.MarkDirtyRepaint();
            Highlight(handle, false);
        }

        /// <summary>
        /// Pega a la cuadrícula el borde (inicio, centro o fin) de la pieza que tenga una línea más
        /// cerca. Devuelve la nueva posición de inicio y en <paramref name="guide"/> la línea usada.
        /// </summary>
        private static float Snap(float start, float size, float cell, out float guide)
        {
            guide = -1f;
            if (cell <= 0f) return start;

            float best = float.MaxValue, result = start;
            for (int i = 0; i < 3; i++)
            {
                float offset = size * 0.5f * i;               // 0 = inicio, ½ = centro, 1 = fin
                float point = start + offset;
                float line = Mathf.Round(point / cell) * cell;
                float distance = Mathf.Abs(line - point);
                // El centro gana los empates: es lo que más cuesta acertar a ojo.
                if (i == 1) distance -= 0.01f;
                if (distance < best)
                {
                    best = distance;
                    result = line - offset;
                    guide = line;
                }
            }

            return result;
        }

        private static void Highlight(Handle handle, bool on)
        {
            handle.Box.style.backgroundColor = new Color(1f, 0.82f, 0.4f, on ? 0.22f : 0.10f);
            HudStyle.BorderColor(handle.Box, new Color(1f, 0.82f, 0.4f, on ? 1f : 0.8f));
        }

        // ---------------------------------------------------------------- dibujo

        private void DrawGrid(MeshGenerationContext ctx)
        {
            var r = _grid.contentRect;
            if (r.width < 2f) return;
            var p = ctx.painter2D;

            // Velo: el juego se sigue viendo, pero queda claro que se está editando.
            p.fillColor = new Color(0f, 0f, 0f, 0.35f);
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Fill();

            float cellX = r.width / Columns, cellY = r.height / Rows;
            for (int i = 1; i < Columns; i++)
                Line(p, new Vector2(i * cellX, r.yMin), new Vector2(i * cellX, r.yMax), LineColor(i, Columns), i == Columns / 2 ? 2f : 1f);
            for (int j = 1; j < Rows; j++)
                Line(p, new Vector2(r.xMin, j * cellY), new Vector2(r.xMax, j * cellY), LineColor(j, Rows), j == Rows / 2 ? 2f : 1f);

            // La línea a la que se ha pegado la pieza que se arrastra.
            var snap = new Color(0.45f, 0.9f, 1f, 0.95f);
            if (_guide.x >= 0f) Line(p, new Vector2(_guide.x, r.yMin), new Vector2(_guide.x, r.yMax), snap, 2.5f);
            if (_guide.y >= 0f) Line(p, new Vector2(r.xMin, _guide.y), new Vector2(r.xMax, _guide.y), snap, 2.5f);
        }

        private static Color LineColor(int index, int count)
        {
            if (index == count / 2) return new Color(1f, 0.82f, 0.4f, 0.75f);   // centro
            if (index % 4 == 0) return new Color(1f, 1f, 1f, 0.18f);
            return new Color(1f, 1f, 1f, 0.07f);
        }

        private static void Line(Painter2D p, Vector2 a, Vector2 b, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        // ---------------------------------------------------------------- barra de herramientas

        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement { name = "toolbar", pickingMode = PickingMode.Position };
            bar.style.position = Position.Absolute;
            bar.style.left = Length.Percent(50f);
            bar.style.top = Length.Percent(50f);
            bar.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 36;
            bar.style.paddingRight = 36;
            bar.style.paddingTop = 24;
            bar.style.paddingBottom = 26;
            bar.style.backgroundColor = new Color(0.047f, 0.031f, 0.039f, 0.92f);
            HudStyle.Border(bar, 2f, HudStyle.Frame);
            HudStyle.Radius(bar, 16f);

            var title = HudStyle.OutlinedLabel("title", 38f);
            title.style.color = HudStyle.Gold;
            LocalizedUi.Bind(title, "hud.layout.title");
            bar.Add(title);

            var hint = new Label { pickingMode = PickingMode.Ignore };
            LocalizedUi.Bind(hint, "hud.layout.hint");
            hint.style.fontSize = 24;
            hint.style.color = new Color(0.92f, 0.88f, 0.78f, 0.85f);
            hint.style.marginTop = 6;
            hint.style.marginBottom = 18;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.maxWidth = 760;
            bar.Add(hint);

            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            bar.Add(buttons);

            buttons.Add(ToolbarButton("hud.layout.reset", () =>
            {
                HudSettings.ResetAnchors();
                UiSounds.Back();
            }));
            buttons.Add(ToolbarButton("hud.layout.done", () => Finished?.Invoke()));

            return bar;
        }

        private static Button ToolbarButton(string key, Action onClick)
        {
            var button = new Button(onClick);
            LocalizedUi.Bind(button, key);
            button.style.minWidth = 230;
            button.style.height = 66;
            button.style.marginLeft = 12;
            button.style.marginRight = 12;
            button.style.fontSize = 28;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = new Color(0.96f, 0.81f, 0.52f);
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            HudStyle.Border(button, 2f, new Color(0.59f, 0.44f, 0.29f, 0.8f));
            HudStyle.Radius(button, 8f);

            var skin = MenuSkinDresser.Skin;
            if (skin != null) MenuSkinDresser.DressButton(button, skin, Color.white);
            return button;
        }
    }
}
