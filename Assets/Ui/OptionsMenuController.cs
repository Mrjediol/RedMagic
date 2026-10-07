using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Localization;
using RedMagic.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Conecta los sliders y toggles de OptionsMenu.uxml con el AudioManager, y los selectores de
    /// resolución (<see cref="DisplaySettings"/>) e idioma (<see cref="Loc"/>). Se abre desde otra
    /// pantalla (menú principal o pausa) con <see cref="Open"/> y el botón Volver regresa a esa pantalla.
    ///
    /// Dos pestañas: <b>General</b> (audio, pantalla, idioma) e <b>Interfaz</b> (<see cref="HudSettings"/>:
    /// efectos de golpe y de poca vida, chuleta de controles y "Colocar elementos", que abre la
    /// cuadrícula de <see cref="GameHud"/>).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class OptionsMenuController : MonoBehaviour, IMenuScreen
    {
        [Tooltip("Si está marcado, el menú de opciones se muestra al arrancar (normalmente desactivado).")]
        [SerializeField] private bool visibleOnStart;

        private UIDocument _document;
        private VisualElement _root;

        private Slider _masterSlider, _musicSlider, _sfxSlider;
        private Toggle _masterMute, _musicMute, _sfxMute;
        private Label _masterValue, _musicValue, _sfxValue;
        private Button _backButton;
        private Button _resolutionPrev, _resolutionNext, _languagePrev, _languageNext;
        private Label _resolutionValue, _languageValue;
        private Toggle _controlsToggle;

        // Pestaña Interfaz
        private Button _generalTab, _interfaceTab;
        private VisualElement _generalPage, _interfacePage;
        private Slider _hurtSlider, _lowHealthSlider;
        private Label _hurtValue, _lowHealthValue;
        private Toggle _hurtToggle, _lowHealthToggle;
        private Button _layoutButton, _layoutResetButton;
        private Button _enemyModePrev, _enemyModeNext;
        private Label _enemyModeValue;
        private Toggle _enemyNumbersToggle, _enemyCooldownsToggle;

        private bool _hidden = true;
        private bool _open;
        private IMenuScreen _returnTo;

        /// <summary>True mientras el menú de opciones está mostrándose.</summary>
        public bool IsOpen => _open;

        private void OnEnable()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
                return;

            _hidden = !visibleOnStart;
            _open = visibleOnStart;

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

            _resolutionPrev = _root.Q<Button>("resolutionPrev");
            _resolutionNext = _root.Q<Button>("resolutionNext");
            _resolutionValue = _root.Q<Label>("resolutionValue");
            _languagePrev = _root.Q<Button>("languagePrev");
            _languageNext = _root.Q<Button>("languageNext");
            _languageValue = _root.Q<Label>("languageValue");
            _controlsToggle = _root.Q<Toggle>("controlsToggle");

            _generalTab = _root.Q<Button>("generalTab");
            _interfaceTab = _root.Q<Button>("interfaceTab");
            _generalPage = _root.Q<VisualElement>("generalPage");
            _interfacePage = _root.Q<VisualElement>("interfacePage");
            _hurtSlider = _root.Q<Slider>("hurtSlider");
            _hurtValue = _root.Q<Label>("hurtValue");
            _hurtToggle = _root.Q<Toggle>("hurtToggle");
            _lowHealthSlider = _root.Q<Slider>("lowHealthSlider");
            _lowHealthValue = _root.Q<Label>("lowHealthValue");
            _lowHealthToggle = _root.Q<Toggle>("lowHealthToggle");
            _layoutButton = _root.Q<Button>("layoutButton");
            _layoutResetButton = _root.Q<Button>("layoutResetButton");
            _enemyModePrev = _root.Q<Button>("enemyModePrev");
            _enemyModeNext = _root.Q<Button>("enemyModeNext");
            _enemyModeValue = _root.Q<Label>("enemyModeValue");
            _enemyNumbersToggle = _root.Q<Toggle>("enemyNumbersToggle");
            _enemyCooldownsToggle = _root.Q<Toggle>("enemyCooldownsToggle");

            WireInterfaceTab();
            ShowTab(false);

            WireGroup(_masterSlider, _masterMute, AudioManager.Master);
            WireGroup(_musicSlider, _musicMute, AudioManager.Music);
            WireGroup(_sfxSlider, _sfxMute, AudioManager.Sfx);

            if (_backButton != null)
            {
                _backButton.clicked += Close;
                _backButton.AddToClassList(UiSounds.NoClickClass);   // el cierre del menú ya suena
            }

            WireArrow(_resolutionPrev, PrevResolution);
            WireArrow(_resolutionNext, NextResolution);
            WireArrow(_languagePrev, PrevLanguage);
            WireArrow(_languageNext, NextLanguage);
            Loc.Changed += OnLanguageChanged;

            // Claves "#…" del UXML → texto del idioma activo; antes de vestir, que la skin mueve el
            // rótulo de los botones a un hijo.
            LocalizedUi.BindTree(_root);
            UiSounds.Bind(_root);
            DressWithSkin();
            RefreshFromAudioManager();
            RefreshSelectors();
            ApplyVisibility();
        }

        /// <summary>
        /// Pone el arte de <see cref="MenuSkin"/> encima del UXML. Sin ese asset no hace nada y el
        /// menú se ve con el aspecto plano del USS, como siempre.
        /// </summary>
        private void DressWithSkin()
        {
            var skin = MenuSkinDresser.Skin;
            if (skin == null) return;

            MenuSkinDresser.DressPanel(_root.Q<VisualElement>("optionsPanel"), skin.panel);
            MenuSkinDresser.DressTitle(_root.Q<Label>(className: "options-title"), skin.titleBar,
                                       skin.titleBarHeight, skin.titleBarMaxWidth);

            MenuSkinDresser.DressToggle(_masterMute, skin);
            MenuSkinDresser.DressToggle(_musicMute, skin);
            MenuSkinDresser.DressToggle(_sfxMute, skin);
            MenuSkinDresser.DressToggle(_controlsToggle, skin);
            MenuSkinDresser.DressToggle(_hurtToggle, skin);
            MenuSkinDresser.DressToggle(_lowHealthToggle, skin);
            MenuSkinDresser.DressToggle(_enemyNumbersToggle, skin);
            MenuSkinDresser.DressToggle(_enemyCooldownsToggle, skin);

            MenuSkinDresser.DressButton(_backButton, skin, Color.white);
            MenuSkinDresser.DressButton(_layoutButton, skin, Color.white);
            MenuSkinDresser.DressButton(_layoutResetButton, skin, Color.white);
        }

        private void OnDisable()
        {
            UnwireGroup(_masterSlider, _masterMute);
            UnwireGroup(_musicSlider, _musicMute);
            UnwireGroup(_sfxSlider, _sfxMute);
            UnwireInterfaceTab();

            if (_backButton != null)
            {
                _backButton.clicked -= Close;
            }

            UnwireArrow(_resolutionPrev, PrevResolution);
            UnwireArrow(_resolutionNext, NextResolution);
            UnwireArrow(_languagePrev, PrevLanguage);
            UnwireArrow(_languageNext, NextLanguage);
            Loc.Changed -= OnLanguageChanged;
        }

        // ------------------------------------------------------------------ pestañas

        private void OnGeneralTab() => ShowTab(false);
        private void OnInterfaceTab() => ShowTab(true);

        private void ShowTab(bool interfaceTab)
        {
            if (_generalPage != null) _generalPage.style.display = interfaceTab ? DisplayStyle.None : DisplayStyle.Flex;
            if (_interfacePage != null) _interfacePage.style.display = interfaceTab ? DisplayStyle.Flex : DisplayStyle.None;
            _generalTab?.EnableInClassList("option-tab--active", !interfaceTab);
            _interfaceTab?.EnableInClassList("option-tab--active", interfaceTab);
            if (interfaceTab) RefreshInterfaceTab();
        }

        // ------------------------------------------------------------------ pestaña Interfaz (HudSettings)

        private void WireInterfaceTab()
        {
            if (_generalTab != null) _generalTab.clicked += OnGeneralTab;
            if (_interfaceTab != null) _interfaceTab.clicked += OnInterfaceTab;

            _hurtSlider?.RegisterValueChangedCallback(OnHurtSlider);
            _hurtToggle?.RegisterValueChangedCallback(OnHurtToggle);
            _lowHealthSlider?.RegisterValueChangedCallback(OnLowHealthSlider);
            _lowHealthToggle?.RegisterValueChangedCallback(OnLowHealthToggle);
            _controlsToggle?.RegisterValueChangedCallback(OnControlsToggled);

            if (_layoutButton != null) _layoutButton.clicked += OnPlaceHud;
            if (_layoutResetButton != null) _layoutResetButton.clicked += HudSettings.ResetAnchors;

            WireArrow(_enemyModePrev, PrevEnemyMode);
            WireArrow(_enemyModeNext, NextEnemyMode);
            _enemyNumbersToggle?.RegisterValueChangedCallback(OnEnemyNumbersToggle);
            _enemyCooldownsToggle?.RegisterValueChangedCallback(OnEnemyCooldownsToggle);

            RefreshInterfaceTab();
        }

        private void UnwireInterfaceTab()
        {
            if (_generalTab != null) _generalTab.clicked -= OnGeneralTab;
            if (_interfaceTab != null) _interfaceTab.clicked -= OnInterfaceTab;

            _hurtSlider?.UnregisterValueChangedCallback(OnHurtSlider);
            _hurtToggle?.UnregisterValueChangedCallback(OnHurtToggle);
            _lowHealthSlider?.UnregisterValueChangedCallback(OnLowHealthSlider);
            _lowHealthToggle?.UnregisterValueChangedCallback(OnLowHealthToggle);
            _controlsToggle?.UnregisterValueChangedCallback(OnControlsToggled);

            if (_layoutButton != null) _layoutButton.clicked -= OnPlaceHud;
            if (_layoutResetButton != null) _layoutResetButton.clicked -= HudSettings.ResetAnchors;

            UnwireArrow(_enemyModePrev, PrevEnemyMode);
            UnwireArrow(_enemyModeNext, NextEnemyMode);
            _enemyNumbersToggle?.UnregisterValueChangedCallback(OnEnemyNumbersToggle);
            _enemyCooldownsToggle?.UnregisterValueChangedCallback(OnEnemyCooldownsToggle);
        }

        // Vida de enemigos: selector de modo con flechas, como resolución e idioma.
        private static readonly EnemyHealthMode[] EnemyModes =
            { EnemyHealthMode.Off, EnemyHealthMode.Overhead, EnemyHealthMode.Focus, EnemyHealthMode.List };

        private void PrevEnemyMode() => StepEnemyMode(-1);
        private void NextEnemyMode() => StepEnemyMode(1);

        private void StepEnemyMode(int step)
        {
            int index = System.Array.IndexOf(EnemyModes, HudSettings.EnemyHealth);
            HudSettings.EnemyHealth = EnemyModes[((index + step) % EnemyModes.Length + EnemyModes.Length) % EnemyModes.Length];
            RefreshInterfaceTab();
        }

        // Claves literales (no "$prefix.{modo}") para que la auditoría de idiomas las vea.
        private static string EnemyModeLabel(EnemyHealthMode mode) => mode switch
        {
            EnemyHealthMode.Overhead => Loc.Get("options.hud.enemy_mode.overhead"),
            EnemyHealthMode.Focus => Loc.Get("options.hud.enemy_mode.focus"),
            EnemyHealthMode.List => Loc.Get("options.hud.enemy_mode.list"),
            _ => Loc.Get("options.disabled"),
        };

        private static void OnEnemyNumbersToggle(ChangeEvent<bool> evt) => HudSettings.EnemyHealthNumbers = evt.newValue;

        private static void OnEnemyCooldownsToggle(ChangeEvent<bool> evt) => HudSettings.EnemyCooldownsDebug = evt.newValue;

        private void RefreshInterfaceTab()
        {
            _hurtSlider?.SetValueWithoutNotify(HudSettings.HurtIntensity);
            _hurtToggle?.SetValueWithoutNotify(HudSettings.HurtEffect);
            _lowHealthSlider?.SetValueWithoutNotify(HudSettings.LowHealthIntensity);
            _lowHealthToggle?.SetValueWithoutNotify(HudSettings.LowHealthEffect);
            _controlsToggle?.SetValueWithoutNotify(HudSettings.ShowControls);
            _enemyNumbersToggle?.SetValueWithoutNotify(HudSettings.EnemyHealthNumbers);
            _enemyCooldownsToggle?.SetValueWithoutNotify(HudSettings.EnemyCooldownsDebug);
            if (_enemyModeValue != null) _enemyModeValue.text = EnemyModeLabel(HudSettings.EnemyHealth);
            _enemyNumbersToggle?.SetEnabled(HudSettings.EnemyHealth != EnemyHealthMode.Off);
            _enemyCooldownsToggle?.SetEnabled(HudSettings.EnemyHealth != EnemyHealthMode.Off);
            RefreshEffectRow(_hurtSlider, _hurtValue, HudSettings.HurtEffect, HudSettings.HurtIntensity);
            RefreshEffectRow(_lowHealthSlider, _lowHealthValue, HudSettings.LowHealthEffect, HudSettings.LowHealthIntensity);
        }

        // Apagado: el slider se atenúa y el valor dice "Desactivado".
        private static void RefreshEffectRow(Slider slider, Label value, bool on, float intensity)
        {
            slider?.SetEnabled(on);
            if (value != null) value.text = on ? Mathf.RoundToInt(intensity * 100f) + "%" : Loc.Get("options.disabled");
        }

        private void OnHurtSlider(ChangeEvent<float> evt)
        {
            HudSettings.HurtIntensity = evt.newValue;
            RefreshEffectRow(_hurtSlider, _hurtValue, HudSettings.HurtEffect, evt.newValue);
        }

        private void OnHurtToggle(ChangeEvent<bool> evt)
        {
            HudSettings.HurtEffect = evt.newValue;
            RefreshEffectRow(_hurtSlider, _hurtValue, evt.newValue, HudSettings.HurtIntensity);
        }

        private void OnLowHealthSlider(ChangeEvent<float> evt)
        {
            HudSettings.LowHealthIntensity = evt.newValue;
            RefreshEffectRow(_lowHealthSlider, _lowHealthValue, HudSettings.LowHealthEffect, evt.newValue);
        }

        private void OnLowHealthToggle(ChangeEvent<bool> evt)
        {
            HudSettings.LowHealthEffect = evt.newValue;
            RefreshEffectRow(_lowHealthSlider, _lowHealthValue, evt.newValue, HudSettings.LowHealthIntensity);
        }

        private static void OnControlsToggled(ChangeEvent<bool> evt) => HudSettings.ShowControls = evt.newValue;

        /// <summary>
        /// "Colocar elementos": Opciones se aparta (sigue abierto y el juego en pausa) y el HUD
        /// enseña su cuadrícula; al pulsar Listo (o Escape) vuelve aquí.
        /// </summary>
        private void OnPlaceHud()
        {
            if (!GameHud.BeginLayoutEdit(() => SetVisible(true))) return;
            SetVisible(false);
        }

        // ------------------------------------------------------------------ resolución e idioma

        private static void WireArrow(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked += onClick;
        }

        private static void UnwireArrow(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked -= onClick;
        }

        private void PrevResolution() => StepResolution(-1);
        private void NextResolution() => StepResolution(1);
        private void PrevLanguage() => StepLanguage(-1);
        private void NextLanguage() => StepLanguage(1);

        private void StepResolution(int step)
        {
            int count = DisplaySettings.Presets.Count;
            if (count == 0) return;
            DisplaySettings.SetPreset(((DisplaySettings.PresetIndex + step) % count + count) % count);
            RefreshSelectors();
        }

        private void StepLanguage(int step)
        {
            var languages = Loc.Languages;
            if (languages.Count == 0) return;

            int current = 0;
            for (int i = 0; i < languages.Count; i++)
                if (languages[i].Code == Loc.Language) current = i;

            Loc.SetLanguage(languages[((current + step) % languages.Count + languages.Count) % languages.Count].Code);
            // Loc.Changed → OnLanguageChanged repinta.
        }

        private void OnLanguageChanged()
        {
            RefreshFromAudioManager(); // "Silenciado" / "Muted"
            RefreshInterfaceTab();     // "Desactivado" / "Off"
            RefreshSelectors();
        }

        private void RefreshSelectors()
        {
            var preset = DisplaySettings.Current;
            if (_resolutionValue != null)
                _resolutionValue.text = preset != null ? Loc.Get("options.resolution_value", Loc.Get(preset.labelKey), preset.aspect) : "-";

            if (_languageValue != null)
            {
                string name = Loc.Language;
                foreach (var language in Loc.Languages)
                    if (language.Code == Loc.Language) name = language.DisplayName;
                _languageValue.text = name;
            }
        }

        /// <summary>Abre Opciones desde <paramref name="returnTo"/> y pausa el juego.</summary>
        public void Open(IMenuScreen returnTo)
        {
            _returnTo = returnTo;
            if (!_open && GameStateManager.Instance != null)
                GameStateManager.Instance.SetPaused(true);
            _open = true;
            SystemSounds.Play(s => s.optionsMenu.open);

            RefreshFromAudioManager();
            ShowTab(false);   // siempre se abre por General
            SetVisible(true);

            // Con mando: dar foco para poder navegar los sliders/toggles con el d-pad.
            if (InputDeviceManager.GamepadActive)
                _masterSlider?.Focus();
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
            // 'visibility' en vez de 'display:none': un root de UIDocument oculto con display no recibe
            // tamaño del panel y luego no se redimensiona al mostrarlo. 'hidden' participa en el layout
            // pero no se dibuja ni recibe eventos de puntero.
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

        /// <summary>Vuelve a la pantalla que abrió Opciones (botón Volver o Escape/Atrás). Suena el cierre del menú.</summary>
        public void GoBack()
        {
            // Colocando el HUD, Escape / Atrás es "Listo": vuelve a Opciones, no lo cierra.
            if (GameHud.IsEditingLayout)
            {
                GameHud.EndLayoutEdit();
                return;
            }

            Close();
        }

        private void Close()
        {
            if (_open && GameStateManager.Instance != null)
                GameStateManager.Instance.SetPaused(false);
            _open = false;
            SystemSounds.Play(s => s.optionsMenu.close);

            SetVisible(false);

            if (_returnTo != null) _returnTo.SetVisible(true);
            else Debug.LogWarning("[OptionsMenu] Sin pantalla de retorno; usa Open(returnTo).", this);
        }


        private static void UpdateValueLabel(Label label, float volume, bool muted)
        {
            if (label == null) return;
            label.text = muted ? Loc.Get("options.muted") : Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f) + "%";
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
