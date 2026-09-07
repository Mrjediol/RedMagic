using System;
using System.Collections;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Lanza la habilidad equipada. Va en el jugador (o en cualquier personaje que deba usar
    /// habilidades) y es el único que guarda estado de lanzamiento: cooldown, buffs temporales y
    /// qué asset hay equipado. Los assets de habilidad no guardan nada.
    ///
    /// Convive con <see cref="PlayerAttack"/> en vez de sustituirlo: mientras haya una habilidad
    /// equipada, el ataque normal de espada se desactiva y el botón de atacar lanza la habilidad;
    /// al desequiparla vuelve la espada. Eso permite comparar cualquier habilidad contra el ataque
    /// base sin tocar el prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityUser : MonoBehaviour
    {
        [Header("Habilidad")]
        [Tooltip("Habilidad equipada al empezar. Vacío = ataque normal de PlayerAttack.")]
        [SerializeField] private AbilityDefinition equipped;

        [Tooltip("Capas contra las que impactan las habilidades de este personaje.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string attackActionName = "Attack";
        [Tooltip("Usa también el botón táctil de ataque.")]
        [SerializeField] private bool useTouchButton = true;

        private PlayerMovement _movement;
        private PlayerAnimator _animator;
        private PlayerAttack _basicAttack;
        private Health _health;
        private SpriteRenderer _sprite;
        private InputAction _attackAction;

        private float _cooldownTimer;
        private bool _casting;

        private float _damageBuff = 1f;
        private float _damageBuffTimer;

        /// <summary>Habilidad equipada ahora mismo (null = ataque normal).</summary>
        public AbilityDefinition Equipped => equipped;

        /// <summary>Se dispara al cambiar de habilidad. Lo escucha el menú de pruebas.</summary>
        public event Action<AbilityDefinition> EquippedChanged;

        /// <summary>Segundos que faltan para poder volver a lanzar.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

        /// <summary>Nivel (1..3) del arma equipada.</summary>
        public int EquippedLevel =>
            AbilityLevelManager.Instance != null ? AbilityLevelManager.Instance.GetLevel(equipped) : 1;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _animator = GetComponent<PlayerAnimator>();
            _basicAttack = GetComponent<PlayerAttack>();
            _health = GetComponent<Health>();
            _sprite = GetComponentInChildren<SpriteRenderer>();

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null) _attackAction = map.FindAction(attackActionName, throwIfNotFound: false);
                else Debug.LogWarning("[AbilityUser] No existe el action map '" + actionMapName + "'.", this);
            }

            ApplyBasicAttackState();
        }

        private void OnEnable() => _attackAction?.Enable();

        private void OnDisable() => _attackAction?.Disable();

        // ------------------------------------------------------------------ equipar

        /// <summary>
        /// Equipa una habilidad (o null para volver al ataque normal). Corta el cooldown para que
        /// al cambiar en el menú se pueda probar en el acto.
        /// </summary>
        public void Equip(AbilityDefinition ability)
        {
            equipped = ability;
            _cooldownTimer = 0f;

            ApplyBasicAttackState();
            EquippedChanged?.Invoke(equipped);
        }

        /// <summary>
        /// Silencia o devuelve el ataque de espada según haya habilidad equipada. Son dos scripts
        /// leyendo el mismo botón: si los dos actuaran, una pulsación haría las dos cosas y además
        /// se pelearían por la cola de pulsaciones táctiles, que se vacía al leerla.
        ///
        /// Se silencia con <see cref="PlayerAttack.Suppressed"/> y no desactivando el componente:
        /// los dos sacan la acción de ataque del mismo asset de input, así que comparten el mismo
        /// objeto <c>InputAction</c> y el <c>OnDisable</c> de <c>PlayerAttack</c> la apagaría para
        /// los dos — equipar una habilidad dejaba el botón muerto.
        /// </summary>
        private void ApplyBasicAttackState()
        {
            if (_basicAttack != null) _basicAttack.Suppressed = equipped != null;
        }

        // ------------------------------------------------------------------ buffs

        /// <summary>Multiplica el daño de las habilidades durante unos segundos.</summary>
        public void AddDamageBuff(float multiplier, float seconds)
        {
            if (multiplier <= 0f || seconds <= 0f) return;

            // Se queda el mejor de los dos y se refresca el tiempo: apilar multiplicadores sin
            // límite convierte cualquier prueba en un uno-golpe.
            _damageBuff = Mathf.Max(_damageBuff, multiplier);
            _damageBuffTimer = Mathf.Max(_damageBuffTimer, seconds);
        }

        // ------------------------------------------------------------------ bucle

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (_damageBuffTimer > 0f)
            {
                _damageBuffTimer -= Time.deltaTime;
                if (_damageBuffTimer <= 0f) _damageBuff = 1f;
            }

            if (equipped == null) return;

            // En pausa o muerto no se lanza, pero se vacía la cola táctil para que no se acumule.
            if (!GameStateManager.CanPlayerAct || (_health != null && _health.IsDead))
            {
                if (useTouchButton) TouchInput.ConsumeAttack();
                return;
            }

            bool pressed = (_attackAction != null && _attackAction.WasPressedThisFrame())
                           || (useTouchButton && TouchInput.ConsumeAttack());

            if (pressed) TryCast();
        }

        /// <summary>Lanza la habilidad equipada. Devuelve false si aún no puede.</summary>
        public bool TryCast()
        {
            if (equipped == null || _casting || _cooldownTimer > 0f) return false;

            var ctx = BuildContext();
            if (!equipped.CanCast(ctx)) return false;

            _cooldownTimer = equipped.CooldownAt(EquippedLevel);
            equipped.PlayCastSfx();
            if (_animator != null) _animator.TriggerAttack();

            if (equipped.Windup > 0f) StartCoroutine(CastAfterWindup(equipped, equipped.Windup));
            else equipped.Execute(ctx);

            return true;
        }

        private IEnumerator CastAfterWindup(AbilityDefinition ability, float windup)
        {
            _casting = true;
            yield return new WaitForSeconds(windup);
            _casting = false;

            // El contexto se reconstruye después del windup a propósito: la dirección y la posición
            // valen las del momento del golpe, no las de cuando se pulsó.
            if (ability == equipped && (_health == null || !_health.IsDead)) ability.Execute(BuildContext());
        }

        private AbilityContext BuildContext()
        {
            int facing = ResolveFacing();
            int level = EquippedLevel;

            // El nivel del arma se resuelve aquí y no dentro de cada habilidad: los assets no
            // saben quién los lanza, y dos personajes pueden llevar la misma arma a niveles
            // distintos. El daño del nivel se multiplica con el buff temporal, que es otro
            // multiplicador del lanzador.
            float damageScale = _damageBuff;
            float sizeScale = 1f;
            float lifesteal = 0f;

            if (equipped != null)
            {
                var tier = equipped.TierFor(level);
                if (tier != null)
                {
                    damageScale *= tier.damageMultiplier;
                    sizeScale = tier.sizeMultiplier;
                    lifesteal = tier.lifesteal;
                }
            }

            return new AbilityContext(gameObject, this, _health, hitLayers, facing,
                                      new Vector2(facing, 0f), gameObject.tag,
                                      damageScale, sizeScale, lifesteal, level);
        }

        private int ResolveFacing()
        {
            if (_movement != null) return _movement.Facing;
            if (_sprite != null) return _sprite.flipX ? -1 : 1;
            return 1;
        }
    }
}
