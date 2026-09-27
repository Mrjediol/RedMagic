using System.Collections;
using RedMagic.Abilities;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// El altar del reroll de la tienda (el objeto "Reroll" de la escena, con su SpriteRenderer).
    /// Para el jugador es un altar más: <see cref="ShopManager"/> lo enfoca, enseña su cartel y lo
    /// usa con la misma cercanía y tecla que los items, y debajo lleva la misma fila de precio
    /// (<see cref="ShopPriceTag"/>: icono de reroll + "1", roja sin rerolls, misma sacudida al negar).
    ///
    /// Lo añade <see cref="ShopManager"/> en tiempo de ejecución (igual que <see cref="ShopAltar"/>
    /// en los puntos de altar): no hay nada que configurar en la escena. Números en
    /// <see cref="ShopFxConfig"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopRerollAltar : MonoBehaviour, IShopInteractable
    {
        public Vector3 ItemPosition => transform.position;
        public bool CanInteract => !_playing;

        private SpriteRenderer _icon, _halo;
        private ShopPriceTag _tag;
        private Vector3 _baseScale;
        private Quaternion _baseRotation;
        private bool _focused, _playing;

        /// <param name="priceOffset">Dónde va el "precio", en unidades de mundo bajo el icono (como en los altares).</param>
        public void Setup(float priceOffset)
        {
            Build();

            var icon = ShopConfig.Instance.rerollIcon != null ? ShopConfig.Instance.rerollIcon : _icon.sprite;
            float scaleY = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            _tag.Set(icon, "1", priceOffset / scaleY);
        }

        public void SetAffordable(bool affordable) => _tag?.SetAffordable(affordable);

        public void SetFocused(bool focused) => _focused = focused;

        public void Deny() => _tag?.Deny();

        /// <summary>Destello + giro con golpe de escala. Mientras dura, no se puede enfocar.</summary>
        public void PlayTrigger(GameObject sortingReference)
        {
            var fx = ShopFxConfig.Current;
            var at = _icon.bounds.center;
            AbilityFx.Flash(ProceduralSprites.Ring, at, Vector2.one * fx.rerollFlashSize, fx.rerollAccent,
                            fx.rerollFlashDuration, 0f, 1.8f, sortingReference);
            AbilityFx.Flash(ProceduralSprites.Glow, at, Vector2.one * fx.rerollFlashSize * 0.8f,
                            new Color(fx.rerollAccent.r, fx.rerollAccent.g, fx.rerollAccent.b, 0.8f),
                            fx.rerollFlashDuration * 0.8f, 0f, 1.3f, sortingReference);

            StopAllCoroutines();
            _tag.StopDeny();
            StartCoroutine(TriggerRoutine(fx));
        }

        // ------------------------------------------------------------------ vida

        private void Update()
        {
            if (_halo == null) return;

            var fx = ShopFxConfig.Current;
            float alpha = _focused && !_playing ? fx.rerollHaloFocusedAlpha : fx.rerollHaloAlpha;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * Mathf.PI * 1.2f);
            _halo.color = new Color(fx.rerollAccent.r, fx.rerollAccent.g, fx.rerollAccent.b, alpha * pulse);
            SetWorldSize(_halo, fx.rerollHaloSize);
        }

        private IEnumerator TriggerRoutine(ShopFxConfig fx)
        {
            _playing = true;
            float duration = fx.rerollPunchDuration;

            // Gira y late el icono, no su precio: la fila se queda quieta en el mundo.
            var tag = _tag.gameObject.transform;
            Vector3 tagPosition = tag.position, tagScale = tag.localScale;
            Quaternion tagRotation = tag.rotation;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float punch = 1f + (fx.rerollPunchScale - 1f) * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                transform.localScale = _baseScale * punch;
                transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, -360f * fx.rerollSpinTurns * ease);
                tag.SetPositionAndRotation(tagPosition, tagRotation);
                tag.localScale = tagScale / punch;
                yield return null;
            }

            transform.localScale = _baseScale;
            transform.localRotation = _baseRotation;
            tag.SetPositionAndRotation(tagPosition, tagRotation);
            tag.localScale = tagScale;
            _playing = false;
        }

        // ------------------------------------------------------------------ construcción (una vez)

        private void Build()
        {
            if (_icon != null) return;

            _icon = GetComponent<SpriteRenderer>();
            if (_icon == null) _icon = gameObject.AddComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;

            var haloGo = new GameObject("RerollHalo");
            haloGo.transform.SetParent(transform, false);
            _halo = haloGo.AddComponent<SpriteRenderer>();
            _halo.sprite = ProceduralSprites.Glow;
            var additive = ShopFxConfig.Current.additiveMaterial;
            if (additive != null) _halo.sharedMaterial = additive;
            _halo.sortingLayerID = _icon.sortingLayerID;
            _halo.sortingOrder = _icon.sortingOrder - 1;

            // El precio se mide igual que en los altares (0.01 u por píxel) aunque el icono esté escalado.
            float scaleX = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
            _tag = new ShopPriceTag(this, transform, 0.01f / scaleX, _icon);
        }

        /// <summary>Escala el sprite para que mida <paramref name="size"/> unidades de mundo, sea cual sea la escala del padre.</summary>
        private void SetWorldSize(SpriteRenderer renderer, float size)
        {
            var bounds = renderer.sprite.bounds.size;
            var parent = transform.lossyScale;
            renderer.transform.localScale = new Vector3(size / bounds.x / Mathf.Max(0.001f, Mathf.Abs(parent.x)),
                                                        size / bounds.y / Mathf.Max(0.001f, Mathf.Abs(parent.y)), 1f);
        }
    }
}
