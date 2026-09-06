using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RedMagic.Core
{
    public enum InputMode
    {
        Touch,
        Gamepad
    }

    /// <summary>
    /// Detecta si el jugador está usando la pantalla táctil o un mando y avisa de los cambios.
    /// Se auto-crea antes de cargar la primera escena (no hace falta ponerlo en ninguna escena).
    ///
    /// Cambia a <see cref="InputMode.Gamepad"/> cuando se conecta o se usa un mando, y vuelve a
    /// <see cref="InputMode.Touch"/> cuando se desconecta o el jugador toca la pantalla.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputDeviceManager : MonoBehaviour
    {
        public static InputDeviceManager Instance { get; private set; }

        public InputMode CurrentMode { get; private set; } = InputMode.Touch;

        /// <summary>Atajo estático para los scripts de gameplay: true si el modo actual es mando.</summary>
        public static bool GamepadActive => Instance != null && Instance.CurrentMode == InputMode.Gamepad;

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
            SetMode(Gamepad.all.Count > 0 ? InputMode.Gamepad : InputMode.Touch);
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
                    if (Gamepad.all.Count == 0) SetMode(InputMode.Touch);
                    break;
            }
        }

        // Cambia de modo según el dispositivo que el jugador usa de verdad: si hay un mando
        // conectado pero toca la pantalla -> Touch, y si pulsa un botón del mando -> Gamepad.
        private void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == null) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            if (device is Gamepad)
            {
                if (CurrentMode != InputMode.Gamepad && eventPtr.HasButtonPress())
                    SetMode(InputMode.Gamepad);
            }
            else if (device is Touchscreen || device is Pointer)
            {
                if (CurrentMode != InputMode.Touch && eventPtr.HasButtonPress())
                    SetMode(InputMode.Touch);
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
