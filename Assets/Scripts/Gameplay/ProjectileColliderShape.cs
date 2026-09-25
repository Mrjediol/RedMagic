using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>Forma del collider de un proyectil cuando el arma la define (override).</summary>
    public enum ProjectileColliderType
    {
        Circle,
        Box,
        Capsule,
    }

    /// <summary>
    /// Traduce una forma de collider dada en <b>unidades del mundo</b> (lo que escribe el diseñador
    /// en el arma) a los valores locales de un <see cref="Collider2D"/> cuyo transform está escalado
    /// (el proyectil se redimensiona a <c>size</c>), y la dibuja como gizmo. Una sola fuente para el
    /// runtime (<c>ShotProjectile</c>) y la vista previa (<c>WeaponUser</c>): lo que se ve en la
    /// escena es exactamente lo que colisiona.
    /// </summary>
    public static class ProjectileColliderShape
    {
        /// <summary>
        /// Deja <paramref name="collider"/> midiendo <paramref name="worldSize"/> (Circle: X = radio;
        /// Box/Capsule: ancho × alto) con su centro en <paramref name="worldOffset"/>, ambos en el
        /// marco del proyectil (X = hacia su punta) y en unidades del mundo.
        /// </summary>
        public static void Configure(Collider2D collider, ProjectileColliderType type, Vector2 worldSize,
                                     Vector2 worldOffset, Vector3 lossyScale)
        {
            float sx = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y));
            var offset = new Vector2(worldOffset.x / sx, worldOffset.y / sy);

            switch (collider)
            {
                case CircleCollider2D circle when type == ProjectileColliderType.Circle:
                    // Un CircleCollider2D escala su radio por la mayor de las dos escalas.
                    circle.radius = Mathf.Max(0.001f, worldSize.x) / Mathf.Max(sx, sy);
                    circle.offset = offset;
                    break;

                case BoxCollider2D box when type == ProjectileColliderType.Box:
                    box.size = new Vector2(Mathf.Max(0.001f, worldSize.x) / sx, Mathf.Max(0.001f, worldSize.y) / sy);
                    box.offset = offset;
                    break;

                case CapsuleCollider2D capsule when type == ProjectileColliderType.Capsule:
                    capsule.size = new Vector2(Mathf.Max(0.001f, worldSize.x) / sx, Mathf.Max(0.001f, worldSize.y) / sy);
                    capsule.direction = worldSize.x >= worldSize.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                    capsule.offset = offset;
                    break;
            }
        }

        /// <summary>
        /// Dibuja la forma (unidades del mundo) con los Gizmos actuales. El <c>Gizmos.matrix</c> ya
        /// debe estar en el marco del proyectil (posición + rotación, sin escala).
        /// </summary>
        public static void DrawGizmo(ProjectileColliderType type, Vector2 worldSize, Vector2 worldOffset)
        {
            Vector3 c = worldOffset;
            float w = Mathf.Max(0.001f, worldSize.x), h = Mathf.Max(0.001f, worldSize.y);

            switch (type)
            {
                case ProjectileColliderType.Circle:
                    DrawCircle(c, w);
                    break;

                case ProjectileColliderType.Box:
                    Gizmos.DrawWireCube(c, new Vector3(w, h, 0f));
                    break;

                case ProjectileColliderType.Capsule:
                    DrawCapsule(c, w, h);
                    break;
            }
        }

        public static void DrawCircle(Vector3 center, float radius, int segments = 32)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static void DrawCapsule(Vector3 center, float width, float height)
        {
            bool horizontal = width >= height;
            float radius = (horizontal ? height : width) * 0.5f;
            float half = Mathf.Max(0f, (horizontal ? width : height) * 0.5f - radius);

            Vector3 axis = horizontal ? Vector3.right : Vector3.up;
            Vector3 side = horizontal ? Vector3.up : Vector3.right;
            Vector3 a = center - axis * half, b = center + axis * half;

            Gizmos.DrawLine(a + side * radius, b + side * radius);
            Gizmos.DrawLine(a - side * radius, b - side * radius);
            DrawCircle(a, radius);
            DrawCircle(b, radius);
        }
    }
}
