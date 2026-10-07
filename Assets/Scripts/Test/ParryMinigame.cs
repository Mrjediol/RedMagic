using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Gameplay;
using RedMagic.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.Test
{
    /// <summary>Dificultad del minijuego de parry: sólo cambia la ventana de parry.</summary>
    public enum ParryDifficulty
    {
        Easy,
        Medium,
        Hard,
    }

    /// <summary>
    /// Minijuego de parry (escena <c>Test/ParryTest</c>, también como build suelta para probar el parry):
    /// subir hasta la plataforma de arriba encadenando parrys en el aire a las balas de las torretas.
    ///
    /// <list type="bullet">
    /// <item>Abajo hay tres baldosas (fácil / medio / difícil); pisarlas cambia la ventana de parry
    /// del jugador y reinicia el marcador.</item>
    /// <item>Un <b>intento</b> empieza con el primer parry acertado en el aire y se pierde al volver a
    /// tocar el suelo de abajo.</item>
    /// <item>Al quedarse de pie arriba (<see cref="goalHeight"/>): enhorabuena, sonido de victoria,
    /// resumen (dificultad, intentos, % de parrys acertados) y vuelta abajo tras
    /// <see cref="returnDelay"/>.</item>
    /// </list>
    /// En este modo el jugador no puede morir (revive a tope): fallar es caerse, no acabar la partida.
    /// La UI es UI Toolkit en código, sobre el mismo PanelSettings que el HUD.
    /// </summary>
    public class ParryMinigame : MonoBehaviour
    {
        [Header("Ventana de parry por dificultad (segundos)")]
        [Min(0.01f)] [SerializeField] private float easyWindow = 0.15f;
        [Min(0.01f)] [SerializeField] private float mediumWindow = 0.10f;
        [Min(0.01f)] [SerializeField] private float hardWindow = 0.05f;
        [SerializeField] private ParryDifficulty startDifficulty = ParryDifficulty.Medium;

        [Header("Baldosas de dificultad (en el suelo de abajo)")]
        [Tooltip("Centro de la baldosa de Fácil; las otras dos van a su derecha.")]
        [SerializeField] private Vector2 firstPadPosition = new Vector2(4f, 0.05f);
        [Min(0.5f)] [SerializeField] private float padSpacing = 4f;
        [SerializeField] private Vector2 padSize = new Vector2(3f, 0.25f);
        [SerializeField] private Color easyColor = new Color(0.35f, 0.85f, 0.4f);
        [SerializeField] private Color mediumColor = new Color(0.95f, 0.8f, 0.25f);
        [SerializeField] private Color hardColor = new Color(0.95f, 0.3f, 0.3f);

        [Header("Recorrido")]
        [Tooltip("Dónde aparece el jugador al volver abajo.")]
        [SerializeField] private Vector2 spawnPoint = new Vector2(0f, 1.2f);
        [Tooltip("De pie por debajo de esta altura = está en el suelo de abajo.")]
        [SerializeField] private float floorHeight = 2f;
        [Tooltip("De pie por encima de esta altura = ha llegado arriba.")]
        [SerializeField] private float goalHeight = 19.5f;
        [Tooltip("Segundos que se ve el resumen antes de volver abajo.")]
        [Min(0f)] [SerializeField] private float returnDelay = 4f;

        private PlayerParry _parry;
        private PlayerMovement _movement;
        private Health _health;

        private ParryDifficulty _difficulty;
        private int _attempts;
        private int _parries;
        private int _parryHits;
        private bool _climbing;
        private bool _won;
        private float _returnTimer;

        private SpriteRenderer[] _pads;
        private UIDocument _document;
        private Label _difficultyLabel, _attemptsLabel, _parriesLabel, _hintLabel;
        private VisualElement _winPanel;
        private Label _winDifficulty, _winAttempts, _winParries;
        private Label[] _padLabels;

        // ------------------------------------------------------------------ ciclo de vida

        private void Start()
        {
            var player = GameObject.FindWithTag(Teams.PlayerTag);
            if (player == null || !player.TryGetComponent(out _parry))
            {
                Debug.LogWarning("[ParryMinigame] No hay jugador con PlayerParry en la escena.", this);
                enabled = false;
                return;
            }

            _movement = player.GetComponent<PlayerMovement>();
            _health = player.GetComponent<Health>();

            _parry.WindowOpened += OnWindowOpened;
            _parry.WindowClosed += OnWindowClosed;
            Combat.Parry.Succeeded += OnParrySucceeded;
            if (_health != null) _health.DeathGuard = KeepAlive;

            BuildPads();
            BuildUi();
            SelectDifficulty(startDifficulty);
        }

        private void OnDestroy()
        {
            if (_parry != null)
            {
                _parry.WindowOpened -= OnWindowOpened;
                _parry.WindowClosed -= OnWindowClosed;
            }
            Combat.Parry.Succeeded -= OnParrySucceeded;
            if (_health != null && _health.DeathGuard == KeepAlive) _health.DeathGuard = null;
        }

        private void Update()
        {
            if (_parry == null) return;

            PositionPadLabels();

            if (_won)
            {
                _returnTimer -= Time.unscaledDeltaTime;
                if (_returnTimer <= 0f) ReturnDown();
                return;
            }

            Vector2 position = _parry.transform.position;
            bool grounded = _movement != null && _movement.IsGrounded;
            if (!grounded) return;

            if (position.y <= floorHeight)
            {
                _climbing = false;   // de vuelta abajo: el intento (si lo había) se ha perdido
                for (int i = 0; i < 3; i++)
                    if (OnPad(position, i) && _difficulty != (ParryDifficulty)i) SelectDifficulty((ParryDifficulty)i);
            }
            else if (position.y >= goalHeight)
            {
                Win();
            }
        }

        // ------------------------------------------------------------------ reglas

        private bool KeepAlive(Health health)
        {
            health.ReviveAt(1f);
            return true;
        }

        private void OnWindowOpened()
        {
            if (_won) return;
            _parries++;
            RefreshHud();
        }

        private void OnWindowClosed(bool success)
        {
            if (_won || !success) return;
            _parryHits++;
            RefreshHud();
        }

        private void OnParrySucceeded(IncomingHit hit)
        {
            if (_won || _climbing || hit.Target != _health) return;
            if (_movement != null && _movement.IsGrounded) return;

            _climbing = true;   // primer parry en el aire: empieza un intento
            _attempts++;
            RefreshHud();
        }

        private void SelectDifficulty(ParryDifficulty difficulty)
        {
            _difficulty = difficulty;
            _parry.Window = WindowOf(difficulty);
            ResetScore();

            for (int i = 0; i < _pads.Length; i++)
            {
                var color = ColorOf((ParryDifficulty)i);
                if (i != (int)difficulty) color *= new Color(0.45f, 0.45f, 0.45f, 1f);   // las no elegidas, apagadas
                _pads[i].color = color;
            }

            SystemSounds.Play(s => s.uiClick);
        }

        private void ResetScore()
        {
            _attempts = 0;
            _parries = 0;
            _parryHits = 0;
            _climbing = false;
            RefreshHud();
        }

        private void Win()
        {
            _won = true;
            _returnTimer = returnDelay;

            _winDifficulty.text = Loc.Get("parrygame.difficulty", DifficultyName(_difficulty));
            _winAttempts.text = Loc.Get("parrygame.attempts", Mathf.Max(1, _attempts));
            _winParries.text = ParriesText();
            _winPanel.style.display = DisplayStyle.Flex;

            SystemSounds.Play(s => s.runComplete);
        }

        private void ReturnDown()
        {
            _won = false;
            _winPanel.style.display = DisplayStyle.None;

            var player = _parry.gameObject;
            player.transform.position = spawnPoint;
            var body = player.GetComponentInChildren<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            player.GetComponentInChildren<Knockback>()?.Cancel();
            if (_health != null) _health.Heal(_health.MaxHealth);

            ResetScore();
        }

        private bool OnPad(Vector2 position, int index)
        {
            Vector2 center = PadCenter(index);
            return Mathf.Abs(position.x - center.x) <= padSize.x * 0.5f;
        }

        private Vector2 PadCenter(int index) => firstPadPosition + new Vector2(padSpacing * index, 0f);

        private float WindowOf(ParryDifficulty difficulty) => difficulty switch
        {
            ParryDifficulty.Easy => easyWindow,
            ParryDifficulty.Hard => hardWindow,
            _ => mediumWindow,
        };

        private Color ColorOf(ParryDifficulty difficulty) => difficulty switch
        {
            ParryDifficulty.Easy => easyColor,
            ParryDifficulty.Hard => hardColor,
            _ => mediumColor,
        };

        // Claves literales para que la auditoría de localización las vea.
        private static string DifficultyName(ParryDifficulty difficulty) => difficulty switch
        {
            ParryDifficulty.Easy => Loc.Get("parrygame.easy"),
            ParryDifficulty.Hard => Loc.Get("parrygame.hard"),
            _ => Loc.Get("parrygame.medium"),
        };

        private string ParriesText()
        {
            int percent = _parries > 0 ? Mathf.RoundToInt(100f * _parryHits / _parries) : 0;
            return Loc.Get("parrygame.parries", percent, _parryHits, _parries);
        }

        // ------------------------------------------------------------------ baldosas

        private void BuildPads()
        {
            _pads = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Pad_" + (ParryDifficulty)i);
                go.transform.SetParent(transform, false);
                go.transform.position = PadCenter(i);
                go.transform.localScale = Vector3.one;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = AbilityFx.DefaultSprite;
                renderer.sortingOrder = 2;
                var bounds = renderer.sprite.bounds.size;
                go.transform.localScale = new Vector3(padSize.x / bounds.x, padSize.y / bounds.y, 1f);
                _pads[i] = renderer;
            }
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = Resources.Load<PanelSettings>("CurrencyHudPanelSettings");
            _document.sortingOrder = 20;
            var root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;

            // Marcador arriba en el centro.
            var score = Box(new Color(0f, 0f, 0f, 0.55f));
            score.style.position = Position.Absolute;
            score.style.top = 24;
            score.style.left = Length.Percent(50);
            score.style.translate = new Translate(Length.Percent(-50), 0);
            score.style.alignItems = Align.Center;
            root.Add(score);

            score.Add(Text("parrygame.title", 34, new Color(1f, 0.35f, 0.35f)));
            _difficultyLabel = AddText(score, 28, Color.white);
            _attemptsLabel = AddText(score, 26, Color.white);
            _parriesLabel = AddText(score, 26, Color.white);

            // Instrucciones abajo en el centro.
            var hint = Box(new Color(0f, 0f, 0f, 0.45f));
            hint.style.position = Position.Absolute;
            hint.style.bottom = 24;
            hint.style.left = Length.Percent(50);
            hint.style.translate = new Translate(Length.Percent(-50), 0);
            hint.style.alignItems = Align.Center;
            hint.style.maxWidth = Length.Percent(80);
            _hintLabel = Text("parrygame.hint", 24, new Color(0.95f, 0.9f, 0.78f));
            _hintLabel.style.whiteSpace = WhiteSpace.Normal;
            _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.Add(_hintLabel);
            hint.Add(Text("parrygame.controls", 22, new Color(0.8f, 0.8f, 0.8f)));
            root.Add(hint);

            // Rótulos de las baldosas (siguen su posición en pantalla).
            _padLabels = new Label[3];
            for (int i = 0; i < 3; i++)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.fontSize = 26;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.color = ColorOf((ParryDifficulty)i);
                label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
                var difficulty = (ParryDifficulty)i;
                LocalizedUi.Bind(label, () => label.text = $"{DifficultyName(difficulty)}\n{WindowOf(difficulty):0.00} s");
                root.Add(label);
                _padLabels[i] = label;
            }

            // Resumen al llegar arriba.
            _winPanel = Box(new Color(0.08f, 0.02f, 0.03f, 0.85f));
            _winPanel.style.position = Position.Absolute;
            _winPanel.style.top = Length.Percent(50);
            _winPanel.style.left = Length.Percent(50);
            _winPanel.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            _winPanel.style.alignItems = Align.Center;
            _winPanel.style.paddingLeft = 48; _winPanel.style.paddingRight = 48;
            _winPanel.style.paddingTop = 32; _winPanel.style.paddingBottom = 32;
            _winPanel.style.borderTopWidth = _winPanel.style.borderBottomWidth =
                _winPanel.style.borderLeftWidth = _winPanel.style.borderRightWidth = 3;
            _winPanel.style.borderTopColor = _winPanel.style.borderBottomColor =
                _winPanel.style.borderLeftColor = _winPanel.style.borderRightColor = new Color(1f, 0.3f, 0.3f);
            _winPanel.Add(Text("parrygame.win_title", 56, new Color(1f, 0.85f, 0.3f)));
            _winDifficulty = AddText(_winPanel, 32, Color.white);
            _winAttempts = AddText(_winPanel, 32, Color.white);
            _winParries = AddText(_winPanel, 32, Color.white);
            _winPanel.Add(Text("parrygame.returning", 24, new Color(0.8f, 0.8f, 0.8f)));
            _winPanel.style.display = DisplayStyle.None;
            root.Add(_winPanel);

            LocalizedUi.Bind(root, RefreshHud);   // al cambiar de idioma
        }

        private void RefreshHud()
        {
            if (_difficultyLabel == null) return;
            _difficultyLabel.text = Loc.Get("parrygame.difficulty", DifficultyName(_difficulty)) + "  ·  " +
                                    Loc.Get("parrygame.window", WindowOf(_difficulty).ToString("0.00"));
            _attemptsLabel.text = Loc.Get("parrygame.attempts", _attempts);
            _parriesLabel.text = ParriesText();
        }

        private void PositionPadLabels()
        {
            var cam = Camera.main;
            if (cam == null || _padLabels == null || _document == null || _document.rootVisualElement.panel == null) return;

            for (int i = 0; i < _padLabels.Length; i++)
            {
                Vector3 world = PadCenter(i) + new Vector2(0f, padSize.y + 0.2f);
                Vector2 panel = RuntimePanelUtils.CameraTransformWorldToPanel(_document.rootVisualElement.panel, world, cam);
                _padLabels[i].style.left = panel.x;
                _padLabels[i].style.top = panel.y;
                bool visible = cam.WorldToViewportPoint(world).z > 0f && !_won;
                _padLabels[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static VisualElement Box(Color background)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.backgroundColor = background;
            box.style.paddingLeft = 24; box.style.paddingRight = 24;
            box.style.paddingTop = 12; box.style.paddingBottom = 12;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
                box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 12;
            return box;
        }

        private static Label Text(string key, float size, Color color)
        {
            var label = AddText(null, size, color);
            LocalizedUi.Bind(label, key);
            return label;
        }

        private static Label AddText(VisualElement parent, float size, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = color;
            label.style.marginTop = 2; label.style.marginBottom = 2;
            parent?.Add(label);
            return label;
        }
    }
}
