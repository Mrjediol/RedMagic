using RedMagic.Audio;
using UnityEngine;
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
    public class MainMenuController : MonoBehaviour
    {
        [Header("Escenas")]
        [Tooltip("Nombre de la escena de juego que carga el botón Jugar (debe estar en Build Settings).")]
        [SerializeField] private string gameplaySceneName = "SampleScene";

        [Header("Opciones")]
        [Tooltip("Menú de opciones. Se muestra al pulsar Opciones y este menú se oculta.")]
        [SerializeField] private OptionsMenuController optionsMenu;

        [Header("Música")]
        [Tooltip("id del SoundData que suena en bucle mientras se está en el menú. Vacío = sin música.")]
        [SerializeField] private string menuMusicId = "Music_Menu";

        private UIDocument _document;
        private VisualElement _root;
        private Button _playButton;
        private Button _optionsButton;
        private Button _quitButton;
        private bool _hidden;

        private void OnEnable()
        {
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

            ApplyVisibility();
        }

        private void Start()
        {
            // La música del menú suena en bucle mientras se está en el menú (SoundData con loop = true).
            // PlayMusic ignora la llamada si ese tema ya está sonando, así que volver de Opciones no lo reinicia.
            if (!string.IsNullOrWhiteSpace(menuMusicId) && AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(menuMusicId);
        }

        private void OnDisable()
        {
            Unwire(_playButton, OnPlayClicked);
            Unwire(_optionsButton, OnOptionsClicked);
            Unwire(_quitButton, OnQuitClicked);
        }

        /// <summary>Muestra u oculta el menú sin desactivar el GameObject (evita perder el layout de UI Toolkit).</summary>
        public void SetVisible(bool visible)
        {
            _hidden = !visible;
            ApplyVisibility();
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
            button.RegisterCallback<PointerEnterEvent>(OnButtonHover);
        }

        private void Unwire(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked -= onClick;
            button.UnregisterCallback<PointerEnterEvent>(OnButtonHover);
        }

        private static void OnButtonHover(PointerEnterEvent _)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonHover");
        }

        private static void PlayClick()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonClick");
        }

        private void OnPlayClicked()
        {
            PlayClick();

            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogWarning("[MainMenu] gameplaySceneName está vacío.", this);
                return;
            }

            if (AudioManager.Instance != null)
                AudioManager.Instance.StopMusic();

            SceneManager.LoadScene(gameplaySceneName);
        }

        private void OnOptionsClicked()
        {
            PlayClick();

            if (optionsMenu == null)
            {
                Debug.LogWarning("[MainMenu] No hay optionsMenu asignado en el Inspector.", this);
                return;
            }

            optionsMenu.SetVisible(true);
            SetVisible(false);
        }

        private void OnQuitClicked()
        {
            PlayClick();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
