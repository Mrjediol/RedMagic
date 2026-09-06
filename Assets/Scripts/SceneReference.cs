using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Core
{
    /// <summary>
    /// Referencia a una escena que sobrevive a renombrados y movimientos de archivo.
    ///
    /// El problema que resuelve: guardar el destino como <c>string</c> ("MainHub") acopla el código
    /// al nombre exacto del archivo. Renombrar la escena en el editor no actualiza ese string, no
    /// falla la compilación, no se pone nada en rosa en el Inspector — el fallo sólo aparece en
    /// runtime como una línea de log que el jugador nunca ve. Aquí el enlace real es el
    /// <see cref="UnityEditor.SceneAsset"/>, que Unity referencia por GUID: renombrar o mover la
    /// escena mantiene la referencia y el path/nombre horneados se regeneran solos.
    ///
    /// Uso: declara <c>[SerializeField] private SceneReference miEscena;</c> y arrastra la escena
    /// al Inspector. En build se usan los valores horneados; en el editor se resuelve directamente
    /// desde el asset, así que un renombrado hecho con esta escena cerrada tampoco rompe el Play.
    /// </summary>
    [System.Serializable]
    public class SceneReference : ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        [Tooltip("Arrastra aquí el asset de la escena. La referencia es por GUID, así que aguanta " +
                 "renombrados y cambios de carpeta.")]
        [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

        // Horneados desde sceneAsset al serializar. Son los únicos valores que existen en un build,
        // donde UnityEditor.SceneAsset no está disponible.
        [SerializeField, HideInInspector] private string scenePath;
        [SerializeField, HideInInspector] private string sceneName;

        /// <summary>Ruta completa del asset ("Assets/Scenes/MainHub.unity"). Vacía si no hay escena asignada.</summary>
        public string Path
        {
            get
            {
#if UNITY_EDITOR
                // En el editor la verdad está en el propio asset: si alguien renombró la escena
                // mientras esta otra estaba cerrada, el valor horneado está desfasado pero el
                // enlace por GUID no. Resolver aquí hace que Play funcione igualmente.
                if (sceneAsset != null)
                    return UnityEditor.AssetDatabase.GetAssetPath(sceneAsset);
#endif
                return scenePath;
            }
        }

        /// <summary>Nombre sin extensión ("MainHub"). Útil para logs y para <see cref="SceneManager.GetSceneByName"/>.</summary>
        public string Name
        {
#if UNITY_EDITOR
            get => sceneAsset != null ? sceneAsset.name : sceneName;
#else
            get => sceneName;
#endif
        }

        /// <summary>True si hay una escena asignada (no dice nada de si está en Build Settings).</summary>
        public bool IsAssigned => !string.IsNullOrEmpty(Path);

        /// <summary>
        /// True si la escena está asignada y además incluida y habilitada en Build Settings.
        /// Es la comprobación que evita el fallo silencioso de <see cref="SceneManager.LoadScene(string)"/>.
        /// </summary>
        public bool CanLoad
        {
            get
            {
                string path = Path;
                return !string.IsNullOrEmpty(path) && Application.CanStreamedLevelBeLoaded(path);
            }
        }

        public SceneReference() { }

        /// <summary>
        /// Valida la referencia y devuelve el path listo para cargar, o null si no se puede,
        /// dejando en consola un mensaje que dice exactamente qué falta.
        /// </summary>
        public string ResolveForLoad(Object context = null)
        {
            string path = Path;

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[SceneReference] No hay ninguna escena asignada en el Inspector.", context);
                return null;
            }

            if (!Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError($"[SceneReference] La escena '{path}' existe pero no está en Build Settings " +
                               "(o está deshabilitada). Añádela en File > Build Profiles > Scene List.", context);
                return null;
            }

            return path;
        }

        /// <summary>Carga la escena en modo Single. Devuelve false (y explica por qué) si no puede.</summary>
        public bool Load(Object context = null)
        {
            string path = ResolveForLoad(context);
            if (path == null) return false;

            SceneManager.LoadScene(path);
            return true;
        }

        /// <summary>Carga la escena de forma asíncrona. Devuelve null (y explica por qué) si no puede.</summary>
        public AsyncOperation LoadAsync(LoadSceneMode mode, Object context = null)
        {
            string path = ResolveForLoad(context);
            return path == null ? null : SceneManager.LoadSceneAsync(path, mode);
        }

        /// <summary>La escena cargada correspondiente, o una Scene inválida si no lo está.</summary>
        public Scene GetLoadedScene()
        {
            string path = Path;
            return string.IsNullOrEmpty(path) ? default : SceneManager.GetSceneByPath(path);
        }

        public bool IsLoaded
        {
            get
            {
                var scene = GetLoadedScene();
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public override string ToString() => IsAssigned ? Name : "<sin asignar>";

        // ------------------------------------------------------------------ serialización

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            // OnBeforeSerialize corre en el hilo principal al guardar escena/prefab/asset, que es
            // donde AssetDatabase es válido. Aquí se hornea lo que necesitará el build.
            if (sceneAsset == null)
            {
                scenePath = string.Empty;
                sceneName = string.Empty;
                return;
            }

            scenePath = UnityEditor.AssetDatabase.GetAssetPath(sceneAsset);
            sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
#endif
        }

        public void OnAfterDeserialize()
        {
            // Puede correr fuera del hilo principal, así que no se toca ninguna API de Unity.
        }
    }
}
