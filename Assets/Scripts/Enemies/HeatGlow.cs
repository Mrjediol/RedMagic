using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// "Calentamiento" de un arma: añádelo al GameObject que lleva el sprite del arma y listo. Al disparar el
    /// sprite se ve al rojo (shader "RedMagic/Sprite Lit FX", propiedad <c>_Heat</c>) y se enfría en línea recta
    /// hasta volver a lo normal <see cref="readyMargin"/> s antes de poder disparar otra vez: duración =
    /// cooldown − margen (cooldown 2 → 1.5 s; 5 → 4.5 s).
    /// Se engancha solo al <see cref="EnemyAttack"/> / <see cref="EnemyStats"/> del enemigo (busca en los padres)
    /// y, si el material del sprite no tiene el shader, le pone uno que sí. Fuera de un enemigo: llama a
    /// <see cref="Ignite"/> al disparar y fija <see cref="cooldownOverride"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class HeatGlow : MonoBehaviour
    {
        [Tooltip("Segundos antes del próximo disparo en que ya vuelve a verse normal.")]
        [Min(0f)] public float readyMargin = 0.5f;
        [Tooltip("Cooldown de ataque a usar. 0 = el del EnemyStats del enemigo.")]
        [Min(0f)] public float cooldownOverride = 0f;
        [Tooltip("Duración mínima del enfriado, por si el cooldown es muy corto.")]
        [Min(0.05f)] public float minDuration = 0.2f;
        [Range(0f, 1f)] public float peak = 1f;

        private const string ShaderName = "RedMagic/Sprite Lit FX";
        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private static Material _fallbackMaterial;

        private SpriteRenderer _renderer;
        private EnemyStats _stats;
        private EnemyAttack _attack;
        private MaterialPropertyBlock _block;
        private float _heat;
        private float _decayPerSecond;

        public float Heat => _heat;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _stats = GetComponentInParent<EnemyStats>();
            _attack = GetComponentInParent<EnemyAttack>();
            _block = new MaterialPropertyBlock();
            EnsureShader();
        }

        private void EnsureShader()
        {
            var m = _renderer.sharedMaterial;
            if (m != null && m.HasProperty(HeatId)) return;
            if (_fallbackMaterial == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null) { Debug.LogWarning($"[HeatGlow] No existe el shader '{ShaderName}'.", this); return; }
                _fallbackMaterial = new Material(shader) { name = "HeatGlow (runtime)" };
            }
            _renderer.sharedMaterial = _fallbackMaterial;
        }

        private void OnEnable()
        {
            if (_attack != null) _attack.SoundTriggered += OnTrigger;
            _heat = 0f;
            Apply();
        }

        private void OnDisable()
        {
            if (_attack != null) _attack.SoundTriggered -= OnTrigger;
        }

        private void OnTrigger(SoundTrigger t)
        {
            if (t == SoundTrigger.OnAttack) Ignite();
        }

        /// <summary>Calor al máximo ahora; baja hasta 0 a ritmo del cooldown de ataque.</summary>
        public void Ignite()
        {
            float cooldown = cooldownOverride > 0f ? cooldownOverride : _stats != null ? _stats.Tuning.attackCooldown : 2f;
            float duration = Mathf.Max(minDuration, cooldown - readyMargin);
            _heat = peak;
            _decayPerSecond = peak / duration;
            Apply();
        }

        private void Update()
        {
            if (_heat <= 0f) return;
            _heat = Mathf.Max(0f, _heat - _decayPerSecond * Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(HeatId, _heat);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
