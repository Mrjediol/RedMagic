using RedMagic.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// La chuleta de teclas de PC, abajo a la izquierda, translúcida para que no moleste. Es el
    /// equivalente de los botones en pantalla del móvil: con teclado y ratón se enseña esto, en
    /// táctil los botones, con mando ninguna de las dos. Se puede apagar en Opciones
    /// (<see cref="RedMagic.Settings.HudSettings.ShowControls"/>); quién la enseña lo decide
    /// <see cref="GameHud"/>.
    ///
    /// <b>Las teclas se escriben a mano</b>: salen de <c>Settings/RedMagicControls.inputactions</c>
    /// (Move WASD+flechas, Jump espacio, Attack clic izquierdo, Weapon Q, Dash shift izq., Parry clic derecho) y de las que leen los
    /// scripts directamente (E interactuar, I items, Esc pausa). Si cambias un binding, cambia
    /// también <see cref="Rows"/>.
    /// </summary>
    public class HudControlsLegend : VisualElement
    {
        /// <summary>Tecla → qué hace (claves de idioma). El orden es el de la lista en pantalla.</summary>
        private static readonly (string Key, string Action)[] Rows =
        {
            ("WASD", "legend.move"),
            ("legend.key_space", "legend.jump"),
            ("Shift", "legend.dash"),
            ("legend.key_left_click", "legend.attack"),
            ("Q", "legend.weapon"),
            ("legend.key_right_click", "legend.parry"),
            ("E", "legend.interact"),
            ("I", "legend.items"),
            ("Esc", "legend.options"),
        };

        public HudControlsLegend()
        {
            name = "controls-legend";
            pickingMode = PickingMode.Ignore;
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 14;
            style.paddingRight = 16;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            HudStyle.Radius(this, 10f);

            foreach (var (key, action) in Rows) Add(Row(key, action));
        }

        private static VisualElement Row(string key, string action)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2;
            row.style.marginBottom = 2;

            // Las teclas con nombre ("Espacio", "Clic izq.") se traducen; "WASD", "E"… no tienen clave.
            var keyLabel = new Label { pickingMode = PickingMode.Ignore };
            LocalizedUi.Bind(keyLabel, () => keyLabel.text = Loc.GetOr(key, key));
            keyLabel.style.width = 104;
            keyLabel.style.flexShrink = 0;
            keyLabel.style.fontSize = 21;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            keyLabel.style.color = new Color(0.95f, 0.9f, 0.78f, 0.92f);
            keyLabel.style.backgroundColor = new Color(1f, 1f, 1f, 0.09f);
            keyLabel.style.paddingTop = 2;
            keyLabel.style.paddingBottom = 2;
            HudStyle.Radius(keyLabel, 5f);

            // Arte (HudSkin): tecla de piedra. Va en una caja aparte porque la capa del marco es un
            // hijo y UI Toolkit pinta los hijos encima del texto del propio Label.
            var skin = HudSkin.Current;
            if (skin != null && skin.keyCap.IsSet)
            {
                var keyBox = new VisualElement { pickingMode = PickingMode.Ignore };
                keyBox.style.width = 104;
                keyBox.style.flexShrink = 0;
                skin.keyCap.Dress(keyBox);
                keyLabel.style.width = StyleKeyword.Auto;
                keyLabel.style.flexGrow = 1;
                keyLabel.style.backgroundColor = Color.clear;
                keyBox.Add(keyLabel);
                row.Add(keyBox);
            }
            else row.Add(keyLabel);

            var actionLabel = new Label { pickingMode = PickingMode.Ignore };
            LocalizedUi.Bind(actionLabel, action);
            actionLabel.style.marginLeft = 10;
            actionLabel.style.fontSize = 21;
            actionLabel.style.color = new Color(0.92f, 0.88f, 0.78f, 0.62f);
            row.Add(actionLabel);

            return row;
        }
    }
}
