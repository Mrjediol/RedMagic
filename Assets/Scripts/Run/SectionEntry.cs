using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Marca dónde aparece el jugador al entrar en una sección. Va en cada escena de sección y de
    /// jefe: <see cref="RunManager"/> la busca dentro de la escena recién cargada y coloca ahí al
    /// jugador.
    ///
    /// Se busca por componente y no por nombre de GameObject a propósito: el nombre del objeto se
    /// puede cambiar sin querer al montar el nivel y nada avisaría.
    /// </summary>
    [DisallowMultipleComponent]
    public class SectionEntry : MonoBehaviour
    {
        [Tooltip("Hacia dónde mira el jugador al aparecer. 1 = derecha, -1 = izquierda.")]
        [SerializeField] private int facing = 1;

        /// <summary>Posición de aparición.</summary>
        public Vector3 SpawnPosition => transform.position;

        /// <summary>1 (derecha) o -1 (izquierda).</summary>
        public int Facing => facing < 0 ? -1 : 1;

        /// <summary>
        /// Busca el punto de entrada dentro de una escena concreta. Devuelve null si la sección se
        /// montó sin él, para que RunManager pueda avisar en vez de dejar al jugador en el origen.
        /// </summary>
        public static SectionEntry FindIn(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                var entry = root.GetComponentInChildren<SectionEntry>(true);
                if (entry != null) return entry;
            }

            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(Facing * 0.9f, 0f, 0f));
        }
    }
}
