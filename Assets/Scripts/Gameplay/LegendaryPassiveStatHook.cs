using RedMagic.Combat;
using RedMagic.Economy;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Aplica al <see cref="Health"/> de ESTA instancia de jugador los bonos por-instancia de las
    /// pasivas legendarias (vida máxima, armadura, regeneración) — los que no pueden vivir en un
    /// multiplicador estático como <see cref="PlayerStats"/> porque tocan campos propios de
    /// <see cref="Health"/> (<c>MaxHealth</c>, <c>DamageMultiplier</c>), no un producto que se lea
    /// cada vez.
    ///
    /// Va en <c>Player.prefab</c>. Cachea la vida máxima y el multiplicador de daño <b>de
    /// fábrica</b> en <see cref="Awake"/> para que el bono se sume siempre sobre el valor original
    /// del prefab, nunca sobre lo que dejó la última vez que se aplicó (importante porque
    /// <c>RunManager</c> crea una instancia de jugador nueva por partida, pero por si el mismo
    /// componente llegara a re-aplicarse sobre sí mismo).
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class LegendaryPassiveStatHook : MonoBehaviour
    {
        private Health _health;
        private float _baseMaxHealth;
        private float _baseDamageMultiplier;
        private float _regenTimer;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _baseMaxHealth = _health.MaxHealth;
            _baseDamageMultiplier = _health.DamageMultiplier;
        }

        private void OnEnable()
        {
            LegendaryPassiveEffects.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            LegendaryPassiveEffects.Changed -= Apply;
        }

        private void Update()
        {
            if (_health.IsDead || LegendaryPassiveEffects.HpRegenPerTick <= 0f) return;

            _regenTimer += Time.deltaTime;
            if (_regenTimer < LegendaryPassiveEffects.HpRegenTickSeconds) return;

            _regenTimer -= LegendaryPassiveEffects.HpRegenTickSeconds;
            _health.Heal(LegendaryPassiveEffects.HpRegenPerTick);
        }

        private void Apply()
        {
            _health.SetMaxHealth(_baseMaxHealth + LegendaryPassiveEffects.MaxHealthBonus, healToFull: false);
            _health.DamageMultiplier = _baseDamageMultiplier * LegendaryPassiveEffects.ArmorDamageMultiplier;
        }
    }
}
