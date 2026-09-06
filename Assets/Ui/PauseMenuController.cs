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
    ///  - Botón de pausa en pantalla (esquina superior derecha), pensado para móvil/táctil.
    ///  - Botón "Atrás" de Android: Unity lo mapea a la tecla Escape del Input System
    ///    (KEYCODE_BACK -> Key.Escape), así que <see cref="Keyboard"/> lo cubre sin código extra.
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

            _open = false;
            ShowOverlay(false);
            ShowPauseButton(true);
        }

        private void OnDisable()
        {
            Unwire(_openPauseButton, Pause);
            Unwire(_resumeButton, Resume);
            Unwire(_optionsButton, OpenOptions);
            Unwire(_mainMenuButton, GoToMainMenu);
        }

        private void Update()
        {
            // Update se ejecuta con Time.timeScale = 0. El botón "Atrás" de Android llega aquí como Escape.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                OnBackInput();
        }

        /// <summary>Escape / botón Atrás de Android.</summary>
        private void OnBackInput()
        {
            if (!_open)
            {
                Pause();
                return;
            }

            // Si Opciones está encima, "Atrás" vuelve al menú de pausa.
            if (optionsMenu != null && optionsMenu.IsOpen)
            {
                optionsMenu.GoBack();
                return;
            }

            Resume();
        }

        private void Pause()
        {
            if (_open) return;
            _open = true;

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetPaused(true);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonClick");

            ShowPauseButton(false);
            ShowOverlay(true);
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
            ShowPauseButton(true);
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
        }

        // _overlay y _openPauseButton son elementos HIJOS (no el root del UIDocument), así que
        // alternar display es fiable: el panel ya tiene layout y display:none quita también el picking.
        private void ShowOverlay(bool visible)
        {
            if (_overlay != null)
                _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ShowPauseButton(bool visible)
        {
            if (_openPauseButton != null)
                _openPauseButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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
