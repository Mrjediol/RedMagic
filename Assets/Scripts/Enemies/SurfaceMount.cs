using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Pega un enemigo estático (una torreta) a una superficie — suelo, plataforma, pared o techo —: lo gira para que su
    /// "abajo" apunte contra ella y lo mueve hasta que la parte más baja de su dibujo la toque. Mide con los vértices de los
    /// sprites (la forma de verdad, no la caja), y lo repite durante los primeros frames: al arrancar, la rig pone su pose y
    /// lo medido al crearlo ya no vale (por eso salía despegada).
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceMount : MonoBehaviour
    {
        private const int SettleFrames = 4;

        private Vector2 _point, _normal = Vector2.up;
        private int _framesLeft;

        /// <summary>Gira y apoya <paramref name="enemy"/> en el punto de la superficie con esa normal.</summary>
        public static SurfaceMount Attach(GameObject enemy, Vector2 surfacePoint, Vector2 normal)
        {
            var m = enemy.GetComponent<SurfaceMount>();
            if (m == null) m = enemy.AddComponent<SurfaceMount>();
            m._point = surfacePoint;
            m._normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector2.up;
            m._framesLeft = SettleFrames;
            m.enabled = true;
            m.Place();
            return m;
        }

        private void LateUpdate()
        {
            if (_framesLeft-- <= 0) { enabled = false; return; }
            Place();
        }

        private void Place()
        {
            var root = transform;
            float angle = Mathf.Atan2(_normal.y, _normal.x) * Mathf.Rad2Deg - 90f;   // arriba de la torreta = la normal
            root.rotation = Quaternion.Euler(0f, 0f, angle);
            root.position = new Vector3(_point.x, _point.y, root.position.z);

            // Lo que más asoma hacia la superficie (distancia con signo sobre la normal; negativo = metido dentro).
            float lowest = float.MaxValue;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.enabled || sr.sprite == null) continue;
                var t = sr.transform;
                foreach (var v in sr.sprite.vertices)
                    lowest = Mathf.Min(lowest, Vector2.Dot((Vector2)t.TransformPoint(v) - _point, _normal));
            }
            if (lowest == float.MaxValue) return;
            root.position -= (Vector3)(_normal * lowest);
        }
    }
}
