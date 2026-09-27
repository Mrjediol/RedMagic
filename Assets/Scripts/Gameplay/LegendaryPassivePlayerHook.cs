using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Fx;
using RedMagic.Hub;
using RedMagic.Localization;
using RedMagic.Run;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Lo de las pasivas legendarias que tiene que vivir en ESTA instancia de jugador: <b>El Libro
    /// Sin Nombre</b>. Engancha <see cref="Health.DeathGuard"/> (revivir una vez por run al
    /// <see cref="LegendaryPassiveTuning.reviveHealthFraction"/> de la vida) y, en nivel 2,
    /// <see cref="Health.HitAbsorber"/> (escudo que absorbe
    /// <see cref="LegendaryPassiveTuning.reviveShieldHits"/> golpes tras revivir).
    ///
    /// Va en <c>Player.prefab</c>. "Una vez por run" lo lleva <see cref="LegendaryPassiveRunner"/>
    /// (<see cref="LegendaryPassiveRunner.ReviveAvailable"/>), no este componente: el jugador de la
    /// run es otra instancia que la del hub.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class LegendaryPassivePlayerHook : MonoBehaviour
    {
        private Health _health;
        private SpriteRenderer _bodyRenderer;
        private SpriteRenderer _shield;
        private int _shieldHits;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _health.DeathGuard = TryRevive;
            _health.HitAbsorber = TryAbsorb;
            LegendaryPassiveRunner.RunStateChanged += OnRunStateChanged;
        }

        private void OnDisable()
        {
            if (_health.DeathGuard == (System.Func<Health, bool>)TryRevive) _health.DeathGuard = null;
            if (_health.HitAbsorber == (System.Func<Health, float, bool>)TryAbsorb) _health.HitAbsorber = null;
            LegendaryPassiveRunner.RunStateChanged -= OnRunStateChanged;
            SetShield(0);
        }

        // El escudo es de la run en la que se revivió: no pasa a la siguiente.
        private void OnRunStateChanged(bool started)
        {
            if (!started) SetShield(0);
        }

        private void Update()
        {
            if (_shield == null || _shieldHits <= 0) return;

            // Pulso lento: se ve vivo sin distraer.
            var c = LegendaryPassiveTuning.Current.shieldColor;
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            _shield.color = new Color(c.r, c.g, c.b, c.a * pulse);
        }

        // ------------------------------------------------------------------ revivir

        private bool TryRevive(Health health)
        {
            int level = LegendaryPassiveEffects.Level(LegendaryPassiveEffectKind.ReviveOnce);
            if (level <= 0 || !LegendaryPassiveRunner.ReviveAvailable) return false;
            if (RunManager.Instance == null || !RunManager.Instance.RunInProgress) return false;

            var tuning = LegendaryPassiveTuning.Current;
            LegendaryPassiveRunner.ConsumeRevive();

            health.ReviveAt(tuning.reviveHealthFraction);
            health.StartInvulnerability(tuning.reviveInvulnerability);

            var center = BodyCenter();
            AbilityFx.Flash(ProceduralSprites.Glow, center, Vector2.one * 4f, tuning.shieldColor * 2f,
                            0.6f, 0f, 1.6f, gameObject);
            CameraFollow.ShakeAll(0.25f, 0.3f);

            var passive = LegendaryPassiveEffects.Find(LegendaryPassiveEffectKind.ReviveOnce);
            RewardPopupUi.Show(passive != null ? passive.icon : null, Loc.Get("reward.revived"),
                               passive != null ? passive.DisplayName : "", new Color(0.3f, 1f, 0.9f));

            if (level >= 2) SetShield(tuning.reviveShieldHits);
            return true;
        }

        // ------------------------------------------------------------------ escudo

        private bool TryAbsorb(Health health, float amount)
        {
            if (_shieldHits <= 0) return false;

            SetShield(_shieldHits - 1);
            var color = LegendaryPassiveTuning.Current.shieldColor;
            AbilityFx.Flash(ProceduralSprites.Ring, BodyCenter(), ShieldSize(), new Color(color.r, color.g, color.b, 1f),
                            0.3f, 0f, _shieldHits > 0 ? 1.2f : 1.8f, gameObject);
            return true;
        }

        private void SetShield(int hits)
        {
            _shieldHits = Mathf.Max(0, hits);

            if (_shieldHits > 0 && _shield == null)
            {
                var go = new GameObject("PassiveShield");
                go.transform.SetParent(transform, false);
                _shield = go.AddComponent<SpriteRenderer>();
                _shield.sprite = ProceduralSprites.Glow;
                AbilityFx.CopySorting(_shield, _bodyRenderer != null ? _bodyRenderer.gameObject : gameObject);
            }

            if (_shield == null) return;

            _shield.gameObject.SetActive(_shieldHits > 0);
            if (_shieldHits <= 0) return;

            // Tamaño en mundo fijo aunque el jugador tenga escala por escena (PlayerScaleConfig).
            _shield.transform.position = BodyCenter();
            var lossy = transform.lossyScale;
            var size = ShieldSize();
            var bounds = _shield.sprite.bounds.size;
            _shield.transform.localScale = new Vector3(size.x / bounds.x / Mathf.Max(0.001f, Mathf.Abs(lossy.x)),
                                                       size.y / bounds.y / Mathf.Max(0.001f, Mathf.Abs(lossy.y)), 1f);
        }

        private Vector2 ShieldSize()
        {
            if (_bodyRenderer == null || _bodyRenderer.sprite == null) return Vector2.one * 2f;
            var b = _bodyRenderer.bounds.size;
            float d = Mathf.Max(b.x, b.y) * 1.4f;
            return new Vector2(d, d);
        }

        private Vector3 BodyCenter() =>
            _bodyRenderer != null ? _bodyRenderer.bounds.center : transform.position;
    }
}
