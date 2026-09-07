using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace RedMagic.Core
{
    /// <summary>
    /// Marca una instancia como salida de <see cref="PrefabPool"/> y recuerda de qué prefab, para
    /// que <see cref="PrefabPool.Despawn"/> sepa a qué pool devolverla.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledInstance : MonoBehaviour
    {
        public GameObject SourcePrefab { get; internal set; }

        /// <summary>Devuelve esta instancia a su pool. No-op si no vino de un pool.</summary>
        public void Despawn() => PrefabPool.Despawn(gameObject);
    }

    /// <summary>
    /// Pool para objetos que <b>sí</b> tienen prefab (efectos one-shot, proyectiles de habilidad).
    /// Un pool por prefab, creado a demanda. Sustituye a <c>Instantiate</c>/<c>Destroy</c>:
    /// <see cref="Spawn"/> saca uno (creando si hace falta), <see cref="Despawn"/> lo desactiva y lo
    /// devuelve. Igual que <see cref="Pool{T}"/>: instancias bajo el root de <see cref="PoolRunner"/>
    /// y reciclado forzado en cada carga de escena.
    /// </summary>
    public static class PrefabPool
    {
        private static readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools =
            new Dictionary<GameObject, ObjectPool<GameObject>>();

        private static readonly HashSet<GameObject> _active = new HashSet<GameObject>();
        private static readonly List<GameObject> _releaseScratch = new List<GameObject>();
        private static bool _sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            _pools.Clear();
            _active.Clear();
            _sceneHookRegistered = false;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
                                       Transform parent = null)
        {
            if (prefab == null) return null;
            EnsureSceneHook();

            var pool = GetPool(prefab);
            var instance = pool.Get();

            var t = instance.transform;
            t.SetParent(parent != null ? parent : PoolRunner.Root, false);
            t.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            _active.Add(instance);
            return instance;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position) =>
            Spawn(prefab, position, Quaternion.identity);

        /// <summary>Devuelve una instancia a su pool. Null-safe; si no vino de un pool no hace nada.</summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null || !_active.Remove(instance)) return;

            var marker = instance.GetComponent<PooledInstance>();
            if (marker != null && marker.SourcePrefab != null &&
                _pools.TryGetValue(marker.SourcePrefab, out var pool))
            {
                pool.Release(instance);
            }
            else
            {
                Object.Destroy(instance);
            }
        }

        private static ObjectPool<GameObject> GetPool(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out var existing)) return existing;

            var pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = Object.Instantiate(prefab, PoolRunner.Root);
                    var marker = go.GetComponent<PooledInstance>();
                    if (marker == null) marker = go.AddComponent<PooledInstance>();
                    marker.SourcePrefab = prefab;
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: _ => { },
                actionOnRelease: go =>
                {
                    if (go == null) return;
                    go.transform.SetParent(PoolRunner.Root, false);
                    go.SetActive(false);
                },
                actionOnDestroy: go => { if (go != null) Object.Destroy(go); },
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: 2048);

            _pools[prefab] = pool;
            return pool;
        }

        private static void EnsureSceneHook()
        {
            if (_sceneHookRegistered) return;
            _sceneHookRegistered = true;
            PoolRunner.Register(ReleaseAllActive);
        }

        private static void ReleaseAllActive()
        {
            _releaseScratch.Clear();
            _releaseScratch.AddRange(_active);
            for (int i = 0; i < _releaseScratch.Count; i++) Despawn(_releaseScratch[i]);
        }
    }
}
