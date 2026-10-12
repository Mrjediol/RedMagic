using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// Sprite hijo de una capa de 'extremos del nivel' (<see cref="ParallaxBackgroundLayer"/>, p. ej. la puerta): se
    /// mueve con ella y se coloca a mano dentro (herramienta Mover). En el otro extremo del nivel dibuja una copia en
    /// espejo en el sitio que le corresponde, y se pone justo delante de la capa en el orden de dibujo.
    /// Así son los escalones de la puerta (uno por escalón, para colocarlos por separado).
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(110)] // después de ParallaxBackgroundLayer (100), que calcula los extremos
    [RequireComponent(typeof(SpriteRenderer))]
    public class LevelEndsAttachment : MonoBehaviour
    {
        [Tooltip("Orden de dibujo respecto a la capa padre: 1 = justo delante de ella.")]
        [SerializeField] private int orderOffset = 1;

        private SpriteRenderer _sprite, _mirror;
        private ParallaxBackgroundLayer _layer;

        private void OnEnable()
        {
            _sprite = GetComponent<SpriteRenderer>();
            var t = transform.Find("Espejo");
            if (t == null)
            {
                var go = new GameObject("Espejo") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                t = go.transform;
                go.AddComponent<SpriteRenderer>();
            }
            _mirror = t.GetComponent<SpriteRenderer>();
        }

        private void OnDisable()
        {
            if (_mirror != null) _mirror.enabled = false;
        }

        private void LateUpdate()
        {
            if (_layer == null) _layer = transform.parent != null ? transform.parent.GetComponentInParent<ParallaxBackgroundLayer>() : null;
            if (_layer == null || !_layer.HasEnds || _sprite == null || _mirror == null)
            {
                if (_mirror != null) _mirror.enabled = false;
                return;
            }
            _sprite.sortingLayerName = _layer.SortingLayerName;
            _sprite.sortingOrder = _layer.SortingOrder + orderOffset;

            // mismo desplazamiento desde la esquina de la otra copia, con x reflejada
            Vector3 p = transform.position;
            Vector2 d = (Vector2)p - _layer.LeftEnd;
            _mirror.enabled = _sprite.enabled;
            _mirror.transform.position = new Vector3(_layer.RightEnd.x - d.x, _layer.RightEnd.y + d.y, p.z);
            _mirror.sprite = _sprite.sprite;
            _mirror.color = _sprite.color;
            _mirror.sharedMaterial = _sprite.sharedMaterial;
            _mirror.flipX = !_sprite.flipX;
            _mirror.flipY = _sprite.flipY;
            _mirror.sortingLayerName = _sprite.sortingLayerName;
            _mirror.sortingOrder = _sprite.sortingOrder;
        }
    }
}
