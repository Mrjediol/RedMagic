using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RedMagic.Economy
{
    /// <summary>
    /// La fila "icono + precio" bajo un altar de la tienda (canvas de mundo, 0.01 u por píxel):
    /// crema si se puede pagar, roja si no, y <see cref="Deny"/> = sacudida + destello rojo. Es la
    /// ÚNICA implementación de ese "no llega": la usan los altares de items (<see cref="ShopAltar"/>)
    /// y el del reroll (<see cref="ShopRerollAltar"/>). Se construye una vez y se reutiliza.
    /// </summary>
    public sealed class ShopPriceTag
    {
        private static readonly Color PriceColor = new(1f, 0.9f, 0.55f);
        private static readonly Color TooDearColor = new(1f, 0.35f, 0.3f);

        private readonly MonoBehaviour _host;
        private readonly Canvas _canvas;
        private readonly CanvasGroup _group;
        private readonly Image _icon;
        private readonly Text _text;
        private readonly RectTransform _row;
        private readonly Vector2 _rowRest; // posición de reposo: la sacudida siempre vuelve aquí
        private Coroutine _deny;
        private bool _affordable = true;

        public GameObject gameObject => _canvas.gameObject;

        public float Alpha
        {
            get => _group.alpha;
            set => _group.alpha = value;
        }

        /// <param name="host">Quien corre la corrutina de la sacudida.</param>
        /// <param name="localScale">Escala local del canvas (0.01 = 1 px ↔ 0.01 u con padre a escala 1).</param>
        public ShopPriceTag(MonoBehaviour host, Transform parent, float localScale, Renderer sortingReference)
        {
            _host = host;

            var canvasGo = new GameObject("ShopPrice", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localScale = Vector3.one * localScale;
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.overrideSorting = true;
            _canvas.sortingLayerID = sortingReference.sortingLayerID;
            _canvas.sortingOrder = sortingReference.sortingOrder;
            _group = canvasGo.GetComponent<CanvasGroup>();
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(300f, 80f);

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(canvasGo.transform, false);
            _row = (RectTransform)row.transform;
            _row.sizeDelta = new Vector2(300f, 80f);
            _rowRest = _row.anchoredPosition;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var iconGo = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            ((RectTransform)iconGo.transform).sizeDelta = new Vector2(56f, 56f);
            _icon = iconGo.GetComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;

            var textGo = new GameObject("Price", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(row.transform, false);
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(150f, 70f);
            _text = textGo.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 54;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.raycastTarget = false;
            var outline = textGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        /// <summary>Icono (moneda, reroll…) y cantidad; <paramref name="localY"/> en unidades locales del padre.</summary>
        public void Set(Sprite icon, string amount, float localY)
        {
            _canvas.transform.localPosition = new Vector3(0f, localY, 0f);
            _group.alpha = 1f;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _text.text = amount;
            SetAffordable(_affordable);
        }

        public void SetAffordable(bool affordable)
        {
            _affordable = affordable;
            if (_deny == null) _text.color = affordable ? PriceColor : TooDearColor;
        }

        /// <summary>"No llega": sacudida y destello rojo del precio.</summary>
        public void Deny()
        {
            if (!_canvas.gameObject.activeInHierarchy) return;
            StopDeny();
            _deny = _host.StartCoroutine(DenyRoutine());
        }

        /// <summary>Corta la sacudida (o la da por cortada si el dueño paró sus corrutinas) y vuelve al reposo.</summary>
        public void StopDeny()
        {
            if (_deny != null) _host.StopCoroutine(_deny);
            _deny = null;
            _row.anchoredPosition = _rowRest;
            SetAffordable(_affordable);
        }

        private IEnumerator DenyRoutine()
        {
            const float duration = 0.35f;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - t / duration;
                _row.anchoredPosition = _rowRest + Vector2.right * (Mathf.Sin(t * 60f) * 18f * k);
                _text.color = Color.Lerp(TooDearColor, Color.white, Mathf.PingPong(t * 12f, 1f));
                yield return null;
            }

            _row.anchoredPosition = _rowRest;
            _deny = null;
            SetAffordable(_affordable);
        }
    }
}
