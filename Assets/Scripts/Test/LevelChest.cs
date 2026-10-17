using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Hub;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Levels
{
    /// <summary>
    /// Cofre de oro del nivel. Al interactuar: anima la apertura (fotogramas de la hoja), da <see cref="gold"/> de oro y,
    /// con <see cref="itemChance"/>, suelta además un item al azar del catálogo; luego se desvanece subiendo un poco y se
    /// apaga. Avisa con <see cref="Opened"/> (la <see cref="Run.ParkourRoom"/> que lo contiene abre entonces sus puertas).
    /// Interacción como el resto de interactuables: trigger, cartel <c>prompt.chest_open</c>, E / Enter / botón norte /
    /// botón táctil, y nada mientras haya un menú abierto. El origen del objeto es el centro del cofre.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class LevelChest : MonoBehaviour, ISoundEventSource
    {
        [Header("Botín")]
        [Min(0)] [SerializeField] private int gold = 500;
        [Tooltip("Probabilidad (0-1) de soltar además un item al azar.")]
        [Range(0f, 1f)] [SerializeField] private float itemChance = 0.1f;
        [Tooltip("Dónde sale el item, respecto al cofre.")]
        [SerializeField] private Vector2 itemOffset = new Vector2(0f, 1.2f);

        [Header("Animación")]
        [Tooltip("Fotogramas de la apertura, de cerrado a abierto.")]
        [SerializeField] private Sprite[] frames;
        [Min(1f)] [SerializeField] private float framesPerSecond = 12f;
        [Tooltip("Segundos abierto, ya con el botín dado, antes de desvanecerse.")]
        [Min(0f)] [SerializeField] private float holdOpen = 0.8f;
        [Min(0.05f)] [SerializeField] private float fadeDuration = 0.6f;
        [Tooltip("Cuánto sube mientras se desvanece (unidades).")]
        [SerializeField] private float fadeRise = 0.35f;
        [Tooltip("Tamaño final al desvanecerse (1 = igual).")]
        [Range(0.5f, 1.5f)] [SerializeField] private float fadeScale = 1.1f;

        [Header("Input")]
        [Tooltip("Asset de acciones. Vacío = teclas de reserva (E / Enter / botón norte / botón táctil).")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";

        private const string PromptKey = "prompt.chest_open";
        private const string PlayerTag = "Player";

        /// <summary>Se ha abierto (y dado su botín). Lo escucha la sala de parkour para abrir su puerta.</summary>
        public event Action<LevelChest> Opened;
        public event Action<SoundTrigger> SoundTriggered;

        public bool IsOpen { get; private set; }

        public void DeclareSoundTriggers(List<SoundTrigger> into)
        {
            into.Add(SoundTrigger.OnInteract);
            into.Add(SoundTrigger.OnLoot);
        }

        private SpriteRenderer _sprite;
        private InputAction _interact;
        private bool _playerInRange;
        private bool _promptShown;

        private void Reset()
        {
            var c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        private void Awake()
        {
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (inputActions != null)
                _interact = inputActions.FindActionMap(actionMapName, throwIfNotFound: false)?.FindAction(actionName, throwIfNotFound: false);
            if (_sprite != null && frames != null && frames.Length > 0) _sprite.sprite = frames[0];
        }

        private void OnEnable() => _interact?.Enable();

        private void OnDisable()
        {
            _playerInRange = false;
            HidePrompt();
        }

        private void Update()
        {
            if (IsOpen || !_playerInRange) return;
            if (!GameStateManager.CanPlayerAct)
            {
                HidePrompt();
                return;
            }
            if (!_promptShown) { InteractionPromptUi.ShowKey(this, PromptKey); _promptShown = true; }
            TouchInput.RequestInteractButton();
            if (InteractInput.Pressed(_interact)) Open();
        }

        /// <summary>Abre el cofre. Público para poder lanzarlo desde otro sitio (un evento, una prueba).</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            HidePrompt();
            SoundTriggered?.Invoke(SoundTrigger.OnInteract);
            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            if (_sprite != null && frames != null)
            {
                var wait = new WaitForSeconds(1f / framesPerSecond);
                for (int i = 1; i < frames.Length; i++)
                {
                    yield return wait;
                    _sprite.sprite = frames[i];
                }
            }

            GiveLoot();
            Opened?.Invoke(this);

            yield return new WaitForSeconds(holdOpen);

            var start = transform.position;
            var startScale = transform.localScale;
            var color = _sprite != null ? _sprite.color : Color.white;
            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / fadeDuration);
                transform.position = start + Vector3.up * (fadeRise * k);
                transform.localScale = startScale * Mathf.Lerp(1f, fadeScale, k);
                if (_sprite != null) _sprite.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private void GiveLoot()
        {
            SoundTriggered?.Invoke(SoundTrigger.OnLoot);
            if (gold > 0) CurrencyManager.Instance?.Add(Currency.Gold, gold);
            if (UnityEngine.Random.value < itemChance)
            {
                var item = ItemPickup.RandomItem();
                if (item != null) ItemPickup.Spawn(item, transform.position + (Vector3)itemOffset);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(PlayerTag)) _playerInRange = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(PlayerTag)) return;
            _playerInRange = false;
            HidePrompt();
        }

        private void HidePrompt()
        {
            _promptShown = false;
            InteractionPromptUi.Hide(this);
        }
    }
}
