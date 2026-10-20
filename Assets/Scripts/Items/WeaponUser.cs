using System.Collections;
using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Arsenal;
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
    public class WeaponUser : MonoBehaviour, ISoundEventSource, ICooldownSource, IReserveCooldownSource
    {
        /// <summary>OnLoot al recoger un item del suelo (<see cref="ItemPickup"/>). Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(System.Collections.Generic.List<SoundTrigger> into) => into.Add(SoundTrigger.OnLoot);

        /// <summary>Lo llama <see cref="ItemPickup"/> al entregar el item.</summary>
        public void NotifyItemPickedUp() => SoundTriggered?.Invoke(SoundTrigger.OnLoot);

        [Header("Objetivo")]
        [Tooltip("Capas contra las que impactan los disparos de este personaje.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Sincronía con la animación")]
        [Tooltip("Segundos desde que arranca la animación de ataque hasta que sale el disparo.\n\n" +
                 "El disparo tiene que salir CON el gesto, no al pulsar: sin esto el proyectil " +
                 "aparecía antes de que el personaje moviera el brazo. El clip de ataque del " +
                 "jugador dura 0,42 s (5 dibujos a 12 fps) y el lanzamiento se lee en el 4º-5º, " +
                 "de ahí el valor por defecto. 0 = sale al pulsar (comportamiento anterior).\n\n" +
                 "Es un retardo y no un AnimationEvent como el de los enemigos porque el Animator " +
                 "del jugador vive en el hijo 'Sprite', y los AnimationEvent sólo llegan a " +
                 "componentes de su propio GameObject.")]
        [Min(0f)]
        [SerializeField] private float releaseDelay = 0.28f;

        [Header("Vista previa en la escena (editor)")]
        [Tooltip("Arma que se dibuja en la escena al seleccionar el jugador o el propio asset del arma, " +
                 "sin entrar en Play: el proyectil a su tamaño (Base Shot ▸ Size), su collider y la boca " +
                 "(Muzzle Offset). En Play se dibuja el arma equipada. Vacío = sólo en Play.")]
        [SerializeField] private WeaponDefinition previewWeapon;

        [Tooltip("Dibujar la vista previa también al seleccionar el JUGADOR. Apagado, sólo sale al seleccionar el " +
                 "asset del arma (o con 'Always Show Preview').")]
        [SerializeField] private bool previewWhenPlayerSelected;

        [Tooltip("Dibujar la vista previa siempre, aunque no esté seleccionado nada.")]
        [SerializeField] private bool alwaysShowPreview;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [Tooltip("Acción del arma (Q / triángulo). La espada sigue en Attack: tener arma no quita el cuerpo a cuerpo.")]
        [SerializeField] private string weaponActionName = "Weapon";
        [Tooltip("Acción de cambiar entre el arma activa y la de reserva (Tab / mando ▲). Cambiar con el botón pulsado " +
                 "suelta la actual y pulsa la nueva.")]
        [SerializeField] private string swapActionName = "SwapWeapon";
        [Tooltip("Usa también la entrada táctil: el botón del arma (sólo armas sin carga) y, con los controles por " +
                 "gestos, mantener un dedo fuera de la zona de movimiento (carga y suelta).")]
        [SerializeField] private bool useTouchButton = true;

        private PlayerMovement _movement;
        private PlayerAnimator _animator;
        private AbilityUser _abilityUser;
        private Health _health;
        private SpriteRenderer _sprite;
        private InputAction _attackAction;
        private InputAction _swapAction;

        // Armas del arsenal: un controlador por arma equipada (activa o de reserva); el de reserva sigue recargando.
        private readonly Dictionary<WeaponDefinition, ArsenalController> _arsenal = new Dictionary<WeaponDefinition, ArsenalController>();
        private readonly List<WeaponDefinition> _arsenalGone = new List<WeaponDefinition>();
        private bool _arsenalHeld;

        private float _cooldownTimer;
        private float _cooldownDuration;

        private bool _charging;
        private float _charge;
        private GameObject _chargeFx;
        private bool _chargeFxPooled;   // sale de PrefabPool (arte del arma) y no de AbilityFx.SpawnSprite

        // Cometa: la carga dibujada por código. Al soltar pasa a 'pendiente' (el orbe sigue en la boca) hasta
        // que el disparo sale de verdad, para que el orbe se convierta en el cometa sin hueco.
        private CometChargeFx _cometCharge;
        private CometChargeFx _cometPending;

        /// <summary>El WeaponUser del jugador activo (hub o run), o null. Para efectos de item que tocan el cooldown.</summary>
        public static WeaponUser Current { get; private set; }

        /// <summary>
        /// Justo antes de soltar cada disparo, con el contexto del gesto. Los efectos "el siguiente
        /// disparo…" (Bastón: ×3 de daño; Yelmo: explosión) se suscriben aquí y pueden subir
        /// <see cref="CastArgs.DamageScale"/>. La instancia se reutiliza: no la guardes.
        /// </summary>
        public static event System.Action<CastArgs> Casting;

        private static readonly CastArgs SharedCastArgs = new CastArgs();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            Casting = null;
        }

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

        // ------------------------------------------------------------------ HUD (ICooldownSource)

        public CooldownSlot Slot => CooldownSlot.Weapon;

        /// <summary>Hay un arma equipada y ninguna habilidad la pisa.</summary>
        public bool CooldownActive => Weapon != null && (_abilityUser == null || _abilityUser.Equipped == null);

        public CooldownState Cooldown
        {
            get
            {
                var arsenal = Weapon != null && Weapon.IsArsenal ? ArsenalFor(Weapon) : null;
                if (arsenal != null) return new CooldownState { Remaining = 1f - Mathf.Clamp01(arsenal.Ready), Duration = 1f, Charge = 0f };

                // En segundos reales: el temporizador corre más deprisa con los items de enfriamiento.
                float rate = Mathf.Max(0.01f, PlayerStats.Multiplier(PlayerStat.CooldownRate));
                return new CooldownState
                {
                    Remaining = CooldownRemaining / rate,
                    Duration = _cooldownDuration / rate,
                    Charge = ChargeFraction,
                };
            }
        }

        public Sprite CooldownIcon => Weapon != null ? Weapon.Icon : null;

        // ------------------------------------------------------------------ HUD: arma de reserva (IReserveCooldownSource)

        private WeaponDefinition Reserve =>
            WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory.SecondWeapon : null;

        public bool HasReserve => Reserve != null;

        public Sprite ReserveIcon => Reserve != null ? Reserve.Icon : null;

        /// <summary>La del arsenal sigue recargando en reserva; las demás comparten el temporizador del gesto, así que en reserva están listas.</summary>
        public CooldownState ReserveCooldown
        {
            get
            {
                var arsenal = ArsenalFor(Reserve);
                return arsenal != null
                    ? new CooldownState { Remaining = 1f - Mathf.Clamp01(arsenal.Ready), Duration = 1f }
                    : default;
            }
        }

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _animator = GetComponent<PlayerAnimator>();
            _abilityUser = GetComponent<AbilityUser>();
            _health = GetComponent<Health>();
            _sprite = GetComponentInChildren<SpriteRenderer>();

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null)
                {
                    _attackAction = map.FindAction(weaponActionName, throwIfNotFound: false);
                    _swapAction = map.FindAction(swapActionName, throwIfNotFound: false);
                }
                else Debug.LogWarning($"[WeaponUser] No existe el action map '{actionMapName}'.", this);
            }
        }

        private void OnEnable()
        {
            _attackAction?.Enable();
            _swapAction?.Enable();
            Current = this;
        }

        private void OnDisable()
        {
            _attackAction?.Disable();
            _swapAction?.Disable();
            ReleaseArsenal();
            CancelCharge();
            CancelPendingComet();
            if (Current == this) Current = null;
        }

        private void Update()
        {
            // Items que aceleran el enfriamiento (Botas, Rapidez 2) cambian el ritmo, no el cooldown base.
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime * PlayerStats.Multiplier(PlayerStat.CooldownRate);

            TickArsenal();
            if (_swapAction != null && _swapAction.WasPressedThisFrame() && GameStateManager.CanPlayerAct) Swap();

            var weapon = Weapon;

            if (weapon == null)
            {
                CancelCharge();
                if (useTouchButton) TouchInput.ConsumeWeapon();
                return;
            }

            // Con arma pero también con habilidad equipada: el arma cede.
            if (_abilityUser != null && _abilityUser.Equipped != null)
            {
                CancelCharge();
                if (useTouchButton) TouchInput.ConsumeWeapon();
                return;
            }

            if (!GameStateManager.CanPlayerAct || (_health != null && _health.IsDead))
            {
                CancelCharge();
                if (useTouchButton) TouchInput.ConsumeWeapon();
                return;
            }

            if (weapon.IsArsenal) UpdateArsenal(weapon);
            else if (weapon.Shot.chargeTime > 0f) UpdateCharge(weapon);
            else UpdateInstant(weapon);
        }

        // ------------------------------------------------------------------ armas del arsenal

        /// <summary>El controlador de <paramref name="weapon"/> (lo crea la primera vez), o null si no es del arsenal.</summary>
        public ArsenalController ArsenalFor(WeaponDefinition weapon)
        {
            if (weapon == null || !weapon.IsArsenal) return null;
            if (_arsenal.TryGetValue(weapon, out var c)) return c;
            var kit = ArsenalRuntime.For(weapon);
            if (kit == null) return null;
            c = kit.CreateController();
            c.Bind(weapon, this);
            _arsenal[weapon] = c;
            ArsenalStatusDrawer.Ensure();
            return c;
        }

        /// <summary>Los otros controladores del arsenal equipados (para efectos sobre el arma de reserva).</summary>
        public IEnumerable<ArsenalController> OtherArsenal(ArsenalController self)
        {
            foreach (var c in _arsenal.Values) if (c != self) yield return c;
        }

        /// <summary>Pulsar / mantener / soltar el arma activa del arsenal. Sale al pulsar (sin esperar al gesto).</summary>
        private void UpdateArsenal(WeaponDefinition weapon)
        {
            var c = ArsenalFor(weapon);
            if (c == null) return;
            bool touch = useTouchButton && TouchInput.ConsumeWeapon();
            bool held = (_attackAction != null && _attackAction.IsPressed()) || (useTouchButton && TouchInput.WeaponHeld) || touch;
            var aim = BuildArsenalAim();
            if (held && !_arsenalHeld) c.OnPress(aim);
            else if (held) c.OnHold(aim, Time.deltaTime);
            else if (_arsenalHeld) c.OnRelease(aim);
            _arsenalHeld = held && !touch;
            if (touch) c.OnRelease(aim);
        }

        /// <summary>Recargas y lo que viva solo de cada arma equipada; quita los controladores de armas que ya no están.</summary>
        private void TickArsenal()
        {
            var inv = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            WeaponDefinition active = inv != null ? inv.Weapon : null, reserve = inv != null ? inv.SecondWeapon : null;
            ArsenalFor(active); ArsenalFor(reserve);
            _arsenalGone.Clear();
            foreach (var kv in _arsenal) if (kv.Key != active && kv.Key != reserve) _arsenalGone.Add(kv.Key);
            foreach (var w in _arsenalGone) { _arsenal[w].Dispose(); _arsenal.Remove(w); }
            float dt = Time.deltaTime * ArsenalBuild.CooldownRate;
            foreach (var kv in _arsenal) kv.Value.Tick(dt, kv.Key == active);
        }

        /// <summary>Suelta lo que se esté manteniendo (cambio de arma, menú, muerte).</summary>
        private void ReleaseArsenal()
        {
            if (!_arsenalHeld) return;
            _arsenalHeld = false;
            ArsenalFor(Weapon)?.OnRelease(BuildArsenalAim());
        }

        /// <summary>Cambia a la de reserva. Con el botón pulsado, suelta la actual y pulsa la nueva.</summary>
        private void Swap()
        {
            var inv = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            if (inv == null || inv.SecondWeapon == null) return;
            bool wasHeld = _arsenalHeld;
            ReleaseArsenal();
            CancelCharge();
            if (!inv.SwapWeapons()) return;
            LastSwapTime = Time.time;
            Swapped?.Invoke();
            var next = ArsenalFor(inv.Weapon);
            if (wasHeld && next != null) { next.OnPress(BuildArsenalAim()); _arsenalHeld = true; }
        }

        /// <summary>Instante (Time.time) del último cambio de arma: el Erizo del Abanico estalla al cambiar.</summary>
        public float LastSwapTime { get; private set; } = -99f;

        /// <summary>Se ha cambiado de arma.</summary>
        public event System.Action Swapped;

        /// <summary>Animación de ataque (para las armas del arsenal que lanzan algo).</summary>
        public void PlayAttackAnimation() { if (_animator != null) _animator.TriggerAttack(); }

        /// <summary>
        /// Boca y dirección hacia el cursor (o el stick / joystick táctil; sin apuntar, recto hacia donde mira). La boca
        /// sale del lado hacia el que se apunta, a la altura del pecho (<c>Base Shot ▸ Muzzle Offset</c> desde el centro del cuerpo).
        /// </summary>
        public ArsenalAim BuildArsenalAim()
        {
            Vector2 center = PlayerHit.BodyCenter(this);
            int facing = ResolveFacing();
            Vector2? cursor = ProjectileAim.MouseWorld();
            Vector2 target = cursor ?? center + new Vector2(facing * 20f, 0f);
            int side = Mathf.Abs(target.x - center.x) < 0.05f ? facing : (target.x > center.x ? 1 : -1);
            var weapon = Weapon;
            Vector2 off = weapon != null ? weapon.Shot.muzzleOffset : new Vector2(0.5f, 0.3f);
            Vector2 muzzle = center + new Vector2(off.x * side, off.y);
            Vector2 dir = target - muzzle;
            if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(side, 0f);
            return new ArsenalAim { origin = muzzle, dir = dir.normalized, target = target };
        }

        // ------------------------------------------------------------------ sin carga

        private void UpdateInstant(WeaponDefinition weapon)
        {
            bool held = (_attackAction != null && _attackAction.IsPressed()) || (useTouchButton && TouchInput.WeaponHeld);
            bool touch = useTouchButton && TouchInput.ConsumeWeapon();

            if ((held || touch) && _cooldownTimer <= 0f)
                Fire(weapon, 1f);
        }

        // ------------------------------------------------------------------ mantener para cargar

        private void UpdateCharge(WeaponDefinition weapon)
        {
            // El toque rápido (pulsar y soltar en el mismo frame) no carga nada: se descarta para que no
            // dispare luego si se cambia a un arma sin carga.
            if (useTouchButton) TouchInput.ConsumeWeapon();
            bool held = (_attackAction != null && _attackAction.IsPressed()) || (useTouchButton && TouchInput.WeaponHeld);

            if (held)
            {
                if (!_charging)
                {
                    _charging = true;
                    _charge = 0f;
                    if (weapon.IsComet) _cometCharge = CometChargeFx.Begin(CometRuntime.For(weapon), () => CometMuzzleWorld(weapon));
                    else _chargeFx = MakeChargeFx(weapon);
                    AudioManager.Instance?.Play(weapon.ChargeSound, transform.position);
                }

                _charge = Mathf.Min(weapon.Shot.chargeTime, _charge + Time.deltaTime);
                UpdateChargeFx(weapon);
                if (_cometCharge != null) _cometCharge.SetFraction(_charge / weapon.Shot.chargeTime);
                return;
            }

            if (!_charging) return;

            // Se soltó el botón: dispara si llegó al mínimo y no hay cooldown; si no, se cancela.
            float frac = Mathf.Clamp01(_charge / weapon.Shot.chargeTime);
            // El cometa sólo sale con la carga completa («listo»); soltar antes la cancela.
            float minCharge = weapon.IsComet ? 1f : weapon.Shot.minChargeToFire;
            bool canFire = frac >= minCharge && _cooldownTimer <= 0f;

            if (canFire && _cometCharge != null)
            {
                CancelPendingComet();
                _cometPending = _cometCharge;
                _cometCharge = null;
            }
            CancelCharge();

            if (canFire) Fire(weapon, frac);
        }

        private void CancelCharge()
        {
            _charging = false;
            _charge = 0f;
            if (_cometCharge != null) _cometCharge.Cancel();
            _cometCharge = null;
            if (_chargeFx != null)
            {
                if (_chargeFxPooled) Core.PrefabPool.Despawn(_chargeFx);
                else Destroy(_chargeFx);
            }
            _chargeFx = null;
            _chargeFxPooled = false;
        }

        private void CancelPendingComet()
        {
            if (_cometPending != null) _cometPending.Cancel();
            _cometPending = null;
        }

        private GameObject MakeChargeFx(WeaponDefinition weapon)
        {
            Vector2 pos = MuzzleWorld(weapon);

            // Arte del arma (BaseShot ▸ Arte ▸ Charge Vfx Prefab): un bucle pooled que crece con la carga.
            var prefab = weapon.Shot.chargeVfxPrefab;
            if (prefab != null)
            {
                var pooled = Core.PrefabPool.Spawn(prefab, pos, Quaternion.identity);
                if (pooled != null)
                {
                    _chargeFxPooled = true;
                    var art = pooled.GetComponentInChildren<SpriteRenderer>();
                    if (art != null) AbilityFx.CopySorting(art, gameObject);
                    return pooled;
                }
            }

            _chargeFxPooled = false;
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

            if (_chargeFxPooled)
            {
                // El prefab está autorizado a su tamaño de plena carga: el pool devuelve instancias
                // con la escala del uso anterior, así que se parte siempre de la del prefab.
                _chargeFx.transform.localScale = weapon.Shot.chargeVfxPrefab.transform.localScale * Mathf.Lerp(0.35f, 1f, frac);
                return;
            }

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

        /// <summary>
        /// Arranca el ataque: gasta el cooldown y lanza la animación <b>ya</b>, pero el disparo
        /// espera a <see cref="releaseDelay"/>.
        ///
        /// El cooldown se cuenta desde la pulsación, no desde que sale el tiro, para que retrasar
        /// el gesto no baje la cadencia del arma.
        /// </summary>
        private void Fire(WeaponDefinition weapon, float chargeFraction)
        {
            _cooldownTimer = weapon.BaseCooldown;
            _cooldownDuration = weapon.BaseCooldown;
            if (_animator != null) _animator.TriggerAttack();

            // El cometa sale casi al soltar: el orbe ya está cargado en la boca.
            float delay = weapon.IsComet ? CometRuntime.For(weapon).fireDelay : releaseDelay;
            if (delay <= 0f)
            {
                Release(chargeFraction);
                return;
            }

            StartCoroutine(ReleaseRoutine(chargeFraction, delay));
        }

        private IEnumerator ReleaseRoutine(float chargeFraction, float delay)
        {
            yield return new WaitForSeconds(delay);

            // Si mientras tanto se ha muerto o se ha abierto un menú, el gesto no llega a salir.
            if (!GameStateManager.CanPlayerAct || (_health != null && _health.IsDead))
            {
                CancelPendingComet();
                yield break;
            }

            Release(chargeFraction);
        }

        /// <summary>
        /// Suelta el disparo. El contexto se construye <b>aquí</b>, no al pulsar: la boca y el lado
        /// hacia el que sale son los del instante del gesto, así que girarse durante el ataque
        /// dispara hacia donde se mira, no hacia donde se miraba.
        /// </summary>
        private void Release(float chargeFraction)
        {
            if (_cometPending != null) _cometPending.Release();
            _cometPending = null;

            var loadout = WeaponLoadout.Instance;
            if (loadout == null || loadout.Inventory.Weapon == null) return;

            int facing = ResolveFacing();

            var args = SharedCastArgs;
            args.User = this;
            args.Facing = facing;
            args.Origin = PlayerHit.BodyCenter(this);
            args.DamageScale = 1f;
            args.Impact = null;
            args.ForceGilded = false;
            Casting?.Invoke(args);
            AudioManager.Instance?.Play(loadout.Inventory.Weapon.FireSound, transform.position);

            ShotResolver.Fire(loadout.Inventory, BuildContext(facing, args.DamageScale, args.Impact, args.ForceGilded),
                              chargeFraction);
        }

        private ShotContext BuildContext(int facing, float damageScale, ShotImpactHook impact, bool forceGilded = false)
        {
            return new ShotContext(gameObject, this, _health, hitLayers, facing,
                                   new Vector2(facing, 0f), gameObject.tag, damageScale, impact, forceGilded);
        }

        // ------------------------------------------------------------------ cooldown desde fuera

        /// <summary>Deja el arma lista para disparar ya (Bastón: la baja recarga el siguiente disparo).</summary>
        public void ResetCooldown() => _cooldownTimer = 0f;

        /// <summary>Resta <paramref name="seconds"/> al cooldown que esté corriendo (Reset 2).</summary>
        public void ReduceCooldown(float seconds)
        {
            if (seconds > 0f && _cooldownTimer > 0f) _cooldownTimer = Mathf.Max(0f, _cooldownTimer - seconds);
        }

        private Vector2 MuzzleWorld(WeaponDefinition weapon)
        {
            int facing = ResolveFacing();
            var offset = weapon.Shot.muzzleOffset;
            return (Vector2)transform.position + new Vector2(offset.x * facing, offset.y);
        }

        /// <summary>Boca del cometa: como <see cref="MuzzleWorld"/>, pero al lado del cursor con la mejora de apuntar con el ratón.</summary>
        private Vector2 CometMuzzleWorld(WeaponDefinition weapon)
        {
            var offset = weapon.Shot.muzzleOffset;
            int side = CometProjectile.MuzzleSide(CometRuntime.For(weapon), weapon.Shot.aimMode, transform.position, ResolveFacing());
            return (Vector2)transform.position + new Vector2(offset.x * side, offset.y);
        }

        private int ResolveFacing()
        {
            if (_movement != null) return _movement.Facing;
            if (_sprite != null) return _sprite.flipX ? -1 : 1;
            return 1;
        }

        // ------------------------------------------------------------------ vista previa (editor)

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var weapon = Application.isPlaying && Weapon != null ? Weapon : previewWeapon;
            if (weapon == null) return;

            if (!alwaysShowPreview && !PreviewSelected(weapon)) return;
            WeaponShotPreview.Draw(weapon, transform, ResolveFacingForPreview());
        }

        // Se ve al seleccionar el jugador (o algo suyo) o el asset del arma que se está afinando.
        private bool PreviewSelected(WeaponDefinition weapon)
        {
            if (UnityEditor.Selection.activeObject == weapon) return true;
            if (!previewWhenPlayerSelected) return false;
            foreach (var t in UnityEditor.Selection.transforms)
                if (t == transform || t.IsChildOf(transform)) return true;
            return false;
        }

        private int ResolveFacingForPreview()
        {
            if (Application.isPlaying) return ResolveFacing();
            var sprite = GetComponentInChildren<SpriteRenderer>();
            return sprite != null && sprite.flipX ? -1 : 1;
        }
#endif
    }
}
