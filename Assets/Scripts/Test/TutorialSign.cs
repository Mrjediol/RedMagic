using RedMagic.Localization;
using TMPro;
using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// Cartel de texto del tutorial puesto en el nivel (texto de mundo, TextMesh Pro con la fuente Cinzel). El texto sale
    /// de los ficheros de idioma por <see cref="key"/> (nunca escrito a mano en la escena), así se traduce y cambia en vivo
    /// con el idioma. En el Inspector se escriben el español y el inglés y se guardan solos en <c>es.txt</c> / <c>en.txt</c>
    /// (<c>TutorialSignEditor</c>); duplicar el cartel y cambiarle el texto le da su propia clave.
    /// Se crea con <b>GameObject ▸ RedMagic ▸ Texto de tutorial</b>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshPro))]
    [DisallowMultipleComponent]
    public class TutorialSign : MonoBehaviour
    {
        [Tooltip("Clave del texto en Assets/Resources/Localization/<idioma>.txt. Se rellena sola al crear el cartel.")]
        [SerializeField] private string key;

        [Header("Aparecer al acercarse")]
        [Tooltip("Distancia al jugador (unidades) a la que el cartel se ve entero; más lejos se desvanece. 0 = siempre visible.")]
        [Min(0f)] [SerializeField] private float showDistance = 0f;
        [Tooltip("Unidades en las que se desvanece, a partir de Show Distance.")]
        [Min(0.1f)] [SerializeField] private float fadeWidth = 3f;

        private TextMeshPro _text;
        private Transform _player;
        private float _alpha = 1f;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Refresh();
            }
        }

        private void OnEnable()
        {
            _text = GetComponent<TextMeshPro>();
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.Changed -= Refresh;

        public void Refresh()
        {
            if (_text == null) _text = GetComponent<TextMeshPro>();
            if (_text == null || string.IsNullOrEmpty(key)) return;
            string value = Loc.Get(key);
            if (_text.text != value) _text.text = value;
        }

        private void Update()
        {
            if (!Application.isPlaying || showDistance <= 0f || _text == null) return;
            if (_player == null || !_player.gameObject.activeInHierarchy)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                _player = go != null ? go.transform : null;
            }
            float target = 1f;
            if (_player != null)
            {
                float d = Vector2.Distance(_player.position, transform.position);
                target = 1f - Mathf.Clamp01((d - showDistance) / fadeWidth);
            }
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * 3f);
            _text.alpha = _alpha;
        }
    }
}
