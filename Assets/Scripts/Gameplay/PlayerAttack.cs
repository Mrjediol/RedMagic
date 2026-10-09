using System;
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
    /// Ataque cuerpo a cuerpo del jugador: un <b>combo</b> de golpes encadenados, con las tres
    /// fuentes de entrada (teclado / mando / táctil) igual que el movimiento.
    ///
    /// Cada golpe es una entrada de <see cref="combo"/> (o de <see cref="airCombo"/> en el aire):
    /// su tajo (<see cref="MeleeSlashFx"/>, sacado del pool), orientación, potencia visual, daño y
    /// tiempos. La zona de daño <b>es el propio tajo</b>: un enemigo recibe el golpe cuando el arco
    /// ya barrido lo alcanza (<see cref="MeleeSlashFx.Covers"/>), así lo que se ve es lo que pega.
    ///
    /// Encadenado: pulsar durante un golpe guarda la pulsación (buffer) y el siguiente sale en
    /// cuanto el actual permite cancelar (<see cref="Hit.recovery"/>). Si pasa
    /// <see cref="chainWindow"/> sin pulsar, el combo vuelve al primer golpe; tras el último hay
    /// <see cref="comboCooldown"/>. <see cref="UnlockedHits"/> limita cuántas entradas se usan
    /// (las builds pueden ampliar el combo de 3 a 5). Daño y alcance escalan con
    /// <see cref="PlayerStat.MeleeDamage"/> / <see cref="PlayerStat.MeleeRange"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public class PlayerAttack : MonoBehaviour, ISoundEventSource, ICooldownSource
    {
        [Serializable]
        public sealed class Hit
        {
            public string label = "Golpe";
            [Tooltip("Tajo que se ve (y define la zona de daño). Prefab con MeleeSlashFx.")]
            public MeleeSlashFx slash;
            [Tooltip("Rotación del tajo en grados, mirando a la derecha (se espeja al mirar a la izquierda).")]
            public float rotation;
            [Tooltip("Invierte el sentido del barrido (descendente ↔ ascendente).")]
            public bool reverse;
            [Tooltip("Tamaño/grosor del tajo (1 = el del prefab). También agranda la zona de daño.")]
            [Min(0.1f)] public float power = 1f;
            [Tooltip("Origen del tajo respecto al jugador, mirando a la derecha.")]
            public Vector2 offset = new Vector2(0.3f, 0.2f);
            [Tooltip("Escala X/Y del arco antes de rotar: (1,1) = círculo; (1.6, 0.7) = barrido horizontal ancho y aplanado.")]
            public Vector2 stretch = Vector2.one;

            [Tooltip("Multiplica el daño base.")]
            [Min(0f)] public float damageMultiplier = 1f;
            [Tooltip("Multiplica el retroceso que cada enemigo tiene en su Knockback (0 = no empuja).")]
            [Min(0f)] public float knockbackMultiplier = 1f;

            [Tooltip("Retardo desde que sale el golpe hasta que empieza el tajo.")]
            [Min(0f)] public float windup = 0.04f;
            [Tooltip("Desde que sale el golpe, cuándo se puede encadenar el siguiente.")]
            [Min(0f)] public float recovery = 0.22f;
        }

        /// <summary>OnAttack al iniciar cada golpe. Ver <see cref="SoundEmitter"/>.</summary>
        public event Action<SoundTrigger> SoundTriggered;

        /// <summary>Se dispara al salir cada golpe con su índice en el combo (0 = primero, -1 = golpe de dash).</summary>
        public event Action<int> HitStarted;

        public void DeclareSoundTriggers(List<SoundTrigger> into) { into.Add(SoundTrigger.OnAttack); }

        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string attackActionName = "Attack";

        [Header("Combo")]
        [Tooltip("Daño base de un golpe (cada golpe lo multiplica).")]
        [SerializeField] private float damage = 25f;
        [Tooltip("Golpes en el suelo, en orden.")]
        [SerializeField] private Hit[] combo = Array.Empty<Hit>();
        [Tooltip("Golpes en el aire. Vacío = usa los del suelo.")]
        [SerializeField] private Hit[] airCombo = Array.Empty<Hit>();
        [Tooltip("Cuántos golpes del combo están disponibles sin items (los items de build suman golpes extra).")]
        [Min(1)] [SerializeField] private int unlockedHits = 3;
        [Tooltip("Tiempo tras poder encadenar en el que todavía cuenta la pulsación para el siguiente golpe.")]
        [SerializeField] private float chainWindow = 0.35f;
        [Tooltip("Cuánto antes de poder encadenar se guarda una pulsación (buffer).")]
        [SerializeField] private float inputBuffer = 0.2f;
        [Tooltip("Enfriamiento tras el último golpe del combo.")]
        [SerializeField] private float comboCooldown = 0.35f;
        [Tooltip("Al salir un golpe, el tajo anterior se desvanece en este tiempo (no hay dos a la vez).")]
        [SerializeField] private float interruptFade = 0.06f;
        [Header("Dash")]
        [Tooltip("Golpe al atacar durante un dash (uno por dash). Sin tajo = el dash no ataca.")]
        [SerializeField] private Hit dashHit = new Hit();
        [Tooltip("Tras acabar el dash, durante cuánto todavía sale el golpe de dash.")]
        [SerializeField] private float dashGrace = 0.12f;

        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Depuración")]
        [Tooltip("Dibuja el alcance del golpe actual en la vista de escena.")]
        [SerializeField] private bool drawGizmo = true;

        private PlayerMovement _movement;
        private PlayerAnimator _animator;
        private Health _health;
        private InputAction _attackAction;

        private int _nextIndex;           // golpe que sale con la próxima pulsación
        private float _sinceHit = 999f;   // tiempo desde que salió el golpe actual
        private float _bufferTimer;       // >0 = hay una pulsación guardada
        private float _cooldownTimer;
        private Hit _current;
        private Vector2 _currentOffset;
        private int _currentFacing = 1;
        private float _sinceDash = 999f;
        private bool _dashHitUsed;
        private bool _finisherReady;
        private bool _slashStarted;
        private MeleeSlashFx _slash;

        private readonly List<Collider2D> _hits = new List<Collider2D>();
        private readonly HashSet<Health> _alreadyHit = new HashSet<Health>();
        private ContactFilter2D _filter;

        /// <summary>True mientras el tajo del golpe actual barre (puede hacer daño).</summary>
        public bool IsAttacking => _slash != null && _slash.IsSweeping;

        /// <summary>Golpes disponibles del combo. Las builds pueden subirlo hasta el tamaño de la lista.</summary>
        public int UnlockedHits
        {
            get => unlockedHits;
            set => unlockedHits = Mathf.Max(1, value);
        }

        /// <summary>
        /// Silencia el ataque básico sin desactivar el componente. Lo usa <c>WeaponUser</c>
        /// mientras hay un arma equipada.
        ///
        /// <b>No vale con poner <c>enabled = false</c></b>: los dos scripts sacan su acción de
        /// ataque del mismo <see cref="InputActionAsset"/>, así que comparten el <b>mismo</b>
        /// objeto <see cref="InputAction"/>. Al desactivar este componente, su <c>OnDisable</c>
        /// llamaba a <c>Disable()</c> sobre esa acción compartida y dejaba también al arma
        /// sin entrada.
        ///
        /// Silenciado no consume la cola táctil: esas pulsaciones son para quien esté al mando.
        /// </summary>
        public bool Suppressed { get; set; }

        // ------------------------------------------------------------------ HUD (ICooldownSource)

        public CooldownSlot Slot => CooldownSlot.Attack;

        /// <summary>La espada sólo ocupa la casilla de ataque cuando nadie la silencia (sin arma equipada).</summary>
        public bool CooldownActive => !Suppressed;

        public CooldownState Cooldown => new CooldownState { Remaining = Mathf.Max(0f, _cooldownTimer), Duration = comboCooldown };

        public Sprite CooldownIcon => null;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _animator = GetComponent<PlayerAnimator>();
            _health = GetComponent<Health>();

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = hitLayers,
                useTriggers = true
            };

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null)
                    _attackAction = map.FindAction(attackActionName, throwIfNotFound: false);
                else
                    Debug.LogWarning($"[PlayerAttack] No existe el action map '{actionMapName}' en {inputActions.name}.", this);
            }
        }

        private void OnEnable()
        {
            _attackAction?.Enable();
            if (_movement != null) { _movement.Dashed -= OnDashed; _movement.Dashed += OnDashed; }
            Parry.Succeeded -= OnParrySucceeded;
            Parry.Succeeded += OnParrySucceeded;
            _finisherReady = false;
            _sinceDash = 999f;
            _dashHitUsed = false;
            // Domain Reload desactivado: el estado del combo no sobrevive entre sesiones de Play.
            _nextIndex = 0;
            _sinceHit = 999f;
            _bufferTimer = 0f;
            _cooldownTimer = 0f;
            _current = null;
            _slash = null;
        }

        private void OnDisable()
        {
            _attackAction?.Disable();
            if (_movement != null) _movement.Dashed -= OnDashed;
            Parry.Succeeded -= OnParrySucceeded;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_cooldownTimer > 0f) _cooldownTimer -= dt;
            if (_bufferTimer > 0f) _bufferTimer -= dt;
            _sinceHit += dt;
            _sinceDash += dt;

            UpdateCurrentHit();

            if (Suppressed) return;

            // En pausa o muerto no se ataca, pero se vacía la cola táctil para que no se acumule.
            if (!GameStateManager.CanPlayerAct || (_health != null && _health.IsDead))
            {
                TouchInput.ConsumeAttack();
                _bufferTimer = 0f;
                return;
            }

            bool pressed = (_attackAction != null && _attackAction.WasPressedThisFrame()) || TouchInput.ConsumeAttack();
            if (pressed) _bufferTimer = inputBuffer;

            // Combo caducado: se vuelve al primer golpe (el remate tras parry no caduca).
            if (_current != null && _sinceHit > _current.recovery + chainWindow) _nextIndex = 0;

            if (_bufferTimer <= 0f) return;

            // Golpe en dash: uno por dash, cancela el golpe en curso, no espera al enfriamiento y no
            // toca el combo (1 · dash · 2 · 3 · dash · 4 · 5).
            bool inDash = _movement != null && (_movement.IsDashing || _sinceDash <= dashGrace);
            if (inDash && !_dashHitUsed && dashHit != null && dashHit.slash != null)
            {
                _bufferTimer = 0f;
                _dashHitUsed = true;


                StartHit(dashHit, -1, _nextIndex, setCooldown: false);   // no rompe el combo: sigue donde iba
                return;
            }

            if (_cooldownTimer > 0f && !_finisherReady) return;   // el remate tras parry no espera
            if (_current != null && _sinceHit < _current.recovery) return;   // todavía no se puede cancelar

            var list = ActiveList();
            int total = HitCount(list);
            if (total == 0) return;
            _bufferTimer = 0f;
            int index = _finisherReady ? total - 1 : (_nextIndex < total ? _nextIndex : 0);
            _finisherReady = false;
            bool last = index == total - 1;
            StartHit(list[index], index, last ? 0 : index + 1, setCooldown: last);
        }

        private Hit[] ActiveList()
        {
            bool air = _movement != null && !_movement.IsGrounded && airCombo.Length > 0;
            return air ? airCombo : combo;
        }

        /// <summary>Golpes usables de una lista: los desbloqueados + los que añaden los items.</summary>
        private int HitCount(Hit[] list) =>
            Mathf.Min(UnlockedHits + PlayerStats.ComboHitBonus, list.Length);


        /// <summary>Parry acertado: el siguiente golpe del combo es directamente el último (el remate).</summary>
        private void OnParrySucceeded(IncomingHit hit)
        {
            if (hit.Target == null || hit.Target != _health) return;
            _finisherReady = true;
        }
        private void OnDashed(int direction)
        {
            _sinceDash = 0f;
            _dashHitUsed = false;
        }

        private void StartHit(Hit hit, int index, int nextIndex, bool setCooldown)
        {
            _current = hit;
            _sinceHit = 0f;
            if (_slash != null) _slash.Interrupt(interruptFade);
            _slashStarted = false;
            _slash = null;
            _alreadyHit.Clear();

            _nextIndex = nextIndex;
            if (setCooldown) _cooldownTimer = comboCooldown + _current.recovery;

            if (_animator != null) _animator.TriggerAttack();
            SoundTriggered?.Invoke(SoundTrigger.OnAttack);
            HitStarted?.Invoke(index);

            if (_current.windup <= 0f) SpawnSlash();
        }

        private void UpdateCurrentHit()
        {
            if (_current == null) return;
            if (!_slashStarted && _sinceHit >= _current.windup) SpawnSlash();
            if (_slash == null) return;

            if (!_slash.isActiveAndEnabled) { _slash = null; return; }   // el tajo terminó y volvió al pool
            _slash.transform.position = SlashOrigin(_currentOffset, _currentFacing);   // sigue al jugador (dash, salto)
            if (_slash.IsSweeping) ApplyHits();
        }

        private void SpawnSlash()
        {
            _slashStarted = true;
            if (_current.slash == null) return;

            int facing = _currentFacing = Facing;
            _currentOffset = _current.offset;
            float rot = facing > 0 ? _current.rotation : -_current.rotation;
            var go = PrefabPool.Spawn(_current.slash.gameObject, SlashOrigin(_currentOffset, facing), Quaternion.Euler(0f, 0f, rot));
            _slash = go.GetComponent<MeleeSlashFx>();
            float range = PlayerStats.Multiplier(PlayerStat.MeleeRange);
            _slash.Play(_current.power * range, facing < 0, _current.reverse, _current.stretch);
        }

        private void ApplyHits()
        {
            Vector2 origin = _slash.transform.position;
            _hits.Clear();
            Physics2D.OverlapCircle(origin, _slash.CurrentWorldRadius * Mathf.Max(_current.stretch.x, _current.stretch.y) * 1.15f, _filter, _hits);

            float dmg = damage * _current.damageMultiplier * PlayerStats.Multiplier(PlayerStat.MeleeDamage);
            foreach (var hit in _hits)
            {
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                var target = hit.GetComponentInParent<Health>();
                if (target == null || target == _health || target.IsDead) continue;
                if (_alreadyHit.Contains(target)) continue;

                // Dentro del arco ya barrido: el punto del collider más cercano al origen (cuerpo
                // pegado al jugador) o su centro (cuerpo grande).
                if (!_slash.Covers(hit.ClosestPoint(origin)) && !_slash.Covers(hit.bounds.center)) continue;
                _alreadyHit.Add(target);   // un golpe por objetivo y por tajo

                // Por PlayerHit: los items "al matar" cuentan también las bajas de espada.
                // Devuelve false si no entró (i-frames): entonces tampoco se empuja.
                if (!Items.PlayerHit.Deal(target, dmg, transform.position, 0f, Items.HitKind.Melee)) continue;

                // El empujón va hacia donde mira el jugador, no "alejándose de su posición": con
                // el enemigo pegado encima ambas cosas se separan.
                if (_current.knockbackMultiplier > 0f)
                {
                    var knockback = target.GetComponent<Knockback>();
                    if (knockback != null) knockback.Apply(_currentFacing, _current.knockbackMultiplier);
                }
            }
        }

        private int Facing => _movement != null ? _movement.Facing : 1;

        private Vector2 SlashOrigin(Vector2 offset, int facing) =>
            (Vector2)transform.position + new Vector2(offset.x * facing, offset.y);

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo || combo == null) return;
            var list = combo;
            for (int i = 0; i < list.Length && i < unlockedHits; i++)
            {
                var h = list[i];
                if (h?.slash == null) continue;
                Gizmos.color = Color.Lerp(new Color(1f, 0.8f, 0.2f, 0.5f), new Color(1f, 0.3f, 0.2f, 0.5f), i / 4f);
                Vector2 o = (Vector2)transform.position + new Vector2(h.offset.x * Facing, h.offset.y);
                Gizmos.DrawWireSphere(o, 2.5f * h.power);
            }
        }
    }
}
