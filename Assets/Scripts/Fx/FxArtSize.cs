using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Marca un prefab de arte cuyo tamaño lo manda <b>quien lo saca</b> (el "Size" de un
    /// ProjectileSpec, el "heldSize" de un ataque), no la escala del prefab. Escala en uniforme
    /// para que el dibujo mida el ancho pedido; el collider, al colgar de la misma raíz, escala con él.
    ///
    /// Lo añade el importador de jefes a sus proyectiles y objetos. Un prefab sin este componente
    /// conserva su propia escala (el arte hecho a mano de antes no cambia).
    /// </summary>
    [DisallowMultipleComponent]
    public class FxArtSize : MonoBehaviour
    {
        private Vector3 _baseScale;
        private float _naturalWidth;
        private bool _cached;

        private void Awake() => Cache();

        private void Cache()
        {
            if (_cached) return;
            _cached = true;
            _baseScale = transform.localScale;

            var r = GetComponentInChildren<SpriteRenderer>(true);
            if (r == null || r.sprite == null) return;

            float local = r.transform == transform ? 1f : Mathf.Abs(r.transform.localScale.x);
            _naturalWidth = r.sprite.bounds.size.x * local * Mathf.Abs(_baseScale.x);
        }

        /// <summary>Deja el dibujo con <paramref name="width"/> unidades de ancho. ≤0 = tamaño del prefab.</summary>
        public void FitWidth(float width)
        {
            Cache();
            if (width <= 0f || _naturalWidth <= 0.0001f)
            {
                transform.localScale = _baseScale;
                return;
            }

            float k = width / _naturalWidth;
            transform.localScale = new Vector3(_baseScale.x * k, _baseScale.y * k, _baseScale.z);
        }
    }
}
