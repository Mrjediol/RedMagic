using System.Collections;
using RedMagic.Abilities;
using RedMagic.Fx;
using RedMagic.Gameplay;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RedMagic.Economy
{
    /// <summary>
    /// Lo que se ve sobre un altar de la tienda: el icono del item flotando con su <b>aura</b> de
    /// rareza (halo aditivo que late + partículas que suben; chispazos en legendario; más brillo si
    /// es el seleccionado) y, debajo, moneda + precio (rojo si no llega el oro). Al comprar,
    /// <see cref="PlayPurchase"/>: el icono crece y destella, estalla, vuela al jugador y deja un
    /// anillo en él; aura y precio se funden.
    ///
    /// Lo crea y lo llena <see cref="ShopManager"/>. Construye sus piezas (sprites, sistemas de
    /// partículas, canvas del precio) una sola vez y las reutiliza: nada se instancia por compra.
    /// Números en <see cref="ShopFxConfig"/>, colores en <see cref="ItemRarityColors"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopAltar : MonoBehaviour
    {
        private static readonly Color PriceColor = new(1f, 0.9f, 0.55f);
        private static readonly Color TooDearColor = new(1f, 0.35f, 0.3f);
        private static Material _particleMaterial;

        public ShopStockEntry Entry { get; private set; }
        public bool HasItem => Entry.Item != null;

        /// <summary>Centro del icono en el mundo (para medir la distancia al jugador).</summary>
        public Vector3 ItemPosition => transform.position;

        private SpriteRenderer _icon, _iconFlash, _halo;
        private ParticleSystem _aura, _burst;
        private Canvas _priceCanvas;
        private CanvasGroup _priceGroup;
        private Image _coin;
        private Text _price;

        private Color _color = Color.white;
        private ItemRarity _rarity;
        private float _iconScale = 1f;
        private float _phase, _bobAmplitude, _bobSpeed;
        private float _haloFade = 1f;
        private float _nextSparkle;
        private bool _affordable = true, _focused, _purchasing;
        private Coroutine _deny;
        private Vector2 _priceRowRest; // posición de reposo de la fila del precio: la sacudida siempre vuelve aquí

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _particleMaterial = null;

        // ------------------------------------------------------------------ API

        public void Show(ShopStockEntry entry, float iconSize, float priceOffset, float bobAmplitude,
                         float bobSpeed, Sprite coinIcon, GameObject sortingReference)
        {
            Build(sortingReference);
            StopAllCoroutines();
            _deny = null;
            _purchasing = false;

            Entry = entry;
            _bobAmplitude = bobAmplitude;
            _bobSpeed = bobSpeed;
            _phase = Random.value * 10f;
            _haloFade = 1f;

            var item = entry.Item;
            SetVisible(item != null);
            if (item == null) return;

            _rarity = item.Rarity;
            _color = ItemRarities.ColorOf(_rarity);

            _icon.sprite = item.Icon != null ? item.Icon : AbilityFx.DefaultSprite;
            _icon.color = item.Icon != null ? Color.white : item.Accent;
            _icon.transform.localPosition = Vector3.zero;
            AbilityFx.Resize(_icon.transform, _icon, Vector2.one * iconSize);
            _iconScale = _icon.transform.localScale.x;
            _iconFlash.sprite = _icon.sprite;
            _iconFlash.color = new Color(1f, 1f, 1f, 0f);

            _priceCanvas.transform.localPosition = new Vector3(0f, priceOffset, 0f);
            _priceGroup.alpha = 1f;
            _coin.sprite = coinIcon;
            _coin.enabled = coinIcon != null;
            _price.text = entry.Price.ToString();
            SetAffordable(_affordable);

            var fx = ShopFxConfig.Current;
            _aura.transform.localPosition = new Vector3(0f, fx.particleOriginOffset, 0f);
            _aura.Clear();
            _aura.Play();
            ApplyAuraRate();
            _nextSparkle = Time.time + fx.For(_rarity).sparkleInterval * Random.Range(0.3f, 1f);
        }

        /// <summary>Altar vacío al instante: sin icono, aura ni precio.</summary>
        public void Clear()
        {
            Entry = default;
            StopAllCoroutines();
            _deny = null;
            _purchasing = false;
            SetVisible(false);
        }

        public void SetAffordable(bool affordable)
        {
            _affordable = affordable;
            if (_price != null && _deny == null) _price.color = affordable ? PriceColor : TooDearColor;
        }

        /// <summary>Es el altar que el jugador tiene delante: el aura brilla un poco más.</summary>
        public void SetFocused(bool focused)
        {
            if (_focused == focused) return;
            _focused = focused;
            ApplyAuraRate();
        }

        /// <summary>"No llega": sacudida y destello rojo del precio.</summary>
        public void Deny()
        {
            if (_priceCanvas == null || !_priceCanvas.gameObject.activeInHierarchy || _purchasing) return;
            if (_deny != null) StopCoroutine(_deny);
            _deny = StartCoroutine(DenyRoutine());
        }

        /// <summary>
        /// Efecto de compra hacia <paramref name="player"/>. El altar queda vacío YA (no se puede
        /// volver a comprar) y el efecto sigue solo; el juego no se pausa.
        /// </summary>
        public void PlayPurchase(Transform player)
        {
            if (!HasItem) return;
            Entry = default;
            _focused = false;
            if (_deny != null) { StopCoroutine(_deny); _deny = null; }
            StartCoroutine(PurchaseRoutine(player));
        }

        // ------------------------------------------------------------------ vida

        private void Update()
        {
            if (_icon == null || !_icon.gameObject.activeSelf) return;

            var fx = ShopFxConfig.Current;
            var aura = fx.For(_rarity);
            float t = Time.time + _phase;

            if (!_purchasing)
                _icon.transform.localPosition = Vector3.up * (Mathf.Sin(t * _bobSpeed) * _bobAmplitude);

            // Halo: late en tamaño y alfa.
            float wave = 0.5f + 0.5f * Mathf.Sin(t * aura.pulseSpeed * Mathf.PI * 2f);
            float boost = _focused ? fx.focusBoost : 1f;
            float alpha = Mathf.Lerp(aura.haloAlphaMin, aura.haloAlphaMax, wave) * boost * _haloFade;
            _halo.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(alpha));
            float size = aura.haloSize * (1f + aura.pulseScale * (wave * 2f - 1f));
            SetWorldSize(_halo, size);

            // Chispazos (legendario, o cualquier rareza con intervalo > 0).
            if (!_purchasing && aura.sparkleInterval > 0f && aura.sparkleCount > 0 && Time.time >= _nextSparkle)
            {
                _nextSparkle = Time.time + aura.sparkleInterval * Random.Range(0.7f, 1.3f);
                Sparkle(_aura, transform.position, aura.sparkleCount, aura.particleSize * 2.2f, 1.2f);
            }
        }

        private IEnumerator PurchaseRoutine(Transform player)
        {
            _purchasing = true;
            var fx = ShopFxConfig.Current;
            var aura = fx.For(_rarity);
            bool legendary = _rarity == ItemRarity.Legendary;

            // El altar se apaga: sin más partículas de aura, halo y precio se funden.
            var emission = _aura.emission;
            emission.enabled = false;
            StartCoroutine(FadeOutRoutine(fx.fadeOutDuration));

            // 1. El icono crece de golpe y destella en blanco.
            Vector3 start = _icon.transform.position;
            for (float t = 0f; t < fx.popDuration; t += Time.deltaTime)
            {
                float p = 1f - (1f - t / fx.popDuration) * (1f - t / fx.popDuration);
                _icon.transform.localScale = Vector3.one * (_iconScale * Mathf.Lerp(1f, fx.popScale, p));
                _iconFlash.color = new Color(fx.whiteFlashIntensity, fx.whiteFlashIntensity, fx.whiteFlashIntensity, p);
                yield return null;
            }
            _icon.transform.localScale = Vector3.one * (_iconScale * fx.popScale);

            // 2. Estallido del color de la rareza desde el altar.
            _burst.transform.position = start;
            int count = Mathf.RoundToInt(aura.purchaseBurst * (legendary ? fx.legendaryBurstMultiplier : 1f));
            EmitBurst(count, fx);
            if (legendary)
            {
                CameraFollow.ShakeAll(fx.legendaryShakeAmplitude, fx.legendaryShakeDuration);
                Sparkle(_burst, start, fx.legendaryExtraSparkles, fx.burstParticleSize * 2f, fx.burstSpeed * 0.6f);
            }

            // 3. Vuela en arco hasta el jugador (que puede estar moviéndose) y se mete en él.
            float flashLeft = fx.whiteFlashDuration;
            for (float t = 0f; t < fx.flyTime; t += Time.deltaTime)
            {
                float p = t / fx.flyTime;
                Vector3 target = BodyCenter(player, start);
                Vector3 pos = Vector3.Lerp(start, target, p * p * (3f - 2f * p)) + Vector3.up * (fx.flyArcHeight * 4f * p * (1f - p));
                _icon.transform.position = pos;
                _icon.transform.localScale = Vector3.one * (_iconScale * Mathf.Lerp(fx.popScale, fx.flyEndScale, p));

                flashLeft -= Time.deltaTime;
                _iconFlash.color = new Color(fx.whiteFlashIntensity, fx.whiteFlashIntensity, fx.whiteFlashIntensity,
                                             Mathf.Clamp01(flashLeft / fx.whiteFlashDuration));
                yield return null;
            }

            // 4. Llegada: anillo y destello del color de la rareza sobre el jugador.
            Vector3 end = BodyCenter(player, start);
            var sortingRef = player != null ? player.gameObject : gameObject;
            AbilityFx.Flash(ProceduralSprites.Ring, end, Vector2.one * fx.arrivalRingSize, _color,
                            fx.arrivalRingDuration, 0f, 1.8f, sortingRef);
            AbilityFx.Flash(ProceduralSprites.Glow, end, Vector2.one * fx.arrivalRingSize * 0.8f,
                            new Color(_color.r, _color.g, _color.b, 0.8f), fx.arrivalRingDuration * 0.8f, 0f, 1.3f, sortingRef);

            _icon.transform.localPosition = Vector3.zero;
            _icon.transform.localScale = Vector3.one * _iconScale;
            _icon.gameObject.SetActive(false);
            _purchasing = false;
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - t / duration;
                _haloFade = k;
                _priceGroup.alpha = k;
                yield return null;
            }

            _haloFade = 0f;
            _priceGroup.alpha = 0f;
            _halo.gameObject.SetActive(false);
            _priceCanvas.gameObject.SetActive(false);
        }

        private IEnumerator DenyRoutine()
        {
            var rt = (RectTransform)_price.transform.parent;
            var basePos = _priceRowRest; // no la actual: un Deny encima de otro la dejaría desplazada
            const float duration = 0.35f;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - t / duration;
                rt.anchoredPosition = basePos + Vector2.right * (Mathf.Sin(t * 60f) * 18f * k);
                _price.color = Color.Lerp(TooDearColor, Color.white, Mathf.PingPong(t * 12f, 1f));
                yield return null;
            }

            rt.anchoredPosition = basePos;
            _deny = null;
            SetAffordable(_affordable);
        }

        // ------------------------------------------------------------------ partículas

        private void ApplyAuraRate()
        {
            if (_aura == null || !HasItem) return;

            var fx = ShopFxConfig.Current;
            var aura = fx.For(_rarity);
            float boost = _focused ? fx.focusBoost : 1f;

            var main = _aura.main;
            main.startColor = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(0.85f * boost));
            main.startSize = new ParticleSystem.MinMaxCurve(aura.particleSize * 0.7f, aura.particleSize * 1.3f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(fx.particleLifetime * 0.75f, fx.particleLifetime * 1.25f);

            var shape = _aura.shape;
            shape.scale = new Vector3(fx.particleAreaWidth, 0.05f, 0.05f);

            var velocity = _aura.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(fx.particleRiseSpeed * 0.8f, fx.particleRiseSpeed * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var emission = _aura.emission;
            emission.enabled = true;
            emission.rateOverTime = aura.particlesPerSecond * boost;
        }

        private void EmitBurst(int count, ShopFxConfig fx)
        {
            if (count <= 0) return;

            var main = _burst.main;
            main.startColor = _color;
            main.startSpeed = new ParticleSystem.MinMaxCurve(fx.burstSpeed * 0.5f, fx.burstSpeed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(fx.burstLifetime * 0.6f, fx.burstLifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(fx.burstParticleSize * 0.6f, fx.burstParticleSize);
            _burst.Emit(count);
        }

        /// <summary>Chispas: más grandes y casi blancas, disparadas en todas direcciones.</summary>
        private void Sparkle(ParticleSystem system, Vector3 at, int count, float size, float speed)
        {
            var white = Color.Lerp(_color, Color.white, 0.55f);
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = at + (Vector3)(dir * 0.3f),
                    applyShapeToPosition = false,
                    velocity = dir * (speed * Random.Range(0.5f, 1f)),
                    startSize = size * Random.Range(0.7f, 1.2f),
                    startColor = white,
                    startLifetime = Random.Range(0.4f, 0.8f),
                }, 1);
            }
        }

        // ------------------------------------------------------------------ construcción (una vez)

        private void Build(GameObject sortingReference)
        {
            if (_icon != null) return;

            var additive = ShopFxConfig.Current.additiveMaterial;

            _halo = NewSprite("ShopHalo", transform, ProceduralSprites.Glow, additive);
            _icon = NewSprite("ShopIcon", transform, null, null);
            _iconFlash = NewSprite("Flash", _icon.transform, null, additive);

            AbilityFx.CopySorting(_icon, sortingReference, 2);
            CopyLayer(_halo, _icon, -1);
            CopyLayer(_iconFlash, _icon, 1);

            _aura = NewParticles("ShopAura", loop: true, _icon);
            // En bucle con la emisión apagada: siempre "vivo", así Emit() simula al momento.
            _burst = NewParticles("ShopBurst", loop: true, _icon);
            ConfigureAura(_aura);
            ConfigureBurst(_burst);

            BuildPrice();
        }

        private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }

        private static void CopyLayer(Renderer target, Renderer reference, int orderOffset)
        {
            target.sortingLayerID = reference.sortingLayerID;
            target.sortingOrder = reference.sortingOrder + orderOffset;
        }

        private ParticleSystem NewParticles(string name, bool loop, SpriteRenderer sortingReference)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ParticleMaterial();
            CopyLayer(renderer, sortingReference, -1);
            return ps;
        }

        private static void ConfigureAura(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;

            FadeInOut(ps);
            Shrink(ps, 0.3f);
        }

        private static void ConfigureBurst(ParticleSystem ps)
        {
            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0f;
            limit.dampen = 0.12f;

            FadeInOut(ps);
            Shrink(ps, 0.1f);
            ps.Play();
        }

        private static void FadeInOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
        }

        private static void Shrink(ParticleSystem ps, float endSize)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
        }

        /// <summary>Material aditivo con el brillo radial como textura, compartido por todos los altares.</summary>
        private static Material ParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;

            var source = ShopFxConfig.Current.additiveMaterial;
            _particleMaterial = source != null
                ? new Material(source)
                : new Material(Shader.Find("Sprites/Default"));
            _particleMaterial.name = "ShopParticles (runtime)";
            _particleMaterial.mainTexture = ProceduralSprites.Glow.texture;
            _particleMaterial.SetFloat("_UseSpriteColor", 0f); // el color de partícula va en el vértice
            _particleMaterial.hideFlags = HideFlags.DontSave;
            return _particleMaterial;
        }

        private void BuildPrice()
        {
            // Canvas de mundo pequeño (0.01 u por píxel) con moneda + número en fila.
            var canvasGo = new GameObject("ShopPrice", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localScale = Vector3.one * 0.01f;
            _priceCanvas = canvasGo.GetComponent<Canvas>();
            _priceCanvas.renderMode = RenderMode.WorldSpace;
            _priceCanvas.overrideSorting = true;
            _priceCanvas.sortingLayerID = _icon.sortingLayerID;
            _priceCanvas.sortingOrder = _icon.sortingOrder;
            _priceGroup = canvasGo.GetComponent<CanvasGroup>();
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(300f, 80f);

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(canvasGo.transform, false);
            ((RectTransform)row.transform).sizeDelta = new Vector2(300f, 80f);
            _priceRowRest = ((RectTransform)row.transform).anchoredPosition;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var coinGo = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coinGo.transform.SetParent(row.transform, false);
            ((RectTransform)coinGo.transform).sizeDelta = new Vector2(56f, 56f);
            _coin = coinGo.GetComponent<Image>();
            _coin.preserveAspect = true;
            _coin.raycastTarget = false;

            var textGo = new GameObject("Price", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(row.transform, false);
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(150f, 70f);
            _price = textGo.GetComponent<Text>();
            _price.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _price.fontSize = 54;
            _price.fontStyle = FontStyle.Bold;
            _price.alignment = TextAnchor.MiddleLeft;
            _price.horizontalOverflow = HorizontalWrapMode.Overflow;
            _price.raycastTarget = false;
            var outline = textGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        // ------------------------------------------------------------------ utilidades

        private void SetVisible(bool visible)
        {
            if (_icon == null) return;
            _icon.gameObject.SetActive(visible);
            _halo.gameObject.SetActive(visible);
            _priceCanvas.gameObject.SetActive(visible);

            var emission = _aura.emission;
            emission.enabled = visible;
            if (!visible) _aura.Clear();
        }

        /// <summary>Escala el sprite para que mida <paramref name="size"/> unidades de mundo, sea cual sea la escala del padre.</summary>
        private void SetWorldSize(SpriteRenderer renderer, float size)
        {
            var bounds = renderer.sprite.bounds.size;
            var parent = transform.lossyScale;
            renderer.transform.localScale = new Vector3(size / bounds.x / Mathf.Max(0.001f, Mathf.Abs(parent.x)),
                                                        size / bounds.y / Mathf.Max(0.001f, Mathf.Abs(parent.y)), 1f);
        }

        private static Vector3 BodyCenter(Transform player, Vector3 fallback)
        {
            if (player == null) return fallback;
            var renderer = player.GetComponentInChildren<SpriteRenderer>();
            return renderer != null ? renderer.bounds.center : player.position;
        }
    }
}
