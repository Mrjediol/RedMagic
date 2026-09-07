using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace RedMagic.Core
{
    /// <summary>
    /// Enganche opcional para que un componente pooled resetee estado transitorio justo antes de
    /// volver al pool (parar velocidad, limpiar buffers…). Los datos por instancia (posición,
    /// tinte, daño) se reaplican al sacarlo, en su propio <c>Init</c>/<c>Configure</c>.
    /// </summary>
    public interface IPooled
    {
        void OnReturnedToPool();
    }

    /// <summary>
    /// Envoltorio fino sobre <see cref="ObjectPool{T}"/> de Unity para MonoBehaviours que construyen
    /// su propio GameObject en código (proyectiles, haces, números de daño, flashes de impacto).
    ///
    /// Uso: <c>new Pool&lt;T&gt;(factory)</c> donde <c>factory</c> hace el <c>new GameObject</c> +
    /// <c>AddComponent</c> <b>una sola vez</b>; luego <see cref="Get"/> lo reactiva (creando uno más
    /// si el pool está vacío) y <see cref="Release"/> lo desactiva y lo devuelve.
    ///
    /// Instancias ociosas y en uso cuelgan del root persistente de <see cref="PoolRunner"/>, así que
    /// una descarga de escena no las destruye; <see cref="PoolRunner"/> recicla a la fuerza todo lo
    /// que siga activo en cada carga de escena, para que nada se cuele de una sección a la siguiente.
    ///
    /// <b>Domain Reload está desactivado</b>: quien guarde un <c>static Pool&lt;T&gt;</c> debe
    /// ponerlo a null en un <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>.
    /// </summary>
    public sealed class Pool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;
        private readonly HashSet<T> _active = new HashSet<T>();
        private readonly List<T> _releaseScratch = new List<T>();
        private readonly Func<T> _factory;

        public Pool(Func<T> factory, int prewarm = 8, int maxSize = 4096)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _pool = new ObjectPool<T>(Create, OnGet, OnRelease, OnDestroyInstance,
                                      collectionCheck: false,
                                      defaultCapacity: Mathf.Max(1, prewarm),
                                      maxSize: Mathf.Max(1, maxSize));

            PoolRunner.Register(ReleaseAllActive);
            if (prewarm > 0) Prewarm(prewarm);
        }

        public int CountActive => _active.Count;
        public int CountInactive => _pool.CountInactive;

        /// <summary>Saca una instancia (reactivada o recién creada si el pool estaba vacío).</summary>
        public T Get()
        {
            var instance = _pool.Get();
            _active.Add(instance);
            return instance;
        }

        /// <summary>Devuelve una instancia al pool (la desactiva). Idempotente y null-safe.</summary>
        public void Release(T instance)
        {
            if (instance == null || !_active.Remove(instance)) return;
            _pool.Release(instance);
        }

        private void ReleaseAllActive()
        {
            _releaseScratch.Clear();
            _releaseScratch.AddRange(_active);
            for (int i = 0; i < _releaseScratch.Count; i++) Release(_releaseScratch[i]);
        }

        private void Prewarm(int count)
        {
            var buffer = new T[count];
            for (int i = 0; i < count; i++) buffer[i] = _pool.Get();
            for (int i = 0; i < count; i++) _pool.Release(buffer[i]);
        }

        private T Create()
        {
            var instance = _factory();
            instance.transform.SetParent(PoolRunner.Root, false);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private static void OnGet(T instance) => instance.gameObject.SetActive(true);

        private static void OnRelease(T instance)
        {
            if (instance == null) return;
            if (instance is IPooled pooled) pooled.OnReturnedToPool();
            instance.transform.SetParent(PoolRunner.Root, false);
            instance.gameObject.SetActive(false);
        }

        private static void OnDestroyInstance(T instance)
        {
            if (instance != null) UnityEngine.Object.Destroy(instance.gameObject);
        }
    }

    /// <summary>
    /// Dueño persistente de todos los <see cref="Pool{T}"/> y <see cref="PrefabPool"/>: el transform
    /// bajo el que cuelgan las instancias pooled y el gancho de carga de escena que recicla lo que
    /// siga activo para que no sobreviva a la siguiente escena.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoolRunner : MonoBehaviour
    {
        private static PoolRunner _instance;
        private static readonly List<Action> _releasers = new List<Action>();

        public static Transform Root
        {
            get
            {
                EnsureExists();
                return _instance.transform;
            }
        }

        /// <summary>
        /// Una sola vez al arrancar el runtime. Domain Reload está desactivado, así que
        /// <see cref="_releasers"/> sobrevive entre sesiones de Play con delegados que apuntan a
        /// pools muertos: se limpia aquí, antes de cualquier spawn de gameplay (los pools se crean
        /// perezosamente, ya en juego).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnRuntimeStart()
        {
            _releasers.Clear();
            EnsureExists();
        }

        private static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject("[PoolRunner]");
            _instance = go.AddComponent<PoolRunner>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Registra el "reciclar todo lo activo" de un pool, que se llama en cada carga de escena.</summary>
        internal static void Register(Action releaseAllActive)
        {
            if (releaseAllActive != null && !_releasers.Contains(releaseAllActive))
                _releasers.Add(releaseAllActive);
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            for (int i = 0; i < _releasers.Count; i++) _releasers[i]?.Invoke();
        }
    }
}
