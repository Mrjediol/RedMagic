using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Pivote que gira sus hijos alrededor de un objetivo durante unos segundos. Es el soporte de
    /// las habilidades de orbes: los orbes son <see cref="DamageZone"/> colgando de este pivote,
    /// así que girar y hacer daño son dos cosas separadas y cada una se puede tocar por su lado.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbitSpinner : MonoBehaviour
    {
        private Transform _follow;
        private Vector3 _offset;
        private float _degreesPerSecond = 180f;
        private float _lifeTimer = 5f;

        public void Configure(Transform follow, float degreesPerSecond, float duration, Vector3 offset = default)
        {
            _follow = follow;
            _degreesPerSecond = degreesPerSecond;
            _lifeTimer = Mathf.Max(0.1f, duration);
            _offset = offset;
        }

        private void Update()
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Se sigue al objetivo por posición en vez de emparentarlo: así el pivote gira a su
            // ritmo aunque el jugador escale o gire su sprite al cambiar de lado.
            if (_follow != null) transform.position = _follow.position + _offset;

            transform.Rotate(0f, 0f, _degreesPerSecond * Time.deltaTime);
        }
    }
}
