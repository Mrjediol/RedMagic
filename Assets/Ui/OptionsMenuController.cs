using RedMagic.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Conecta los sliders y toggles de OptionsMenu.uxml con el AudioManager.
    /// El botón Volver oculta este menú y devuelve el control al menú principal.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class OptionsMenuController : MonoBehaviour
    {
        [Header("Navegación")]
        [Tooltip("Menú principal que se vuelve a mostrar al pulsar Volver.")]
        [SerializeField] private MainMenuController mainMenu;

        [Tooltip("Si está marcado, el menú de opciones se muestra al arrancar (normalmente desactivado).")]
        [SerializeField] private bool visibleOnStart;

        private UIDocument _document;
        private VisualElement _root;

        private Slider _masterSlider, _musicSlider, _sfxSlider;
        private Toggle _masterMute, _musicMute, _sfxMute;
        private Label _masterValue, _musicValue, _sfxValue;
        private Button _backButton;
        private bool _hidden = true;

        private void OnEnable()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
                return;

            _hidden = !visibleOnStart;

            _masterSlider = _root.Q<Slider>("masterSlider");
            _musicSlider = _root.Q<Slider>("musicSlider");
            _sfxSlider = _root.Q<Slider>("sfxSlider");

            _masterMute = _root.Q<Toggle>("masterMute");
            _musicMute = _root.Q<Toggle>("musicMute");
            _sfxMute = _root.Q<Toggle>("sfxMute");

            _masterValue = _root.Q<Label>("masterValue");
            _musicValue = _root.Q<Label>("musicValue");
            _sfxValue = _root.Q<Label>("sfxValue");

            _backButton = _root.Q<Button>("backButton");

            WireGroup(_masterSlider, _masterMute, AudioManager.Master);
            WireGroup(_musicSlider, _musicMute, AudioManager.Music);
            WireGroup(_sfxSlider, _sfxMute, AudioManager.Sfx);

            if (_backButton != null)
            {
                _backButton.clicked += OnBack;
                _backButton.RegisterCallback<PointerEnterEvent>(OnHover);
            }

            RefreshFromAudioManager();
            ApplyVisibility();
        }

        private void OnDisable()
        {
            UnwireGroup(_masterSlider, _masterMute);
            UnwireGroup(_musicSlider, _musicMute);
            UnwireGroup(_sfxSlider, _sfxMute);

            if (_backButton != null)
            {
                _backButton.clicked -= OnBack;
                _backButton.UnregisterCallback<PointerEnterEvent>(OnHover);
            }
        }

        /// <summary>Muestra u oculta el menú sin desactivar el GameObject (evita perder el layout de UI Toolkit).</summary>
        public void SetVisible(bool visible)
        {
            _hidden = !visible;
            if (visible) RefreshFromAudioManager();
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

        private void WireGroup(Slider slider, Toggle mute, string group)
        {
            if (slider != null)
            {
                slider.lowValue = 0f;
                slider.highValue = 1f;
                slider.userData = group;
                slider.RegisterValueChangedCallback(OnSliderChanged);
            }

            if (mute != null)
            {
                mute.userData = group;
                mute.RegisterValueChangedCallback(OnMuteChanged);
            }
        }

        private void UnwireGroup(Slider slider, Toggle mute)
        {
            if (slider != null) slider.UnregisterValueChangedCallback(OnSliderChanged);
            if (mute != null) mute.UnregisterValueChangedCallback(OnMuteChanged);
        }

        private void RefreshFromAudioManager()
        {
            var audio = AudioManager.Instance;

            RefreshGroup(_masterSlider, _masterMute, _masterValue, AudioManager.Master, audio);
            RefreshGroup(_musicSlider, _musicMute, _musicValue, AudioManager.Music, audio);
            RefreshGroup(_sfxSlider, _sfxMute, _sfxValue, AudioManager.Sfx, audio);
        }

        private static void RefreshGroup(Slider slider, Toggle mute, Label value, string group, AudioManager audio)
        {
            float volume = audio != null ? audio.GetVolume(group) : 1f;
            bool muted = audio != null && audio.IsMuted(group);

            slider?.SetValueWithoutNotify(volume);
            mute?.SetValueWithoutNotify(muted);
            UpdateValueLabel(value, volume, muted);
        }

        private void OnSliderChanged(ChangeEvent<float> evt)
        {
            if (!(evt.target is Slider slider) || !(slider.userData is string group))
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetVolume(group, evt.newValue);

            UpdateValueLabel(LabelFor(group), evt.newValue, MuteValueFor(group));
        }

        private void OnMuteChanged(ChangeEvent<bool> evt)
        {
            if (!(evt.target is Toggle toggle) || !(toggle.userData is string group))
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMute(group, evt.newValue);

            UpdateValueLabel(LabelFor(group), SliderValueFor(group), evt.newValue);
        }

        private void OnBack()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonClick");

            if (mainMenu != null) mainMenu.SetVisible(true);
            SetVisible(false);
        }

        private static void OnHover(PointerEnterEvent _)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("SFX_ButtonHover");
        }

        private static void UpdateValueLabel(Label label, float volume, bool muted)
        {
            if (label == null) return;
            label.text = muted ? "Silenciado" : Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f) + "%";
        }

        private Label LabelFor(string group)
        {
            if (group == AudioManager.Master) return _masterValue;
            if (group == AudioManager.Music) return _musicValue;
            if (group == AudioManager.Sfx) return _sfxValue;
            return null;
        }

        private float SliderValueFor(string group)
        {
            if (group == AudioManager.Master) return _masterSlider != null ? _masterSlider.value : 1f;
            if (group == AudioManager.Music) return _musicSlider != null ? _musicSlider.value : 1f;
            if (group == AudioManager.Sfx) return _sfxSlider != null ? _sfxSlider.value : 1f;
            return 1f;
        }

        private bool MuteValueFor(string group)
        {
            if (group == AudioManager.Master) return _masterMute != null && _masterMute.value;
            if (group == AudioManager.Music) return _musicMute != null && _musicMute.value;
            if (group == AudioManager.Sfx) return _sfxMute != null && _sfxMute.value;
            return false;
        }
    }
}
