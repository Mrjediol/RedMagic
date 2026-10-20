using System;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Items;
using RedMagic.Localization;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Lo que se ve de <see cref="WeaponRunProgress"/>: la pantalla de elegir (pausa el juego) y los avisos cortos.
    /// <list type="bullet">
    /// <item><b>Prisma</b>: si hay dos armas sin evolucionar, primero cuál; luego una de las evoluciones ofrecidas.</item>
    /// <item><b>Núcleo</b>: si hay dos armas con mejoras disponibles, cuál; luego una de las mejoras ofrecidas.</item>
    /// </list>
    /// Se abre sola en cuanto hay algo pendiente y el jugador puede actuar (no a mitad de otro menú). Tarjetas con ratón /
    /// toque, teclas 1-3, o el mando (foco). Persistente, construida en código, en el panel del menú de mejoras (orden 30).
    /// </summary>
    [DisallowMultipleComponent]
    public class ArsenalProgressUi : MonoBehaviour
    {
        private const string PanelSettingsResourcePath = "UpgradeMenuPanelSettings";
        private const float ToastSeconds = 2.6f;

        private static ArsenalProgressUi _instance;

        private UIDocument _document;
        private VisualElement _overlay, _cards;
        private Label _title, _subtitle, _toast, _stored;
        private bool _built, _open;
        private float _toastLeft;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Action> _choices = new List<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ArsenalProgressUi]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ArsenalProgressUi>();
        }

        /// <summary>Aviso corto arriba (clave de idioma).</summary>
        public static void Toast(string key)
        {
            if (_instance == null || !_instance.EnsureBuilt()) return;
            _instance.ToastText(Loc.Get(key));
        }

        /// <summary>Aviso corto arriba con huecos {0}….</summary>
        public static void ToastFormat(string key, params object[] args)
        {
            if (_instance == null || !_instance.EnsureBuilt()) return;
            _instance.ToastText(Loc.Get(key, args));
        }

        private void Awake()
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
            _document.sortingOrder = 1;
        }

        // ------------------------------------------------------------------ construcción

        private bool EnsureBuilt()
        {
            if (_built) return true;
            var root = _document != null && _document.panelSettings != null ? _document.rootVisualElement : null;
            if (root == null) return false;
            MenuStyle.FillParent(root);
            root.pickingMode = PickingMode.Ignore;

            _toast = HudStyle.OutlinedLabel("progress-toast", 30f);
            _toast.style.position = Position.Absolute;
            _toast.style.top = Length.Percent(20); _toast.style.left = 0; _toast.style.right = 0;
            _toast.style.color = new Color(1f, 0.88f, 0.55f);
            _toast.style.display = DisplayStyle.None;
            root.Add(_toast);

            _stored = HudStyle.OutlinedLabel("progress-stored", 22f);
            _stored.style.position = Position.Absolute;
            _stored.style.bottom = 24; _stored.style.left = 0; _stored.style.right = 0;
            _stored.style.color = new Color(1f, 0.88f, 0.55f);
            _stored.style.display = DisplayStyle.None;
            root.Add(_stored);

            for (int i = 0; i < MaxArrows; i++)
            {
                var arrow = new EdgeArrow();
                arrow.style.display = DisplayStyle.None;
                root.Add(arrow);
                _arrows.Add(arrow);
            }

            _overlay = new VisualElement { name = "progress-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            MenuSkinDresser.DressPanel(panel, MenuSkinDresser.Skin?.panel);
            _overlay.Add(panel);
            _title = MenuStyle.Title("");
            _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(_title);
            _subtitle = MenuStyle.Hint("");
            panel.Add(_subtitle);
            _cards = new VisualElement { name = "progress-cards" };
            _cards.style.flexDirection = FlexDirection.Row;
            _cards.style.justifyContent = Justify.Center;
            _cards.style.marginTop = 12;
            panel.Add(_cards);
            var hint = MenuStyle.Hint("");
            LocalizedUi.Bind(hint, "progress.pick_hint");
            panel.Add(hint);

            _built = true;
            return true;
        }

        // ------------------------------------------------------------------ bucle

        private void Update()
        {
            if (!EnsureBuilt()) return;

            if (_toastLeft > 0f)
            {
                _toastLeft -= Time.unscaledDeltaTime;
                if (_toastLeft <= 0f) _toast.style.display = DisplayStyle.None;
            }

            UpdateArrows();

            int cores = WeaponRunProgress.PendingCores;
            bool showStored = !_open && cores > 0;
            _stored.style.display = showStored ? DisplayStyle.Flex : DisplayStyle.None;
            if (showStored) _stored.text = Loc.Get("progress.cores_waiting", cores);

            if (_open) { ReadKeys(); return; }
            if (!GameStateManager.CanPlayerAct) return;
            if (WeaponRunProgress.PendingEvolutions > 0) StartEvolution();
            else if (cores > 0) StartCore();
        }

        private void ReadKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Pick(0);
            else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
            else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
        }

        // ------------------------------------------------------------------ Prisma

        private void StartEvolution()
        {
            var weapons = WeaponRunProgress.Equipped().FindAll(WeaponRunProgress.CanEvolve);
            if (weapons.Count == 0)
            {
                // Todo evolucionado: el Prisma vale como un Núcleo.
                WeaponRunProgress.ConsumeEvolution();
                if (WeaponRunProgress.Equipped().Count > 0) WeaponRunProgress.GrantCore();
                return;
            }
            if (weapons.Count == 1) { ShowEvolutions(weapons[0], false); return; }
            ShowWeapons("progress.evolve_title", "progress.pick_weapon", weapons, w => ShowEvolutions(w, false));
        }

        /// <param name="fromCore">La paga un Núcleo (si no, un Prisma).</param>
        private void ShowEvolutions(WeaponDefinition weapon, bool fromCore)
        {
            var kit = ArsenalRuntime.For(weapon);
            var evos = new List<int>();
            for (int e = 1; e < kit.Tree.evolutions.Count; e++) evos.Add(e);
            Shuffle(evos);
            if (evos.Count > WeaponProgressionConfig.Instance.offerEvolutions) evos.RemoveRange(WeaponProgressionConfig.Instance.offerEvolutions, evos.Count - WeaponProgressionConfig.Instance.offerEvolutions);

            Open(fromCore ? "progress.core_title" : "progress.evolve_title", weapon.DisplayName);
            foreach (int e in evos)
            {
                int evo = e;
                AddCard(weapon.Icon, ArsenalTexts.EvolutionName(kit, evo), ArsenalTexts.EvolutionDesc(kit, evo), () =>
                {
                    if (fromCore) WeaponRunProgress.ConsumeCore();
                    else WeaponRunProgress.ConsumeEvolution();
                    WeaponRunProgress.ApplyEvolution(weapon, evo);
                    Close();
                    ToastText(Loc.Get("progress.evolved", weapon.DisplayName, ArsenalTexts.EvolutionName(kit, evo)));
                });
            }
        }

        // ------------------------------------------------------------------ Núcleo

        /// <summary>Un núcleo sube un nivel el arma: evolución si no la tiene, si no una mejora. Sin nada que subir, almas.</summary>
        private void StartCore()
        {
            var weapons = WeaponRunProgress.Equipped().FindAll(w => WeaponRunProgress.LevelsLeft(w) > 0);
            if (weapons.Count == 0)
            {
                WeaponRunProgress.ConsumeCore();
                CurrencyManager.Instance?.Add(Currency.SoulFragment, WeaponProgressionConfig.Instance.soulsInsteadOfCore);
                Toast("progress.core_to_souls");
                return;
            }
            if (weapons.Count == 1) { LevelUp(weapons[0]); return; }
            ShowWeapons("progress.core_title", "progress.pick_weapon", weapons, LevelUp);
        }

        private void LevelUp(WeaponDefinition weapon)
        {
            if (WeaponRunProgress.CanEvolve(weapon)) ShowEvolutions(weapon, true);
            else ShowUpgrades(weapon);
        }

        private void ShowUpgrades(WeaponDefinition weapon)
        {
            var kit = ArsenalRuntime.For(weapon);
            var ups = WeaponRunProgress.AvailableUpgrades(weapon);
            Shuffle(ups);
            int offer = WeaponProgressionConfig.Instance.offerUpgrades;
            if (ups.Count > offer) ups.RemoveRange(offer, ups.Count - offer);

            Open("progress.core_title", $"{weapon.DisplayName} · {ArsenalTexts.EvolutionName(kit, kit.evolution)}");
            foreach (int u in ups)
            {
                int up = u;
                AddCard(weapon.Icon, ArsenalTexts.UpgradeName(kit, kit.evolution, up), ArsenalTexts.UpgradeDesc(kit, kit.evolution, up), () =>
                {
                    WeaponRunProgress.ConsumeCore();
                    WeaponRunProgress.ApplyUpgrade(weapon, up);
                    Close();
                    ToastText(Loc.Get("progress.upgraded", ArsenalTexts.UpgradeName(kit, kit.evolution, up)));
                });
            }
        }

        // ------------------------------------------------------------------ comunes

        private void ShowWeapons(string titleKey, string subtitleKey, List<WeaponDefinition> weapons, Action<WeaponDefinition> next)
        {
            Open(titleKey, Loc.Get(subtitleKey));
            foreach (var w in weapons)
            {
                var weapon = w;
                var kit = ArsenalRuntime.For(weapon);
                string state = kit.evolution > 0 ? ArsenalTexts.EvolutionName(kit, kit.evolution) : Loc.Get("progress.base_form");
                AddCard(weapon.Icon, weapon.DisplayName, state, () => next(weapon));
            }
        }

        private void Open(string titleKey, string subtitle)
        {
            _title.text = Loc.Get(titleKey);
            _subtitle.text = subtitle;
            _cards.Clear();
            _buttons.Clear();
            _choices.Clear();
            if (!_open)
            {
                _open = true;
                _overlay.style.display = DisplayStyle.Flex;
                GameStateManager.Instance?.SetPaused(true);
                SystemSounds.Play(s => s.upgradeMenu.open);
            }
        }

        private void AddCard(Sprite icon, string title, string description, Action onPick)
        {
            int index = _buttons.Count;
            var button = new Button(() => Pick(index));
            MenuStyle.Card(button);
            MenuSkinDresser.DressPanel(button, MenuSkinDresser.Skin?.card);
            button.style.width = 300;
            button.style.height = StyleKeyword.Auto;
            button.style.minHeight = 260;
            if (icon != null)
            {
                var img = new Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                img.style.width = 72; img.style.height = 72; img.style.alignSelf = Align.Center; img.style.marginBottom = 6;
                button.Add(img);
            }
            button.Add(MenuStyle.CardTitle($"{index + 1}. {title}"));
            var desc = MenuStyle.CardDescription(description);
            desc.style.whiteSpace = WhiteSpace.Normal;
            button.Add(desc);
            _cards.Add(button);
            _buttons.Add(button);
            _choices.Add(onPick);
            if (index == 0 && InputDeviceManager.GamepadActive) button.Focus();
        }

        private void Pick(int index)
        {
            if (!_open || index < 0 || index >= _choices.Count) return;
            SystemSounds.Play(s => s.upgradeBought);
            _choices[index]();
        }

        /// <summary>Tras elegir la evolución o la mejora. (Elegir arma no cierra: <see cref="Open"/> repinta con las opciones.)</summary>
        private void Close()
        {
            _open = false;
            _overlay.style.display = DisplayStyle.None;
            GameStateManager.Instance?.SetPaused(false);
            SystemSounds.Play(s => s.upgradeMenu.close);
        }

        private void ToastText(string text)
        {
            _toast.text = text;
            _toast.style.display = DisplayStyle.Flex;
            _toastLeft = ToastSeconds;
        }

        // ------------------------------------------------------------------ flecha al portador fuera de pantalla

        private const int MaxArrows = 3;
        private const float ArrowMargin = 70f;
        private readonly List<EdgeArrow> _arrows = new List<EdgeArrow>();

        private void UpdateArrows()
        {
            var cam = Camera.main;
            var panel = _document.rootVisualElement.panel;
            var carriers = CoreCarrier.Active;
            for (int i = 0; i < _arrows.Count; i++)
            {
                var arrow = _arrows[i];
                bool show = false;
                if (cam != null && panel != null && i < carriers.Count && carriers[i] != null && !_open)
                {
                    Vector3 sp = cam.WorldToScreenPoint(carriers[i].Center);
                    bool off = sp.z < 0f || sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height;
                    if (off)
                    {
                        // Al borde, en la dirección del portador desde el centro de la pantalla.
                        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
                        Vector2 dir = ((Vector2)sp - center);
                        if (sp.z < 0f) dir = -dir;
                        if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                        float sx = (center.x - ArrowMargin) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
                        float sy = (center.y - ArrowMargin) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                        Vector2 edge = center + dir * Mathf.Min(sx, sy);
                        Vector2 p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(edge.x, Screen.height - edge.y));
                        arrow.Set(p, Mathf.Atan2(-dir.y, dir.x), carriers[i].DropsSouls ? SoulIcon() : WeaponProgressionConfig.Instance.coreSprite);
                        show = true;
                    }
                }
                arrow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static Sprite SoulIcon()
        {
            var visual = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config?.VisualFor(Currency.SoulFragment) : null;
            return visual != null ? visual.icon : WeaponProgressionConfig.Instance.coreSprite;
        }

        /// <summary>Icono de lo que suelta el portador con una flecha que apunta hacia él, pegado al borde de la pantalla.</summary>
        private sealed class EdgeArrow : VisualElement
        {
            private const float Size = 72f;
            private readonly Image _icon;
            private float _angle;   // radianes, en coordenadas de panel (y hacia abajo)

            public EdgeArrow()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.width = Size;
                style.height = Size;
                _icon = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                _icon.style.position = Position.Absolute;
                _icon.style.left = _icon.style.top = Size * 0.2f;
                _icon.style.width = _icon.style.height = Size * 0.6f;
                Add(_icon);
                generateVisualContent += Draw;
            }

            public void Set(Vector2 panelPos, float angle, Sprite icon)
            {
                style.left = panelPos.x - Size * 0.5f;
                style.top = panelPos.y - Size * 0.5f;
                if (_icon.sprite != icon) _icon.sprite = icon;
                if (!Mathf.Approximately(angle, _angle)) { _angle = angle; MarkDirtyRepaint(); }
                float s = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f);
                style.scale = new Scale(new Vector3(s, s, 1f));
            }

            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                Vector2 c = new Vector2(Size, Size) * 0.5f;
                Vector2 d = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));
                Vector2 n = new Vector2(-d.y, d.x);
                p.fillColor = new Color(0.08f, 0.05f, 0.06f, 0.85f);
                p.BeginPath(); p.Arc(c, Size * 0.36f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
                p.fillColor = new Color(1f, 0.88f, 0.45f, 1f);
                p.BeginPath();
                p.MoveTo(c + d * (Size * 0.5f));
                p.LineTo(c + d * (Size * 0.3f) + n * (Size * 0.14f));
                p.LineTo(c + d * (Size * 0.3f) - n * (Size * 0.14f));
                p.ClosePath();
                p.Fill();
                p.strokeColor = new Color(1f, 0.88f, 0.45f, 1f);
                p.lineWidth = 3f;
                p.BeginPath(); p.Arc(c, Size * 0.36f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Stroke();
            }
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
