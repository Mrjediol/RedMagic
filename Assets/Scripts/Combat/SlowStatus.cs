using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Ralentización de un enemigo: menos velocidad de movimiento durante unos segundos, un tinte
    /// azul que dice "está congelado" y, opcionalmente, más daño recibido mientras dure (Hielo 4,
    /// Grimorio).
    ///
    /// No se pone en ningún prefab: <see cref="Apply"/> lo añade la primera vez que algo ralentiza a
    /// ese enemigo y lo reutiliza después. Quien mueve al enemigo (<c>EnemyBrain</c>,
    /// <c>EnemyController</c>) multiplica su velocidad por <see cref="SpeedScale"/>; el daño extra
    /// va por <see cref="Health.StatusDamageMultiplier"/>, así que entra por el único sitio donde se
    /// aplica daño y el número flotante ya es el real. El tinte va por <see cref="HitFlash"/> (capa
    /// de estado debajo del destello del golpe), para que los dos no se pisen los colores.
    ///
    /// Reaplicar refresca la duración y se queda con la ralentización más fuerte.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlowStatus : MonoBehaviour
    {
        private static readonly List<SlowStatus> ActiveList = new List<SlowStatus>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveList.Clear();

        /// <summary>Segundos finales en los que el tinte se desvanece (avisa de que se acaba).</summary>
        private const float FadeOutSeconds = 0.35f;

        private Health _health;
        private HitFlash _flash;

        private float _strength;
        private float _remaining;
        private float _vulnerability;
        private Color _tint = Color.white;
        private float _tintWeight;
        private bool _listed;

        /// <summary>Enemigos ralentizados ahora mismo. Lista compartida: léela, no la guardes.</summary>
        public static IReadOnlyList<SlowStatus> Active => ActiveList;

        public bool IsSlowed => _remaining > 0f && _strength > 0f;

        /// <summary>0..1: cuánto se le quita a la velocidad (0.4 = va al 60%).</summary>
        public float Strength => IsSlowed ? _strength : 0f;

        public float Remaining => Mathf.Max(0f, _remaining);

        /// <summary>Multiplicador de velocidad de movimiento (1 = normal).</summary>
        public float SpeedMultiplier => IsSlowed ? 1f - _strength : 1f;

        public Health Health => _health;

        /// <summary>
        /// Ralentiza a <paramref name="target"/>. Si ya lo estaba, refresca la duración y se queda
        /// con lo más fuerte de cada valor. Devuelve el estado (null si el objetivo no vale).
        /// </summary>
        /// <param name="strength">0..0.95 de velocidad que se le quita.</param>
        /// <param name="duration">Segundos.</param>
        /// <param name="vulnerability">Daño recibido extra mientras dure: 0.15 = +15%.</param>
        /// <param name="tint">Color del tinte; su alfa es la mezcla máxima (con fuerza plena).</param>
        /// <param name="fullTintAtStrength">Fuerza a partir de la cual el tinte llega a su alfa.</param>
        public static SlowStatus Apply(Health target, float strength, float duration, float vulnerability,
                                       Color tint, float fullTintAtStrength)
        {
            if (target == null || target.IsDead || strength <= 0f || duration <= 0f) return null;

            if (!target.TryGetComponent(out SlowStatus status))
                status = target.gameObject.AddComponent<SlowStatus>();

            status.Refresh(strength, duration, vulnerability, tint, fullTintAtStrength);
            return status;
        }

        /// <summary>Multiplicador de velocidad de quien lleve <paramref name="mover"/>. 1 si no está ralentizado.</summary>
        public static float SpeedScale(Component mover) =>
            mover != null && mover.TryGetComponent(out SlowStatus status) ? status.SpeedMultiplier : 1f;

        public static bool IsSlowedTarget(Health target) =>
            target != null && target.TryGetComponent(out SlowStatus status) && status.IsSlowed;

        /// <summary>
        /// True si hay al menos un enemigo ralentizado dentro de la cámara principal (con un
        /// pequeño margen: uno que asoma por el borde cuenta).
        /// </summary>
        public static bool AnyOnScreen(float viewportMargin = 0.05f)
        {
            if (ActiveList.Count == 0) return false;

            var camera = Camera.main;
            if (camera == null) return true;   // sin cámara no se puede filtrar: cuenta cualquiera

            for (int i = 0; i < ActiveList.Count; i++)
            {
                var status = ActiveList[i];
                if (status == null || !status.IsSlowed) continue;

                Vector3 vp = camera.WorldToViewportPoint(status.transform.position);
                if (vp.z >= 0f &&
                    vp.x >= -viewportMargin && vp.x <= 1f + viewportMargin &&
                    vp.y >= -viewportMargin && vp.y <= 1f + viewportMargin)
                    return true;
            }

            return false;
        }

        private void Awake()
        {
            _health = GetComponent<Health>();

            // El tinte va por la capa de estado de HitFlash. Todo enemigo lo lleva (ContentAudit lo
            // repara), pero si falta se añade: sin él el tinte y el destello se pisarían.
            _flash = GetComponent<HitFlash>();
            if (_flash == null && _health != null) _flash = gameObject.AddComponent<HitFlash>();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
            Clear(repaint: false);
        }

        private void Refresh(float strength, float duration, float vulnerability, Color tint, float fullTintAtStrength)
        {
            bool wasSlowed = IsSlowed;

            _strength = wasSlowed ? Mathf.Max(_strength, strength) : strength;
            _strength = Mathf.Clamp(_strength, 0f, 0.95f);
            _remaining = Mathf.Max(_remaining, duration);
            _vulnerability = wasSlowed ? Mathf.Max(_vulnerability, vulnerability) : vulnerability;

            // Tinte proporcional a la fuerza: una ralentización más fuerte, un azul más saturado.
            float intensity = fullTintAtStrength > 0f ? Mathf.Clamp01(_strength / fullTintAtStrength) : 1f;
            _tint = new Color(tint.r, tint.g, tint.b, 1f);
            _tintWeight = Mathf.Clamp01(tint.a) * intensity;

            if (!_listed)
            {
                ActiveList.Add(this);
                _listed = true;
            }

            ApplyEffects(1f);
        }

        private void Update()
        {
            if (!IsSlowed) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                Clear(repaint: true);
                return;
            }

            ApplyEffects(Mathf.Clamp01(_remaining / FadeOutSeconds));
        }

        private void ApplyEffects(float fade)
        {
            if (_health != null) _health.StatusDamageMultiplier = 1f + Mathf.Max(0f, _vulnerability);
            if (_flash != null) _flash.SetStatusTint(_tint, _tintWeight * fade, repaint: true);
        }

        private void OnDied()
        {
            // Al morir manda el Corpse (su propio tinte y fundido): se retira el estado sin repintar.
            Clear(repaint: false);
        }

        private void Clear(bool repaint)
        {
            _remaining = 0f;
            _strength = 0f;
            _vulnerability = 0f;

            if (_health != null) _health.StatusDamageMultiplier = 1f;
            if (_flash != null) _flash.SetStatusTint(Color.white, 0f, repaint);

            if (_listed)
            {
                ActiveList.Remove(this);
                _listed = false;
            }
        }
    }
}
