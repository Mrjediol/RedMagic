using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Final de una sección. Cuando el jugador entra en el trigger, pide a
    /// <see cref="RunManager"/> que avance a la siguiente sección (o al jefe, o al mundo
    /// siguiente: eso lo decide el RunManager, aquí no se sabe ni hace falta).
    ///
    /// Se pone en cada escena de sección, en un collider marcado como <c>Is Trigger</c>.
    /// La escena del jefe normalmente no lleva SectionExit: el avance lo dispara la muerte del
    /// jefe llamando a <see cref="RunManager.AdvanceSection"/> desde su evento <c>Died</c>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class SectionExit : MonoBehaviour
    {
        [Tooltip("Etiqueta del objeto que puede activar la salida.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Si está activo, la salida sólo funciona una vez (evita disparos dobles si el " +
                 "jugador entra y sale del trigger durante la transición).")]
        [SerializeField] private bool oneShot = true;

        private bool _used;

        private void Reset()
        {
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.isTrigger = true;
        }

        private void OnEnable() => _used = false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_used && oneShot) return;
            if (!other.CompareTag(playerTag)) return;

            if (RunManager.Instance == null)
            {
                Debug.LogWarning("[SectionExit] No hay RunManager activo: la salida no hace nada. " +
                                 "¿Se está jugando esta sección suelta desde el editor?", this);
                return;
            }

            _used = true;
            RunManager.Instance.AdvanceSection();
        }

        private void OnDrawGizmos()
        {
            var collider = GetComponent<Collider2D>();
            if (collider == null) return;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            var bounds = collider.bounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
