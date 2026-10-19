using System;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Gameplay;
using RedMagic.Items;
using UnityEngine;
using static RedMagic.Arsenal.ArsenalDraw;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// <b>4 · Media luna</b> (<c>g-media-luna.js</c>, ficha <c>docs/armas/media-luna.md</c>): bumerán de una sola hoja que corta a
    /// la ida y a la vuelta. Evoluciones: <b>Sierra</b> (se queda triturando), <b>Satélite</b> (vive en órbita y se lanza sin
    /// recarga), <b>Malabar</b> (atrapada perfecta con racha; pulsar antes de tiempo es fallo).
    /// </summary>
    [Serializable]
    public sealed class MoonKit : ArsenalKit
    {
        public const int Sierra = 1, Satelite = 2, Malabar = 3;

        private static readonly ArsenalTree TreeData = new ArsenalTree("Bumerán de luz: corta a la ida y a la vuelta; mientras está fuera no se puede volver a lanzar.")
            .Add("Sierra", "Se queda girando donde toca al primer enemigo (o en el extremo) y lo tritura; vuelve al pulsar otra vez o sola al rato.",
                 "Tirón", "A la vuelta arrastra hacia ti a los que corta.",
                 "Desgarro", "Cada corte seguido al mismo enemigo hace más daño.",
                 "Imán", "Mientras tritura, atrae hacia la sierra a los enemigos cercanos.")
            .Add("Satélite", "La hoja vive girando a tu alrededor y corta lo que se acerca; al lanzarla vuelve a la órbita, sin recarga.",
                 "Doble", "Dos hojas en órbita; cada clic lanza una.",
                 "Luna creciente", "Cada vuelta en órbita sin lanzarla la hace crecer y suma daño al próximo lanzamiento.",
                 "Escudo", "Las hojas en órbita destruyen las balas enemigas que tocan.")
            .Add("Malabar", "Atrapada perfecta: pulsa justo cuando vuelve y sale otra vez al instante, más rápida y fuerte (racha de hasta 3). Pulsar antes de tiempo es fallo.",
                 "Filo creciente", "Con cada punto de racha la hoja es más grande y corta más lejos.",
                 "Eco", "Cada atrapada perfecta deja una hoja fantasma que repite el lanzamiento anterior.",
                 "Gran final", "Con la racha llena, la siguiente atrapada perfecta lanza una hoja enorme (golpe fuerte) y la racha vuelve a empezar.");

        public override ArsenalTree Tree => TreeData;
        public override string WeaponId => "moon";
        public override ArsenalController CreateController() => new MoonController();

        [Header("Base")]
        [Tooltip("Daño por pasada.")] public float damage = 18f;
        [Tooltip("Alcance (u).")] public float range = 9f;
        [Tooltip("Ida (s).")] public float outTime = 0.55f;
        [Tooltip("Pausa en el extremo (s).")] public float hoverTime = 0.14f;
        [Tooltip("Vuelta (s).")] public float backTime = 0.48f;
        [Tooltip("Empujón (× el del enemigo).")] public float knockback = 0.55f;
        [Tooltip("Radio de golpe (u).")] public float hitRadius = 0.55f;
        [Tooltip("Recarga tras atraparla (s).")] public float cooldown = 0.15f;
        [Header("Elemento (build)")]
        [Tooltip("Hielo: congelación en la ida (la vuelta rompe).")] public float iceFreeze = 0.5f;
        [Tooltip("Fuego: duración de la estela (s).")] public float fireLife = 0.75f;
        [Tooltip("Fuego: quemadura (s).")] public float fireBurn = 2f;
        [Header("Sierra")]
        [Tooltip("Tiempo triturando si no la llamas (s).")] public float sawTime = 2.5f;
        [Tooltip("Cada cuánto corta (s).")] public float sawTick = 0.15f;
        [Tooltip("Daño por corte.")] public float sawDamage = 5f;
        [Tooltip("Tirón: velocidad del arrastre (u/s).")] public float yankSpeed = 10.5f;
        [Tooltip("Desgarro: + daño por corte seguido.")] public float rendStep = 0.15f;
        [Tooltip("Desgarro: + daño máximo.")] public float rendMax = 1.5f;
        [Tooltip("Imán: radio (u).")] public float magnetRadius = 4.25f;
        [Tooltip("Imán: fuerza (u/s).")] public float magnetSpeed = 2.75f;
        [Header("Satélite")]
        [Tooltip("Radio de la órbita (u).")] public float satRadius = 1.75f;
        [Tooltip("Tiempo por vuelta (s).")] public float satLap = 0.9f;
        [Tooltip("Daño en órbita (×).")] public float satMul = 0.6f;
        [Tooltip("Luna creciente: + daño por vuelta.")] public float waxStep = 0.25f;
        [Tooltip("Luna creciente: + daño máximo.")] public float waxMax = 1.5f;
        [Header("Malabar")]
        [Tooltip("Ventana antes de llegar (s).")] public float perfectWindow = 0.2f;
        [Tooltip("Margen después de atraparla (s).")] public float perfectLate = 0.1f;
        public int rachaMax = 3;
        [Tooltip("+ daño por punto de racha.")] public float jugDmg = 0.35f;
        [Tooltip("− tiempo de vuelo por punto de racha.")] public float jugSpeed = 0.12f;
        [Tooltip("Filo creciente: + tamaño por punto de racha.")] public float edgeGrow = 0.25f;
        [Tooltip("Eco: daño de la hoja fantasma (×).")] public float echoMul = 0.6f;
        [Tooltip("Gran final: × daño.")] public float finaleMul = 3f;
        [Tooltip("Gran final: tamaño.")] public float finaleSize = 2.2f;

        public bool Tiron => Up(Sierra, 0);
        public bool Desgarro => Up(Sierra, 1);
        public bool Iman => Up(Sierra, 2);
        public bool Doble => Up(Satelite, 0);
        public bool Creciente => Up(Satelite, 1);
        public bool Escudo => Up(Satelite, 2);
        public bool Filo => Up(Malabar, 0);
        public bool Eco => Up(Malabar, 1);
        public bool GranFinal => Up(Malabar, 2);
    }

    public sealed class MoonController : ArsenalController<MoonKit>
    {
        private float _cd, _caughtAt = -9f;
        private int _racha;
        private bool _fumbled;
        private readonly List<MoonBlade> _out = new List<MoonBlade>(), _sats = new List<MoonBlade>();
        private ArsenalAim _lastAim, _prevThrow; private bool _hasPrev;

        public ArsenalAim CurrentAim => User != null ? User.BuildArsenalAim() : _lastAim;
        public Vector2 Muzzle => CurrentAim.origin;
        public bool Fumbled => _fumbled;

        public struct Opt { public bool mirror, sat, ghost, giant; public int satIdx, satN; public float dmgMul, speed, size; }

        private Opt JugStats(MoonKit k) => new Opt { dmgMul = 1f + k.jugDmg * _racha, speed = 1f - k.jugSpeed * _racha, size = 1f + (k.Filo ? k.edgeGrow * _racha : 0f) };

        private void Throw(ArsenalAim aim, Opt o)
        {
            User?.PlayAttackAnimation();
            int n = ArsenalBuild.Copies; _out.Clear();
            for (int c = 0; c < n; c++) { var oo = o; oo.mirror = c > 0; var b = new MoonBlade(this, aim, oo); _out.Add(b); ArsenalWorld.Add(b); }
            ArsenalSounds.Play("moon_throw", aim.origin);
            _prevThrow = aim; _hasPrev = true; _fumbled = false;
        }

        /// <summary>Una hoja normal ha vuelto a la mano.</summary>
        public void OnCaught(MoonBlade b)
        {
            _out.Remove(b);
            if (_out.Count > 0) return;
            _cd = K.cooldown; _caughtAt = _fumbled ? -9f : ArsenalWorld.T; _fumbled = false;   // tras un fallo no hay margen tardío
        }

        public override void OnPress(ArsenalAim aim)
        {
            var k = K; _lastAim = aim;
            if (k.Evo(MoonKit.Sierra) && _out.Count > 0) { foreach (var b in _out) b.Recall(); return; }
            if (k.Evo(MoonKit.Satelite))
            {
                foreach (var s in _sats) if (s.InOrbit) { s.Launch(aim); User?.PlayAttackAnimation(); ArsenalSounds.Play("moon_throw", aim.origin); break; }
                return;
            }
            if (k.Evo(MoonKit.Malabar))
            {
                bool inWin = _out.Count > 0 && _out.TrueForAll(b => b.InBackPhase && b.Remain < k.perfectWindow);
                bool late = _out.Count == 0 && ArsenalWorld.T - _caughtAt < k.perfectLate;
                if ((inWin || late) && !_fumbled)
                {   // atrapada perfecta: sale otra vez al instante
                    foreach (var b in _out) b.CatchNow();
                    _out.Clear(); _caughtAt = -9f;
                    bool finale = k.GranFinal && _racha >= k.rachaMax;
                    _racha = finale ? 0 : Mathf.Min(k.rachaMax, _racha + 1);
                    Vector2 lp = ArsenalCombat.PlayerCenter() + Vector2.up * 1.6f;
                    if (finale) ArsenalCombat.Label(lp, "arsenal.moon.finale", ArsenalPal.Gold.light);
                    else ArsenalCombat.Label(lp, "arsenal.moon.streak", ArsenalPal.Gold.light, _racha);
                    ArsenalSounds.Play("moon_perfect", aim.origin);
                    if (k.Eco && _hasPrev) { var g = JugStats(k); g.ghost = true; g.dmgMul *= k.echoMul; ArsenalWorld.Add(new MoonBlade(this, _prevThrow, g)); }
                    Throw(aim, finale ? new Opt { dmgMul = k.finaleMul, size = k.finaleSize, giant = true, speed = 1f } : JugStats(k));
                    return;
                }
                if (_out.Count > 0)
                {   // antes de tiempo = fallo (no se puede spamear hasta acertar)
                    if (!_fumbled) { _fumbled = true; _racha = 0; ArsenalCombat.Label(ArsenalCombat.PlayerCenter() + Vector2.up * 1.6f, "arsenal.moon.fumble", new Color(1f, 0.54f, 0.54f)); ArsenalSounds.Play("moon_fumble", aim.origin); }
                    return;
                }
                if (_cd > 0f) return;
                _racha = 0; Throw(aim, JugStats(k)); return;
            }
            if (_cd > 0f || _out.Count > 0) return;
            Throw(aim, new Opt { dmgMul = 1f, speed = 1f, size = 1f });
        }

        public override void Tick(float dt, bool current)
        {
            var k = K;
            _cd -= dt;
            if (k.Evo(MoonKit.Satelite))
            {   // las hojas viven en órbita (también con la otra arma en la mano)
                int want = (k.Doble ? 2 : 1) * ArsenalBuild.Copies;
                _sats.RemoveAll(b => b.Dead || !b.InWorld);
                if (_sats.Count != want)
                {
                    foreach (var b in _sats) b.Kill();
                    _sats.Clear();
                    for (int i = 0; i < want; i++) { var b = new MoonBlade(this, CurrentAim, new Opt { sat = true, satIdx = i, satN = want, dmgMul = 1f, speed = 1f, size = 1f }); _sats.Add(b); ArsenalWorld.Add(b); }
                }
            }
            else if (_sats.Count > 0) { foreach (var b in _sats) b.Kill(); _sats.Clear(); }
            if (k.Evo(MoonKit.Malabar) && _racha > 0 && _out.Count == 0 && ArsenalWorld.T - _caughtAt > k.perfectLate)
            {
                _racha = 0; ArsenalCombat.Label(ArsenalCombat.PlayerCenter() + Vector2.up * 1.6f, "arsenal.moon.streak_lost", new Color(0.6f, 0.64f, 0.71f));
            }
        }

        public override float Ready
        {
            get
            {
                if (K.Evo(MoonKit.Satelite)) { if (_sats.Count == 0) return 1f; int n = 0; foreach (var s in _sats) if (s.InOrbit) n++; return n / (float)_sats.Count; }
                return _out.Count > 0 ? 0f : 1f - Mathf.Clamp01(_cd / Mathf.Max(0.01f, K.cooldown));
            }
        }

        public override void Refund(float seconds) => _cd -= seconds;

        public override void Dispose()
        {
            foreach (var b in _sats) b.Kill();
            foreach (var b in _out) b.Kill();
            _sats.Clear(); _out.Clear();
        }
    }

    /// <summary>
    /// Hoja. Fases: ida → pausa en el extremo → vuelta → atrapada; Sierra: tritura antes de volver; Satélite: gira alrededor del
    /// player y vuelve a la órbita en vez de atraparse.
    /// </summary>
    public sealed class MoonBlade : ArsenalEntity
    {
        private enum Phase { Out, Hover, Back, Saw, Orbit }

        private readonly MoonController _ctrl;
        private readonly MoonKit _k;
        private readonly MoonController.Opt _o;
        private readonly ArsenalElement _el;
        private readonly bool _saw, _sat, _fire, _ice;
        private readonly ArsenalPal _basePal;
        private readonly float _speed;
        private Phase _phase;
        private float _pt, _up, _wax, _dmgMul, _sawT, _lastFire = -9f, _sfxGrind = -9f;
        private int _lap = -1;
        private bool _recalled;
        private Vector2 _origin, _dir, _perp, _apex, _from, _p, _last, _sawPos, _sawOff;
        private Body _sawE;
        private float _depth;
        private readonly HashSet<Health> _hits = new HashSet<Health>(), _yanked = new HashSet<Health>();
        private readonly Dictionary<Health, int> _rend = new Dictionary<Health, int>();
        private readonly HashSet<Health> _now = new HashSet<Health>();
        private readonly List<(Vector2 p, float t, float r)> _hist = new List<(Vector2, float, float)>(40);
        private readonly List<Collider2D> _overlap = new List<Collider2D>();
        private SoundLoop _loop;

        public bool Dead { get; private set; }
        public bool InOrbit => _phase == Phase.Orbit;
        public bool InBackPhase => _phase == Phase.Back;
        public float Remain => _phase == Phase.Back ? _pt + BackT - T : 9f;

        private float T => ArsenalWorld.T;
        private float OutT => _k.outTime * _speed;
        private float HoverT => _k.hoverTime * _speed;
        private float BackT => _k.backTime * _speed;
        private float Size => (_o.size <= 0f ? 1f : _o.size) * (1f + _wax * 0.25f);
        private ArsenalPal Pal => _wax > 0f || _o.giant ? ArsenalBuild.ElemPal(ArsenalPal.Gold) : _basePal;

        public MoonBlade(MoonController ctrl, ArsenalAim aim, MoonController.Opt o)
        {
            _ctrl = ctrl; _k = (MoonKit)ctrl.KitBase; _o = o; _el = ArsenalBuild.Element;
            _fire = _el == ArsenalElement.Fire; _ice = _el == ArsenalElement.Ice;
            _saw = _k.Evo(MoonKit.Sierra) && !o.ghost; _sat = o.sat;
            _basePal = ArsenalBuild.ElemPal(_k.Evo(MoonKit.Malabar) || (_k.Evo(MoonKit.Satelite) && _k.Doble) ? ArsenalPal.Gold : ArsenalPal.Set);
            _speed = o.speed <= 0f ? 1f : o.speed; _dmgMul = o.dmgMul <= 0f ? 1f : o.dmgMul;
            _phase = _sat ? Phase.Orbit : Phase.Out; _pt = T;
            SetDir(aim.origin, aim.dir);
            _up *= o.mirror ? -3f : 1f;
            _p = _origin; _last = _p;
            if (_sat) { _p = OrbitPos(out _); _lap = Mathf.FloorToInt(T / _k.satLap); }
            else Spin();
        }

        private void SetDir(Vector2 origin, Vector2 dir)
        {
            _origin = origin; _dir = dir; _perp = new Vector2(dir.y, -dir.x);
            _up = _perp.y < 0f ? -1f : 1f;           // el arco hacia arriba
        }

        private void Go(Phase p) { _phase = p; _pt = T; _hits.Clear(); }

        private void Spin()
        {
            if (_o.mirror || _o.ghost || _loop.IsValid || _ctrl.User == null) return;
            _loop = ArsenalSounds.StartLoop("moon_spin", _ctrl.User);
        }

        private void Unspin() { ArsenalSounds.StopLoop(_loop); _loop = default; }

        public void Recall() { if (_phase == Phase.Saw || _phase == Phase.Out || _phase == Phase.Hover) _recalled = true; }

        /// <summary>Satélite: sale desde donde está hacia donde apuntas.</summary>
        public void Launch(ArsenalAim aim)
        {
            if (_phase != Phase.Orbit) return;
            SetDir(_p, aim.dir); _dmgMul = 1f + _wax; Go(Phase.Out); Spin();
        }

        public void CatchNow() { Dead = true; Unspin(); MoonDraw.CatchFx(_ctrl, Pal); }
        public void Kill() { Dead = true; Unspin(); }
        public override void OnRemoved() => Unspin();

        private Vector2 OrbitPos(out float depth)
        {
            float a = T * TAU / _k.satLap + _o.satIdx * TAU / Mathf.Max(1, _o.satN);
            depth = -Mathf.Sin(a);   // mitad de abajo = delante del player
            return ArsenalCombat.PlayerCenter() + new Vector2(Mathf.Cos(a) * _k.satRadius, Mathf.Sin(a) * _k.satRadius * 0.32f);
        }

        public override bool Tick(float dt)
        {
            if (Dead) return false;
            if (_sat && (_ctrl.User != null && !_ctrl.User.isActiveAndEnabled)) { Kill(); return false; }
            if (!Pos(dt, out var p)) return false;
            _p = p;
            _hist.Insert(0, (p, T, Rot())); if (_hist.Count > 40) _hist.RemoveAt(40);
            float r = _k.hitRadius * Size;
            if (_phase == Phase.Saw) Grind(dt, r);
            else
            {
                var list = ArsenalTargets.Alive();
                for (int i = 0; i < list.Count; i++)
                {   // golpes: una vez por pasada (en órbita, una por vuelta)
                    var e = list[i];
                    if (_hits.Contains(e.health)) continue;
                    float x = (p.x - e.c.x) / (e.rx + r), y = (p.y - e.c.y) / (e.ry + r * 0.6f);
                    if (x * x + y * y >= 1f) continue;
                    _hits.Add(e.health);
                    HitBody(e, _phase == Phase.Orbit ? _k.satMul : _dmgMul);
                    if (_phase == Phase.Back && _k.Evo(MoonKit.Sierra) && _k.Tiron) _yanked.Add(e.health);
                    if (_saw && _phase == Phase.Out) { _sawE = e; _sawOff = (p - e.c) * 0.5f; Go(Phase.Saw); break; }   // Sierra: se queda en el primero
                }
            }
            if (_phase == Phase.Back && _yanked.Count > 0)
            {   // Tirón: los cortados a la vuelta vienen detrás de la hoja (y siguen un poco tras atraparla)
                Vector2 pc = ArsenalCombat.PlayerCenter();
                foreach (var e in ArsenalTargets.Alive())
                    if (_yanked.Contains(e.health) && Mathf.Abs(e.c.x - pc.x) > 1.75f) ArsenalCombat.Pull(e, p, _k.yankSpeed, e.flies);
            }
            if (_phase == Phase.Orbit && _k.Escudo) Shield(p, r);
            float gap = _phase == Phase.Orbit ? 0.07f : _phase == Phase.Saw ? 0.05f : 0.028f;
            if (_fire && T - _lastFire > gap) { _lastFire = T; ArsenalWorld.Add(FireBit.Get(p, _k.fireLife, _k.fireBurn)); }
            _last = p;
            return !Dead;
        }

        private void HitBody(Body e, float mul)
        {
            Vector2 d = _p - _last; bool back = _phase == Phase.Back; float k = Mathf.Sign(d.x); if (k == 0f) k = Mathf.Sign(e.c.x - ArsenalCombat.PlayerCenter().x);
            ArsenalCombat.Hit(e, new ArsenalHit
            {
                dmg = _k.damage * mul, from = e.c - new Vector2(k, 0f), knock = _k.knockback * (_o.giant ? 2f : 1f), elem = _el, kind = HitKind.Projectile,
                burn = _fire ? _k.fireBurn : 0f, freeze = _ice && !back ? _k.iceFreeze : 0f,
                heavy = _o.giant || (_ice && back), shake = _o.giant ? 0.2f : 0.08f, hitstop = _o.giant ? 0.06f : 0.03f,
            });
            MoonDraw.SlashFx(e.c, d.x < 0f, Pal, Size);
            if (!_o.mirror && !_o.ghost) ArsenalSounds.Play("moon_hit", e.c);
        }

        /// <summary>Satélite · Escudo: rompe las balas enemigas que toca.</summary>
        private void Shield(Vector2 p, float r)
        {
            var player = _ctrl.User != null ? _ctrl.User.GetComponent<Health>() : null;
            _overlap.Clear();
            Physics2D.OverlapCircle(p, r + 0.15f, new ContactFilter2D { useTriggers = true, useLayerMask = false }, _overlap);
            foreach (var col in _overlap)
            {
                if (col == null || !col.TryGetComponent(out Projectile bullet)) continue;
                if (player != null && !bullet.CanHit(player)) continue;
                Vector2 at = bullet.transform.position; bullet.Deflect();
                var P = Pal; float seed = Mathf.Floor(at.x * 31f);
                ArsenalWorld.AddFx(0.4f, (l, f0) => Sparks(l, ArsenalWorld.T, f0, at, new SparkOpt { n = 8, pal = P, sp = 220, seed = seed, len = 10, life = 0.3f, a0 = 0, a1 = TAU, grav = 520, w = 2.6f, backShare = 0.45f }));
                ArsenalSounds.Play("moon_block", at);
            }
        }

        // posición según la fase (y cambios de fase); false = se acabó
        private bool Pos(float dt, out Vector2 q)
        {
            switch (_phase)
            {
                case Phase.Out:
                {
                    float u = Seg(T, _pt, _pt + OutT), d = _k.range * EaseOut(u), a = 14f * PX * Mathf.Sin(Mathf.PI * u) * _up;
                    q = _origin + _dir * d + _perp * a;
                    if (ArsenalTargets.InGround(q)) u = 1f;          // no atraviesa el terreno: se para donde topa
                    if (u >= 1f || _recalled)
                    {
                        _apex = q;
                        if (_saw && !_recalled) { _sawPos = q; Go(Phase.Saw); }
                        else if (_recalled) { _from = q; Go(Phase.Back); }
                        else Go(Phase.Hover);
                    }
                    return true;
                }
                case Phase.Hover:
                    if (T - _pt >= HoverT || _recalled) { _from = _apex; Go(Phase.Back); }
                    q = _apex + new Vector2(Mathf.Sin((T - _pt) * 12f) * 2f * PX, Mathf.Sin((T - _pt) * 9f) * 2f * PX);
                    return true;
                case Phase.Saw:
                {
                    q = _sawE != null && !_sawE.Dead ? (ArsenalTargets.Of(_sawE.health)?.c ?? _sawE.c) + _sawOff : _sawPos;
                    if (_sawE != null && _sawE.Dead) { _sawPos = q; _sawE = null; }
                    if (T - _pt >= _k.sawTime || _recalled) { _from = q; Go(Phase.Back); if (!_o.mirror) ArsenalSounds.Play("moon_throw", q); }
                    return true;
                }
                case Phase.Back:
                {
                    float u = EaseIn(Seg(T, _pt, _pt + BackT)), a = 6f * PX * Mathf.Sin(Mathf.PI * u) * _up;
                    Vector2 tg = _sat ? OrbitPos(out _) : _o.ghost ? _origin : _ctrl.Muzzle;
                    if (u >= 1f) return Arrive(tg, out q);
                    q = Vector2.Lerp(_from, tg, u) + _perp * a;
                    return true;
                }
                default:
                {   // órbita: una pasada por vuelta; Luna creciente carga mientras no se lanza
                    q = OrbitPos(out _depth);
                    int lap = Mathf.FloorToInt(T / _k.satLap);
                    if (lap != _lap)
                    {
                        if (_lap >= 0) { _hits.Clear(); if (_k.Creciente && _wax < _k.waxMax) _wax = Mathf.Min(_k.waxMax, _wax + _k.waxStep); }
                        _lap = lap;
                    }
                    return true;
                }
            }
        }

        private bool Arrive(Vector2 tg, out Vector2 q)
        {
            q = tg;
            if (_sat) { Go(Phase.Orbit); _lap = Mathf.FloorToInt(T / _k.satLap); _wax = 0f; _dmgMul = 1f; _recalled = false; Unspin(); if (!_o.mirror) ArsenalSounds.Play("moon_catch", tg); q = OrbitPos(out _depth); return true; }
            Dead = true; Unspin();
            if (_o.ghost) return false;
            _ctrl.OnCaught(this);
            if (!_o.mirror) { ArsenalSounds.Play("moon_catch", tg); MoonDraw.CatchFx(_ctrl, Pal); }
            return false;
        }

        /// <summary>Sierra: corta cada <c>sawTick</c> a todo lo que toca; Imán atrae; Desgarro sube con los cortes seguidos.</summary>
        private void Grind(float dt, float r)
        {
            var list = ArsenalTargets.Alive();
            if (_k.Iman) foreach (var e in list) if (e != _sawE && Vector2.Distance(e.c, _p) < _k.magnetRadius) ArsenalCombat.Pull(e, _p, _k.magnetSpeed);
            _sawT -= dt; if (_sawT > 0f) return;
            _sawT = _k.sawTick;
            _now.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                float x = (_p.x - e.c.x) / (e.rx + r + 0.15f), y = (_p.y - e.c.y) / (e.ry + r * 0.6f + 0.15f);
                if (x * x + y * y >= 1f) continue;
                _now.Add(e.health);
                int n = _k.Desgarro ? (_rend.TryGetValue(e.health, out int v) ? v : 0) + 1 : 0; _rend[e.health] = n;
                float mul = 1f + Mathf.Min(_k.rendMax, Mathf.Max(0, n - 1) * _k.rendStep);
                ArsenalCombat.Hit(e, new ArsenalHit { dmg = _k.sawDamage * mul, elem = _el, kind = HitKind.Projectile, freeze = _ice ? _k.iceFreeze * 0.25f : 0f, burn = _fire ? _k.fireBurn : 0f });
                Vector2 sp = _p; var P = Pal; float sd = Mathf.Floor(T * 1000f);
                ArsenalWorld.AddFx(0.4f, (l, f0) => Sparks(l, ArsenalWorld.T, f0, sp, new SparkOpt { n = 4, pal = P, sp = 260, seed = sd, len = 12, life = 0.3f, a0 = 0, a1 = TAU, grav = 520, w = 2.6f, backShare = 0.45f }));
            }
            var keys = new List<Health>(_rend.Keys);
            foreach (var h in keys) if (!_now.Contains(h)) _rend.Remove(h);   // Desgarro: sólo cortes seguidos
            if (_now.Count > 0 && !_o.mirror && T - _sfxGrind > 0.3f) { _sfxGrind = T; ArsenalSounds.Play("moon_grind", _p); }
        }

        private float Rot() => (T - _pt) * (_phase == Phase.Saw ? 46f : 29f) + _o.satIdx * 1.3f + (_o.mirror ? 2f : 0f);

        public override void Draw(L l)
        {
            if (Dead && _phase != Phase.Orbit) return;
            bool behind = _phase == Phase.Orbit && _depth < 0f; L lay = behind ? L.Back : L.Front;
            float rot = Rot(), R = 21f * PX * Size, ga = _o.ghost ? 0.45f : 1f; var P = Pal;
            if (l == L.Back)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {   // estelas en espiral de las dos puntas (posición de cada punta en los últimos 0,075 s)
                    var pts = TempPoints();
                    foreach (var h in _hist) { if (T - h.t > 0.075f) break; pts.Add(h.p + Dir(h.r + sgn * 1.9f) * R); }
                    if (pts.Count > 1)
                    {
                        Ribbon(l, pts, s => 6f * PX * Mathf.Pow(1f - s, 1.3f), P.dark, 0.35f * ga);
                        Ribbon(l, pts, s => 3.2f * PX * (1f - s), P.mid, 0.85f * ga);
                        Ribbon(l, pts, s => 1.3f * PX * (1f - s), P.core, ga);
                    }
                }
            if (l != lay) return;
            for (int k = 3; k >= 1; k--) { if (_hist.Count > k * 2) { var h = _hist[k * 2]; MoonDraw.Crescent(l, h.p, R * (1f - k * 0.06f), h.r, P, (0.18f / k + 0.06f) * ga); } }
            for (int k = 1; k <= 2; k++) MoonDraw.Crescent(l, _p, R, rot - k * 0.32f, P, 0.22f / k * ga);
            MoonDraw.Crescent(l, _p, R, rot, P, ga);
            if (l == L.Front && _phase == Phase.Saw) Glow(_p, R * 1.7f, P.g2, 0.2f + 0.1f * Mathf.Sin(T * 50f));
            if (l == L.Front && _k.Evo(MoonKit.Malabar) && !_o.ghost && !_o.mirror && Remain < _k.perfectWindow && !_ctrl.Fumbled)
            {   // Malabar: ¡ahora!
                float pu = 0.6f + 0.4f * Mathf.Sin(T * 45f); var gp = ArsenalPal.Gold; Vector2 m = _ctrl.Muzzle;
                Glow(_p, R * 2.4f, gp.g2, 0.7f * pu); Glow(m, 36f * PX, gp.g1, 0.6f * pu); Star4(l, m, 22f * PX * pu, T * 6f, gp.core, 1f);
            }
        }
    }

    /// <summary>Trozo de estela de fuego colgado en el aire: quema a lo que lo toca (Media luna con Fuego). Pool propio.</summary>
    public sealed class FireBit : ArsenalEntity
    {
        private static readonly Stack<FireBit> Pool = new Stack<FireBit>(64);
        private Vector2 _p; private float _t0, _life, _burn, _tick, _ox, _h, _w; private bool _back, _ember; private float _seed;

        public static FireBit Get(Vector2 p, float life, float burn)
        {
            var b = Pool.Count > 0 ? Pool.Pop() : new FireBit();
            b._p = p; b._t0 = ArsenalWorld.T; b._life = life; b._burn = burn; b._tick = 0f; b._seed = Mathf.Floor(b._t0 * 1000f);
            var R = new Rng(b._seed); b._ox = (R.Next() - 0.5f) * 10f * PX; b._back = R.Next() < 0.5f; b._h = (16f + R.Next() * 12f) * PX; b._w = (5f + R.Next() * 3f) * PX; b._ember = R.Next() < 0.4f;
            return b;
        }

        public override void OnRemoved() => Pool.Push(this);

        public override bool Tick(float dt)
        {
            _tick -= dt;
            if (_tick <= 0f)
            {
                _tick = 0.3f;
                foreach (var e in ArsenalTargets.Alive())
                    if (Mathf.Abs(e.c.x - _p.x) < e.rx + 0.15f && Mathf.Abs(e.c.y - _p.y) < e.ry + 0.25f)
                        ArsenalCombat.Hit(e, new ArsenalHit { dmg = 2f, elem = ArsenalElement.Fire, burn = _burn, dot = true, kind = HitKind.Burst });
            }
            return ArsenalWorld.T - _t0 < _life;
        }

        public override void Draw(L l)
        {
            if ((_back ? L.Back : L.Front) != l) return;
            float age = ArsenalWorld.T - _t0, a = (1f - age / _life) * Seg(age, 0f, 0.05f);
            Flame(l, _p + new Vector2(_ox, -10f * PX), _h * a, _w * Mathf.Max(0.4f, a), ArsenalWorld.T, _seed, a);
            if (_ember) Disc(l, _p + new Vector2(_ox * 2f, age * 40f * PX), 1.6f * PX, ArsenalPal.Fire.light, a);
            if (l == L.Front) Glow(_p, 14f * PX, ArsenalPal.Fire.g1, 0.25f * a);
        }
    }

    /// <summary>Dibujo de la Media luna (<c>fx.js ▸ crescentBlade</c>, tajos, atrapar).</summary>
    public static class MoonDraw
    {
        private static readonly List<Vector2> Outer = new List<Vector2>(40), Inner = new List<Vector2>(40);

        /// <summary>Media luna en 4 capas (morado → núcleo claro) + filo blanco, centrada en <paramref name="p"/>, girada <paramref name="rot"/>.</summary>
        public static void Crescent(L l, Vector2 p, float R, float rot, ArsenalPal pal, float a)
        {
            if (a <= 0f) return;
            Glow(p, R * 1.7f, pal.g1, 0.3f * a, l);
            Layer(l, p, R * 1.18f, 0.32f, rot, pal.dark, a * 0.45f);
            Layer(l, p, R * 1.0f, 0.36f, rot, pal.deep, a * 0.9f);
            Layer(l, p, R * 0.92f, 0.42f, rot, pal.mid, a);
            Layer(l, p, R * 0.86f, 0.5f, rot, pal.light, a);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            var pts = TempPoints();
            for (int i = 0; i <= 16; i++) { float th = Mathf.Lerp(-1.6f, 1.6f, i / 16f); Vector2 q = Dir(th) * R * 0.93f; pts.Add(p + new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs)); }
            Solid(l).Stroke(pts, 2f * PX, A(Color.white, a));
        }

        // media luna: arco exterior (-1,95..1,95) menos un círculo desplazado hacia dentro (tira de triángulos entre los dos arcos)
        private static void Layer(L l, Vector2 p, float R, float th, float rot, Color c, float a)
        {
            const int N = 18; float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot), ri = R * (1f - th * 0.55f);
            Outer.Clear(); Inner.Clear();
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N;
                Vector2 o = Dir(Mathf.Lerp(-1.95f, 1.95f, u)) * R, n = new Vector2(R * th, 0f) + Dir(Mathf.Lerp(-1.75f, 1.75f, u)) * ri;
                Outer.Add(p + new Vector2(o.x * cs - o.y * sn, o.x * sn + o.y * cs));
                Inner.Add(p + new Vector2(n.x * cs - n.y * sn, n.x * sn + n.y * cs));
            }
            var cv = Solid(l); Color col = A(c, a);
            for (int i = 0; i < N; i++) { cv.Triangle(Outer[i], Outer[i + 1], Inner[i], col); cv.Triangle(Outer[i + 1], Inner[i + 1], Inner[i], col); }
        }

        /// <summary>Tajo diagonal que se abre y se afina + chispas.</summary>
        public static void SlashFx(Vector2 h, bool back, ArsenalPal P, float sc)
        {
            float seed = Mathf.Floor(h.x * 1000f);
            ArsenalWorld.AddFx(0.5f, (l, th) =>
            {
                float u = Seg(ArsenalWorld.T, th, th + 0.22f), len = 70f * PX * sc * EaseOut(Seg(u, 0f, 0.35f)), a = 1f - Seg(u, 0.4f, 1f), ang = (back ? -1f : 1f) * 0.55f;
                if (l == L.Front && u < 1f)
                {
                    Vector2 d = Dir(ang) * len * 0.5f;
                    Needle(l, h - d, h + d, 9f * PX * sc * (1f - u * 0.7f), P.mid, a);
                    Needle(l, h - d, h + d, 3.5f * PX * sc * (1f - u * 0.6f), P.core, a);
                    Glow(h, 40f * PX * sc, P.g1, 0.6f * (1f - u));
                }
                float a0 = back ? Mathf.PI : 0f;
                Sparks(l, ArsenalWorld.T, th, h, new SparkOpt { n = 9, a0 = a0 - 0.7f, a1 = a0 + 0.7f, sp = 300, pal = P, seed = seed, len = 14, life = 0.35f, grav = 520, w = 2.6f, backShare = 0.45f });
            });
        }

        /// <summary>Destello en el cristal al atrapar la hoja.</summary>
        public static void CatchFx(MoonController ctrl, ArsenalPal pal)
        {
            ArsenalWorld.AddFx(0.25f, (l, tc) =>
            {
                if (l != L.Front || ctrl == null) return;
                Vector2 m = ctrl.Muzzle; float u = Seg(ArsenalWorld.T, tc, tc + 0.22f);
                Glow(m, 34f * PX, pal.g2, 0.8f * (1f - u)); Star4(l, m, 20f * PX * (1f + u * 0.4f), 0.6f, pal.core, 1f - u);
                float r = (10f + 24f * EaseOut(u)) * PX;
                EllArc(l, m, r, r, 0f, TAU, 2f * PX, pal.light, 1f - u, new ArcOpt { m = 32, noTaper = true, seed = 1 });
            });
        }
    }
}
