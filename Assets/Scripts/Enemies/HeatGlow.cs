using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// "Calentamiento": al disparar, las piezas indicadas se ven al rojo (shader "RedMagic/Sprite Lit FX",
    /// propiedad <c>_Heat</c>) y se enfrían en línea recta hasta volver a lo normal <see cref="readyMargin"/>
    /// segundos antes de poder disparar otra vez. Duración = <c>attackCooldown</c> − margen (cooldown 2 → 1.5 s;
    /// 5 → 4.5 s). Genérico: cualquier enemigo con <see cref="EnemyStats"/> y <see cref="EnemyAttack"/>.
    /// Las piezas deben usar un material con ese shader.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeatGlow : MonoBehaviour
    {
        [Tooltip("Piezas que se calientan (su material debe ser del shader 'RedMagic/Sprite Lit FX').")]
        public Renderer[] targets;
        [Tooltip("Segundos antes del próximo disparo en que ya vuelve a verse normal.")]
        [Min(0f)] public float readyMargin = 0.5f;
        [Tooltip("Duración mínima del enfriado, por si el cooldown es muy corto.")]
        [Min(0.05f)] public float minDuration = 0.2f;
        [Range(0f, 1f)] public float peak = 1f;

        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private EnemyStats _stats;
        private EnemyAttack _attack;
        private MaterialPropertyBlock _block;
        private float _heat;
        private float _decayPerSecond;

        public float Heat => _heat;

        private void Awake()
        {
            _stats = GetComponent<EnemyStats>();
            _attack = GetComponent<EnemyAttack>();
            _block = new MaterialPropertyBlock();
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
            float cooldown = _stats != null ? _stats.Tuning.attackCooldown : 2f;
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
            if (targets == null) return;
            foreach (var r in targets)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(HeatId, _heat);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
