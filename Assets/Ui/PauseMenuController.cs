using RedMagic.Audio;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Menú de pausa durante el juego (UI Toolkit).
    ///
    /// Formas de pausar:
    ///  - Táctil: botón de pausa en pantalla (esquina superior derecha). Sólo visible en modo táctil.
    ///  - Mando: botón Start / Menú.
    ///  - Android: el botón "Atrás" llega como tecla Escape del Input System.
    ///
    /// Mientras está abierto el juego queda en pausa vía <see cref="GameStateManager"/>;
    /// la música y los SFX de UI siguen sonando.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenuController : MonoBehaviour, IMenuScreen
    {
        [Header("Navegación")]
        [Tooltip("Menú de opciones de la escena de juego (opcional).")]
        [SerializeField] private OptionsMenuController optionsMenu;

        [Tooltip("Escena del menú principal que carga el botón 'Menú principal'.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private UIDocument _document;
        private VisualElement _root;
        private Button _openPauseButton;
        private VisualElement _overlay;
        private Button _resumeButton;
        private Button _optionsButton;
        private Button _mainMenuButton;

        private bool _open;
        private bool _touchMode = true;

        private void OnEnable()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
                return;

            _openPauseButton = _root.Q<Button>("openPauseButton");
            _overlay = _root.Q<VisualElement>("pauseOverlay");
            _resumeButton = _root.Q<Button>("resumeButton");
            _optionsButton = _root.Q<Button>("optionsButton");
            _mainMenuButton = _root.Q<Button>("mainMenuButton");

            Wire(_openPauseButton, Pause);
            Wire(_resumeButton, Resume);
            Wire(_optionsButton, OpenOptions);
            Wire(_mainMenuButton, GoToMainMenu);

            _overlay?.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);

            if (InputDeviceManager.Instance != null)
            {
                InputDeviceManager.Instance.ModeChanged -= OnInputModeChanged;
                InputDeviceManager.Instance.ModeChanged += OnInputModeChanged;
                _touchMode = InputDeviceManager.Instance.CurrentMode == InputMode.Touch;
            }

            _open = false;
            ShowOverlay(false);
            RefreshPauseButton();
        }

        private void OnDisable()
        {
            Unwire(_openPauseButton, Pause);
            Unwire(_resumeButton, Resume);
            Unwire(_optionsButton, OpenOptions);
            Unwire(_mainMenuButton, GoToMainMenu);

            _overlay?.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);

            if (InputDeviceManager.Instance != null)
                InputDeviceManager.Instance.ModeChanged -= OnInputModeChanged;
        }

        private void Update()
        {
            // Update corre con Time.timeScale = 0.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                OnBackInput();
                return;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.startButton.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame))
                OnBackInput();
        }

        private void OnNavigationCancel(NavigationCancelEvent _)
        {
            // Botón B / Este del mando dentro del menú de pausa.
            OnBackInput();
        }

        /// <summary>Escape / Atrás de Android / Start del mando.</summary>
        private void OnBackInput()
        {
            if (!_open)
            {
                Pause();
                return;
            }

            if (optionsMenu != null && optionsMenu.IsOpen)
            {
                optionsMenu.GoBack();
                return;
            }

            Resume();
        }

        private void OnInputModeChanged(InputMode mode)
        {
            _touchMode = mode == InputMode.Touch;
            RefreshPauseButton();

            // Si se pasa a mando con la pausa abierta, dar foco para poder navegar.
            if (_open && !_touchMode)
                FocusFirstButton();
        }

        private void Pause()
        {
            if (_open) return;
            _open = true;

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetPaused(true);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonClick");

            RefreshPauseButton();
            ShowOverlay(true);

            if (!_touchMode)
                FocusFirstButton();
        }

        private void Resume()
        {
            if (!_open) return;
            _open = false;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonClick");
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetPaused(false);

            ShowOverlay(false);
            RefreshPauseButton();
        }

        private void OpenOptions()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonClick");

            if (optionsMenu == null)
            {
                Debug.LogWarning("[PauseMenu] No hay optionsMenu asignado.", this);
                return;
            }

            optionsMenu.Open(this);
            ShowOverlay(false);
        }

        private void GoToMainMenu()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonClick");

            _open = false;
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.ForceResume();

            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>IMenuScreen: usado por OptionsMenuController para volver a mostrar la pausa.</summary>
        public void SetVisible(bool visible)
        {
            ShowOverlay(visible);
            if (visible && !_touchMode)
                FocusFirstButton();
        }

        private void FocusFirstButton()
        {
            // El overlay se muestra con display de forma síncrona, así que el botón ya es enfocable.
            _resumeButton?.Focus();
        }

        // _overlay y _openPauseButton son elementos HIJOS (no el root del UIDocument), así que
        // alternar display es fiable: el panel ya tiene layout y display:none quita también el picking.
        private void ShowOverlay(bool visible)
        {
            if (_overlay != null)
                _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshPauseButton()
        {
            // El botón de pausa en pantalla sólo tiene sentido en táctil y cuando NO estás en pausa.
            if (_openPauseButton != null)
                _openPauseButton.style.display = (_touchMode && !_open) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Wire(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked += onClick;
            button.RegisterCallback<PointerEnterEvent>(OnHover);
        }

        private void Unwire(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked -= onClick;
            button.UnregisterCallback<PointerEnterEvent>(OnHover);
        }

        private static void OnHover(PointerEnterEvent _)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonHover");
        }
    }
}
