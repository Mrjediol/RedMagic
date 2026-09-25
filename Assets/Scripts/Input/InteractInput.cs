using UnityEngine.InputSystem;

namespace RedMagic.Core
{
    /// <summary>
    /// "¿Se ha pulsado Interactuar este frame?" — la acción Interact del asset de input (si quien
    /// pregunta la tiene), E / Intro, el botón norte del mando, o el botón táctil
    /// (<see cref="TouchInput.ConsumeInteract"/>). Único sitio con esta receta: la usan todos los
    /// interactuables (cofres, espejo, yunque, forja, tienda).
    /// </summary>
    public static class InteractInput
    {
        public static bool Pressed(InputAction action = null)
        {
            if (action != null && action.WasPressedThisFrame()) return true;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame) return true;

            return TouchInput.ConsumeInteract();
        }
    }
}
