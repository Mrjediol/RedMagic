using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Items
{
    /// <summary>
    /// Dispara el arma equipada en el <see cref="WeaponLoadout"/> de la run. Va en el jugador y es
    /// el único que guarda estado de disparo: cooldown y carga. Los assets de arma y sus
    /// modificadores no guardan nada (el pipeline se resuelve por composición en
    /// <see cref="ShotResolver"/>).
    ///
    /// <b>Convivencia con los otros dos ataques:</b>
    /// <list type="bullet">
    /// <item>Mientras hay un arma equipada, silencia <see cref="PlayerAttack"/> (la espada), igual
    /// que hace <see cref="AbilityUser"/>.</item>
    /// <item>Si además hay una <b>habilidad</b> equipada en <see cref="AbilityUser"/>, el arma cede:
    /// la habilidad manda y este componente queda inerte hasta que se desequipe. Así el menú de
    /// habilidades (K) y el de items (I) no se pelean por el botón de atacar.</item>
    /// </list>
    ///
    /// <b>Mantener para cargar:</b> si el disparo base tiene <see cref="BaseShot.chargeTime"/> &gt; 0,
    /// mantener el botón acumula carga y al soltar dispara con esa fracción (por debajo de
    /// <see cref="BaseShot.minChargeToFire"/> se cancela sin gastar cooldown). Con
    /// <c>chargeTime = 0</c> dispara al pulsar y se repite mientras se mantenga. La carga es
    /// genérica: cualquier arma futura (haz o proyectil) puede usarla.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponUser : MonoBehaviour
    {
        [Header("Objetivo")]
        [Tooltip("Capas contra las que impactan los disparos de este personaje.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string attackActionName = "Attack";
        [Tooltip("Usa también el botón táctil de ataque (sólo armas sin carga).")]
        [SerializeField] private bool useTouchButton = true;

        private PlayerMovement _movement;
        private PlayerAnimator _animator;
        private PlayerAttack _basicAttack;
        private AbilityUser _abilityUser;
        private Health _health;
        private SpriteRenderer _sprite;
        private InputAction _attackAction;

        private float _cooldownTimer;

        private bool _charging;
        private float _charge;
        private GameObject _chargeFx;

        private bool _suppressingBasic;

        /// <summary>Arma equipada ahora mismo, o null.</summary>
        public WeaponDefinition Weapon =>
            WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory.Weapon : null;

        /// <summary>Segundos que faltan para poder volver a disparar.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

        /// <summary>0..1 de carga acumulada ahora mismo (0 si no está cargando).</summary>
        public float ChargeFraction
        {
            get
            {
                var weapon = Weapon;
                float time = weapon != null ? weapon.Shot.chargeTime : 0f;
                return _charging && time > 0f ? Mathf.Clamp01(_charge / time) : 0f;
            }
        }

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _animator = GetComponent<PlayerAnimator>();
            _basicAttack = GetComponent<PlayerAttack>();
            _abilityUser = GetComponent<AbilityUser>();
            _health = GetComponent<Health>();
            _sprite = GetComponentInChildren<SpriteRenderer>();

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null) _attackAction = map.FindAction(attackActionName, throwIfNotFound: false);
                else Debug.LogWarning($"[WeaponUser] No existe el action map '{actionMapName}'.", this);
            }
        }

        private void OnEnable() => _attackAction?.Enable();

        private void OnDisable()
        {
            _attackAction?.Disable();
            CancelCharge();
            ReleaseBasicAttack();
        }

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            var weapon = Weapon;

            // Sin arma: soltamos nuestra supresión de la espada y salimos.
            if (weapon == null)
            {
                CancelCharge();
                ReleaseBasicAttack();
                return;
            }

            // Con arma pero también con habilidad equipada: el arma cede. No tocamos la supresión
            // de la espada — de eso se encarga AbilityUser mientras la habilidad esté puesta.
            if (_abilityUser != null && _abilityUser.Equipped != null)
            {
                CancelCharge();
                _suppressingBasic = false;   // AbilityUser toma el relevo
                if (useTouchButton) TouchInput.ConsumeAttack();
                return;
            }

            SuppressBasicAttack();

            if (!GameStateManager.CanPlayerAct || (_health != null && _health.IsDead))
            {
                CancelCharge();
                if (useTouchButton) TouchInput.ConsumeAttack();
                return;
            }

            if (weapon.Shot.chargeTime > 0f) UpdateCharge(weapon);
            else UpdateInstant(weapon);
        }

        // ------------------------------------------------------------------ sin carga

        private void UpdateInstant(WeaponDefinition weapon)
        {
            bool held = _attackAction != null && _attackAction.IsPressed();
            bool touch = useTouchButton && TouchInput.ConsumeAttack();

            if ((held || touch) && _cooldownTimer <= 0f)
                Fire(weapon, 1f);
        }

        // ------------------------------------------------------------------ mantener para cargar

        private void UpdateCharge(WeaponDefinition weapon)
        {
            bool held = _attackAction != null && _attackAction.IsPressed();

            if (held)
            {
                if (!_charging)
                {
                    _charging = true;
                    _charge = 0f;
                    _chargeFx = MakeChargeFx(weapon);
                    AudioManager.Instance?.PlaySFX(weapon.Shot.delivery == ShotDelivery.Hitscan
                        ? "SFX_ButtonHover" : "SFX_ButtonClick");
                }

                _charge = Mathf.Min(weapon.Shot.chargeTime, _charge + Time.deltaTime);
                UpdateChargeFx(weapon);
                return;
            }

            if (!_charging) return;

            // Se soltó el botón: dispara si llegó al mínimo y no hay cooldown; si no, se cancela.
            float frac = Mathf.Clamp01(_charge / weapon.Shot.chargeTime);
            bool canFire = frac >= weapon.Shot.minChargeToFire && _cooldownTimer <= 0f;

            CancelCharge();

            if (canFire) Fire(weapon, frac);
        }

        private void CancelCharge()
        {
            _charging = false;
            _charge = 0f;
            if (_chargeFx != null) Destroy(_chargeFx);
            _chargeFx = null;
        }

        private GameObject MakeChargeFx(WeaponDefinition weapon)
        {
            Vector2 pos = MuzzleWorld(weapon);
            var fx = AbilityFx.SpawnSprite("Weapon Charge", null, pos, Vector2.one * 0.15f,
                                           new Color(weapon.Accent.r, weapon.Accent.g, weapon.Accent.b, 0.7f),
                                           0f, gameObject);
            return fx;
        }

        private void UpdateChargeFx(WeaponDefinition weapon)
        {
            if (_chargeFx == null) return;

            float frac = Mathf.Clamp01(_charge / weapon.Shot.chargeTime);
            _chargeFx.transform.position = MuzzleWorld(weapon);

            var renderer = _chargeFx.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                float size = Mathf.Lerp(0.15f, 0.7f, frac);
                AbilityFx.Resize(_chargeFx.transform, renderer, Vector2.one * size);
                var c = renderer.color;
                c.a = Mathf.Lerp(0.4f, 1f, frac);
                renderer.color = c;
            }
        }

        // ------------------------------------------------------------------ disparo

        private void Fire(WeaponDefinition weapon, float chargeFraction)
        {
            _cooldownTimer = weapon.BaseCooldown;
            if (_animator != null) _animator.TriggerAttack();

            var ctx = BuildContext();
            ShotResolver.Fire(WeaponLoadout.Instance.Inventory, ctx, chargeFraction);
        }

        private ShotContext BuildContext()
        {
            int facing = ResolveFacing();
            return new ShotContext(gameObject, this, _health, hitLayers, facing,
                                   new Vector2(facing, 0f), gameObject.tag, 1f);
        }

        private Vector2 MuzzleWorld(WeaponDefinition weapon)
        {
            int facing = ResolveFacing();
            var offset = weapon.Shot.muzzleOffset;
            return (Vector2)transform.position + new Vector2(offset.x * facing, offset.y);
        }

        private int ResolveFacing()
        {
            if (_movement != null) return _movement.Facing;
            if (_sprite != null) return _sprite.flipX ? -1 : 1;
            return 1;
        }

        // ------------------------------------------------------------------ espada

        private void SuppressBasicAttack()
        {
            if (_basicAttack == null || _suppressingBasic) return;
            _basicAttack.Suppressed = true;
            _suppressingBasic = true;
        }

        private void ReleaseBasicAttack()
        {
            if (_basicAttack == null || !_suppressingBasic) return;
            _basicAttack.Suppressed = false;
            _suppressingBasic = false;
        }
    }
}
