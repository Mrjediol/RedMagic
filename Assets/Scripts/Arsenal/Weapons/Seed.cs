using System;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Items;
using UnityEngine;
using static RedMagic.Arsenal.ArsenalDraw;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// <b>5 · Semilla estelar</b> (<c>g-semilla.js</c>, ficha <c>docs/armas/semilla.md</c>): se lanza en parábola hacia el ratón
    /// (cae en el suelo bajo él), se arma y estalla en una columna de luz. Evoluciones: <b>Campo de minas</b> (esperan a que
    /// llegue alguien), <b>Racimo</b> (suelta semillas pequeñas), <b>Madura</b> (mantener para que crezca en la mano).
    /// </summary>
    [Serializable]
    public sealed class SeedKit : ArsenalKit
    {
        public const int CampoMinas = 1, Racimo = 2, Madura = 3;

        private static readonly ArsenalTree TreeData = new ArsenalTree("Mina que se lanza en parábola, se clava en el suelo y estalla en una columna de luz.")
            .Add("Campo de minas", "Se arma y espera a que un enemigo se acerque (hasta 3 a la vez; la más vieja estalla al poner otra).",
                 "Reacción en cadena", "Una mina que estalla hace estallar a las cercanas, y cada una de la cadena hace más daño.",
                 "Raíces", "Al activarse, unas raíces sujetan a los de alrededor y estalla un poco después.",
                 "Semillero", "Cuanto más tiempo lleva puesta una mina, más crece: más daño y más radio.")
            .Add("Racimo", "Estallido menor que suelta semillas pequeñas que estallan en cadena.",
                 "Semillas vivas", "Las semillas pequeñas saltan hacia los enemigos cercanos.",
                 "Generaciones", "Cada semilla pequeña suelta a su vez otras dos aún más pequeñas.",
                 "Siembra", "Las semillas pequeñas que no dan a nadie se quedan como minas un rato.")
            .Add("Madura", "Mantén para que la semilla madure en la mano: más daño, radio y alcance; suelta para lanzarla. Verde es más débil.",
                 "Supernova", "Madura del todo, el estallido se repite dos veces más.",
                 "Implosión", "Antes de estallar atrae a los enemigos cercanos (más cuanto más madura).",
                 "Cosecha", "Cada baja de una semilla deja la siguiente ya medio madura.");

        public override ArsenalTree Tree => TreeData;
        public override string WeaponId => "seed";
        public override ArsenalController CreateController() => new SeedController();

        [Header("Base")]
        [Tooltip("Recarga (s).")] public float cooldown = 0.8f;
        [Tooltip("Alcance horizontal (u).")] public float range = 10.5f;
        [Tooltip("Vuelo (s).")] public float flightTime = 0.5f;
        [Tooltip("Armado hasta estallar (s).")] public float armTime = 0.62f;
        public float damage = 45f;
        [Tooltip("Radio (u).")] public float radius = 1.75f;
        [Tooltip("Empujón (× el del enemigo).")] public float knockback = 1.1f;
        [Tooltip("Lanza hacia arriba (u/s).")] public float launch = 10.5f;
        [Header("Elemento y Duplicador (build)")]
        [Tooltip("Hielo: radio de la nova (u).")] public float novaRadius = 3.25f;
        [Tooltip("Fuego: duración del charco (s).")] public float puddleTime = 1.5f;
        [Tooltip("Fuego: quemadura (s).")] public float puddleBurn = 2f;
        [Tooltip("Duplicador: separación de las dos semillas (u).")] public float dupSpread = 1.125f;
        [Header("Campo de minas")]
        [Tooltip("Radio de disparo (u).")] public float proxRadius = 1.15f;
        public int maxMines = 3;
        [Tooltip("Reacción: radio (u).")] public float chainRadius = 3.75f;
        [Tooltip("Reacción: + daño por mina de la cadena.")] public float chainBonus = 0.3f;
        [Tooltip("Raíces: radio (u).")] public float rootRadius = 2.5f;
        [Tooltip("Raíces: espera hasta estallar (s).")] public float rootDelay = 0.6f;
        [Tooltip("Semillero: s hasta crecer del todo.")] public float sproutTime = 4f;
        [Tooltip("Semillero: + daño al máximo.")] public float sproutMul = 1f;
        [Tooltip("Semillero: + radio al máximo.")] public float sproutSize = 0.5f;
        [Header("Racimo")]
        public int clusterCount = 3;
        [Tooltip("Daño de cada una.")] public float clusterDamage = 22f;
        [Tooltip("Radio (u).")] public float clusterRadius = 1.2f;
        [Tooltip("Semillas vivas: alcance (u).")] public float seekRange = 6f;
        [Tooltip("Generaciones: semillas por semilla.")] public int genCount = 2;
        [Tooltip("Generaciones: daño.")] public float genDamage = 12f;
        [Tooltip("Siembra: duración de la mina (s).")] public float plantLife = 4f;
        [Tooltip("Siembra: radio de disparo (u).")] public float plantRadius = 1f;
        [Header("Madura")]
        [Tooltip("s hasta madurar del todo.")] public float matureTime = 1.2f;
        [Tooltip("× daño sin madurar.")] public float ripeMin = 0.5f;
        [Tooltip("× daño madura.")] public float ripeMax = 1.8f;
        [Tooltip("× alcance madura.")] public float ripeRange = 1.5f;
        [Tooltip("Supernova: tiempo entre estallidos (s).")] public float superGap = 0.3f;
        [Tooltip("Supernova: × daño de cada réplica.")] public float superMul = 0.6f;
        [Tooltip("Implosión: radio (u).")] public float implodeRadius = 4.5f;
        [Tooltip("Implosión: + s de armado atrayendo.")] public float implodeTime = 0.45f;
        [Tooltip("Implosión: fuerza (u/s).")] public float implodeSpeed = 8f;
        [Tooltip("Cosecha: madurez con la que empieza tras una baja.")] public float harvestRipe = 0.5f;

        public bool Cadena => Up(CampoMinas, 0);
        public bool Raices => Up(CampoMinas, 1);
        public bool Semillero => Up(CampoMinas, 2);
        public bool Vivas => Up(Racimo, 0);
        public bool Generaciones => Up(Racimo, 1);
        public bool Siembra => Up(Racimo, 2);
        public bool Supernova => Up(Madura, 0);
        public bool Implosion => Up(Madura, 1);
        public bool Cosecha => Up(Madura, 2);
    }

    public sealed class SeedController : ArsenalController<SeedKit>
    {
        private float _cd, _cdFull = 1f;
        private bool _harvest;
        private SeedInHand _hand;
        private readonly List<StarSeed> _mines = new List<StarSeed>();
        private ArsenalAim _lastAim;

        public ArsenalAim CurrentAim => User != null ? User.BuildArsenalAim() : _lastAim;
        public void Harvested() => _harvest = true;

        private void Lob(ArsenalAim aim, float ripe = -1f)
        {
            var k = K; _cd = _cdFull = k.cooldown; User?.PlayAttackAnimation();
            if (k.Evo(SeedKit.CampoMinas))
            {
                _mines.RemoveAll(m => m.Gone);
                var live = _mines.FindAll(m => !m.Gone);
                while (live.Count >= k.maxMines) { live[0].Detonate(); live.RemoveAt(0); }
            }
            float reach = k.range * (ripe >= 0f ? Mathf.Lerp(1f, k.ripeRange, ripe) : 1f);
            float tx = Mathf.Clamp(aim.target.x, aim.origin.x - reach, aim.origin.x + reach); int n = ArsenalBuild.Copies;
            for (int c = 0; c < n; c++)
            {
                var s = new StarSeed(this, aim.origin, tx + (n > 1 ? (c == 1 ? 1f : -1f) * k.dupSpread : 0f), ripe, _mines);
                ArsenalWorld.Add(s); _mines.Add(s);
            }
            ArsenalSounds.Play("seed_lob", aim.origin);
        }

        // Madura: la semilla crece en la mano mientras mantienes; al soltar (o cambiar de arma) se lanza
        private void ThrowHand(ArsenalAim aim)
        {
            if (_hand == null) return;
            float r = _hand.Ripe; _hand.Gone = true; _hand = null;
            Lob(aim, r);
        }

        public override void OnPress(ArsenalAim aim)
        {
            _lastAim = aim;
            if (_cd > 0f || _hand != null) return;
            var k = K;
            if (k.Evo(SeedKit.Madura))
            {
                float r0 = k.Cosecha && _harvest ? k.harvestRipe : 0f; _harvest = false;
                _hand = new SeedInHand(this, r0); ArsenalWorld.Add(_hand);
                return;
            }
            Lob(aim);
        }

        public override void OnHold(ArsenalAim aim, float dt) { _lastAim = aim; _hand?.Grow(dt); }
        public override void OnRelease(ArsenalAim aim) => ThrowHand(aim);

        public override void Tick(float dt, bool current)
        {
            _cd -= dt;
            if (_hand != null && !current) ThrowHand(CurrentAim);
            _mines.RemoveAll(m => m.Gone);
        }

        public override float Ready => _hand != null ? _hand.Ripe : 1f - Mathf.Clamp01(_cd / Mathf.Max(0.01f, _cdFull));
        public override void Refund(float seconds) => _cd -= seconds;
        public override void Dispose() { if (_hand != null) _hand.Gone = true; _hand = null; }
    }

    /// <summary>Madura: semilla en la mano que crece (anillo de madurez alrededor del cristal; dorada y con un tic al llenarse).</summary>
    public sealed class SeedInHand : ArsenalEntity
    {
        private readonly SeedController _ctrl;
        private float _held;
        private bool _full;
        public bool Gone;

        public SeedInHand(SeedController ctrl, float r0) { _ctrl = ctrl; _held = r0 * ((SeedKit)ctrl.KitBase).matureTime; _full = r0 >= 1f; }

        public float Ripe => Mathf.Clamp01(_held / Mathf.Max(0.01f, ((SeedKit)_ctrl.KitBase).matureTime));

        public void Grow(float dt)
        {
            _held += dt;
            if (!_full && Ripe >= 1f) { _full = true; ArsenalSounds.Play("seed_ripe", _ctrl.CurrentAim.origin); }
        }

        public override bool Tick(float dt) => !Gone;

        public override void Draw(L l)
        {
            if (l != L.Front) return;
            Vector2 m = _ctrl.CurrentAim.origin; float r = Ripe; var P = r >= 1f ? ArsenalPal.Gold : ArsenalBuild.ElemPal(ArsenalPal.Set);
            float pu = r >= 1f ? 0.8f + 0.2f * Mathf.Sin(ArsenalWorld.T * 30f) : 1f;
            Glow(m, (20f + 30f * r) * PX * pu, P.g1, 0.6f);
            EllArc(l, m, 18f * PX, 18f * PX, Mathf.PI * 0.5f, Mathf.PI * 0.5f - TAU * r, 2.4f * PX, P.light, 0.9f, new ArcOpt { m = 32, noTaper = true, seed = 1 });
            SeedDraw.StarSeed(l, m + new Vector2(0f, 2f * PX), ArsenalWorld.T * (4f + 10f * r), P, 1f, 0.5f + 0.7f * r);
        }
    }

    /// <summary>Semilla lanzada: vuela en parábola, se clava, se arma (pulsos) y estalla; Campo de minas espera a que llegue alguien.</summary>
    public sealed class StarSeed : ArsenalEntity
    {
        private enum State { Fly, Armed, Done }

        private readonly SeedController _ctrl;
        private readonly SeedKit _k;
        private readonly List<StarSeed> _mines;
        private readonly ArsenalElement _el;
        private readonly bool _cluster, _prox, _ice, _fire, _rooting;
        private readonly float _ripe, _t0, _tl, _apex, _armT, _tx;
        private readonly Vector2 _from;
        private readonly float _gy;
        private readonly ArsenalPal _pal;
        private State _state = State.Fly;
        private float _armedAt = -1f, _trigger = -1f;
        private int _chainN;
        private bool _boomNow;
        private readonly List<float> _ticks = new List<float>(3);
        private readonly List<(Body e, float x)> _rooted = new List<(Body, float)>();
        private Vector2 _p;

        public bool Gone { get; private set; }
        public bool Armed => _state == State.Armed;
        public float X => _tx;

        public StarSeed(SeedController ctrl, Vector2 from, float tx, float ripe, List<StarSeed> mines)
        {
            _ctrl = ctrl; _k = (SeedKit)ctrl.KitBase; _mines = mines; _el = ArsenalBuild.Element; _ice = _el == ArsenalElement.Ice; _fire = _el == ArsenalElement.Fire;
            _cluster = _k.Evo(SeedKit.Racimo); _prox = _k.Evo(SeedKit.CampoMinas); _rooting = _prox && _k.Raices;
            _ripe = ripe; _from = from; _tx = tx; _p = from;
            _t0 = ArsenalWorld.T; _tl = _t0 + _k.flightTime;
            _gy = ArsenalTargets.GroundY(tx, from.y + 6f);
            _apex = (60f + Mathf.Abs(tx - from.x) / PX * 0.28f) * PX;
            _armT = _k.armTime + (ripe >= 0f && _k.Implosion ? _k.implodeTime : 0f);   // Implosión: atrae un rato más antes de estallar
            _pal = ArsenalBuild.ElemPal(ripe >= 1f ? ArsenalPal.Gold : ArsenalPal.Set);
            if (ripe >= 0f && _k.Implosion) ArsenalWorld.Add(new Implode(this, 0.4f + 0.6f * ripe));
        }

        private float T => ArsenalWorld.T;

        private Vector2 PosAt(float tt)
        {
            float u = Seg(tt, _t0, _tl);
            return new Vector2(Mathf.Lerp(_from.x, _tx, u), Mathf.Lerp(_from.y, _gy + 4f * PX, u) + 4f * _apex * u * (1f - u));
        }

        public void Detonate(int n = 0) { if (_state != State.Armed && _state != State.Fly) return; _chainN = n; _boomNow = true; }

        // multiplicadores: Madura (madurez), Semillero (tiempo puesta), Reacción (orden en la cadena)
        private float GrownK => _prox && _k.Semillero && _armedAt >= 0f ? Mathf.Clamp01((T - _armedAt) / _k.sproutTime) : 0f;
        private float Mul => (_ripe >= 0f ? Mathf.Lerp(_k.ripeMin, _k.ripeMax, _ripe) : 1f) * (1f + _k.sproutMul * GrownK) * (1f + _k.chainBonus * _chainN);
        private float RMul => (_ripe >= 0f ? Mathf.Sqrt(Mathf.Lerp(_k.ripeMin, _k.ripeMax, _ripe)) : 1f) * (1f + _k.sproutSize * GrownK);

        private bool FeetNear(Body e, float maxGap) => (e.c.y - e.ry) - _gy < maxGap;

        public override bool Tick(float dt)
        {
            if (_state == State.Fly)
            {
                _p = PosAt(T);
                if (T >= _tl)
                {
                    _state = State.Armed; _armedAt = T; _p = new Vector2(_tx, _gy + 5f * PX);
                    ArsenalSounds.Play("seed_land", _p);
                    var P = _pal; float x = _tx, gy = _gy;
                    ArsenalWorld.AddFx(0.6f, (l, tl) => { Dust(l, ArsenalWorld.T, tl, x, gy, 4, 5, 22f, 0.6f); if (l == L.Front) { float u = Seg(ArsenalWorld.T, tl, tl + 0.15f); if (u < 1f) Star4(l, new Vector2(x, gy + 4f * PX), 18f * PX * (1f - u * 0.5f), 0f, P.core, 1f - u); } });
                    _ticks.Clear();
                    if (_prox) _ticks.Add(_armedAt + 0.12f); else { _ticks.Add(_armedAt + _armT * 0.24f); _ticks.Add(_armedAt + _armT * 0.55f); _ticks.Add(_armedAt + _armT * 0.79f); }
                    for (int i = 0; i < _ticks.Count; i++) { float tt = _ticks[i]; string id = _prox ? "seed_armed" : "seed_tick" + i; ArsenalWorld.Add(new Delayed(tt - T, () => { if (_state == State.Armed) ArsenalSounds.Play(id, _p); })); }
                }
                if (_boomNow && _state == State.Fly) return true;   // aún en el aire: estalla al caer
                return true;
            }
            if (_state == State.Armed)
            {
                bool boom = false;
                if (_prox)
                {
                    if (_trigger < 0f && T > _armedAt + 0.12f)
                        foreach (var e in ArsenalTargets.Alive())
                            if (Mathf.Abs(e.c.x - _tx) < _k.proxRadius + e.rx * 0.5f && FeetNear(e, 1f))
                            {
                                _trigger = T; ArsenalSounds.Play("seed_trigger", _p);
                                if (_rooting)
                                {
                                    _rooted.Clear();
                                    foreach (var r in ArsenalTargets.Alive()) if (Mathf.Abs(r.c.x - _tx) < _k.rootRadius && FeetNear(r, 1.5f)) _rooted.Add((r, r.c.x));
                                    ArsenalSounds.Play("seed_roots", _p);
                                }
                                break;
                            }
                    if (_trigger >= 0f && T - _trigger >= (_rooting ? _k.rootDelay : 0.12f)) boom = true;
                    if (T - _armedAt > 20f) boom = true;
                }
                else if (T - _armedAt >= _armT) boom = true;
                foreach (var (e, x) in _rooted) if (!e.Dead) { e.knockback?.ApplyVelocity(Vector2.zero, 0.1f); ElementStatus.Of(e.health)?.Stun(0.1f); }   // Raíces: no se mueven
                if (_boomNow) boom = true;
                if (!boom) return true;
                Explode(1f, false);
                if (_ripe >= 1f && _k.Supernova) for (int k = 1; k <= 2; k++) ArsenalWorld.Add(new Delayed(_k.superGap * k, () => Explode(_k.superMul, true)));
                if (_prox && _k.Cadena && _mines != null)
                {   // Reacción en cadena: las minas cercanas estallan detrás
                    foreach (var m in _mines) if (m != this && m.Armed && Mathf.Abs(m.X - _tx) < _k.chainRadius) { int n = _chainN + 1; var mm = m; ArsenalWorld.Add(new Delayed(0.12f, () => mm.Detonate(n))); }
                    if (_chainN > 0) ArsenalCombat.Label(new Vector2(_tx, _gy + 3f), "arsenal.seed.chain", ArsenalPal.Gold.light, _chainN + 1);
                }
                _state = State.Done; Gone = true; return false;
            }
            return false;
        }

        private void Explode(float k, bool echo)
        {
            float x = _tx, cy = _gy + 0.75f, M = Mul * k, RM = RMul;
            ArsenalHit Hit(ArsenalHit h) { h.onKill = _ => _ctrl.Harvested(); return h; }
            if (_ice)
            {
                float nr = _k.novaRadius * RM;
                ArsenalCombat.Area(new Vector2(x, cy), nr, Hit(new ArsenalHit { dmg = _k.damage * 0.5f * M, knock = 0.3f, elem = ArsenalElement.Ice, freeze = 1f, shake = 0.15f, hitstop = echo ? 0f : 0.05f, kind = HitKind.Burst }));
                float gy = _gy; ArsenalWorld.AddFx(1.6f, (l, tb) => FrostNova(l, ArsenalWorld.T, tb, x, gy, nr));
                ArsenalSounds.Play("seed_nova", new Vector2(x, cy));
                return;
            }
            float sc = (_cluster ? 0.7f : 1.1f) * RM;
            var blasted = ArsenalCombat.Area(new Vector2(x, cy), _k.radius * RM * (_cluster ? 0.75f : 1f), Hit(new ArsenalHit
            {
                dmg = _k.damage * M * (_cluster ? 0.6f : 1f), knock = _k.knockback, launch = _k.launch, heavy = true, elem = _el, kind = HitKind.Burst,
                burn = _fire ? _k.puddleBurn : 0f, shake = _cluster ? 0.12f : 0.2f, hitstop = echo ? 0f : 0.06f,
            }));
            if (_fire && !echo) ArsenalWorld.Add(new FireZone(x, _gy, _k.radius * 0.9f * RM, _k.puddleTime, _k.puddleBurn));
            var P = _pal; float g = _gy;
            ArsenalWorld.AddFx(1.4f, (l, tb) => SeedDraw.Eruption(l, ArsenalWorld.T, tb, x, g, sc, P));
            ArsenalSounds.Play(_cluster ? "seed_split" : "seed_boom", new Vector2(x, cy));
            if (_cluster && !echo)
            {
                var used = new HashSet<Health>(); foreach (var b in blasted) used.Add(b.health);   // Semillas vivas: a por los que no tocó el estallido
                for (int i = 0; i < _k.clusterCount; i++)
                {
                    var R = new Rng(i + Mathf.Floor(T * 100f)); int side = i % 2 == 1 ? 1 : -1;
                    float x1 = x + side * (50f + R.Next() * 120f) * PX;
                    if (_k.Vivas) { var e = ArsenalTargets.Nearest(new Vector2(x, cy), _k.seekRange, used); if (e != null) { used.Add(e.health); x1 = e.c.x + (R.Next() - 0.5f) * 10f * PX; } }
                    ArsenalWorld.Add(new Bomblet(_ctrl, x, x1, _gy, 0.28f + i * 0.07f, P, _k.Generaciones, false));
                }
            }
        }

        public override void Draw(L l)
        {
            float t = T;
            if (_state == State.Fly)
            {
                if (l == L.Back)
                {
                    var pts = TempPoints(); for (int i = 0; i <= 10; i++) pts.Add(PosAt(Mathf.Max(_t0, t - 0.09f * i / 10f)));
                    Ribbon(l, pts, s => 8f * PX * Mathf.Pow(1f - s, 1.2f), _pal.dark, 0.35f);
                    Ribbon(l, pts, s => 4f * PX * (1f - s), _pal.light, 0.9f);
                }
                SeedDraw.StarSeed(l, _p, t * 14f, _pal, 1f, _ripe >= 0f ? 0.6f + 0.7f * _ripe : 1f);
                return;
            }
            if (_state != State.Armed) return;
            float blink = 0f; foreach (var tt in _ticks) blink = Mathf.Max(blink, t >= tt ? 1f - Seg(t, tt, tt + 0.16f) : 0f);
            bool armed = _prox && t > _armedAt + 0.12f; float gk = GrownK;
            if (l == L.Front)
            {
                int spikes = 3 + Mathf.RoundToInt(gk * 4);
                for (int i = 0; i < spikes; i++) { float s = i - (spikes - 1) * 0.5f; Needle(l, new Vector2(_tx, _gy + 2f * PX), new Vector2(_tx + s * 7f * PX, _gy + (16f + (s == 0 ? 8f : 0f) + blink * 8f + gk * 14f) * PX), 2.2f * PX, _pal.light, 0.8f); }
                SeedDraw.StarSeed(l, new Vector2(_tx, _gy + (5f + gk * 6f) * PX), 0.785f, _pal, 1f, (0.8f + blink * 0.35f) * (1f + gk * 0.6f));
            }
            if (armed) GroundRing(l, _tx, _gy, _k.proxRadius, 1.4f * PX, _pal.light, 0.35f + 0.15f * Mathf.Sin(t * 8f), new ArcOpt { m = 32, noTaper = true, seed = 2 });
            foreach (var tt in _ticks) { float u = Seg(t, tt, tt + 0.4f); if (u > 0f && u < 1f) GroundRing(l, _tx, _gy, (8f + 44f * EaseOut(u)) * PX, (2.6f * (1f - u) + 0.4f) * PX, _pal.mid, 1f - u, new ArcOpt { m = 32, seed = 3, taper = 1 }); }
            if (_trigger >= 0f && l == L.Front) Glow(new Vector2(_tx, _gy + 8f * PX), 28f * PX * Seg(t, _trigger, _trigger + 0.12f), _pal.g2, 0.6f);
            if (_trigger >= 0f && l == L.Front)
                for (int i = 0; i < _rooted.Count; i++)
                {   // raíces: rayos que suben a los sujetos
                    var e = _rooted[i].e; if (e.Dead) continue;
                    var R = new Rng(i * 7 + Mathf.Floor(t * 20f));
                    Bolt(l, new Vector2(_tx, _gy + 2f * PX), new Vector2(e.c.x, e.c.y - e.ry * 0.6f), 6, ref R, 1.6f * PX, _pal, 0.9f, 0.35f);
                }
        }

        /// <summary>Madura · Implosión: atrae mientras se arma (más fuerte cuanto más madura).</summary>
        private sealed class Implode : ArsenalEntity
        {
            private readonly StarSeed _s; private readonly float _k;
            public Implode(StarSeed s, float k) { _s = s; _k = k; }
            public override bool Tick(float dt)
            {
                if (_s.Gone) return false;
                if (_s._state != State.Armed) return true;
                Vector2 c = new Vector2(_s._tx, _s._gy + 0.75f);
                foreach (var e in ArsenalTargets.Alive()) if (Vector2.Distance(e.c, c) < _s._k.implodeRadius * _k) ArsenalCombat.Pull(e, c, _s._k.implodeSpeed * _k, e.flies);
                return true;
            }
            public override void Draw(L l)
            {
                if (_s._state != State.Armed) return;
                float u = (ArsenalWorld.T * 2.5f) % 1f;
                GroundRing(l, _s._tx, _s._gy, _s._k.implodeRadius * _k * (1f - u), 2f * PX, _s._pal.light, 0.6f * u, new ArcOpt { m = 40, noTaper = true, seed = 6 });
            }
        }
    }

    /// <summary>Semilla pequeña del Racimo (con Generaciones suelta otra tanda; con Siembra, si no da a nadie, queda de mina).</summary>
    public sealed class Bomblet : ArsenalEntity
    {
        private readonly SeedController _ctrl; private readonly SeedKit _k;
        private readonly float _x0, _x1, _t0, _t1, _sc, _hop, _gy0, _gy1; private readonly bool _gen, _mini; private readonly ArsenalPal _pal;

        public Bomblet(SeedController ctrl, float x0, float x1, float gyHint, float dur, ArsenalPal pal, bool gen, bool mini)
        {
            _ctrl = ctrl; _k = (SeedKit)ctrl.KitBase; _x0 = x0; _x1 = x1; _t0 = ArsenalWorld.T; _t1 = _t0 + dur; _pal = pal; _gen = gen; _mini = mini;
            _sc = mini ? 0.42f : 0.6f; _hop = (mini ? 45f : 80f) * PX;
            _gy0 = ArsenalTargets.GroundY(x0, gyHint + 4f); _gy1 = ArsenalTargets.GroundY(x1, gyHint + 4f);
        }

        private Vector2 Pos(float tt) { float u = Seg(tt, _t0, _t1); return new Vector2(Mathf.Lerp(_x0, _x1, u), Mathf.Lerp(_gy0 + 8f * PX, _gy1 + 4f * PX, u) + 4f * _hop * u * (1f - u)); }

        public override bool Tick(float dt)
        {
            if (ArsenalWorld.T < _t1) return true;
            float r = _mini ? _k.clusterRadius * 0.7f : _k.clusterRadius;
            var hit = ArsenalCombat.Area(new Vector2(_x1, _gy1 + 0.75f), r, new ArsenalHit { dmg = _mini ? _k.genDamage : _k.clusterDamage, knock = 0.65f, launch = 5f, heavy = true, kind = HitKind.Burst, shake = _mini ? 0.05f : 0.09f, onKill = _ => _ctrl.Harvested() });
            float x = _x1, gy = _gy1, sc = _sc; var P = _pal;
            ArsenalWorld.AddFx(1.2f, (l, tb) => GroundImpact(l, ArsenalWorld.T, tb, x, gy, sc, P, Mathf.Floor(x * 10f)));
            ArsenalSounds.Play("seed_mini", new Vector2(x, gy), 0.05f);
            if (_gen) for (int i = 0; i < _k.genCount; i++) { var R = new Rng(i + Mathf.Floor(x * 10f)); float dx = (i % 2 == 1 ? 1f : -1f) * (30f + R.Next() * 50f) * PX; ArsenalWorld.Add(new Bomblet(_ctrl, x, x + dx, gy, 0.2f + i * 0.05f, P, false, true)); }
            if (hit.Count == 0 && _k.Siembra) ArsenalWorld.Add(new PlantMine(_ctrl, x, gy, P, _mini));
            return false;
        }

        public override void Draw(L l)
        {
            Vector2 p = Pos(ArsenalWorld.T);
            if (l == L.Back) { var pts = TempPoints(); for (int i = 0; i <= 8; i++) pts.Add(Pos(Mathf.Max(_t0, ArsenalWorld.T - 0.07f * i / 8f))); Ribbon(l, pts, s => 4f * PX * (1f - s), _pal.light, 0.9f); }
            SeedDraw.StarSeed(l, p, ArsenalWorld.T * 18f, _pal, 1f, _sc);
        }
    }

    /// <summary>Racimo · Siembra: semilla pequeña que se queda de mina un rato.</summary>
    public sealed class PlantMine : ArsenalEntity
    {
        private readonly SeedController _ctrl; private readonly SeedKit _k; private readonly float _x, _gy, _t0; private readonly ArsenalPal _pal; private readonly bool _mini;

        public PlantMine(SeedController ctrl, float x, float gy, ArsenalPal pal, bool mini) { _ctrl = ctrl; _k = (SeedKit)ctrl.KitBase; _x = x; _gy = gy; _pal = pal; _mini = mini; _t0 = ArsenalWorld.T; }

        public override bool Tick(float dt)
        {
            if (ArsenalWorld.T - _t0 > _k.plantLife) return false;
            if (ArsenalWorld.T - _t0 <= 0.2f) return true;
            foreach (var e in ArsenalTargets.Alive())
            {
                if (Mathf.Abs(e.c.x - _x) >= _k.plantRadius + e.rx * 0.5f || (e.c.y - e.ry) - _gy >= 1f) continue;
                ArsenalCombat.Area(new Vector2(_x, _gy + 0.75f), _mini ? _k.clusterRadius * 0.7f : _k.clusterRadius, new ArsenalHit { dmg = _mini ? _k.genDamage : _k.clusterDamage, knock = 0.65f, launch = 5f, heavy = true, kind = HitKind.Burst, shake = 0.08f, onKill = _ => _ctrl.Harvested() });
                float x = _x, gy = _gy; var P = _pal;
                ArsenalWorld.AddFx(1.2f, (l, tb) => GroundImpact(l, ArsenalWorld.T, tb, x, gy, 0.6f, P, Mathf.Floor(x * 10f)));
                ArsenalSounds.Play("seed_mini", new Vector2(x, gy), 0.05f);
                return false;
            }
            return true;
        }

        public override void Draw(L l)
        {
            float a = 1f - Seg(ArsenalWorld.T, _t0 + _k.plantLife - 0.4f, _t0 + _k.plantLife);
            if (l == L.Front) SeedDraw.StarSeed(l, new Vector2(_x, _gy + 4f * PX), 0.785f, _pal, a, 0.45f);
            GroundRing(l, _x, _gy, _k.plantRadius, 1.2f * PX, _pal.light, (0.25f + 0.1f * Mathf.Sin(ArsenalWorld.T * 8f)) * a, new ArcOpt { m = 32, noTaper = true, seed = 8 });
        }
    }

    /// <summary>Charco de fuego en el suelo: quema a los que pisan (Semilla con Fuego).</summary>
    public sealed class FireZone : ArsenalEntity
    {
        private readonly float _x, _gy, _halfW, _life, _burn, _t0; private float _tick;
        public FireZone(float x, float gy, float halfW, float life, float burn) { _x = x; _gy = gy; _halfW = halfW; _life = life; _burn = burn; _t0 = ArsenalWorld.T; }

        public override bool Tick(float dt)
        {
            _tick -= dt;
            if (_tick <= 0f)
            {
                _tick = 0.3f;
                foreach (var e in ArsenalTargets.Alive())
                    if (Mathf.Abs(e.c.x - _x) < _halfW && (e.c.y - e.ry) - _gy < 0.75f)
                        ArsenalCombat.Hit(e, new ArsenalHit { dmg = 3f, elem = ArsenalElement.Fire, burn = _burn, dot = true, kind = HitKind.Burst });
            }
            return ArsenalWorld.T - _t0 < _life + 0.35f;
        }

        public override void Draw(L l)
        {
            float t = ArsenalWorld.T, end = _t0 + _life; int n = Mathf.Max(4, Mathf.RoundToInt(_halfW / PX / 14f));
            for (int i = 0; i < n; i++)
            {
                var R = new Rng(i + Mathf.Floor(_x * 40f)); float d = (R.Next() - 0.5f) * _halfW * 1.7f, dpx = Mathf.Abs(d) / PX;
                float on = Seg(t, _t0 + dpx / 400f, _t0 + dpx / 400f + 0.1f) * (1f - Seg(t, end - 0.2f + R.Next() * 0.2f, end + 0.35f));
                bool back = R.Next() < 0.45f; float h = (24f + R.Next() * 22f) * PX, w = (7f + R.Next() * 5f) * PX;
                if ((back ? L.Back : L.Front) == l) Flame(l, new Vector2(_x + d, _gy - 2f * PX), h * on, w * Mathf.Max(0.3f, on), t, i, on);
            }
            if (l == L.Back)
            {
                float a = (1f - Seg(t, end, end + 0.35f)) * 0.7f;
                Solid(l).Ellipse(new Vector2(_x, _gy - 4f * PX), _halfW, 9f * PX, 0f, A(new Color(0.23f, 0.07f, 0.03f), a), A(new Color(0.23f, 0.07f, 0.03f), 0f), 24);
                GroundRing(l, _x, _gy - 3f * PX, _halfW * 0.94f, 2.4f * PX, ArsenalPal.Fire.mid, a, new ArcOpt { m = 32, noTaper = true, wob = 0.1f, seed = 9 });
                GlowCanvas(l).Radial(new Vector2(_x, _gy), _halfW, A(ArsenalPal.Fire.g1, 0.25f * a), 0.5f, A(ArsenalPal.Fire.g1, 0.1f * a), A(ArsenalPal.Fire.g1, 0f));
            }
        }
    }

    /// <summary>Llamada diferida (réplicas de la Supernova, minas de la cadena, tics de armado).</summary>
    public sealed class Delayed : ArsenalEntity
    {
        private readonly float _at; private readonly Action _fn;
        public Delayed(float seconds, Action fn) { _at = ArsenalWorld.T + Mathf.Max(0f, seconds); _fn = fn; }
        public override bool Tick(float dt) { if (ArsenalWorld.T < _at) return true; _fn?.Invoke(); return false; }
        public override void Draw(L l) { }
    }

    /// <summary>Dibujo de la Semilla (<c>fx.js ▸ starSeed, lightColumn</c> + la erupción).</summary>
    public static class SeedDraw
    {
        /// <summary>Estrella de 4 puntas en capas que gira, con brillo.</summary>
        public static void StarSeed(L l, Vector2 p, float rot, ArsenalPal pal, float a, float big = 1f)
        {
            if (l != L.Front || a <= 0f) return;
            float b = big * PX;
            Glow(p, 24f * b, pal.g1, 0.6f * a); Glow(p, 11f * b, pal.g2, 0.8f * a);
            Star4(l, p, 13f * b, rot, pal.mid, a, 0.28f); Star4(l, p, 10f * b, rot + 0.78f, pal.light, a, 0.22f);
            Star4(l, p, 7f * b, rot, pal.core, a, 0.2f); Disc(l, p, 3f * b, pal.core, a);
        }

        /// <summary>Columna vertical de luz que sale del suelo y se afina (u = s desde el estallido).</summary>
        public static void LightColumn(L l, float x, float gy, float u, float sc, ArsenalPal pal)
        {
            float cu = Seg(u, 0f, 0.3f); if (l != L.Front || cu >= 1f || u < 0f) return;
            float h = 190f * PX * sc * EaseOut(Seg(u, 0f, 0.06f)), w = 30f * PX * sc * (1f - cu);
            Vector2 c = new Vector2(x, gy + h * 0.5f);
            Glow(new Vector2(x, gy + h * 0.4f), 70f * PX * sc, pal.g1, 0.7f * (1f - cu));
            Lens(l, c, Mathf.PI * 0.5f, h, w, pal.mid, 0.9f * (1f - cu));
            Lens(l, c, Mathf.PI * 0.5f, h * 0.92f, w * 0.55f, pal.light, 1f - cu);
            Lens(l, c, Mathf.PI * 0.5f, h * 0.85f, w * 0.22f, pal.core, 1f - cu);
        }

        /// <summary>Estallido de la semilla: columna de luz, impacto de suelo, agujas hacia arriba y grietas.</summary>
        public static void Eruption(L l, float t, float t0, float x, float gy, float sc, ArsenalPal pal)
        {
            float u = t - t0;
            LightColumn(l, x, gy, u, sc, pal);
            GroundImpact(l, t, t0, x, gy, sc, pal, Mathf.Floor(x * 10f));
            var R = new Rng(19);
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI * 0.5f + (R.Next() - 0.5f) * 1.3f, nu = Seg(u, 0f, 0.28f), l0 = 12f * PX * sc, l1 = l0 + (50f + R.Next() * 70f) * PX * sc * EaseOut(nu);
                bool back = R.Next() < 0.4f;
                if ((back ? L.Back : L.Front) == l && nu < 1f) { Vector2 o = new Vector2(x, gy + 10f * PX); Needle(l, o + Dir(ang) * l0, o + Dir(ang) * l1, 3.4f * PX * sc * (1f - nu * 0.6f), i % 3 != 0 ? pal.light : pal.core, 1f - nu); }
            }
            Cracks(l, t, t0, x, gy, pal, 41, 6, 60f * sc);
        }
    }
}
