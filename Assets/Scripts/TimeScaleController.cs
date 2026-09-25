using UnityEngine;
using UnityEngine.InputSystem;

public class TimeScaleController : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float step = 0.1f;
    [SerializeField] private float minTimeScale = 0.05f;
    [SerializeField] private float maxTimeScale = 3f;

    private void Update()
    {
        // +
        if (Keyboard.current.jKey.wasPressedThisFrame ||
            Keyboard.current.numpadPlusKey.wasPressedThisFrame)
        {
            ChangeTimeScale(step);
        }

        // -
        if (Keyboard.current.minusKey.wasPressedThisFrame ||
            Keyboard.current.numpadMinusKey.wasPressedThisFrame)
        {
            ChangeTimeScale(-step);
        }

        // 0 = pausa
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            Time.timeScale = 0f;
            Debug.Log("TimeScale: 0.00");
        }

        // 1 = velocidad normal
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            Debug.Log("TimeScale: 1.00");
        }
    }

    private void ChangeTimeScale(float amount)
    {
        Time.timeScale = Mathf.Clamp(
            Time.timeScale + amount,
            minTimeScale,
            maxTimeScale
        );

        Debug.Log("TimeScale: " + Time.timeScale.ToString("0.00"));
    }
}