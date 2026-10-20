using RedMagic.Core;
using RedMagic.Localization;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.Survival
{
    /// <summary>
    /// Lo que se ve del modo Supervivencia, construido en código (mismo panel que <see cref="GameHud"/>):
    /// <list type="bullet">
    /// <item>arriba al centro, la ronda y el mundo, y debajo los enemigos que quedan (o "¡Jefe!");</item>
    /// <item>entre rondas, abajo al centro: "Ronda N superada" y el botón <b>Siguiente ronda</b> (también Intro, o Select
    /// en el mando), o la cuenta atrás si el config la pone;</item>
    /// <item>al morir (o al acabar todos los mundos), un cartel con Reintentar / Menú principal.</item>
    /// </list>
    /// Vive en un hijo del <see cref="SurvivalManager"/>, así que se va con la escena.
    /// </summary>
    public class SurvivalHud : MonoBehaviour
    {
        private const string PanelSettingsResourcePath = "CurrencyHudPanelSettings";

        private SurvivalManager _manager;
        private UIDocument _document;
        private VisualElement _top, _between, _over;
        private Label _wave, _enemies, _cleared, _nextHint, _overTitle;
        private Button _next, _retry, _menu, _mastery, _overMastery;
        private int _shownWave = -1, _shownLeft = -1;
        private SurvivalManager.Phase _shownPhase = (SurvivalManager.Phase)(-1);
        private string _shownHint;
        private bool _built, _overShown;
        private float _overDelay = -1f;

        public static void Ensure(SurvivalManager manager)
        {
            if (manager.GetComponentInChildren<SurvivalHud>() != null) return;
            var go = new GameObject("SurvivalHud");
            go.transform.SetParent(manager.transform, false);
            go.AddComponent<SurvivalHud>()._manager = manager;
        }

        private void Start()
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
            _document.sortingOrder = 5;   // sobre el HUD normal dentro del mismo panel
            _manager.WaveStarted += _ => { _shownWave = -1; };
            _manager.GameOver += _ => _overDelay = 1.6f;   // que se vea caer al jugador antes del cartel
            Loc.Changed += Relabel;
        }

        private void OnDestroy() => Loc.Changed -= Relabel;

        private void Build()
        {
            var root = _document.rootVisualElement;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;

            // Arriba: ronda y enemigos.
            _top = Column(root, "survival-top");
            _top.style.position = Position.Absolute;
            _top.style.top = 10; _top.style.left = 0; _top.style.right = 0;
            _top.style.alignItems = Align.Center;
            _wave = HudStyle.OutlinedLabel("wave", 34f);
            _wave.style.color = HudStyle.Gold;
            _top.Add(_wave);
            _enemies = HudStyle.OutlinedLabel("enemies", 24f);
            _top.Add(_enemies);

            // Entre rondas: a la derecha, a media altura (abajo tapaba la tienda del suelo; arriba va la ficha del item).
            _between = Column(root, "survival-between");
            _between.style.position = Position.Absolute;
            _between.style.top = Length.Percent(36); _between.style.right = 40;
            _between.style.alignItems = Align.FlexEnd;
            _cleared = HudStyle.OutlinedLabel("cleared", 34f);
            _cleared.style.color = HudStyle.Gold;
            _between.Add(_cleared);
            _next = MakeButton("next", () => _manager.RequestNextWave());
            _next.style.marginTop = 10;
            _between.Add(_next);
            _nextHint = HudStyle.OutlinedLabel("next-hint", 20f);
            _nextHint.style.marginTop = 4;
            _between.Add(_nextHint);
            // Sólo en la preparación: las mejoras permanentes no se tocan a mitad de partida.
            _mastery = MakeButton("mastery", OpenMastery);
            _mastery.style.marginTop = 14;
            _between.Add(_mastery);

            // Fin de partida: en el centro, con fondo.
            _over = Column(root, "survival-over");
            _over.style.position = Position.Absolute;
            _over.style.left = 0; _over.style.right = 0; _over.style.top = 0; _over.style.bottom = 0;
            _over.style.alignItems = Align.Center;
            _over.style.justifyContent = Justify.Center;
            _over.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _over.pickingMode = PickingMode.Position;
            _overTitle = HudStyle.OutlinedLabel("over-title", 52f);
            _overTitle.style.color = HudStyle.Gold;
            _over.Add(_overTitle);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 24;
            _retry = MakeButton("retry", () => _manager.Retry());
            _menu = MakeButton("menu", () => _manager.ToMainMenu());
            _menu.style.marginLeft = 24;
            _overMastery = MakeButton("over-mastery", OpenMastery);
            _overMastery.style.marginLeft = 24;
            row.Add(_retry); row.Add(_overMastery); row.Add(_menu);
            _over.Add(row);

            Show(_between, false);
            Show(_over, false);
            _built = true;
            Relabel();
        }

        /// <summary>El menú de mejoras del hub, en la pestaña de armas (mejoras permanentes).</summary>
        private static void OpenMastery() => UpgradeMenuController.Instance?.OpenWeapons();

        public static string DifficultyName(SurvivalDifficulty difficulty) => difficulty switch
        {
            SurvivalDifficulty.Easy => Loc.Get("survival.difficulty.easy"),
            SurvivalDifficulty.Hard => Loc.Get("survival.difficulty.hard"),
            _ => Loc.Get("survival.difficulty.normal"),
        };

        private static VisualElement Column(VisualElement parent, string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.flexDirection = FlexDirection.Column;
            parent.Add(e);
            return e;
        }

        private static Button MakeButton(string name, System.Action onClick)
        {
            var b = new Button(onClick) { name = name };
            b.style.fontSize = 30;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.color = HudStyle.Text;
            b.style.backgroundColor = HudStyle.PanelBg;
            b.style.paddingLeft = b.style.paddingRight = 34;
            b.style.paddingTop = b.style.paddingBottom = 12;
            HudStyle.Border(b, 3f, HudStyle.Frame);
            HudStyle.Radius(b, 12f);
            return b;
        }

        private static void Show(VisualElement e, bool on)
        {
            var d = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display != d) e.style.display = d;
        }

        /// <summary>Textos fijos (al construir y al cambiar de idioma).</summary>
        private void Relabel()
        {
            if (!_built) return;
            _next.text = Loc.Get("survival.next_wave");
            _retry.text = Loc.Get("survival.retry");
            _menu.text = Loc.Get("survival.main_menu");
            _mastery.text = _overMastery.text = Loc.Get("survival.weapon_upgrades");
            _shownWave = -1; _shownLeft = -1; _shownHint = null; _shownPhase = (SurvivalManager.Phase)(-1);
        }

        private void Update()
        {
            if (_manager == null) return;
            if (!_built) { Build(); if (!_built) return; }

            var phase = _manager.CurrentPhase;
            bool over = phase == SurvivalManager.Phase.GameOver || phase == SurvivalManager.Phase.Victory;

            // Arriba.
            if (_manager.Wave != _shownWave && _manager.Stage != null)
            {
                _shownWave = _manager.Wave;
                _wave.text = Loc.Get("survival.wave_stage", _manager.Wave, _manager.Stage.DisplayName, DifficultyName(_manager.Difficulty));
                _cleared.text = Loc.Get("survival.wave_cleared", _manager.Wave);
            }
            bool fighting = phase == SurvivalManager.Phase.Fighting;
            // Un número que cambia cuando cambia el texto: así sólo se reescribe la etiqueta si hace falta.
            bool byTime = _manager.IsDynamicWave && _manager.Config.dynamicEnd == SurvivalWaveEnd.Time && _manager.TimeLeft > 0f;
            bool byKills = _manager.IsDynamicWave && _manager.KillTarget > 0;
            int key = !fighting ? -2 : _manager.IsBossWave ? -3 : byTime ? 100000 + Mathf.CeilToInt(_manager.TimeLeft)
                    : byKills ? 200000 + _manager.Kills : _manager.EnemiesLeft;
            if (key != _shownLeft)
            {
                _shownLeft = key;
                int secs = Mathf.CeilToInt(_manager.TimeLeft);
                _enemies.text = !fighting ? ""
                              : _manager.IsBossWave ? Loc.Get("survival.boss_wave")
                              : byTime ? Loc.Get("survival.time_left", $"{secs / 60}:{secs % 60:00}")
                              : byKills ? Loc.Get("survival.kills", _manager.Kills, _manager.KillTarget)
                              : Loc.Get("survival.enemies_left", _manager.EnemiesLeft);
            }
            // Entre rondas el hueco de arriba es para la ficha de la tienda.
            Show(_top, !over && _manager.Wave > 0 && phase != SurvivalManager.Phase.BetweenWaves);

            // Entre rondas, y la preparación antes de la 1 (mismo bloque: "Empezar" + mejoras de armas).
            bool preparing = phase == SurvivalManager.Phase.Preparing;
            bool between = phase == SurvivalManager.Phase.BetweenWaves || preparing;
            Show(_between, between);
            if (phase != _shownPhase)
            {
                _shownPhase = phase;
                Show(_mastery, preparing);
                _next.text = Loc.Get(preparing ? "survival.start" : "survival.next_wave");
                if (preparing) _cleared.text = Loc.Get("survival.prepare", DifficultyName(_manager.Difficulty));
                else _shownWave = -1;
                if (preparing && InputDeviceManager.GamepadActive) _next.Focus();
            }
            if (between)
            {
                float auto = _manager.AutoNextRemaining;
                string hint = auto > 0f ? Loc.Get("survival.next_wave_auto", Mathf.CeilToInt(auto))
                            : InputDeviceManager.GamepadActive ? Loc.Get("survival.next_wave_pad") : Loc.Get("survival.next_wave_key");
                if (hint != _shownHint) { _shownHint = hint; _nextHint.text = hint; }

                bool pressed = (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                               || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
                if (pressed && GameStateManager.CanPlayerAct) _manager.RequestNextWave();
            }

            // Fin.
            if (over && !_overShown)
            {
                _overDelay -= Time.unscaledDeltaTime;
                if (_overDelay <= 0f)
                {
                    _overShown = true;
                    _overTitle.text = phase == SurvivalManager.Phase.Victory
                        ? Loc.Get("survival.victory", _manager.Wave - 1)
                        : Loc.Get("survival.game_over", _manager.Wave);
                    Show(_over, true);
                    if (InputDeviceManager.GamepadActive) _retry.Focus();
                }
            }
        }
    }
}
