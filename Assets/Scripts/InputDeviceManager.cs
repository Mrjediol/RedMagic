using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RedMagic.Core
{
    public enum InputMode
    {
        /// <summary>Pantalla táctil de verdad: es lo ÚNICO que enciende los controles en pantalla.</summary>
        Touch,

        Gamepad,

        /// <summary>Teclado y ratón. Es el modo normal en PC.</summary>
        KeyboardMouse
    }

    /// <summary>
    /// Detecta con qué está jugando el jugador — pantalla táctil, mando o teclado y ratón — y avisa
    /// de los cambios. Se auto-crea antes de cargar la primera escena (no hace falta ponerlo en
    /// ninguna escena).
    ///
    /// <b>El ratón NO es táctil.</b> En el Input System, <c>Mouse</c> hereda de <c>Pointer</c> igual
    /// que <c>Touchscreen</c>, así que meterlos en el mismo saco — que es lo que se hacía antes —
    /// dejaba a un PC en modo táctil en cuanto hacías el primer clic, y con él aparecían los
    /// botones de pantalla de móvil. Por eso <see cref="InputMode.Touch"/> lo enciende únicamente
    /// un <c>Touchscreen</c>, y sólo ese modo muestra los controles táctiles.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputDeviceManager : MonoBehaviour
    {
        public static InputDeviceManager Instance { get; private set; }

        public InputMode CurrentMode { get; private set; } = InputMode.KeyboardMouse;

        /// <summary>Atajo estático para los scripts de gameplay: true si el modo actual es mando.</summary>
        public static bool GamepadActive => Instance != null && Instance.CurrentMode == InputMode.Gamepad;

        /// <summary>
        /// True si hay que enseñar los controles en pantalla. Sin manager todavía se cae del lado
        /// de la plataforma, no del lado de "enséñalos": en PC no deben llegar a parpadear.
        /// </summary>
        public static bool TouchActive => Instance != null
            ? Instance.CurrentMode == InputMode.Touch
            : Application.isMobilePlatform;

        /// <summary>True si se está jugando con teclado y ratón (lo que enciende la chuleta de teclas).</summary>
        public static bool KeyboardMouseActive =>
            Instance != null && Instance.CurrentMode == InputMode.KeyboardMouse;

        /// <summary>Se dispara al cambiar de modo de entrada. El argumento es el nuevo modo.</summary>
        public event Action<InputMode> ModeChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[InputDeviceManager]");
            go.AddComponent<InputDeviceManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (Instance != this) return;

            InputSystem.onDeviceChange += OnDeviceChange;
            InputSystem.onEvent += OnEvent;
            SetMode(DetectInitialMode());
        }

        /// <summary>
        /// Con qué se empieza antes de que el jugador toque nada. Un móvil no tiene teclado; un PC
        /// con pantalla táctil sí, y ahí mandan teclado y ratón hasta que alguien toque la pantalla.
        /// </summary>
        private static InputMode DetectInitialMode()
        {
            if (Gamepad.all.Count > 0) return InputMode.Gamepad;
            if (Application.isMobilePlatform) return InputMode.Touch;
            if (Touchscreen.current != null && Keyboard.current == null) return InputMode.Touch;
            return InputMode.KeyboardMouse;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onEvent -= OnEvent;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                    SetMode(InputMode.Gamepad);
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    if (Gamepad.all.Count == 0) SetMode(DetectInitialMode());
                    break;
            }
        }

        // Cambia de modo según el dispositivo que el jugador usa de verdad: si hay un mando
        // conectado pero toca la pantalla -> Touch, si pulsa un botón del mando -> Gamepad, y si
        // usa teclado o ratón -> KeyboardMouse.
        //
        // El orden importa: Touchscreen se comprueba ANTES que Mouse porque los dos son Pointer,
        // y confundirlos es lo que metía a un PC en modo táctil al primer clic.
        private void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == null) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (!eventPtr.HasButtonPress()) return;

            if (device is Gamepad)
            {
                if (CurrentMode != InputMode.Gamepad) SetMode(InputMode.Gamepad);
            }
            else if (device is Touchscreen)
            {
                if (CurrentMode != InputMode.Touch) SetMode(InputMode.Touch);
            }
            else if (device is Keyboard || device is Mouse)
            {
                if (CurrentMode != InputMode.KeyboardMouse) SetMode(InputMode.KeyboardMouse);
            }
        }

        private void SetMode(InputMode mode)
        {
            if (mode == CurrentMode) return;

            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
