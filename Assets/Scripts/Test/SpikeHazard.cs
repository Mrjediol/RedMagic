using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// Zona de daño de un tramo de pinchos (la genera <see cref="SpikeTilemap"/>, no se pone a mano). Sólo le hace caso al
    /// jugador: los enemigos y los proyectiles no se enteran (bandos de <see cref="Teams"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class SpikeHazard : MonoBehaviour
    {
        private SpikeTilemap _owner;
        private Collider2D _zone;

        public void Init(SpikeTilemap owner) => _owner = owner;

        private void Awake() => _zone = GetComponent<Collider2D>();

        private void OnTriggerEnter2D(Collider2D other) => Touch(other);
        private void OnTriggerStay2D(Collider2D other) => Touch(other);

        private void Touch(Collider2D other)
        {
            if (_owner == null || other.isTrigger) return;
            var health = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Health>() : null;
            if (health == null) health = other.GetComponentInParent<Health>();
            if (health == null || Teams.Of(health.gameObject) != Team.Player) return;
            // el empujón sale desde el centro del tramo: en el suelo, hacia arriba y afuera
            _owner.Hurt(health, _zone != null ? (Vector2)_zone.bounds.center : (Vector2)transform.position);
        }
    }
}
