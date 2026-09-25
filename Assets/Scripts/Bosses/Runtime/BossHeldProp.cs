using RedMagic.Abilities;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Un objeto que el jefe <b>sujeta</b> un momento (la roca sobre la cabeza antes de lanzarla).
    /// Sigue a su portador con un desplazamiento y se va solo: al soltarlo (<see cref="Release"/>),
    /// si el portador desaparece, o al cumplir su vida máxima — un ataque cortado por un cambio de
    /// fase no puede dejar una roca flotando en el aire.
    ///
    /// Con un prefab "de verdad" va por <see cref="Core.PrefabPool"/>. Si el prefab es un
    /// <b>proyectil</b> (lo normal: se usa el mismo arte que la roca lanzada) no se saca del pool de
    /// proyectiles — su física volaría y dañaría estando en la mano —: se copia su sprite en un
    /// objeto de sólo dibujo. Sin prefab, un cuadrado de color. Uno por ataque, así que
    /// <c>new GameObject</c> vale, como el resto de efectos de segundos del proyecto.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHeldProp : MonoBehaviour
    {
        private Transform _holder;
        private SpriteRenderer _holderBody;
        private Vector2 _offset;
        private float _expiresAt;
        private bool _pooled;
        private bool _released;
        private SpriteRenderer _renderer;

        /// <summary>
        /// Saca el objeto sobre <paramref name="holder"/>. <paramref name="forwardOffset"/>: X hacia
        /// donde mira el portador, Y hacia arriba desde su pivote. <paramref name="size"/> se usa sin
        /// prefab, con un prefab placeholder o con uno de proyectil.
        /// </summary>
        public static BossHeldProp Spawn(GameObject prefab, Transform holder, Vector2 forwardOffset, Vector2 size,
                                         Color tint, Sprite fallbackSprite, float maxLifetime)
        {
            if (holder == null) return null;

            var holderBody = holder.GetComponentInChildren<SpriteRenderer>();
            Vector3 at = holder.position + (Vector3)Offset(forwardOffset, holderBody);
            GameObject go;
            bool pooled;

            if (prefab != null && prefab.GetComponentInChildren<Projectile>(true) == null)
            {
                go = Core.PrefabPool.Spawn(prefab, at, Quaternion.identity);
                if (go == null) return null;
                pooled = true;

                var style = go.GetComponent<Fx.FxPlaceholderStyle>();
                if (style != null) style.Apply(tint, size, holder.gameObject);
                else if (go.TryGetComponent(out Fx.FxArtSize fit)) fit.FitWidth(size.x);
            }
            else
            {
                // Proyectil como arte: sólo su dibujo, sin tinte (el arte trae sus colores).
                var art = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
                bool hasArt = art != null && art.sprite != null;

                go = AbilityFx.SpawnSprite("Boss Held Prop", hasArt ? art.sprite : fallbackSprite, at, size,
                                           hasArt ? Color.white : tint, 0f, holder.gameObject);
                pooled = false;
            }

            var prop = go.GetComponent<BossHeldProp>();
            if (prop == null) prop = go.AddComponent<BossHeldProp>();

            prop.enabled = true;
            prop._holder = holder;
            prop._holderBody = holderBody;
            prop._offset = forwardOffset;
            prop._pooled = pooled;
            prop._released = false;
            prop._expiresAt = Time.time + Mathf.Max(0.1f, maxLifetime);
            prop._renderer = go.GetComponentInChildren<SpriteRenderer>();
            return prop;
        }

        /// <summary>Posición actual en el mundo (desde donde sale el lanzamiento).</summary>
        public Vector2 Position => transform.position;

        /// <summary>Lo suelta: desaparece ya. Idempotente.</summary>
        public void Release()
        {
            if (this == null || _released) return;
            _released = true;
            _holder = null;
            enabled = false;

            if (_pooled) Core.PrefabPool.Despawn(gameObject);
            else Destroy(gameObject);
        }

        private void LateUpdate()
        {
            if (_released) return;

            if (_holder == null || !_holder.gameObject.activeInHierarchy || Time.time >= _expiresAt)
            {
                Release();
                return;
            }

            // El desplazamiento sigue al lado al que mira el portador: si el jugador cruza, la roca
            // pasa con el jefe al otro lado.
            transform.position = _holder.position + (Vector3)Offset(_offset, _holderBody);
            if (_renderer != null && _holderBody != null) _renderer.flipX = _holderBody.flipX;
        }

        private static Vector2 Offset(Vector2 forward, SpriteRenderer body)
        {
            int facing = body != null && body.flipX ? -1 : 1;
            return new Vector2(forward.x * facing, forward.y);
        }
    }
}
