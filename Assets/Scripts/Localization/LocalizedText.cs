using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RedMagic.Localization
{
    /// <summary>
    /// Pone el texto de <see cref="key"/> en el <see cref="Text"/> o <see cref="TMP_Text"/> del mismo
    /// GameObject al activarse y cada vez que cambia el idioma (<see cref="Loc.Changed"/>). Para uGUI y
    /// TextMeshPro; lo equivalente en UI Toolkit es <see cref="LocalizedUi"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Clave del fichero de idioma (Assets/Resources/Localization/<código>.txt).")]
        [SerializeField] private string key;

        private Text _text;
        private TMP_Text _tmp;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Refresh();
            }
        }

        private void Awake()
        {
            _text = GetComponent<Text>();
            _tmp = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.Changed -= Refresh;

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key)) return;
            var value = Loc.Get(key);
            if (_text != null) _text.text = value;
            if (_tmp != null) _tmp.text = value;
        }
    }
}
