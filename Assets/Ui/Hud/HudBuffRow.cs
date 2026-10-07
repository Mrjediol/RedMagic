using System.Collections.Generic;
using System.Linq;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Chapas de efectos de item (bajo las monedas): el contador del Yelmo (0/5 … ¡LISTO!) y lo que
    /// cualquier efecto publique en <see cref="BuffIndicators"/>. Una chapa encendida
    /// (<c>Highlight</c>) late con el color de acento del efecto. Se reconstruye sólo cuando cambia
    /// cuántas hay (<see cref="BuffIndicators.Version"/>).
    /// </summary>
    public class HudBuffRow : VisualElement
    {
        private const float IconSize = 44f;
        private const float FontSize = 24f;

        private readonly List<(BuffIndicators.Indicator data, VisualElement chip, Image icon, Label text)> _chips = new();
        private int _version = -1;

        public HudBuffRow()
        {
            name = "item-buffs";
            pickingMode = PickingMode.Ignore;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginTop = 12;
        }

        public void Tick()
        {
            bool show = BuffIndicators.All.Count > 0;
            style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            if (_version != BuffIndicators.Version) Rebuild();
            Refresh();
        }

        private void Rebuild()
        {
            _version = BuffIndicators.Version;
            Clear();
            _chips.Clear();

            foreach (var data in BuffIndicators.All.Values.OrderBy(d => d.Order))
            {
                var chip = new VisualElement { pickingMode = PickingMode.Ignore };
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;
                chip.style.marginLeft = 12;
                chip.style.paddingLeft = 6;
                chip.style.paddingRight = 12;
                chip.style.paddingTop = 4;
                chip.style.paddingBottom = 4;
                HudStyle.Radius(chip, 12f);
                HudStyle.Border(chip, 2f, Color.clear);

                var icon = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.style.width = IconSize;
                icon.style.height = IconSize;
                icon.style.marginRight = 6;
                chip.Add(icon);

                var text = HudStyle.OutlinedLabel("text", FontSize);
                text.style.unityTextAlign = TextAnchor.MiddleLeft;
                chip.Add(text);

                Add(chip);
                _chips.Add((data, chip, icon, text));
            }
        }

        private void Refresh()
        {
            // Latido de las chapas encendidas, en tiempo real (se ve también en pausa).
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);

            foreach (var (data, chip, icon, text) in _chips)
            {
                icon.sprite = data.Icon;
                icon.style.display = data.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;
                text.text = data.Text;

                var accent = data.Accent;
                if (data.Highlight)
                {
                    chip.style.backgroundColor = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.85f);
                    HudStyle.BorderColor(chip, new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0.55f, 1f, pulse)));
                    text.style.color = Color.Lerp(Color.white, accent, pulse * 0.6f);
                    icon.tintColor = Color.white;
                }
                else
                {
                    chip.style.backgroundColor = HudStyle.PanelBg;
                    HudStyle.BorderColor(chip, new Color(accent.r, accent.g, accent.b, 0.25f));
                    text.style.color = new Color(0.9f, 0.9f, 0.9f);
                    icon.tintColor = new Color(0.75f, 0.75f, 0.75f);
                }
            }
        }
    }
}
