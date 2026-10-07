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
    /// Parry del jugador (en el juego, <b>Reflejo</b>): al pulsar, el cuerpo del jugador se vuelve
    /// un espejo de magia durante una ventana corta, y cualquier proyectil o golpe enemigo que llegue
    /// en esa ventana, <b>venga de donde venga</b>, se bloquea por completo (<see cref="Combat.Parry"/>
    /// es el gancho por el que preguntan <c>Projectile</c> y <c>AbilityHit</c>). Acertar devuelve el
    /// cooldown al momento; fallar deja un cooldown largo. En el aire, acertar devuelve el salto y el
    /// dash aéreos. Opcionalmente devuelve el daño a quien lanzó el golpe.
    ///
    /// Los proyectiles se paran al tocar el cuerpo o, un poco antes, al entrar en un círculo alrededor
    /// (<see cref="catchRadius"/>): así el parry es omnidireccional y no exige que la bala ya esté
    /// dentro del collider.
    ///
    /// <b>Visual</b> (no hay sprite de parry, el jugador aún es por sprite-sheet): un <b>borde rojo</b>
    /// sólido que ciñe la silueta del frame actual (8 copias planas desplazadas detrás del cuerpo,
    /// material <c>Fx_SpriteSilhouette</c>), un <b>aura roja</b> difusa alrededor y un ligero tinte rojo
    /// del cuerpo. Sigue la forma del cuerpo, no es una burbuja: convive con un escudo esférico. Dura
    /// al menos <see cref="visualHold"/> aunque la ventana real sea más corta, y se desvanece en
    /// <see cref="visualFade"/>. Al acertar pasa a blanco un instante y sale un destello en el punto de
    /// contacto. El daño devuelto entra al instante, sin nada que viaje hasta el enemigo. Todo se puede
    /// sustituir por prefabs (<see cref="parryEffect"/>, <see cref="successEffect"/>, <see cref="reflectEffect"/>).
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
        [Tooltip("Sólo para lo que llega por delante (hacia donde mira el jugador). Apagado = en todas direcciones.")]
        [SerializeField] private bool frontOnly = false;

        [Header("Círculo de parada de proyectiles")]
        [Tooltip("Centro del círculo respecto al jugador (el centro del cuerpo).")]
        [SerializeField] private Vector2 catchCenterOffset = new Vector2(0f, 0.1f);
        [Tooltip("Radio en el que un proyectil enemigo se para durante la ventana aunque aún no haya " +
                 "tocado al jugador. 0 = sólo al tocar el cuerpo.")]
        [Min(0f)]
        [SerializeField] private float catchRadius = 1.0f;

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

        [Header("Devolver daño")]
        [Tooltip("El golpe bloqueado hace daño a quien lo lanzó, al instante. Un " +
                 "proyectil del entorno (sin dueño) no devuelve nada.")]
        [SerializeField] private bool reflectDamage = false;
        [Tooltip("Daño devuelto = daño del golpe bloqueado × esto. 0.1, 2, 10…")]
        [Min(0f)]
        [SerializeField] private float reflectMultiplier = 1f;
        [SerializeField] private bool reflectFromProjectiles = true;
        [SerializeField] private bool reflectFromMelee = true;
        [Tooltip("Empujón del daño devuelto (multiplica el Knockback del enemigo). 0 = no empuja.")]
        [Min(0f)]
        [SerializeField] private float reflectKnockback = 1f;

        [Header("Visual — prefabs (vacío = placeholder en código)")]
        [Tooltip("Efecto sobre el jugador al pulsar. Vacío = borde y aura rojos alrededor del jugador.")]
        [SerializeField] private GameObject parryEffect;
        [Tooltip("Efecto en el punto de contacto al bloquear. Vacío = destello de anillo.")]
        [SerializeField] private GameObject successEffect;
        [Tooltip("Efecto sobre el enemigo al que se le devuelve el daño. Vacío = ninguno (sólo su número de daño).")]
        [SerializeField] private GameObject reflectEffect;
        [Tooltip("Posición del efecto de parry respecto al jugador. La X se invierte según hacia dónde se mira.")]
        [SerializeField] private Vector2 effectOffset = new Vector2(0f, 0.1f);

        [Header("Visual — borde y aura (placeholder)")]
        [Tooltip("Color del borde, del aura y del destello.")]
        [SerializeField] private Color auraColor = new Color(1f, 0.12f, 0.16f, 1f);
        [Tooltip("Material de silueta plana para el borde (Fx_SpriteSilhouette). Vacío = no hay borde.")]
        [SerializeField] private Material silhouetteMaterial;
        [Tooltip("Material aditivo para el aura y el destello (Fx_SpriteAdditive). Vacío = sprite normal.")]
        [SerializeField] private Material glowMaterial;
        [Tooltip("Grosor del borde rojo alrededor de la silueta, en unidades de mundo.")]
        [Min(0f)]
        [SerializeField] private float outlineWidth = 0.07f;
        [Tooltip("Diámetro del aura roja difusa alrededor del jugador, en unidades de mundo. 0 = sin aura.")]
        [Min(0f)]
        [SerializeField] private float auraSize = 3.2f;
        [Range(0f, 1f)]
        [SerializeField] private float auraIntensity = 0.75f;
        [Tooltip("Cuánto se tiñe de rojo el propio cuerpo (0 = nada).")]
        [Range(0f, 1f)]
        [SerializeField] private float bodyTint = 0.35f;
        [Tooltip("Segundos que el efecto se queda a tope desde que se pulsa, aunque la ventana sea más corta. " +
                 "Sólo visual: la ventana real de parry no cambia.")]
        [Min(0f)]
        [SerializeField] private float visualHold = 0.18f;
        [Tooltip("Segundos de desvanecido después.")]
        [Min(0f)]
        [SerializeField] private float visualFade = 0.3f;

        [Header("Depuración")]
        [SerializeField] private bool drawGizmo = true;

        private PlayerMovement _movement;
        private Health _health;
        private InputAction _parryAction;
        private SpriteRenderer _bodySprite;

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

        // Placeholder, hijos del jugador creados una vez y reutilizados: 8 copias planas de la silueta
        // desplazadas detrás del cuerpo (= borde), un aura difusa, un tinte encima del cuerpo y un
        // destello en el punto de contacto.
        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f), new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f),
        };
        private readonly List<SpriteRenderer> _outline = new List<SpriteRenderer>();
        private SpriteRenderer _aura;
        private SpriteRenderer _tint;
        private SpriteRenderer _flash;
        private bool _visualOn;
        private float _visualTime;        // segundos (reales) desde que se encendió
        private float _fadeTime;          // segundos (reales) que lleva desvaneciéndose
        private float _whiteTimer;
        private float _flashTimer;
        private const float FlashTime = 0.22f;
        private const float WhiteTime = 0.12f;
        private const float PopTime = 0.06f;

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
            _bodySprite = GetComponentInChildren<SpriteRenderer>();

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
            _flashTimer = 0f;
            SetVisual(false);
            if (_flash != null) _flash.enabled = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_cooldown > 0f) _cooldown -= dt;

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

        // Después de que el Animator cambie el frame: la silueta copia el sprite de este frame.
        private void LateUpdate() => TickPlaceholders(Time.unscaledDeltaTime);

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

        /// <summary>Para los proyectiles enemigos que estén dentro del círculo antes de que toquen el cuerpo.</summary>
        private void CatchProjectiles()
        {
            if (!parryProjectiles || catchRadius <= 0f || _health == null) return;

            int count = Physics2D.OverlapCircle(CatchCenter(), catchRadius * Scale, CatchFilter, CatchBuffer);
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
                float side = (hit.Point.x - transform.position.x) * Facing;
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

            if (Items.PlayerHit.Deal(target, hit.Damage * reflectMultiplier, hit.Point, reflectKnockback,
                                     Items.HitKind.Parry) && reflectEffect != null)
                VfxOneShot.Spawn(reflectEffect, BodyCenter(target), Facing, null, Scale);
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

        private Vector2 CatchCenter() => (Vector2)transform.position + catchCenterOffset * Scale;

        private static Vector2 BodyCenter(Health target) =>
            target.TryGetComponent(out Collider2D body) ? (Vector2)body.bounds.center : (Vector2)target.transform.position;

        private void ShowParryEffect()
        {
            if (parryEffect != null)
            {
                VfxOneShot.SpawnFollowing(parryEffect, transform, effectOffset * Scale, Facing, startup + window, Scale);
                return;
            }

            SetVisual(true);
        }

        private void ShowSuccessEffect(Vector2 point)
        {
            // Con el acierto el borde y el aura vuelven a tope y pasan a blanco un instante.
            _whiteTimer = WhiteTime;
            if (parryEffect == null) SetVisual(true, restart: false);

            if (successEffect != null)
            {
                VfxOneShot.Spawn(successEffect, point, Facing, null, Scale);
                return;
            }

            if (_flash == null) return;
            _flash.transform.position = point;
            _flashTimer = FlashTime;
            _flash.enabled = true;
        }

        /// <summary>Enciende (desde cero o, sin <paramref name="restart"/>, reavivando a tope) o apaga el borde y el aura.</summary>
        private void SetVisual(bool on, bool restart = true)
        {
            if (on && _bodySprite == null) return;

            if (on && (restart || !_visualOn)) _visualTime = 0f;
            else if (on) _visualTime = Mathf.Min(_visualTime, PopTime);   // reavivar sin volver a crecer
            _visualOn = on;

            foreach (var copy in _outline) copy.enabled = on;
            if (_aura != null) _aura.enabled = on && auraSize > 0f;
            if (_tint != null) _tint.enabled = on && bodyTint > 0f;
        }

        private void BuildPlaceholders()
        {
            int order = _bodySprite != null ? _bodySprite.sortingOrder : 1;

            // El borde va justo detrás del cuerpo; el aura, detrás del borde.
            if (silhouetteMaterial != null)
                foreach (var _ in OutlineDirections)
                    _outline.Add(MakeRenderer("ParryOutline", order - 1, silhouetteMaterial));

            _aura = MakeRenderer("ParryAura", order - 2, glowMaterial);
            _aura.sprite = ProceduralSprites.Glow;
            _tint = MakeRenderer("ParryBodyTint", order + 1, silhouetteMaterial);
            _flash = MakeRenderer("ParryFlash", order + 5, glowMaterial);
            _flash.sprite = ProceduralSprites.Ring;
        }

        private SpriteRenderer MakeRenderer(string objectName, int sortingOrder, Material material)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            if (_bodySprite != null) renderer.sortingLayerID = _bodySprite.sortingLayerID;
            renderer.sortingOrder = Mathf.Max(1, sortingOrder);   // nunca 0: quedaría detrás del fondo
            renderer.enabled = false;
            return renderer;
        }

        private void TickPlaceholders(float dt)
        {
            if (_visualOn)
            {
                // A tope mientras dura la ventana y al menos visualHold; luego se desvanece.
                _visualTime += dt;
                if (_whiteTimer > 0f) _whiteTimer -= dt;

                float alpha = 1f;
                bool holding = _windowOpen || _visualTime < visualHold || _whiteTimer > 0f;
                if (holding) _fadeTime = 0f;
                else
                {
                    _fadeTime += dt;
                    alpha = visualFade > 0f ? 1f - _fadeTime / visualFade : 0f;
                }

                if (alpha <= 0f)
                {
                    SetVisual(false);
                }
                else
                {
                    float pop = Mathf.Clamp01(_visualTime / PopTime);
                    pop = 1f - (1f - pop) * (1f - pop);                   // sale de golpe y frena
                    var color = _whiteTimer > 0f ? Color.Lerp(auraColor, Color.white, 0.75f) : auraColor;
                    DrawOutline(color, alpha, pop);
                    DrawAura(color, alpha, pop);
                    if (_tint != null && _tint.enabled)
                    {
                        CopyBody(_tint, Vector2.zero);
                        var c = color; c.a = bodyTint * alpha;
                        _tint.color = c;
                    }
                }
            }

            if (_flash != null && _flash.enabled)
            {
                _flashTimer -= dt;
                float t = 1f - Mathf.Clamp01(_flashTimer / FlashTime);   // 0 → 1
                float size = Mathf.Lerp(0.4f, 1.8f, t);
                _flash.transform.localScale = new Vector3(size, size, 1f);
                var c = Color.Lerp(Color.white, auraColor, t);
                c.a = 1f - t;
                _flash.color = c;
                if (_flashTimer <= 0f) _flash.enabled = false;
            }
        }

        private void DrawOutline(Color color, float alpha, float pop)
        {
            float width = outlineWidth * Scale * Mathf.Lerp(2.2f, 1f, pop);   // empieza grueso y se asienta
            var c = color; c.a = alpha;
            for (int i = 0; i < _outline.Count; i++)
            {
                CopyBody(_outline[i], OutlineDirections[i] * width);
                _outline[i].color = c;
            }
        }

        private void DrawAura(Color color, float alpha, float pop)
        {
            if (_aura == null || !_aura.enabled) return;

            // Centrado en el cuerpo; el tamaño es en mundo y sigue la escala del jugador (hijo suyo).
            _aura.transform.position = _bodySprite.bounds.center;
            float size = auraSize / ProceduralSprites.Glow.bounds.size.x * Mathf.Lerp(0.5f, 1f, pop);
            _aura.transform.localScale = new Vector3(size, size, 1f);
            var c = color; c.a = auraIntensity * alpha;
            _aura.color = c;
        }

        /// <summary>Copia el frame actual del cuerpo en <paramref name="copy"/>, desplazado <paramref name="worldOffset"/>.</summary>
        private void CopyBody(SpriteRenderer copy, Vector2 worldOffset)
        {
            copy.sprite = _bodySprite.sprite;
            copy.flipX = _bodySprite.flipX;
            copy.flipY = _bodySprite.flipY;

            var body = _bodySprite.transform;
            var t = copy.transform;
            t.SetPositionAndRotation(body.position + (Vector3)worldOffset, body.rotation);
            var world = body.lossyScale;
            var parent = transform.lossyScale;
            t.localScale = new Vector3(world.x / parent.x, world.y / parent.y, 1f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo || catchRadius <= 0f) return;
            Gizmos.color = IsParrying ? new Color(1f, 0.2f, 0.25f, 0.9f) : new Color(1f, 0.2f, 0.25f, 0.35f);
            Gizmos.DrawWireSphere(CatchCenter(), catchRadius * Scale);
        }
    }
}
