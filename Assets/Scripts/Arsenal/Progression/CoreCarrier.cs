using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Portador de núcleo: un enemigo cualquiera convertido en raro (aspecto y fuerza de
    /// <see cref="WeaponProgressionConfig.carrierLook"/>) que, si muere, suelta un <see cref="CorePickup"/> — una mejora
    /// entera — y si no, se escapa a los <see cref="WeaponProgressionConfig.carrierLifetime"/> s (los últimos
    /// <see cref="WeaponProgressionConfig.carrierBlink"/> parpadea). Es lo que mete habilidad en la suerte: verlo, ir a por él
    /// y matarlo a tiempo.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoreCarrier : MonoBehaviour
    {
        private Health _health;
        private SpriteRenderer[] _renderers;
        private float _age;
        private bool _done;

        /// <summary>Convierte <paramref name="enemy"/> en portador (aspecto, fuerza, aviso).</summary>
        public static CoreCarrier Make(GameObject enemy)
        {
            if (enemy == null || enemy.GetComponent<CoreCarrier>() != null) return null;
            var cfg = WeaponProgressionConfig.Instance;
            if (cfg.carrierLook != null) cfg.carrierLook.Apply(enemy, cfg.variantShader);
            var c = enemy.AddComponent<CoreCarrier>();
            ArsenalProgressUi.Toast("progress.carrier_appeared");
            return c;
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void Update()
        {
            if (_done || _health == null || _health.IsDead) return;
            var cfg = WeaponProgressionConfig.Instance;
            _age += Time.deltaTime;
            float left = cfg.carrierLifetime - _age;

            if (left <= cfg.carrierBlink)
            {
                bool on = Mathf.Repeat(Time.time * Mathf.Lerp(10f, 4f, Mathf.Clamp01(left / Mathf.Max(0.01f, cfg.carrierBlink))), 1f) < 0.6f;
                foreach (var r in _renderers) if (r != null) r.enabled = on;
            }
            if (left > 0f) return;

            // Se escapa: sin morir, así que tampoco suelta botín.
            _done = true;
            ArsenalProgressUi.Toast("progress.carrier_escaped");
            ArsenalReactFx.Steam(Body());   // se esfuma
            Destroy(gameObject);
        }

        private void OnDied()
        {
            if (_done) return;
            _done = true;
            foreach (var r in _renderers) if (r != null) r.enabled = true;
            CorePickup.Spawn(Body());
        }

        private Vector2 Body()
        {
            var col = GetComponent<Collider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
        }
    }
}
