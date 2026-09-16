using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Pone el arte de <see cref="MenuSkin"/> sobre los menús de UXML (principal, pausa, opciones).
    ///
    /// Está aparte de los tres controladores porque los tres visten lo mismo — placa de título,
    /// botones de tres estados, panel — y repetir el cableado de eventos en cada uno era la forma
    /// segura de que se fueran separando. Cada método es <b>idempotente</b> (marca lo ya vestido con
    /// <see cref="SkinnedClass"/>): <c>OnEnable</c> puede correr varias veces sin duplicar capas ni
    /// volver a registrar callbacks.
    ///
    /// Todo es opcional: sin <c>Resources/MenuSkin.asset</c>, o con una pieza sin sprites,
    /// <see cref="MenuSkin"/> devuelve null / el marco no está puesto y la pantalla se queda con el
    /// aspecto plano del USS de siempre.
    /// </summary>
    public static class MenuSkinDresser
    {
        /// <summary>Marca de "ya vestido", para no montar dos veces sobre el mismo elemento.</summary>
        private const string SkinnedClass = "rm-skinned";

        /// <summary>Aire debajo de la placa de título, el que el USS le daba a la etiqueta.</summary>
        private const float TitleBottomMargin = 28f;

        private static MenuSkin _skin;
        private static bool _looked;

        /// <summary>El skin, o null si el asset no está. Se busca una sola vez.</summary>
        public static MenuSkin Skin
        {
            get
            {
                if (_looked) return _skin;
                _looked = true;
                _skin = MenuSkin.Load();
                return _skin;
            }
        }

        // Domain Reload desactivado: la caché sobrevive entre sesiones de Play y se quedaría con una
        // referencia muerta tras reimportar el asset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetCache()
        {
            _skin = null;
            _looked = false;
        }

        /// <summary>Marco de fondo de un panel. No hace nada si el marco no tiene sprites.</summary>
        public static void DressPanel(VisualElement panel, UiFrame frame)
        {
            if (panel == null || frame == null || !frame.IsSet) return;
            if (panel.ClassListContains(SkinnedClass)) return;

            panel.AddToClassList(SkinnedClass);
            frame.Dress(panel);
        }

        /// <summary>
        /// Mete <paramref name="title"/> dentro de una placa de piedra. Se envuelve en vez de
        /// vestir la propia etiqueta porque el texto de un <see cref="Label"/> se dibuja por debajo
        /// de sus hijos: el marco taparía el título.
        /// </summary>
        public static void DressTitle(Label title, UiFrame frame, float height, float maxWidth,
                                      float bottomMargin = TitleBottomMargin)
        {
            if (title == null || frame == null || !frame.IsSet) return;
            if (title.ClassListContains(SkinnedClass)) return;

            var parent = title.parent;
            if (parent == null) return;

            title.AddToClassList(SkinnedClass);

            var plaque = new VisualElement { name = "title-plaque" };
            plaque.style.alignSelf = Align.Center;
            plaque.style.alignItems = Align.Center;
            plaque.style.justifyContent = Justify.Center;
            if (height > 0f) plaque.style.height = height;

            // La placa se ajusta al texto (ancho automático) en vez de ocupar el 100%: así el
            // rótulo nunca puede salirse por los lados de la piedra — es la placa la que crece.
            // maxWidth sólo la limita para que no se coma la pantalla.
            if (maxWidth > 0f) plaque.style.maxWidth = maxWidth;

            parent.Insert(parent.IndexOf(title), plaque);
            title.RemoveFromHierarchy();
            plaque.Add(title);

            // El hueco que el USS le daba al título por debajo pasa a la placa: si se dejara en la
            // etiqueta, contaría DENTRO de la piedra y descentraría el texto — pero si no se
            // traslada, el título queda pegado a la primera fila.
            plaque.style.marginBottom = bottomMargin;
            title.style.marginTop = 0;
            title.style.marginBottom = 0;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.flexShrink = 0;

            frame.Dress(plaque);
        }

        /// <summary>
        /// Viste un botón con los tres estados del kit (reposo / ratón encima / pulsado). El foco de
        /// mando cuenta como "encima", que es lo que hace navegable el menú con d-pad.
        /// </summary>
        public static void DressButton(Button button, MenuSkin skin, Color tint)
        {
            if (button == null || skin == null || !skin.button.IsSet) return;
            if (button.ClassListContains(SkinnedClass)) return;

            button.AddToClassList(SkinnedClass);

            // El rótulo pasa a ser un hijo. UI Toolkit dibuja los hijos POR ENCIMA del texto propio
            // del elemento, así que el marco (que es un hijo) taparía el texto del botón. Es la
            // misma razón por la que los títulos se envuelven en vez de vestirse. Las propiedades
            // de fuente (color, tamaño, negrita, alineación) se heredan, así que el USS de
            // .menu-button — incluido su :hover — sigue mandando sobre el rótulo.
            var caption = new Label(button.text)
            {
                name = "button-caption",
                pickingMode = PickingMode.Ignore,
            };
            caption.style.flexGrow = 1;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.text = string.Empty;
            button.Add(caption);

            var normal = skin.button;
            var hover = skin.buttonHover.IsSet ? skin.buttonHover : normal;
            var pressed = skin.buttonPressed.IsSet ? skin.buttonPressed : hover;

            bool over = false, down = false, focused = false;
            UiFrame current = null;

            void Apply()
            {
                var target = down ? pressed : over || focused ? hover : normal;
                if (target == current) return;

                current = target;
                target.Dress(button);
                UiFrame.Tint(button, tint);
            }

            Apply();

            button.RegisterCallback<PointerEnterEvent>(_ => { over = true; Apply(); });
            button.RegisterCallback<PointerLeaveEvent>(_ => { over = false; down = false; Apply(); });
            button.RegisterCallback<PointerDownEvent>(_ => { down = true; Apply(); });
            button.RegisterCallback<PointerUpEvent>(_ => { down = false; Apply(); });
            button.RegisterCallback<FocusInEvent>(_ => { focused = true; Apply(); });
            button.RegisterCallback<FocusOutEvent>(_ => { focused = false; Apply(); });
        }

        /// <summary>
        /// Botón redondo de cerrar. No lleva marco sino el sprite entero (no se estira), así que
        /// aquí basta con intercambiar la imagen de fondo — el mismo trato que le da la pantalla de
        /// items a su X.
        /// </summary>
        public static void DressCloseButton(Button button, MenuSkin skin)
        {
            if (button == null || skin == null || !skin.closeButton.IsSet) return;
            if (button.ClassListContains(SkinnedClass)) return;

            button.AddToClassList(SkinnedClass);

            var sprites = skin.closeButton;
            float size = skin.closeButtonSize;

            button.text = string.Empty;
            button.style.width = size * sprites.Aspect;
            button.style.height = size;
            button.style.backgroundColor = Color.clear;
            button.style.unityBackgroundImageTintColor = Color.white;
            MenuStyle.SetBorder(button, 0, Color.clear, 0);
            button.style.backgroundImage = new StyleBackground(sprites.normal);

            bool over = false, focused = false;

            void Apply()
            {
                bool on = over || focused;
                button.style.backgroundImage = new StyleBackground(sprites.Get(on));
                button.style.scale = new Scale(on ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one);
            }

            button.RegisterCallback<PointerEnterEvent>(_ => { over = true; Apply(); });
            button.RegisterCallback<PointerLeaveEvent>(_ => { over = false; Apply(); });
            button.RegisterCallback<FocusInEvent>(_ => { focused = true; Apply(); });
            button.RegisterCallback<FocusOutEvent>(_ => { focused = false; Apply(); });
        }

        /// <summary>Casilla de verificación con el arte del kit (apagada / encendida, con hover).</summary>
        public static void DressToggle(Toggle toggle, MenuSkin skin)
        {
            if (toggle == null || skin == null || !skin.checkOff.IsSet) return;
            if (toggle.ClassListContains(SkinnedClass)) return;

            var checkmark = toggle.Q(className: "unity-toggle__checkmark") ?? FindCheckmark(toggle);
            if (checkmark == null)
            {
                Debug.LogWarning($"[MenuSkinDresser] El Toggle '{toggle.name}' no tiene checkmark: " +
                                 "se queda con el aspecto del USS.");
                return;
            }

            toggle.AddToClassList(SkinnedClass);

            // El USS de opciones le pinta a la casilla un fondo negro, un borde, y en :checked un
            // fondo rojo MÁS un tinte de imagen — que recolorearía el arte. Todo eso se pisa aquí
            // en línea (el estilo en línea gana al USS) para que sólo se vea el sprite.
            float size = skin.checkBoxSize;
            checkmark.style.width = size;
            checkmark.style.height = size;
            checkmark.style.backgroundColor = Color.clear;
            checkmark.style.unityBackgroundImageTintColor = Color.white;
            MenuStyle.SetBorder(checkmark, 0, Color.clear, 0);

            bool over = false;

            void Apply()
            {
                var pair = toggle.value ? skin.checkOn : skin.checkOff;
                var sprite = pair.Get(over);
                if (sprite != null) checkmark.style.backgroundImage = new StyleBackground(sprite);
            }

            Apply();

            toggle.RegisterValueChangedCallback(_ => Apply());
            toggle.RegisterCallback<PointerEnterEvent>(_ => { over = true; Apply(); });
            toggle.RegisterCallback<PointerLeaveEvent>(_ => { over = false; Apply(); });
        }

        /// <summary>
        /// Reserva por si el nombre de clase interno del <see cref="Toggle"/> cambia en una versión
        /// futura de Unity: busca a mano el hijo que hace de casilla.
        /// </summary>
        private static VisualElement FindCheckmark(VisualElement root)
        {
            foreach (var child in root.Children())
            {
                if (child.name == "unity-checkmark") return child;

                var nested = FindCheckmark(child);
                if (nested != null) return nested;
            }

            return null;
        }
    }
}
