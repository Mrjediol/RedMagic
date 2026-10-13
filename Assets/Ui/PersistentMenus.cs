using UnityEngine;

namespace RedMagic.UI
{
    /// <summary>
    /// Raíz de los menús que tienen que estar en TODAS las escenas de juego: controles táctiles, pausa y opciones.
    /// Antes vivían dentro de MainHub e Ice1, así que al empezar una run (MainHub se descarga) o en cualquier nivel nuevo
    /// no había ni botón de pausa ni Esc/Start ni controles. Ahora es un prefab (<c>Resources/MenusUI</c>) que se crea
    /// solo antes de la primera escena y sobrevive a los cambios de escena, como el HUD.
    ///
    /// En el menú principal se apaga (tiene su propia pantalla de opciones y no hay nada que pausar): sus hijos se
    /// desactivan mientras <see cref="MainMenuController.IsOpen"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PersistentMenus : MonoBehaviour
    {
        private const string ResourcePath = "MenusUI";

        private static PersistentMenus _instance;
        private bool _shown = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[PersistentMenus] Falta Resources/" + ResourcePath + ".prefab: sin pausa ni controles táctiles.");
                return;
            }
            var go = Instantiate(prefab);
            go.name = "[MenusUI]";
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            bool show = !MainMenuController.IsOpen;
            if (show == _shown) return;
            _shown = show;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(show);
        }
    }
}
