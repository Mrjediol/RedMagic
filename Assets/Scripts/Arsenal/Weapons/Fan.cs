using System;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Items;
using UnityEngine;
using static RedMagic.Arsenal.ArsenalDraw;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// <b>1 · Abanico de cristal</b> (<c>g-abanico.js</c>, ficha <c>docs/armas/abanico.md</c>): escopeta corta de esquirlas.
    /// Evoluciones: <b>Erizo</b> (se clavan y estallan al cambiar de arma), <b>Lanza</b> (ráfaga en línea que perfora),
    /// <b>Caleidoscopio</b> (rebotes que ganan daño). Ajustes de la web en unidades (px × 0,025).
    /// </summary>
    [Serializable]
    public sealed class FanKit : ArsenalKit
    {
        public const int Erizo = 1, Lanza = 2, Caleidoscopio = 3;

        private static readonly ArsenalTree TreeData = new ArsenalTree("Abanico de esquirlas de corto alcance, sin carga.")
            .Add("Erizo", "Se clavan en enemigos y suelo y estallan al cambiar de arma (o solas al rato). Premia alternar armas.",
                 "Retorno", "Tras estallar vuelven al cristal atravesando enemigos; cada una recarga el arma de reserva.",
                 "Metralla", "Cada estallido suelta esquirlas pequeñas en todas direcciones.",
                 "Hemorragia", "Cada esquirla clavada hace daño continuo mientras está dentro.")
            .Add("Lanza", "Ráfaga en línea, rápida y de largo alcance, que atraviesa al primer enemigo.",
                 "Perforación", "Atraviesan a todos; cada enemigo atravesado suma daño.",
                 "Aceleración", "Manteniendo el disparo, cada ráfaga sale antes que la anterior.",
                 "Cristal mayor", "La última de cada ráfaga es una lanza grande: mucho daño y golpe fuerte.")
            .Add("Caleidoscopio", "Rebotan en suelo, plataformas y enemigos, y cada rebote suma daño.",
                 "Buscador", "Todos los rebotes van hacia el enemigo más cercano.",
                 "Prisma", "Al rebotar en un enemigo se divide en dos.",
                 "Eterno", "+3 rebotes y no caducan mientras sigan rebotando.");

        public override ArsenalTree Tree => TreeData;
        public override string WeaponId => "fan";
        public override ArsenalController CreateController() => new FanController();

        [Header("Base")]
        [Tooltip("Recarga (s).")] [Min(0.05f)] public float cooldown = 0.45f;
        [Tooltip("Esquirlas por disparo.")] [Min(1)] public int count = 5;
        [Tooltip("Apertura del abanico (rad, media).")] [Min(0f)] public float spread = 0.2f;
        [Tooltip("Velocidad (u/s).")] [Min(1f)] public float speed = 23.75f;
        [Tooltip("Daño por esquirla.")] [Min(0f)] public float damage = 9f;
        [Tooltip("Empujón (× el del enemigo).")] [Min(0f)] public float knockback = 0.35f;
        [Tooltip("Alcance (s de vuelo).")] [Min(0.05f)] public float life = 0.6f;

        [Header("Elemento (build)")]
        [Tooltip("Hielo: congelación por esquirla.")] [Range(0f, 1f)] public float freeze = 0.34f;
        [Tooltip("Fuego: quemadura (s).")] [Min(0f)] public float burn = 1.5f;

        [Header("Erizo")]
        [Tooltip("Estallan solas a los (s).")] public float spikeLife = 2.5f;
        [Tooltip("Daño del estallido.")] public float spikeBlast = 10f;
        [Tooltip("Radio del estallido (u).")] public float spikeBlastRadius = 0.85f;
        [Tooltip("Retorno: daño al volver.")] public float returnDamage = 7f;
        [Tooltip("Retorno: recarga del arma de reserva por esquirla (s).")] public float refund = 0.15f;
        [Tooltip("Metralla: esquirlas por estallido.")] public int shrapCount = 3;
        [Tooltip("Metralla: daño.")] public float shrapDamage = 4f;
        [Tooltip("Hemorragia: daño/s por clavada.")] public float bleedDps = 4f;

        [Header("Lanza")]
        public float lanceCooldown = 0.55f;
        [Tooltip("Tiempo entre esquirlas de la ráfaga (s).")] public float lanceGap = 0.035f;
        [Tooltip("Velocidad (u/s).")] public float lanceSpeed = 33.75f;
        [Tooltip("Alcance (s de vuelo).")] public float lanceLife = 0.75f;
        [Tooltip("Perforación: + daño por enemigo atravesado.")] public float pierceDmgUp = 0.25f;
        [Tooltip("Aceleración: − recarga por ráfaga (fracción).")] public float accelStep = 0.12f;
        [Tooltip("Aceleración: recarga mínima (×).")] public float accelMin = 0.5f;
        [Tooltip("Cristal mayor: × daño.")] public float bigMul = 3f;
        [Tooltip("Cristal mayor: tamaño.")] public float bigSize = 1.8f;

        [Header("Caleidoscopio")]
        public int kalBounces = 3;
        [Tooltip("+ daño por rebote.")] public float kalBounceDmg = 0.25f;
        [Tooltip("Buscador: alcance al buscar (u).")] public float kalSeekRange = 12.5f;
        [Tooltip("Prisma: daño de la copia (×).")] public float splitMul = 0.5f;

        public bool Retorno => Up(Erizo, 0);
        public bool Metralla => Up(Erizo, 1);
        public bool Hemorragia => Up(Erizo, 2);
        public bool Perforacion => Up(Lanza, 0);
        public bool Aceleracion => Up(Lanza, 1);
        public bool CristalMayor => Up(Lanza, 2);
        public bool Buscador => Up(Caleidoscopio, 0);
        public bool Prisma => Up(Caleidoscopio, 1);
        public bool Eterno => Up(Caleidoscopio, 2);
    }

    public sealed class FanController : ArsenalController<FanKit>
    {
        private float _cd, _cdFull = 1f, _accel;
        private ArsenalAim _lastAim;
        private readonly List<(float at, bool last, int copies)> _queue = new List<(float, bool, int)>();

        public ArsenalPal Pal => ArsenalBuild.ElemPal(K.Evo(FanKit.Caleidoscopio) ? ArsenalPal.Gold : ArsenalPal.Set);

        /// <summary>Boca actual (las esquirlas del Retorno vuelven aquí).</summary>
        public Vector2 Muzzle => CurrentAim.origin;

        public override void OnPress(ArsenalAim aim) { if (_cd <= 0f) Fire(aim); }
        public override void OnHold(ArsenalAim aim, float dt) { if (_cd <= 0f) Fire(aim); }
        public override void OnRelease(ArsenalAim aim) => _accel = 0f;

        /// <summary>Hacia dónde apunta ahora (sin jugador —renders del editor—, el último disparo).</summary>
        private ArsenalAim CurrentAim => User != null ? User.BuildArsenalAim() : _lastAim;

        private void Fire(ArsenalAim aim)
        {
            _lastAim = aim;
            var k = K; int n = ArsenalBuild.Copies; var P = Pal;
            User?.PlayAttackAnimation();
            if (k.Evo(FanKit.Lanza))
            {
                float b = k.lanceCooldown;
                _cd = _cdFull = b * Mathf.Max(k.accelMin, 1f - (k.Aceleracion ? _accel : 0f));
                if (k.Aceleracion) _accel = Mathf.Min(1f - k.accelMin, _accel + k.accelStep);
                // ráfaga con las esquirlas BASE (count); con Duplicador cada una sale doble, en dos filas paralelas
                for (int i = 0; i < k.count; i++) _queue.Add((ArsenalWorld.T + i * k.lanceGap, i == k.count - 1, n));
            }
            else
            {
                _cd = _cdFull = k.cooldown;
                int cnt = k.count * n; float spr = k.spread * (n > 1 ? 1.35f : 1f);
                for (int i = 0; i < cnt; i++)
                {
                    float u = cnt == 1 ? 0.5f : i / (float)(cnt - 1), a = aim.Ang + Mathf.Lerp(-spr, spr, u) + (UnityEngine.Random.value - 0.5f) * 0.02f;
                    float sp = k.speed * (0.95f + UnityEngine.Random.value * 0.1f);
                    ArsenalWorld.Add(FanShard.Get(this, aim.origin, Dir(a) * sp, P, default));
                }
            }
            Vector2 o = aim.origin; float dir = aim.Ang;
            ArsenalWorld.AddFx(0.25f, (l, t0) => CastFx(l, ArsenalWorld.T, t0 - 0.01f, t0, o, P, 0.8f, dir));
            ArsenalSounds.Play("fan_release", aim.origin);
        }

        public override void Tick(float dt, bool current)
        {
            _cd -= dt;
            if (_queue.Count == 0) return;
            var k = K; var aim = CurrentAim;
            while (_queue.Count > 0 && _queue[0].at <= ArsenalWorld.T)
            {
                var q = _queue[0]; _queue.RemoveAt(0);
                float a = aim.Ang + (UnityEngine.Random.value - 0.5f) * 0.03f; Vector2 nrm = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                bool big = q.last && k.CristalMayor;
                for (int c = 0; c < q.copies; c++)
                {
                    float off = q.copies > 1 ? (c == 0 ? -0.175f : 0.175f) : 0f;
                    ArsenalWorld.Add(FanShard.Get(this, aim.origin + nrm * off, Dir(a) * k.lanceSpeed, Pal, new FanShard.Opt { lance = true, big = big }));
                }
            }
        }

        public override float Ready => 1f - Mathf.Clamp01(_cd / Mathf.Max(0.01f, _cdFull));
        public override void Refund(float seconds) => _cd -= seconds;

        /// <summary>Retorno del Erizo: cada esquirla que vuelve recarga un poco el arma de reserva.</summary>
        public void RefundOthers(float seconds) { foreach (var o in Others()) o.Refund(seconds); }

        public float LastSwap => User != null ? User.LastSwapTime : -99f;
    }

    /// <summary>
    /// Esquirla: vuela, choca (enemigo / suelo / plataforma) y según la evolución rebota, atraviesa, se clava o vuelve.
    /// Pool propio (sin GameObjects).
    /// </summary>
    public sealed class FanShard : ArsenalProjectile
    {
        public struct Opt { public bool lance, big, mini, split; public int bounces; public bool hasBounces; }

        private enum State { Fly, Stick, Return }
        private static readonly Stack<FanShard> Pool = new Stack<FanShard>(64);
        private static int _platformMask = -2;

        private FanController _ctrl;
        private FanKit _k;
        private Opt _o;
        private ArsenalPal _pal;
        private State _state;
        private float _age, _life, _ang, _size;
        private int _bounces, _nb, _pierce, _pierced;
        private readonly HashSet<Health> _hit = new HashSet<Health>();
        private readonly List<Vector2> _trail = new List<Vector2>(10);
        private Body _stickE; private Vector2 _off; private float _stickAt, _stickSwap, _bleedT;
        private Vector2 _b0; private float _bt;
        private ArsenalElement _el;
        private bool _kal, _spike, _lance;

        public static FanShard Get(FanController ctrl, Vector2 p, Vector2 v, ArsenalPal pal, Opt o)
        {
            var s = Pool.Count > 0 ? Pool.Pop() : new FanShard();
            s.Init(ctrl, p, v, pal, o);
            return s;
        }

        private void Init(FanController ctrl, Vector2 p, Vector2 v, ArsenalPal pal, Opt o)
        {
            _ctrl = ctrl; _k = (FanKit)ctrl.KitBase; _o = o; _pal = pal; pos = p; vel = v;
            _el = ArsenalBuild.Element;
            _kal = _k.Evo(FanKit.Caleidoscopio); _spike = _k.Evo(FanKit.Erizo) && !o.mini; _lance = o.lance;
            _size = o.big ? _k.bigSize : o.mini ? 0.55f : 1f;
            _state = State.Fly; _age = 0f; _ang = Mathf.Atan2(v.y, v.x);
            _bounces = o.hasBounces ? o.bounces : _kal ? _k.kalBounces + (_k.Eterno ? 3 : 0) : 0; _nb = 0;
            _pierce = _lance ? (_k.Perforacion ? 99 : 1) : 0; _pierced = 0;
            _life = o.mini ? 0.35f : _lance ? _k.lanceLife : _k.life;
            dmgMul = 1f; captured = false; ejected = false; hostile = false; depth = 0f;
            _hit.Clear(); _trail.Clear(); _stickE = null;
        }

        public override bool Capturable => !_o.mini && _state == State.Fly && !captured;

        public override ArsenalProjectile CloneForCapture()
        {
            var c = Get(_ctrl, pos, vel, _pal, new Opt { lance = _o.lance, big = _o.big, mini = _o.mini, split = _o.split, bounces = _bounces, hasBounces = true });
            c.dmgMul = dmgMul;
            return c;
        }

        public override void OnRemoved() { _ctrl = null; _k = null; _stickE = null; Pool.Push(this); }

        private static int PlatformMask
        {
            get
            {
                if (_platformMask == -2) { int l = LayerMask.NameToLayer("Platform"); _platformMask = l >= 0 ? 1 << l : 0; }
                return _platformMask;
            }
        }

        public override bool Tick(float dt)
        {
            if (_ctrl == null) return false;
            _age += dt;
            if (captured) { Push(); return true; }
            switch (_state)
            {
                case State.Fly: return Fly(dt);
                case State.Stick: return Stuck(dt);
                default: return Back(dt);
            }
        }

        private void Push() { _trail.Add(pos); if (_trail.Count > 8) _trail.RemoveAt(0); }

        private bool Fly(float dt)
        {
            if (_age > _life + (ejected ? 0.6f : 0f)) return false;
            const int n = 3;
            for (int k = 0; k < n; k++)
            {
                Vector2 np = pos + vel * dt / n;
                if (ArsenalTargets.Raycast(pos, np, out var h, _hit)) { if (!OnEnemy(h)) return false; if (_state != State.Fly) return true; continue; }
                if (ArsenalTargets.GroundHit(pos, np, out var gp, out var gn)) { if (!OnSurface(gp, gn, false)) return false; if (_state != State.Fly) return true; continue; }
                if (_kal && vel.y < 0f && PlatformMask != 0)
                {
                    var ph = Physics2D.Linecast(pos, np, PlatformMask);
                    if (ph.collider != null && ph.normal.y > 0.5f) { if (!OnSurface(ph.point, ph.normal, true)) return false; continue; }
                }
                pos = np;
            }
            _ang = Mathf.Atan2(vel.y, vel.x); Push();
            return true;
        }

        private float Dmg() => _k.damage * dmgMul * (_o.big ? _k.bigMul : 1f) * (_o.mini ? _k.shrapDamage / Mathf.Max(0.01f, _k.damage) : 1f)
                              * (1f + (_kal ? _nb * _k.kalBounceDmg : 0f)) * (1f + (_lance && _k.Perforacion ? _pierced * _k.pierceDmgUp : 0f));

        private bool OnEnemy(RayHit h)
        {
            var e = h.body; _hit.Add(e.health);
            ArsenalCombat.Hit(e, new ArsenalHit
            {
                dmg = Dmg(), from = pos, knock = _k.knockback * (_o.big ? 2.5f : 1f), elem = _el, kind = HitKind.Projectile,
                freeze = _el == ArsenalElement.Ice ? _k.freeze : 0f, burn = _el == ArsenalElement.Fire ? _k.burn : 0f,
                heavy = _o.big, hitstop = _o.big ? 0.05f : 0f, shake = _o.big ? 0.12f : 0f,
            });
            var P = _pal; float ang = _ang, sc = _o.big ? 0.8f : _o.mini ? 0.3f : 0.45f; Vector2 hp = h.p; float seed = Mathf.Floor(hp.x * 40f);
            ArsenalWorld.AddFx(0.9f, (l, t0) => Impact(l, ArsenalWorld.T, t0, hp, sc, P, ang + Mathf.PI, 2.2f, seed, 6, 4));

            if (_pierce > 0) { _pierce--; _pierced++; ArsenalSounds.Play("fan_hit", hp); return true; }   // Lanza: atraviesa
            if (_bounces > 0)                                                                                // Caleidoscopio
            {
                _bounces--; _nb++; pos = h.p;
                if (_k.Eterno) _age = 0f;
                float sp = vel.magnitude; Body next = _k.Buscador ? ArsenalTargets.Nearest(h.p, _k.kalSeekRange, _hit) : null;
                float a;
                if (next != null) a = Mathf.Atan2(next.c.y - h.p.y, next.c.x - h.p.x);
                else
                {   // reflejo sobre la normal del cuerpo (elipse) en el punto de impacto
                    Vector2 nrm = new Vector2((h.p.x - e.c.x) / (e.rx * e.rx), (h.p.y - e.c.y) / (e.ry * e.ry)).normalized;
                    Vector2 r = vel - 2f * Vector2.Dot(vel, nrm) * nrm; a = Mathf.Atan2(r.y, r.x);
                }
                if (_k.Prisma && !_o.split)                                                                  // Prisma: se divide en dos
                {
                    var skip = new HashSet<Health>(_hit); if (next != null) skip.Add(next.health);
                    Body n2 = _k.Buscador ? ArsenalTargets.Nearest(h.p, _k.kalSeekRange, skip) : null;
                    float a2 = n2 != null ? Mathf.Atan2(n2.c.y - h.p.y, n2.c.x - h.p.x) : a - 0.35f;
                    if (next == null) a += 0.35f;
                    var c = Get(_ctrl, h.p + Dir(a2) * 0.1f, Dir(a2) * sp, _pal, new Opt { split = true, bounces = Mathf.Max(0, _bounces - 1), hasBounces = true });
                    c.dmgMul = dmgMul * _k.splitMul; c._nb = _nb; foreach (var x in _hit) c._hit.Add(x);
                    ArsenalWorld.Add(c);
                    var P2 = _pal; Vector2 pp = h.p;
                    ArsenalWorld.AddFx(0.3f, (l, t0) => { if (l == L.Front) PrismGem(l, ArsenalWorld.T, pp, 1f - Seg(ArsenalWorld.T, t0, t0 + 0.25f), P2); });
                }
                vel = Dir(a) * sp; pos += vel * 0.006f;                                                      // sale del cuerpo
                ArsenalSounds.Play("fan_ricochet", pos);
                return true;
            }
            if (_spike) { Stick(e, h.p); ArsenalSounds.Play(_el == ArsenalElement.Ice ? "fan_stick" : "fan_hit", h.p); return true; }
            ArsenalSounds.Play("fan_hit", h.p);
            return false;
        }

        private bool OnSurface(Vector2 p, Vector2 normal, bool platform)
        {
            var P = _pal;
            if (_bounces > 0)
            {
                _bounces--; _nb++; pos = p + normal * 0.03f;
                if (_k.Eterno) _age = 0f;
                float sp = vel.magnitude; Body next = _k.Buscador ? ArsenalTargets.Nearest(p, _k.kalSeekRange, _hit) : null;
                if (next != null) vel = (next.c - p).normalized * sp;
                else { vel = vel - 2f * Vector2.Dot(vel, normal) * normal; vel *= 0.9f; }
                ArsenalSounds.Play("fan_ricochet", p);
                Vector2 gp = p; float a0 = Mathf.Atan2(normal.y, normal.x); float seed = Mathf.Floor(p.x * 13f);
                ArsenalWorld.AddFx(0.5f, (l, t0) => Sparks(l, ArsenalWorld.T, t0, gp, new SparkOpt { n = 8, pal = P, seed = seed, sp = 230, grav = 520, len = 10, a0 = a0 - 1.2f, a1 = a0 + 1.2f, life = 0.35f, w = 2.6f, backShare = 0.45f }));
                return true;
            }
            if (platform) { pos = p - normal * 0.05f; return true; }   // sin rebotes atraviesa la plataforma
            Vector2 sp2 = p; float sd = Mathf.Floor(p.x * 9f), na = Mathf.Atan2(normal.y, normal.x);
            ArsenalWorld.AddFx(0.7f, (l, t0) =>
            {
                Sparks(l, ArsenalWorld.T, t0, sp2, new SparkOpt { n = 8, pal = P, seed = sd, sp = 230, grav = 520, len = 10, a0 = na - 1.2f, a1 = na + 1.2f, life = 0.35f, w = 2.6f, backShare = 0.45f });
                if (normal.y > 0.5f) Dust(l, ArsenalWorld.T, t0, sp2.x, sp2.y, sd, 4, 22f, 0.6f);
            });
            ArsenalSounds.Play("fan_ground", p);
            if (_spike) { Stick(null, p); return true; }
            return false;
        }

        private void Stick(Body e, Vector2 p)
        {
            _state = State.Stick; _stickE = e; pos = p;
            if (e != null) _off = p - e.c;
            _stickAt = ArsenalWorld.T; _stickSwap = _ctrl.LastSwap; _bleedT = 0.5f;
        }

        private bool Stuck(float dt)
        {
            var e = _stickE;
            if (e != null)
            {
                if (e.Dead) return Detonate();
                var b = ArsenalTargets.Of(e.health); if (b != null) pos = b.c + _off;
            }
            if (e != null && _k.Hemorragia)
            {
                _bleedT -= dt;
                if (_bleedT <= 0f) { _bleedT = 0.5f; ArsenalCombat.Hit(e, new ArsenalHit { dmg = _k.bleedDps * 0.5f, dot = true, kind = HitKind.Projectile }); }
            }
            if (_ctrl.LastSwap > _stickSwap || ArsenalWorld.T - _stickAt >= _k.spikeLife) return Detonate();
            return true;
        }

        /// <summary>Erizo: estallido (golpe fuerte → rompe congelados), Metralla, y Retorno.</summary>
        private bool Detonate()
        {
            var P = _pal; Vector2 hp = pos;
            ArsenalCombat.Area(hp, _k.spikeBlastRadius, new ArsenalHit
            {
                dmg = _k.spikeBlast, knock = 0.4f, heavy = true, elem = _el, kind = HitKind.Projectile,
                freeze = _el == ArsenalElement.Ice ? _k.freeze * 0.5f : 0f, burn = _el == ArsenalElement.Fire ? _k.burn : 0f,
            }, false);
            float seed = Mathf.Floor(hp.x * 31f);
            ArsenalWorld.AddFx(0.7f, (l, t0) =>
            {
                Impact(l, ArsenalWorld.T, t0, hp, 0.5f, P, 0f, TAU, seed, 8, 3);
                Sparks(l, ArsenalWorld.T, t0, hp, new SparkOpt { n = 6, pal = P, seed = seed + 1, sp = 200, grav = 520, len = 10, a0 = 0, a1 = TAU, life = 0.3f, w = 2.6f, backShare = 0.45f });
            });
            ArsenalSounds.Play("fan_detonate", hp);
            if (_k.Metralla)
                for (int i = 0; i < _k.shrapCount; i++)
                {
                    float a = _stickE != null ? (i / (float)_k.shrapCount) * TAU + UnityEngine.Random.value * 0.6f
                                              : Mathf.PI * (0.15f + 0.7f * (i + UnityEngine.Random.value * 0.8f) / _k.shrapCount);
                    var c = Get(_ctrl, hp + Dir(a) * 0.2f + (_stickE != null ? Vector2.zero : Vector2.up * 0.08f), Dir(a) * 16.25f, P, new Opt { mini = true });
                    if (_stickE != null) c._hit.Add(_stickE.health);
                    ArsenalWorld.Add(c);
                }
            if (_k.Retorno) { _state = State.Return; _b0 = pos; _bt = 0f; _hit.Clear(); ArsenalSounds.Play("fan_rip", pos); return true; }
            return false;
        }

        /// <summary>Retorno: vuelve en curva al cristal acelerando y golpea lo que atraviesa.</summary>
        private bool Back(float dt)
        {
            _bt += dt; Vector2 m = _ctrl.Muzzle; float u = EaseIn(Mathf.Clamp01(_bt / 0.38f));
            Vector2 mid = new Vector2((_b0.x + m.x) * 0.5f, Mathf.Max(_b0.y, m.y) + 1.25f);
            Vector2 p = (1 - u) * (1 - u) * _b0 + 2 * (1 - u) * u * mid + u * u * m;
            if (ArsenalTargets.Raycast(pos, p, out var h, _hit))
            {
                _hit.Add(h.body.health);
                ArsenalCombat.Hit(h.body, new ArsenalHit { dmg = _k.returnDamage, knock = 0.5f, from = pos, elem = _el, kind = HitKind.Projectile, freeze = _el == ArsenalElement.Ice ? _k.freeze * 0.5f : 0f });
            }
            Vector2 d = p - pos; if (d.sqrMagnitude > 1e-6f) _ang = Mathf.Atan2(d.y, d.x);
            pos = p; Push();
            if (u >= 1f)
            {
                _ctrl.RefundOthers(_k.refund);
                var P = _pal; var ctrl = _ctrl;
                ArsenalWorld.AddFx(0.25f, (l, t0) =>
                {
                    if (l != L.Front || ctrl == null) return;
                    Vector2 m2 = ctrl.Muzzle; float k = Seg(ArsenalWorld.T, t0, t0 + 0.2f);
                    Glow(m2, 0.75f, P.g2, 0.7f * (1f - k)); Star4(l, m2, 0.35f, 0.8f, P.core, 1f - k);
                });
                ArsenalSounds.Play("fan_absorb", m, 0.15f);
                return false;
            }
            return true;
        }

        public override void Draw(L l)
        {
            var P = _pal;
            if (captured)
            {   // orbitando una grieta: la mitad trasera de la órbita va detrás
                if (depth < 0f) { if (l == L.Back) { Lens(l, pos, _ang, 22f * PX, 4f * PX, P.mid, 0.8f); Lens(l, pos, _ang, 14f * PX, 2f * PX, P.core, 0.9f); } }
                else { CrystalShard(l, pos, _ang, _size, P, 1f); if (l == L.Front) Star4(l, pos, 8f * PX, ArsenalWorld.T * 9f, Color.white, 0.9f); }
                return;
            }
            if (l == L.Back && _trail.Count > 1 && _state != State.Stick)
            {   // estela: del extremo actual hacia atrás, afilada en la cola (tres pasadas + halo suave)
                var pts = TempPoints(); pts.Add(pos); for (int i = _trail.Count - 1; i >= 0; i--) pts.Add(_trail[i]);
                float k = _size * PX;
                Ribbon(l, pts, s => 13f * k * Mathf.Pow(1f - s, 1.1f), P.dark, 0.22f, true);
                Ribbon(l, pts, s => 9f * k * Mathf.Pow(1f - s, 1.2f), P.dark, 0.35f);
                Ribbon(l, pts, s => 5f * k * Mathf.Pow(1f - s, 1.3f), P.mid, 0.8f);
                Ribbon(l, pts, s => 2f * k * (1f - s), P.core, 1f);
            }
            if (_state == State.Stick)
            {
                float left = _k.spikeLife - (ArsenalWorld.T - _stickAt), wig = left < 0.4f ? Mathf.Sin(ArsenalWorld.T * 90f) * (1f - left / 0.4f) * 2f * PX : 0f;
                Vector2 f = Dir(_ang);
                CrystalShard(l, pos - f * 8f * PX + new Vector2(wig, 0f), _ang, 0.85f, P, 1f);
                if (l == L.Front)
                {
                    Star4(l, pos - f * 4f * PX, (5f + 3f * Mathf.Sin(ArsenalWorld.T * 20f + pos.x)) * PX, 0.4f, P.core, 0.8f);
                    // aviso de que va a estallar: brillo que late más rápido al acercarse el final
                    float warn = Mathf.Clamp01(1f - left / 0.8f);
                    if (warn > 0f)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(ArsenalWorld.T * (20f + 40f * warn));
                        Glow(pos, (7f + 4f * pulse) * PX, P.g1, 0.5f * warn);
                        Disc(l, pos, (1.5f + 1.5f * pulse) * PX, P.core, warn);
                    }
                }
            }
            else CrystalShard(l, pos, _ang, _size, P, 1f);
        }

        /// <summary>Gema prisma girando (Prisma del Caleidoscopio, del Rayo).</summary>
        public static void PrismGem(L l, float t, Vector2 p, float a, ArsenalPal pal)
        {
            if (l != L.Front || a <= 0f) return;
            var cv = Solid(l); float rot = t * 2.5f, cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            Vector2 R(float x, float y) => p + new Vector2(x * cs - y * sn, x * sn + y * cs);
            foreach (var (r, c) in new[] { (11f, pal.deep), (8f, pal.mid), (4.5f, pal.core) })
            {
                float rr = r * PX; var pts = TempPoints();
                pts.Add(R(0, rr * 1.3f)); pts.Add(R(rr, 0)); pts.Add(R(0, -rr * 1.3f)); pts.Add(R(-rr, 0));
                cv.Polygon(pts, A(c, a));
            }
            Glow(p, 20f * PX, pal.g2, 0.5f * a);
        }
    }
}
