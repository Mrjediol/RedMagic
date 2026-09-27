using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RedMagic.UI
{
    /// <summary>
    /// Lógica de los botones del menú principal (UI Toolkit).
    /// Se engancha a los botones definidos en MainMenu.uxml.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour, IMenuScreen
    {
        /// <summary>
        /// True mientras la escena del menú principal está activa (aunque esté mostrando el
        /// submenú de Opciones encima). Otros HUD persistentes (<see cref="CurrencyHud"/>) lo usan
        /// para no enseñarse aquí — no hay moneda que contar todavía.
        /// </summary>
        public static bool IsOpen { get; private set; }

        [Header("Escenas")]
        [Tooltip("Escena que carga el botón Jugar: el hub. La referencia es al asset, no a su " +
                 "nombre, así que aguanta renombrados. Debe estar en Build Settings.")]
        [SerializeField] private SceneReference hubScene = new SceneReference();

        [Header("Opciones")]
        [Tooltip("Menú de opciones. Se muestra al pulsar Opciones y este menú se oculta.")]
        [SerializeField] private OptionsMenuController optionsMenu;

        [Header("Música")]
        [Tooltip("Suena en bucle mientras se está en el menú. Vacío = sin música.")]
        [SerializeField] private AudioClip menuMusic;

        private UIDocument _document;
        private VisualElement _root;
        private Button _playButton;
        private Button _optionsButton;
        private Button _quitButton;
        private bool _hidden;

        private void OnEnable()
        {
            IsOpen = true;

            if (_document == null)
                _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
                return;

            _playButton = _root.Q<Button>("playButton");
            _optionsButton = _root.Q<Button>("optionsButton");
            _quitButton = _root.Q<Button>("quitButton");

            Wire(_playButton, OnPlayClicked);
            Wire(_optionsButton, OnOptionsClicked);
            Wire(_quitButton, OnQuitClicked);

            LocalizedUi.BindTree(_root); // claves "#…" del UXML, antes de vestir
            UiSounds.Bind(_root);
            DressWithSkin();
            ApplyVisibility();
            FocusForGamepad();
        }

        /// <summary>
        /// Pone el arte de <see cref="MenuSkin"/> encima del UXML. Sin ese asset no hace nada y el
        /// menú se ve con el aspecto plano del USS, como siempre.
        /// </summary>
        private void DressWithSkin()
        {
            var skin = MenuSkinDresser.Skin;
            if (skin == null) return;

            // Sin título (MainMenu.jpeg hace de portada): nada que enmarcar en titleContainer.

            MenuSkinDresser.DressButton(_playButton, skin, Color.white);
            MenuSkinDresser.DressButton(_optionsButton, skin, Color.white);
            MenuSkinDresser.DressButton(_quitButton, skin, skin.quitTint);
        }

        /// <summary>Con mando, deja el primer botón enfocado para poder navegar con el d-pad.</summary>
        private void FocusForGamepad()
        {
            if (!_hidden && InputDeviceManager.GamepadActive)
                _playButton?.Focus();
        }

        private void Start()
        {
            // Estar en el menú principal cuenta como "menú abierto": el juego queda en pausa
            // (inofensivo aquí, pero coherente con el resto de menús).
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetPaused(true);

            // PlayMusic ignora la llamada si ese tema ya está sonando, así que volver de Opciones no lo reinicia.
            if (menuMusic != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(menuMusic);
        }

        private void OnDisable()
        {
            IsOpen = false;

            Unwire(_playButton, OnPlayClicked);
            Unwire(_optionsButton, OnOptionsClicked);
            Unwire(_quitButton, OnQuitClicked);
        }

        private void Update()
        {
            // Botón "Atrás" de Android (= tecla Escape en el Input System): si Opciones está
            // abierto, vuelve al menú. En el menú principal no hace nada (añade aquí un
            // "¿salir del juego?" si lo quieres).
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (optionsMenu != null && optionsMenu.IsOpen)
                optionsMenu.GoBack();
        }

        /// <summary>Muestra u oculta el menú sin desactivar el GameObject (evita perder el layout de UI Toolkit).</summary>
        public void SetVisible(bool visible)
        {
            _hidden = !visible;
            ApplyVisibility();
            FocusForGamepad();
        }

        private void ApplyVisibility()
        {
            // Se usa 'visibility' en vez de 'display:none': un root de UIDocument oculto con display
            // no recibe tamaño del panel y luego no vuelve a dimensionarse al mostrarlo. 'hidden'
            // participa en el layout pero no se dibuja ni recibe eventos de puntero.
            if (_root != null)
                _root.style.visibility = _hidden ? Visibility.Hidden : Visibility.Visible;
        }

        private void Wire(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked += onClick;
        }

        private void Unwire(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked -= onClick;
        }

        private void OnPlayClicked()
        {
            // Se valida ANTES de tocar pausa o música: si la escena no se pudiera cargar, el menú
            // se queda como estaba en vez de cortar la música y dejar al jugador en un menú mudo.
            string path = hubScene.ResolveForLoad(this);
            if (path == null) return;

            // Al entrar al hub: despausar del todo y parar la música del menú.
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.ForceResume();

            if (AudioManager.Instance != null)
                AudioManager.Instance.StopMusic();

            SceneManager.LoadScene(path);
        }

        private void OnOptionsClicked()
        {
            if (optionsMenu == null)
            {
                Debug.LogWarning("[MainMenu] No hay optionsMenu asignado en el Inspector.", this);
                return;
            }

            optionsMenu.Open(this);
            SetVisible(false);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
