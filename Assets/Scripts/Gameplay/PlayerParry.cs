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
    /// <b>Visual</b> (no hay sprite de parry, el jugador aún es por sprite-sheet): la silueta del
    /// propio jugador se ilumina (copias aditivas de su sprite actual, el aura ciñe el cuerpo y no es
    /// una burbuja, así que convive con un escudo esférico). Al acertar, destello en el punto de
    /// contacto; si devuelve daño, un <b>hilo carmesí</b> sale de ese punto hasta quien lanzó el golpe
    /// y el daño entra cuando llega: la magia del ataque vuelve por el mismo vínculo que la trajo, que
    /// es la excusa para hacer daño a distancia. Todo se puede sustituir por prefabs
    /// (<see cref="parryEffect"/>, <see cref="successEffect"/>, <see cref="reflectEffect"/>).
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

        [Header("Devolver daño (hilo carmesí)")]
        [Tooltip("El golpe bloqueado vuelve por un hilo de magia y hace daño a quien lo lanzó. Un " +
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
        [Tooltip("Segundos (reales) que tarda el hilo en llegar al enemigo; el daño entra al llegar. 0 = al instante.")]
        [Min(0f)]
        [SerializeField] private float reflectTravelTime = 0.12f;

        [Header("Visual — prefabs (vacío = placeholder en código)")]
        [Tooltip("Efecto sobre el jugador al pulsar. Vacío = la silueta del jugador se ilumina.")]
        [SerializeField] private GameObject parryEffect;
        [Tooltip("Efecto en el punto de contacto al bloquear. Vacío = destello de anillo.")]
        [SerializeField] private GameObject successEffect;
        [Tooltip("Efecto sobre el enemigo cuando le llega el daño devuelto. Vacío = el mismo destello.")]
        [SerializeField] private GameObject reflectEffect;
        [Tooltip("Posición del efecto de parry respecto al jugador. La X se invierte según hacia dónde se mira.")]
        [SerializeField] private Vector2 effectOffset = new Vector2(0f, 0.1f);

        [Header("Visual — placeholder")]
        [Tooltip("Color del reflejo (silueta, destello e hilo).")]
        [SerializeField] private Color placeholderColor = new Color(1f, 0.18f, 0.22f, 1f);
        [Tooltip("Material aditivo para la silueta y el destello (Fx_SpriteAdditive). Vacío = sprite normal teñido.")]
        [SerializeField] private Material glowMaterial;
        [Tooltip("Material del hilo (Fx_ParticleAdditive: color por vértice). Vacío = el de por defecto.")]
        [SerializeField] private Material threadMaterial;
        [Tooltip("Cuánto más grande que el cuerpo es el halo de la silueta.")]
        [Min(1f)]
        [SerializeField] private float rimScale = 1.18f;
        [Tooltip("Segundos de desvanecido de la silueta tras cerrarse la ventana.")]
        [Min(0f)]
        [SerializeField] private float glowFade = 0.12f;
        [Tooltip("Grosor del hilo en unidades de mundo.")]
        [Min(0.005f)]
        [SerializeField] private float threadWidth = 0.09f;
        [Tooltip("Cuánto zigzaguea el hilo (unidades). 0 = recto.")]
        [Min(0f)]
        [SerializeField] private float threadJitter = 0.18f;

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

        // Placeholder: halo + brillo del cuerpo (copias del sprite actual) y un destello, hijos del
        // jugador y reutilizados; los hilos, uno por golpe devuelto a la vez, se reutilizan también.
        private SpriteRenderer _rim;
        private SpriteRenderer _sheen;
        private SpriteRenderer _flash;
        private float _glowAlpha;
        private float _whiteTimer;
        private float _flashTimer;
        private const float FlashTime = 0.18f;
        private const float WhiteTime = 0.1f;
        private const int ThreadPoints = 8;
        private const float ThreadFade = 0.15f;
        private readonly List<LineRenderer> _threads = new List<LineRenderer>();

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
            _glowAlpha = 0f;
            _flashTimer = 0f;
            if (_rim != null) _rim.enabled = false;
            if (_sheen != null) _sheen.enabled = false;
            if (_flash != null) _flash.enabled = false;
            foreach (var thread in _threads) thread.enabled = false;
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

            StartCoroutine(ReflectRoutine(target, hit.Point, hit.Damage * reflectMultiplier));
        }

        /// <summary>El hilo viaja del punto de contacto al enemigo; el daño entra al llegar.</summary>
        private IEnumerator ReflectRoutine(Health target, Vector2 from, float amount)
        {
            var thread = GetThread();
            Vector2 seed = Random.insideUnitCircle * 100f;   // forma del zigzag, fija para este hilo

            for (float t = 0f; t < reflectTravelTime; t += Time.unscaledDeltaTime)
            {
                if (target == null || target.IsDead) { thread.enabled = false; yield break; }
                DrawThread(thread, from, BodyCenter(target), t / reflectTravelTime, seed, 1f);
                yield return null;
            }

            if (target == null || target.IsDead) { thread.enabled = false; yield break; }

            Vector2 to = BodyCenter(target);
            if (Items.PlayerHit.Deal(target, amount, from, reflectKnockback, Items.HitKind.Parry))
                ShowReflectEffect(to);

            for (float t = 0f; t < ThreadFade; t += Time.unscaledDeltaTime)
            {
                DrawThread(thread, from, to, 1f, seed, 1f - t / ThreadFade);
                yield return null;
            }

            thread.enabled = false;
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

            if (_rim == null || _bodySprite == null) return;
            _glowAlpha = 1f;
            _rim.enabled = true;
            _sheen.enabled = true;
        }

        private void ShowSuccessEffect(Vector2 point)
        {
            _whiteTimer = WhiteTime;   // la silueta pasa a blanco un instante con el acierto
            if (parryEffect == null && _rim != null && _bodySprite != null)
            {
                _glowAlpha = 1f;
                _rim.enabled = true;
                _sheen.enabled = true;
            }

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

        private void ShowReflectEffect(Vector2 point)
        {
            var prefab = reflectEffect != null ? reflectEffect : successEffect;
            if (prefab != null) VfxOneShot.Spawn(prefab, point, Facing, null, Scale);
        }

        private void BuildPlaceholders()
        {
            int order = _bodySprite != null ? _bodySprite.sortingOrder : 0;

            _rim = MakeRenderer("ParryRim", order - 1);       // halo detrás del cuerpo
            _sheen = MakeRenderer("ParrySheen", order + 1);   // brillo encima del cuerpo
            _flash = MakeRenderer("ParryFlash", order + 5);
            _flash.sprite = ProceduralSprites.Ring;
        }

        private SpriteRenderer MakeRenderer(string objectName, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            if (glowMaterial != null) renderer.sharedMaterial = glowMaterial;
            if (_bodySprite != null) renderer.sortingLayerID = _bodySprite.sortingLayerID;
            renderer.sortingOrder = Mathf.Max(1, sortingOrder);
            renderer.enabled = false;
            return renderer;
        }

        private LineRenderer GetThread()
        {
            foreach (var existing in _threads)
                if (!existing.enabled) { existing.enabled = true; return existing; }

            var go = new GameObject("ParryThread");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = ThreadPoints;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            if (threadMaterial != null) line.sharedMaterial = threadMaterial;
            if (_bodySprite != null) line.sortingLayerID = _bodySprite.sortingLayerID;
            line.sortingOrder = (_bodySprite != null ? _bodySprite.sortingOrder : 0) + 4;
            _threads.Add(line);
            return line;
        }

        /// <summary>
        /// Hilo de <paramref name="from"/> a <paramref name="to"/>, dibujado hasta la fracción
        /// <paramref name="reach"/>: un zigzag fino que tiembla (ruido que cambia con el tiempo).
        /// </summary>
        private void DrawThread(LineRenderer line, Vector2 from, Vector2 to, float reach, Vector2 seed, float alpha)
        {
            Vector2 head = Vector2.Lerp(from, to, Mathf.Clamp01(reach));
            Vector2 normal = Vector2.Perpendicular((to - from).normalized);
            float time = Time.unscaledTime * 18f;

            for (int i = 0; i < ThreadPoints; i++)
            {
                float u = i / (float)(ThreadPoints - 1);
                bool end = i == 0 || i == ThreadPoints - 1;
                float wave = end ? 0f : (Mathf.PerlinNoise(seed.x + u * 4f, seed.y + time) - 0.5f) * 2f * threadJitter * Scale;
                line.SetPosition(i, Vector2.Lerp(from, head, u) + normal * wave);
            }

            float width = threadWidth * Scale;
            line.startWidth = width;
            line.endWidth = width * 1.6f;   // más gordo en la punta, que es donde va el golpe
            var bright = Color.Lerp(placeholderColor, Color.white, 0.35f);
            var c0 = placeholderColor; c0.a *= alpha;
            var c1 = bright; c1.a *= alpha;
            line.startColor = c0;
            line.endColor = c1;
        }

        private void TickPlaceholders(float dt)
        {
            if (_rim != null && _rim.enabled)
            {
                if (_whiteTimer > 0f) _whiteTimer -= dt;
                if (!_windowOpen && _whiteTimer <= 0f)
                    _glowAlpha -= glowFade > 0f ? dt / glowFade : 1f;

                var color = _whiteTimer > 0f ? Color.Lerp(placeholderColor, Color.white, 0.7f) : placeholderColor;
                CopyBody(_rim, rimScale, color, Mathf.Clamp01(_glowAlpha));
                CopyBody(_sheen, 1f, color, Mathf.Clamp01(_glowAlpha) * 0.55f);

                if (_glowAlpha <= 0f)
                {
                    _rim.enabled = false;
                    _sheen.enabled = false;
                }
            }

            if (_flash != null && _flash.enabled)
            {
                _flashTimer -= dt;
                float t = 1f - Mathf.Clamp01(_flashTimer / FlashTime);   // 0 → 1
                float size = Mathf.Lerp(0.3f, 1.3f, t);
                _flash.transform.localScale = new Vector3(size, size, 1f);
                var c = Color.Lerp(Color.white, placeholderColor, t);
                c.a = 1f - t;
                _flash.color = c;
                if (_flashTimer <= 0f) _flash.enabled = false;
            }
        }

        /// <summary>
        /// Copia el frame actual del cuerpo en <paramref name="copy"/>, escalado alrededor del centro
        /// del sprite (no del pivote de los pies), con el mismo volteo.
        /// </summary>
        private void CopyBody(SpriteRenderer copy, float scale, Color color, float alpha)
        {
            copy.sprite = _bodySprite.sprite;
            copy.flipX = _bodySprite.flipX;
            copy.flipY = _bodySprite.flipY;

            var body = _bodySprite.transform;
            var t = copy.transform;
            t.SetPositionAndRotation(body.position, body.rotation);
            t.localScale = Vector3.one;
            var world = body.lossyScale * scale;
            var parent = transform.lossyScale;
            t.localScale = new Vector3(world.x / parent.x, world.y / parent.y, 1f);

            if (scale != 1f && _bodySprite.sprite != null)
            {
                // Escalar desde el pivote desplaza el dibujo; se recoloca para que el centro coincida.
                Vector3 center = _bodySprite.bounds.center;
                Vector3 offset = center - body.position;
                t.position = center - offset * scale;
            }

            color.a *= alpha;
            copy.color = color;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo || catchRadius <= 0f) return;
            Gizmos.color = IsParrying ? new Color(1f, 0.2f, 0.25f, 0.9f) : new Color(1f, 0.2f, 0.25f, 0.35f);
            Gizmos.DrawWireSphere(CatchCenter(), catchRadius * Scale);
        }
    }
}
