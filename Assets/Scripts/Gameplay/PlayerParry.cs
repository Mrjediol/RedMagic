using System.Collections;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Parry del jugador: al pulsar se abre una ventana corta en la que cualquier proyectil o golpe
    /// enemigo que llegue se bloquea por completo (<see cref="Combat.Parry"/> es el gancho por el
    /// que preguntan <c>Projectile</c> y <c>AbilityHit</c>). Acertar devuelve el cooldown al
    /// momento; fallar deja un cooldown largo. En el aire, acertar devuelve el salto y el dash
    /// aéreos. Opcionalmente devuelve el daño a quien lanzó el golpe.
    ///
    /// Los proyectiles se paran de dos formas: al tocar el cuerpo durante la ventana, y antes,
    /// al entrar en la zona de parada delante del jugador (<see cref="catchZoneSize"/>), que es lo
    /// que hace que se sienta como un tajo y no como un escudo pegado al cuerpo.
    ///
    /// El visual es un efecto delante del jugador (no hay sprite de parry): un prefab opcional
    /// (<see cref="parryEffect"/>, p. ej. el Tajo horneado en el VFX Lab) o, si se deja vacío, una
    /// media luna dibujada en código.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public class PlayerParry : MonoBehaviour, IParryReceiver, ISoundEventSource, ICooldownSource
    {
        /// <summary>OnParry al abrir la ventana, OnParrySuccess al bloquear. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(List<SoundTrigger> into)
        {
            into.Add(SoundTrigger.OnParry);
            into.Add(SoundTrigger.OnParrySuccess);
        }

        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string parryActionName = "Parry";

        [Header("Ventana")]
        [Tooltip("Segundos desde que se pulsa hasta que la ventana se abre. 0 = al instante.")]
        [Min(0f)]
        [SerializeField] private float startup = 0f;
        [Tooltip("Segundos que dura la ventana de parry. A 60 fps, 0.1 s = 6 frames. Pequeña = difícil.")]
        [Min(0.01f)]
        [SerializeField] private float window = 0.12f;
        [Tooltip("Cooldown si la ventana se cierra sin bloquear nada (fallo). Cuenta desde que se pulsa.")]
        [Min(0f)]
        [SerializeField] private float missCooldown = 4f;
        [Tooltip("Cooldown tras acertar. 0 = se puede volver a hacer parry al momento.")]
        [Min(0f)]
        [SerializeField] private float successCooldown = 0f;

        [Header("Qué se puede parar")]
        [SerializeField] private bool parryProjectiles = true;
        [Tooltip("Golpes de área de enemigos y jefes (espadazos, embestidas…). El daño por contacto " +
                 "(chocar con el cuerpo de un enemigo) nunca se para: no es un ataque.")]
        [SerializeField] private bool parryMelee = true;
        [Tooltip("Sólo para lo que llega por delante (hacia donde mira el jugador).")]
        [SerializeField] private bool frontOnly = false;

        [Header("Zona de parada de proyectiles (relativa al jugador, hacia donde mira)")]
        [Tooltip("Centro de la zona. La X se invierte según hacia dónde se mira.")]
        [SerializeField] private Vector2 catchZoneOffset = new Vector2(0.55f, 0.1f);
        [Tooltip("Tamaño de la zona. Un proyectil enemigo que esté dentro durante la ventana se para " +
                 "aunque aún no haya tocado al jugador. (0, 0) = sólo al tocar el cuerpo.")]
        [SerializeField] private Vector2 catchZoneSize = new Vector2(1.3f, 1.7f);

        [Header("Al acertar")]
        [Tooltip("Segundos (reales) de congelación del juego al bloquear: el 'golpe' que se siente. 0 = sin pausa.")]
        [Min(0f)]
        [SerializeField] private float hitStopDuration = 0.08f;
        [Tooltip("Velocidad del juego durante esa congelación (0 = parado del todo).")]
        [Range(0f, 1f)]
        [SerializeField] private float hitStopTimeScale = 0.05f;
        [Tooltip("Segundos tras acertar en los que lo que siga llegando (otra bala pegada, el resto " +
                 "del mismo espadazo) también se bloquea, pero sin recompensas.")]
        [Min(0f)]
        [SerializeField] private float graceAfterSuccess = 0.25f;

        [Header("Al acertar en el aire")]
        [SerializeField] private bool airReward = true;
        [Tooltip("Saltos aéreos que se devuelven.")]
        [Min(0)]
        [SerializeField] private int airJumpsRefund = 1;
        [Tooltip("Dashes aéreos que se devuelven.")]
        [Min(0)]
        [SerializeField] private int airDashesRefund = 1;
        [Tooltip("Además, el dash queda listo al instante (sin esperar su cooldown).")]
        [SerializeField] private bool resetDashCooldown = true;
        [Tooltip("Permite acumular por encima del máximo (parry sin haber gastado el doble salto = " +
                 "tener dos). Apagado = como mucho se recargan los que había.")]
        [SerializeField] private bool stackBeyondMax = false;
        [Tooltip("Velocidad hacia arriba que da el parry aéreo (un pequeño rebote). 0 = ninguno.")]
        [Min(0f)]
        [SerializeField] private float airBounce = 0f;

        [Header("Devolver daño (objeto mágico)")]
        [Tooltip("El golpe bloqueado hace daño a quien lo lanzó. Un proyectil del entorno (sin dueño) no devuelve nada.")]
        [SerializeField] private bool reflectDamage = false;
        [Tooltip("Daño devuelto = daño del golpe bloqueado × esto. 0.1, 2, 10…")]
        [Min(0f)]
        [SerializeField] private float reflectMultiplier = 1f;
        [SerializeField] private bool reflectFromProjectiles = true;
        [SerializeField] private bool reflectFromMelee = true;
        [Tooltip("Empujón del daño devuelto (multiplica el Knockback del enemigo). 0 = no empuja.")]
        [Min(0f)]
        [SerializeField] private float reflectKnockback = 1f;

        [Header("Visual")]
        [Tooltip("Efecto delante del jugador al pulsar (p. ej. el Tajo del VFX Lab). Vacío = media luna " +
                 "de placeholder dibujada en código.")]
        [SerializeField] private GameObject parryEffect;
        [Tooltip("Efecto en el punto de impacto al bloquear. Vacío = destello de placeholder.")]
        [SerializeField] private GameObject successEffect;
        [Tooltip("Efecto sobre el enemigo al devolverle el daño. Vacío = el mismo destello.")]
        [SerializeField] private GameObject reflectEffect;
        [Tooltip("Posición del efecto respecto al jugador. La X se invierte según hacia dónde se mira.")]
        [SerializeField] private Vector2 effectOffset = new Vector2(0.55f, 0.1f);
        [Tooltip("Color del placeholder (por defecto el verde lima de la casilla del HUD).")]
        [SerializeField] private Color placeholderColor = new Color(0.62f, 1f, 0.35f, 1f);
        [Tooltip("Alto del placeholder en unidades de mundo (con el jugador a escala 1).")]
        [Min(0.1f)]
        [SerializeField] private float placeholderSize = 1.7f;
        [Tooltip("Segundos que se queda visible la media luna tras cerrarse la ventana (desvanecido).")]
        [Min(0f)]
        [SerializeField] private float placeholderFade = 0.12f;

        [Header("Depuración")]
        [SerializeField] private bool drawGizmo = true;

        private PlayerMovement _movement;
        private Health _health;
        private InputAction _parryAction;

        private float _cooldown;
        private float _cooldownDuration;
        private float _startupTimer;
        private float _windowTimer;
        private bool _windowOpen;
        private bool _succeededThisWindow;
        private float _graceUntil = float.NegativeInfinity;
        private GameObject _lastMeleeAttacker;

        private Coroutine _hitStop;
        private bool _hitStopOwnsTimeScale;

        // Placeholder: una media luna y un destello, hijos del jugador, reutilizados (uno de cada).
        private SpriteRenderer _crescent;
        private SpriteRenderer _flash;
        private float _crescentAlpha;
        private float _crescentPop;
        private float _flashTimer;
        private const float FlashTime = 0.18f;

        private static readonly List<Collider2D> CatchBuffer = new List<Collider2D>(32);
        private static readonly HashSet<Projectile> CatchSeen = new HashSet<Projectile>();
        private static readonly ContactFilter2D CatchFilter = new ContactFilter2D { useTriggers = true };   // los proyectiles suelen ser trigger

        /// <summary>True mientras la ventana de parry está abierta (ya pasado el startup).</summary>
        public bool IsParrying => _windowOpen && _startupTimer <= 0f;

        // ------------------------------------------------------------------ HUD (ICooldownSource)

        public CooldownSlot Slot => CooldownSlot.Parry;
        public bool CooldownActive => enabled;
        public CooldownState Cooldown => new CooldownState { Remaining = Mathf.Max(0f, _cooldown), Duration = _cooldownDuration };
        public Sprite CooldownIcon => null;

        // ------------------------------------------------------------------ ciclo de vida

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _health = GetComponent<Health>();

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                _parryAction = map?.FindAction(parryActionName, throwIfNotFound: false);
                if (_parryAction == null)
                    Debug.LogWarning($"[PlayerParry] No existe la acción '{actionMapName}/{parryActionName}' en {inputActions.name}.", this);
            }

            BuildPlaceholders();
        }

        private void OnEnable()
        {
            _parryAction?.Enable();

            // Domain Reload apagado + jugador que persiste: nada de una sesión anterior.
            _cooldown = 0f;
            _cooldownDuration = 0f;
            _windowOpen = false;
            _graceUntil = float.NegativeInfinity;
            _lastMeleeAttacker = null;
        }

        private void OnDisable()
        {
            // No se desactiva la acción: el InputActionAsset es compartido (ver PlayerAttack.Suppressed).
            CloseWindow(notifyMiss: false);
            EndHitStop();
            if (_crescent != null) _crescent.enabled = false;
            if (_flash != null) _flash.enabled = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_cooldown > 0f) _cooldown -= dt;

            TickPlaceholders(Time.unscaledDeltaTime);

            if (!GameStateManager.CanPlayerAct || (_health != null && _health.IsDead))
            {
                TouchInput.ConsumeParry();
                if (_windowOpen) CloseWindow(notifyMiss: false);
                return;
            }

            bool pressed = (_parryAction != null && _parryAction.WasPressedThisFrame()) || TouchInput.ConsumeParry();
            if (pressed && _cooldown <= 0f) OpenWindow();

            if (!_windowOpen) return;

            if (_startupTimer > 0f)
            {
                _startupTimer -= dt;
                if (_startupTimer > 0f) return;
            }

            CatchProjectiles();

            _windowTimer -= dt;
            if (_windowTimer <= 0f) CloseWindow(notifyMiss: true);
        }

        // ------------------------------------------------------------------ ventana

        private void OpenWindow()
        {
            _windowOpen = true;
            _succeededThisWindow = false;
            _startupTimer = startup;
            _windowTimer = window;

            // El cooldown de fallo arranca ya: si se acierta, OnSuccess lo sustituye.
            _cooldown = startup + window + missCooldown;
            _cooldownDuration = _cooldown;

            SoundTriggered?.Invoke(SoundTrigger.OnParry);
            ShowParryEffect();
        }

        private void CloseWindow(bool notifyMiss)
        {
            if (!_windowOpen) return;
            _windowOpen = false;
            if (notifyMiss && !_succeededThisWindow) Combat.Parry.RaiseMissed(_health);
        }

        /// <summary>Para los proyectiles enemigos que estén en la zona de delante antes de que lleguen.</summary>
        private void CatchProjectiles()
        {
            if (!parryProjectiles || catchZoneSize.x <= 0f || catchZoneSize.y <= 0f || _health == null) return;

            int count = Physics2D.OverlapBox(CatchZoneCenter(), catchZoneSize * Scale, 0f, CatchFilter, CatchBuffer);
            CatchSeen.Clear();

            for (int i = 0; i < count; i++)
            {
                var projectile = CatchBuffer[i] != null ? CatchBuffer[i].GetComponentInParent<Projectile>() : null;
                if (projectile == null || !CatchSeen.Add(projectile)) continue;
                if (!projectile.CanHit(_health)) continue;   // del jugador, ya gastado, o aliado

                var hit = new IncomingHit(_health, projectile.Damage, projectile.Owner,
                                          projectile.transform.position, ParryHitKind.Projectile);
                if (TryParry(hit)) projectile.Deflect();
            }
        }

        // ------------------------------------------------------------------ IParryReceiver

        public bool TryParry(in IncomingHit hit)
        {
            if (!enabled) return false;
            if (hit.Kind == ParryHitKind.Projectile && !parryProjectiles) return false;
            if (hit.Kind == ParryHitKind.Melee && !parryMelee) return false;

            if (frontOnly)
            {
                float side = (hit.Point.x - transform.position.x) * (_movement != null ? _movement.Facing : 1);
                if (side < -0.05f) return false;
            }

            bool inGrace = Time.time < _graceUntil;

            // El resto del mismo espadazo (la caja de daño puede durar varios frames): bloqueado, sin premio.
            if (inGrace && hit.Kind == ParryHitKind.Melee && hit.Attacker != null && hit.Attacker == _lastMeleeAttacker)
                return true;

            if (IsParrying)
            {
                OnSuccess(hit);
                return true;
            }

            return inGrace;
        }

        private void OnSuccess(in IncomingHit hit)
        {
            _succeededThisWindow = true;
            _cooldown = successCooldown;
            _cooldownDuration = successCooldown;
            _graceUntil = Time.time + graceAfterSuccess;
            if (hit.Kind == ParryHitKind.Melee) _lastMeleeAttacker = hit.Attacker;

            if (airReward && _movement != null && !_movement.IsGrounded)
            {
                _movement.RefundAirMoves(airJumpsRefund, airDashesRefund, resetDashCooldown, stackBeyondMax);
                _movement.Bounce(airBounce);
            }

            Reflect(hit);

            SoundTriggered?.Invoke(SoundTrigger.OnParrySuccess);
            ShowSuccessEffect(hit.Point);
            StartHitStop();

            Combat.Parry.RaiseSucceeded(hit);
        }

        private void Reflect(in IncomingHit hit)
        {
            if (!reflectDamage || reflectMultiplier <= 0f || hit.Attacker == null || hit.Damage <= 0f) return;
            if (hit.Kind == ParryHitKind.Projectile && !reflectFromProjectiles) return;
            if (hit.Kind == ParryHitKind.Melee && !reflectFromMelee) return;

            var target = hit.Attacker.GetComponentInParent<Health>();
            if (target == null || target == _health || target.IsDead || Teams.Allied(gameObject, target)) return;

            if (Items.PlayerHit.Deal(target, hit.Damage * reflectMultiplier, transform.position, reflectKnockback,
                                     Items.HitKind.Parry))
                ShowReflectEffect(target);
        }

        // ------------------------------------------------------------------ hit-stop

        private void StartHitStop()
        {
            if (hitStopDuration <= 0f) return;

            // Sólo si el juego va a velocidad normal: no pisar la pausa ni una cámara lenta de depuración.
            if (!_hitStopOwnsTimeScale && !Mathf.Approximately(Time.timeScale, 1f)) return;

            if (_hitStop != null) StopCoroutine(_hitStop);
            _hitStop = StartCoroutine(HitStopRoutine());
        }

        private IEnumerator HitStopRoutine()
        {
            _hitStopOwnsTimeScale = true;
            Time.timeScale = hitStopTimeScale;
            yield return new WaitForSecondsRealtime(hitStopDuration);
            EndHitStop();
        }

        private void EndHitStop()
        {
            _hitStop = null;
            if (!_hitStopOwnsTimeScale) return;
            _hitStopOwnsTimeScale = false;

            // Si mientras tanto se pausó (timeScale 0), la pausa manda y ella lo devolverá a 1.
            if (Mathf.Approximately(Time.timeScale, hitStopTimeScale)) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ visual

        private float Scale => Mathf.Abs(transform.lossyScale.y);

        private int Facing => _movement != null ? _movement.Facing : 1;

        private Vector2 CatchZoneCenter() =>
            (Vector2)transform.position + new Vector2(catchZoneOffset.x * Facing, catchZoneOffset.y) * Scale;

        private Vector3 EffectPosition() =>
            transform.position + (Vector3)(new Vector2(effectOffset.x * Facing, effectOffset.y) * Scale);

        private void ShowParryEffect()
        {
            if (parryEffect != null)
            {
                VfxOneShot.SpawnFollowing(parryEffect, transform, effectOffset * Scale, Facing, startup + window, Scale);
                return;
            }

            if (_crescent == null) return;
            _crescentAlpha = 1f;
            _crescentPop = 0f;
            _crescent.enabled = true;
        }

        private void ShowSuccessEffect(Vector2 point)
        {
            if (successEffect != null)
            {
                VfxOneShot.Spawn(successEffect, point, Facing, null, Scale);
                return;
            }

            if (_flash == null) return;
            _flash.transform.position = point;
            _flashTimer = FlashTime;
            _flash.enabled = true;
            _crescentAlpha = 1f;   // la media luna se reaviva con el acierto
        }

        private void ShowReflectEffect(Health target)
        {
            var prefab = reflectEffect != null ? reflectEffect : successEffect;
            if (prefab != null) VfxOneShot.Spawn(prefab, target.transform.position, Facing, null, Scale);
        }

        private void BuildPlaceholders()
        {
            var reference = GetComponentInChildren<SpriteRenderer>();

            _crescent = MakePlaceholder("ParryCrescent", ProceduralSprites.Crescent, reference);
            _flash = MakePlaceholder("ParryFlash", ProceduralSprites.Ring, reference);
        }

        private SpriteRenderer MakePlaceholder(string objectName, Sprite sprite, SpriteRenderer reference)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = placeholderColor;
            if (reference != null)
            {
                renderer.sortingLayerID = reference.sortingLayerID;
                renderer.sortingOrder = reference.sortingOrder + 5;
            }
            renderer.enabled = false;
            return renderer;
        }

        private void TickPlaceholders(float dt)
        {
            if (_crescent != null && _crescent.enabled)
            {
                // Pegada al jugador y girada hacia donde mira; crece de golpe y se desvanece al cerrar.
                // Es hijo del jugador: su escala local ya hereda la del jugador (PlayerScaleConfig).
                int facing = Facing;
                _crescent.transform.position = EffectPosition();

                _crescentPop = Mathf.MoveTowards(_crescentPop, 1f, dt / 0.05f);
                float size = placeholderSize / ProceduralSprites.Crescent.bounds.size.y;
                float pop = Mathf.Lerp(0.6f, 1f, 1f - (1f - _crescentPop) * (1f - _crescentPop));
                _crescent.transform.localScale = new Vector3(size * pop * facing, size * pop, 1f);

                if (!_windowOpen && _flashTimer <= 0f)
                    _crescentAlpha -= placeholderFade > 0f ? dt / placeholderFade : 1f;

                var c = _flashTimer > 0f ? Color.Lerp(placeholderColor, Color.white, 0.6f) : placeholderColor;
                c.a *= Mathf.Clamp01(_crescentAlpha);
                _crescent.color = c;
                if (_crescentAlpha <= 0f) _crescent.enabled = false;
            }

            if (_flash != null && _flash.enabled)
            {
                _flashTimer -= dt;
                float t = 1f - Mathf.Clamp01(_flashTimer / FlashTime);   // 0 → 1
                float size = Mathf.Lerp(0.4f, 1.6f, t);
                _flash.transform.localScale = new Vector3(size, size, 1f);
                var c = Color.Lerp(Color.white, placeholderColor, t);
                c.a = 1f - t;
                _flash.color = c;
                if (_flashTimer <= 0f) _flash.enabled = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo || catchZoneSize.x <= 0f || catchZoneSize.y <= 0f) return;
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            Gizmos.color = IsParrying ? new Color(0.62f, 1f, 0.35f, 0.9f) : new Color(0.62f, 1f, 0.35f, 0.35f);
            Gizmos.DrawWireCube(CatchZoneCenter(), catchZoneSize * Scale);
        }
    }
}
