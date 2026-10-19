using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Estados elementales de un enemigo (regla común de las armas, nunca de una arma concreta): <b>congelación</b> acumulada
    /// (barra 0..1 que se vacía sola; llena = <b>congelado</b> unos segundos: no se mueve, no ataca, no le empujan),
    /// <b>quemado</b> (daño cada pocos segundos) y <b>aturdido</b> (Vapor). No se pone en ningún prefab: <see cref="Of"/> lo
    /// añade la primera vez. Mientras está congelado o aturdido mantiene activo su <see cref="Knockback"/> con velocidad
    /// cero, que es la señal que la IA ya respeta para no tocar la velocidad; <c>EnemyBrain</c> además no decide ni ataca
    /// (<see cref="Disables"/>). El tinte va por <see cref="HitFlash.SetStatusTint"/>; el bloque de hielo, la barra y las
    /// llamas los dibuja <see cref="ArsenalStatusDrawer"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElementStatus : MonoBehaviour
    {
        private static readonly List<ElementStatus> ActiveList = new List<ElementStatus>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveList.Clear();

        public static IReadOnlyList<ElementStatus> Active => ActiveList;

        private static readonly Color FrozenTint = new Color(0.55f, 0.9f, 1f, 0.75f), BurnTint = new Color(1f, 0.55f, 0.2f, 0.45f);

        public Health Health { get; private set; }
        public Knockback Knockback { get; private set; }
        private Rigidbody2D _rb;
        private HitFlash _flash;

        /// <summary>Barra de congelación 0..1.</summary>
        public float Freeze { get; private set; }
        public float FrozenRemaining { get; private set; }
        public float BurnRemaining { get; private set; }
        public float StunRemaining { get; private set; }
        /// <summary>Instante (tiempo del arsenal) en que se congeló: para la animación del bloque de hielo.</summary>
        public float FrozenAt { get; private set; }

        private float _burnTick;
        private bool _listed;
        private float _tintShown = -1f;

        public bool IsFrozen => FrozenRemaining > 0f;
        public bool IsBurning => BurnRemaining > 0f;
        public bool HasIce => Freeze > 0f || IsFrozen;

        /// <summary>El estado de <paramref name="h"/> (lo crea si hace falta), o null si no vale.</summary>
        public static ElementStatus Of(Health h)
        {
            if (h == null || h.IsDead) return null;
            if (!h.TryGetComponent(out ElementStatus s)) s = h.gameObject.AddComponent<ElementStatus>();
            return s;
        }

        public static ElementStatus Peek(Component c) => c != null && c.TryGetComponent(out ElementStatus s) ? s : null;

        /// <summary>true si <paramref name="mover"/> está congelado o aturdido: su IA no decide ni ataca.</summary>
        public static bool Disables(Component mover)
        {
            var s = Peek(mover);
            return s != null && (s.IsFrozen || s.StunRemaining > 0f);
        }

        private void Awake()
        {
            Health = GetComponent<Health>();
            Knockback = GetComponent<Knockback>();
            _rb = GetComponentInParent<Rigidbody2D>();
            _flash = GetComponentInChildren<HitFlash>();
            if (Health != null) Health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Died -= OnDied;
            ActiveList.Remove(this);
        }

        private void OnDisable() => Clear();

        private void OnDied() => Clear();

        /// <summary>Quita todo (muerte, reaparición).</summary>
        public void Clear()
        {
            Freeze = 0f; FrozenRemaining = 0f; BurnRemaining = 0f; StunRemaining = 0f;
            Unlist();
            Tint();
        }

        /// <summary>Suma congelación; al llenarse congela (devuelve true si se ha congelado ahora).</summary>
        public bool AddFreeze(float amount, float frozenTime)
        {
            if (amount <= 0f || IsFrozen) return false;
            Freeze = Mathf.Min(1f, Freeze + amount);
            List();
            if (Freeze < 1f) { Tint(); return false; }
            FrozenRemaining = frozenTime;
            FrozenAt = ArsenalWorld.T;
            Hold();
            Tint();
            return true;
        }

        /// <summary>Descongela (deshielo, añicos).</summary>
        public void Thaw() { FrozenRemaining = 0f; Freeze = 0f; Tint(); }

        public void Burn(float seconds)
        {
            if (seconds <= 0f) return;
            BurnRemaining = Mathf.Max(BurnRemaining, seconds);
            _burnTick = Mathf.Min(_burnTick, 0.1f);
            List();
            Tint();
        }

        public void Extinguish() { BurnRemaining = 0f; Tint(); }

        public void Stun(float seconds)
        {
            if (seconds <= 0f) return;
            StunRemaining = Mathf.Max(StunRemaining, seconds);
            List();
            Hold();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Health == null || Health.IsDead) return;
            var rules = ArsenalAssets.Load();
            float decay = rules != null ? rules.freezeDecay : 0.18f;

            if (IsFrozen) { FrozenRemaining -= dt; if (FrozenRemaining <= 0f) { FrozenRemaining = 0f; Freeze = 0f; } }
            else if (Freeze > 0f) Freeze = Mathf.Max(0f, Freeze - dt * decay);
            if (StunRemaining > 0f) StunRemaining -= dt;

            if (IsBurning)
            {
                BurnRemaining -= dt; _burnTick -= dt;
                if (_burnTick <= 0f)
                {
                    _burnTick = rules != null ? rules.burnTickEvery : 0.35f;
                    ArsenalCombat.Hit(ArsenalTargets.Of(Health), new ArsenalHit { dmg = rules != null ? rules.burnTickDamage : 4f, elem = ArsenalElement.None, dot = true, kind = Items.HitKind.Burst, noReact = true });
                }
            }

            if (IsFrozen || StunRemaining > 0f) Hold();
            if (!IsFrozen && StunRemaining <= 0f && !IsBurning && Freeze <= 0f) Unlist();
            Tint();
        }

        /// <summary>Quieto: un empujón de velocidad cero (la IA se aparta mientras dura); los de suelo siguen cayendo.</summary>
        private void Hold()
        {
            if (Knockback == null) return;
            float vy = _rb != null && _rb.gravityScale > 0.001f ? Mathf.Min(0f, _rb.linearVelocity.y) : 0f;
            Knockback.ApplyVelocity(new Vector2(0f, vy), 0.12f);
        }

        private void Tint()
        {
            if (_flash == null) return;
            float w = IsFrozen ? 1f : Freeze > 0f ? Freeze * 0.6f : IsBurning ? 0.6f : 0f;
            if (Mathf.Approximately(w, _tintShown)) return;
            _tintShown = w;
            _flash.SetStatusTint(IsBurning && !HasIce ? BurnTint : FrozenTint, w);
        }

        private void List() { if (!_listed) { ActiveList.Add(this); _listed = true; } }
        private void Unlist() { if (_listed) { ActiveList.Remove(this); _listed = false; } }
    }
}
