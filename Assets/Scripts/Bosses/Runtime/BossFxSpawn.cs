using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Bifurcación compartida por los visuales pooled del jefe (onda, guadaña, hazard, plataforma,
    /// ancla): si el jefe trae un prefab placeholder para ese visual, se saca una instancia suya
    /// por <see cref="Core.PrefabPool"/>; si no, el llamante tira de su <see cref="Core.Pool{T}"/>
    /// de código como siempre.
    ///
    /// Está aquí para que cada tipo sólo tenga que decir de qué slot del <see cref="BossController"/>
    /// tira y cómo se recicla — la lógica "prefab o código" no se repite cinco veces.
    /// </summary>
    internal static class BossFxSpawn
    {
        /// <summary>
        /// Saca una instancia de <paramref name="prefab"/> del PrefabPool y devuelve su
        /// componente <typeparamref name="T"/>, o null si no hay prefab (el llamante usa su pool de
        /// código) o si el prefab está mal montado (se avisa y también se cae al de código).
        /// </summary>
        internal static T FromPrefab<T>(GameObject prefab) where T : Component
        {
            if (prefab == null) return null;

            var go = Core.PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            var comp = go != null ? go.GetComponent<T>() : null;
            if (comp != null) return comp;

            if (go != null) Core.PrefabPool.Despawn(go);
            Debug.LogWarning($"[Bosses] El prefab de FX '{prefab.name}' no lleva {typeof(T).Name}; " +
                             $"se usa el visual de código.");
            return null;
        }

        /// <summary>Devuelve la instancia a su sitio: PrefabPool si vino de prefab, pool de código si no.</summary>
        internal static void Release<T>(T instance, bool fromPrefab, Core.Pool<T> codePool) where T : Component
        {
            if (instance == null) return;
            if (fromPrefab) Core.PrefabPool.Despawn(instance.gameObject);
            else codePool?.Release(instance);
        }
    }
}
