#if UNITY_EDITOR
using RedMagic.Fx;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Vista previa en la escena del disparo de un arma, para afinar <c>Base Shot</c> sin entrar en
    /// Play (la dibuja <see cref="WeaponUser"/> al seleccionar el jugador o el asset del arma):
    /// <list type="bullet">
    /// <item><b>amarillo</b> — la boca: punto exacto de salida (<c>muzzleOffset</c>, X reflejada según
    /// hacia dónde mira) y la dirección de salida;</item>
    /// <item><b>cian</b> — el proyectil a su tamaño real en el mundo (<c>size</c> para el de código o un
    /// placeholder; el sprite del prefab tal cual para arte final), orientado como saldrá;</item>
    /// <item><b>verde</b> — su collider: el override del arma si está encendido, si no el del prefab
    /// (o el círculo del proyectil de código);</item>
    /// <item>armas de haz: el rectángulo del rayo (<c>beamLength × beamWidth</c>).</item>
    /// </list>
    /// Usa las mismas cuentas que el runtime (<see cref="ProjectileAim.Rotation"/>,
    /// <see cref="ProjectileColliderShape"/>), así que lo que se ve es lo que sale.
    /// </summary>
    public static class WeaponShotPreview
    {
        private static readonly Color MuzzleColor = new Color(1f, 0.9f, 0.2f, 1f);
        private static readonly Color SpriteColor = new Color(0.3f, 0.9f, 1f, 1f);
        private static readonly Color ColliderColor = new Color(0.3f, 1f, 0.4f, 1f);

        public static void Draw(WeaponDefinition weapon, Transform owner, int facing)
        {
            var shot = weapon.Shot;
            facing = facing < 0 ? -1 : 1;

            Vector3 origin = owner.position;
            Vector3 muzzle = origin + new Vector3(shot.muzzleOffset.x * facing, shot.muzzleOffset.y, 0f);
            Vector2 dir = new Vector2(facing, 0f);

            var oldMatrix = Gizmos.matrix;
            var oldColor = Gizmos.color;

            // Del jugador a la boca, y la cruz de la boca.
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(MuzzleColor.r, MuzzleColor.g, MuzzleColor.b, 0.35f);
            Gizmos.DrawLine(origin, muzzle);
            Gizmos.color = MuzzleColor;
            const float cross = 0.12f;
            Gizmos.DrawLine(muzzle - Vector3.right * cross, muzzle + Vector3.right * cross);
            Gizmos.DrawLine(muzzle - Vector3.up * cross, muzzle + Vector3.up * cross);
            ProjectileColliderShape.DrawCircle(muzzle, 0.04f, 12);
            Gizmos.DrawLine(muzzle, muzzle + (Vector3)dir * 0.6f);

            if (shot.delivery == ShotDelivery.Hitscan) DrawBeam(shot, muzzle, dir);
            else if (shot.delivery == ShotDelivery.Projectile) DrawProjectile(shot, muzzle, dir, facing);

            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;

            Handles.color = MuzzleColor;
            Handles.Label(muzzle + Vector3.up * 0.35f,
                          $"{weapon.DisplayName}\nsize {shot.size.x:0.##}×{shot.size.y:0.##} · boca ({shot.muzzleOffset.x:0.##}, {shot.muzzleOffset.y:0.##})" +
                          (shot.overrideCollider ? $"\ncollider {shot.colliderType} {shot.colliderSize.x:0.##}×{shot.colliderSize.y:0.##}" : ""));
        }

        private static void DrawBeam(BaseShot shot, Vector3 muzzle, Vector2 dir)
        {
            Gizmos.matrix = Matrix4x4.TRS(muzzle, Quaternion.FromToRotation(Vector3.right, dir), Vector3.one);
            Gizmos.color = SpriteColor;
            Gizmos.DrawWireCube(new Vector3(shot.beamLength * 0.5f, 0f, 0f), new Vector3(shot.beamLength, shot.beamWidth, 0f));
        }

        private static void DrawProjectile(BaseShot shot, Vector3 muzzle, Vector2 dir, int facing)
        {
            var prefab = shot.projectilePrefab;
            var prefabShot = prefab != null ? prefab.GetComponent<ShotProjectile>() : null;

            // Igual que ShotProjectile.ApplyFacing: gira si el arma lo fuerza o el prefab gira; si no,
            // sin rotación y reflejado en X al mirar a la izquierda.
            bool rotates = shot.faceDirection || prefab == null || (prefabShot != null && prefabShot.FacesTravelDirection);
            Quaternion rotation = rotates ? ProjectileAim.Rotation(dir, shot.facingAxis) : Quaternion.identity;
            Vector3 mirror = !rotates && facing < 0 ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            Gizmos.matrix = Matrix4x4.TRS(muzzle, rotation, mirror);

            // El de código y un placeholder (FxPlaceholderStyle) se redimensionan a 'size'; el arte
            // final se queda con su propia escala.
            bool resized = prefab == null || prefab.GetComponent<FxPlaceholderStyle>() != null;

            Vector2 spriteSize = shot.size;
            Vector3 spriteCenter = Vector3.zero;
            if (!resized && TrySpriteBounds(prefab, out var size, out var center))
            {
                spriteSize = size;
                spriteCenter = center;
            }

            Gizmos.color = new Color(SpriteColor.r, SpriteColor.g, SpriteColor.b, 0.18f);
            Gizmos.DrawCube(spriteCenter, new Vector3(spriteSize.x, spriteSize.y, 0f));
            Gizmos.color = SpriteColor;
            Gizmos.DrawWireCube(spriteCenter, new Vector3(spriteSize.x, spriteSize.y, 0f));

            Gizmos.color = ColliderColor;
            if (shot.overrideCollider)
            {
                ProjectileColliderShape.DrawGizmo(shot.colliderType, shot.colliderSize, shot.colliderOffset);
            }
            else if (prefab == null || resized)
            {
                // Círculo del proyectil de código (radio = media celda) o collider ajustado al sprite.
                var box = prefab != null ? prefab.GetComponent<BoxCollider2D>() : null;
                if (box != null) Gizmos.DrawWireCube(Vector3.zero, new Vector3(shot.size.x, shot.size.y, 0f));
                else ProjectileColliderShape.DrawCircle(Vector3.zero, Mathf.Max(shot.size.x, shot.size.y) * 0.5f);
            }
            else
            {
                DrawPrefabCollider(prefab);
            }
        }

        private static bool TrySpriteBounds(GameObject prefab, out Vector2 size, out Vector3 center)
        {
            size = Vector2.zero;
            center = Vector3.zero;

            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || renderer.sprite == null) return false;

            Vector3 scale = Abs(renderer.transform.lossyScale);
            var bounds = renderer.sprite.bounds;
            size = Vector3.Scale(bounds.size, scale);
            center = renderer.transform.position - prefab.transform.position + Vector3.Scale(bounds.center, scale);
            return true;
        }

        private static void DrawPrefabCollider(GameObject prefab)
        {
            var collider = prefab.GetComponentInChildren<Collider2D>(true);
            if (collider == null) return;

            Vector3 scale = Abs(collider.transform.lossyScale);
            Vector3 at = collider.transform.position - prefab.transform.position;

            switch (collider)
            {
                case CircleCollider2D circle:
                    ProjectileColliderShape.DrawCircle(at + Vector3.Scale(circle.offset, scale),
                                                       circle.radius * Mathf.Max(scale.x, scale.y));
                    break;
                case BoxCollider2D box:
                    Gizmos.DrawWireCube(at + Vector3.Scale(box.offset, scale), Vector3.Scale(box.size, scale));
                    break;
                case CapsuleCollider2D capsule:
                    ProjectileColliderShape.DrawGizmo(ProjectileColliderType.Capsule,
                                                      Vector3.Scale(capsule.size, scale),
                                                      at + Vector3.Scale(capsule.offset, scale));
                    break;
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
#endif
