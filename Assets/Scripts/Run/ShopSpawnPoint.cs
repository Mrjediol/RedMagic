using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Marca dónde plantar la tienda si a esta sección le toca tenerla. Es opcional: sin marcador,
    /// <see cref="RunManager"/> la coloca un poco antes del <see cref="SectionExit"/>. Ponlo cuando
    /// el sitio automático caiga mal (encima de un hueco, dentro de una pared…).
    ///
    /// Sólo se lee el primero que se encuentre en la escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
        }
    }
}
