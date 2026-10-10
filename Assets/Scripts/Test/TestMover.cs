using UnityEngine;

namespace RedMagic.Test
{
    /// <summary>
    /// Para escenas de prueba: mueve el objeto alrededor de donde empieza, rápido y sin física (blancos que
    /// se mueven para probar el homing). Ida y vuelta horizontal o vertical, círculo u ocho.
    /// </summary>
    public class TestMover : MonoBehaviour
    {
        public enum Path { Horizontal, Vertical, Circle, Eight }

        [SerializeField] private Path path = Path.Horizontal;

        [Tooltip("Unidades desde el punto de partida (radio en círculo / ocho).")]
        [Min(0f)] [SerializeField] private float amplitude = 4f;

        [Tooltip("Vueltas (o idas y vueltas) por segundo.")]
        [Min(0f)] [SerializeField] private float frequency = 0.5f;

        [Tooltip("Desfase en vueltas, para que dos blancos iguales no vayan sincronizados.")]
        [SerializeField] private float phase;

        private Vector3 _origin;
        private bool _started;

        private void Start()
        {
            _origin = transform.position;
            _started = true;
        }

        private void Update()
        {
            if (!_started) return;
            float a = (Time.time * frequency + phase) * Mathf.PI * 2f;
            Vector3 o = path switch
            {
                Path.Vertical => new Vector3(0f, Mathf.Sin(a) * amplitude, 0f),
                Path.Circle => new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * amplitude,
                Path.Eight => new Vector3(Mathf.Sin(a), Mathf.Sin(a * 2f) * 0.5f, 0f) * amplitude,
                _ => new Vector3(Mathf.Sin(a) * amplitude, 0f, 0f),
            };
            transform.position = _origin + o;
        }
    }
}
