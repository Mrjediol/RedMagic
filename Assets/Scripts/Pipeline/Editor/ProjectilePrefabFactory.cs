using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Construye (o actualiza) el prefab de un proyectil pooled a partir de un sprite ya cortado.
    ///
    /// Es la pieza que le falta al pipeline para que "el enemigo tira algo" sea tan mecánico como
    /// el resto: <see cref="SheetSlicer"/> ya exporta el objeto suelto de un frame de ataque (la
    /// piedra que se ve separada de la mano) como su propio sprite; esto lo convierte en un prefab
    /// con <c>Rigidbody2D</c> + collider + <c>Projectile</c>, listo para <c>RangedAttack</c>.
    ///
    /// No hay <c>FxPlaceholderStyle</c> aquí a propósito: el sprite ya es arte real (sale del
    /// propio personaje, no de una forma geométrica), así que no necesita teñirse ni redimensionarse
    /// por disparo — es exactamente el caso "prefab, sin marcador → arte real" de las convenciones
    /// de FX del proyecto.
    /// </summary>
    public static class ProjectilePrefabFactory
    {
        /// <summary>
        /// Crea o actualiza <c>&lt;folder&gt;/Fx_&lt;name&gt;.prefab</c> con el sprite dado. El
        /// tamaño en mundo sale de los bounds del propio sprite (ya a escala real desde el corte),
        /// multiplicado por <paramref name="scale"/> para permitir ajuste fino sin recortar.
        /// </summary>
        public static GameObject BuildFromSprite(string folder, string name, Sprite sprite, float scale)
        {
            if (sprite == null) return null;

            SheetSlicer.EnsureFolder(folder);
            string path = $"{folder}/Fx_{name}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = existing != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(existing)
                : new GameObject($"Fx_{name}");

            var renderer = Get<SpriteRenderer>(root);
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 6;

            Vector2 size = (Vector2)sprite.bounds.size * Mathf.Max(0.01f, scale);
            root.transform.localScale = Vector3.one;

            var body = Get<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = Get<CircleCollider2D>(root);
            collider.isTrigger = true;
            collider.radius = Mathf.Max(size.x, size.y) * 0.5f;

            // Reescala el sprite (no el collider): el radio ya está en unidades de mundo, y sólo
            // el arte necesita ajustarse si `scale` != 1.
            renderer.transform.localScale = Vector3.one * scale;

            // destroyWhenDone se queda apagado (default de la clase): RangedAttack lo poolea vía
            // PrefabPool, nunca Instantiate/Destroy.
            Get<Projectile>(root);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static T Get<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
