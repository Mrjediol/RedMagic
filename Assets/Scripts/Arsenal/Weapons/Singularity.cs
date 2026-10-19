using System;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Gameplay;
using RedMagic.Items;
using UnityEngine;
using static RedMagic.Arsenal.ArsenalDraw;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// <b>2 · Singularidad · Grieta</b> (<c>g-singularidad.js</c> + <c>fx-sing.js</c>, ficha <c>docs/armas/singularidad.md</c>):
    /// viaja un orbe y abre una grieta en el sitio que atrae enemigos y <b>captura</b> los proyectiles del player (regla
    /// genérica: lo capturable que entra en su radio orbita y sale despedido al colapsar). Evoluciones: <b>Fauces</b>
    /// (devora), <b>Errante</b> (se desplaza arrastrando), <b>Arsenal</b> (lo capturado sale reforzado y apuntado).
    /// </summary>
    [Serializable]
    public sealed class SingularityKit : ArsenalKit
    {
        public const int Fauces = 1, Errante = 2, Arsenal = 3;

        private static readonly ArsenalTree TreeData = new ArsenalTree("Lanza un orbe que abre una grieta: atrae y colapsa; captura tus proyectiles.")
            .Add("Fauces", "Devora: lo que llega a la grieta recibe daño continuo y desaparece dentro.",
                 "Hambre", "Cada baja alarga la grieta y la hace crecer.",
                 "Digestión", "Se traga de golpe a los que tienen poca vida.",
                 "Regurgitar", "Al colapsar escupe un proyectil teledirigido por cada baja.")
            .Add("Errante", "Mientras atrae, la grieta se desplaza hacia el ratón arrastrando a los enemigos.",
                 "Cazadora", "Va sola, más rápido, a por el enemigo más cercano.",
                 "Rastro", "Deja grietas pequeñas por el camino que atraen y dañan un momento.",
                 "Masa", "Más grande y dura más; arranca a los voladores y los estampa contra el suelo.")
            .Add("Arsenal", "Captura más lejos; lo capturado sale reforzado y APUNTADO a los enemigos, no en todas direcciones.",
                 "Recarga", "Cada proyectil capturado recarga un poco tu otra arma.",
                 "Multiplicar", "Cada proyectil capturado sale doble.",
                 "Escudo de vacío", "También captura las balas enemigas y las devuelve como tuyas.");

        public override ArsenalTree Tree => TreeData;
        public override string WeaponId => "sing";
        public override ArsenalController CreateController() => new SingularityController();

        [Header("Base")]
        [Min(0.1f)] public float cooldown = 3.5f;
        [Tooltip("Distancia máxima (u).")] public float range = 9f;
        [Tooltip("Viaje del orbe (s).")] public float travelTime = 0.5f;
        [Tooltip("Tiempo atrayendo (s).")] public float pullTime = 1.8f;
        [Tooltip("Radio de atracción (u).")] public float pullRadius = 3.75f;
        [Tooltip("Fuerza de atracción (u/s).")] public float pullSpeed = 3.75f;
        [Tooltip("Daño del colapso.")] public float burstDamage = 40f;
        [Tooltip("Radio del colapso (u).")] public float burstRadius = 3.25f;
        [Tooltip("Empujón del colapso (× el del enemigo).")] public float burstKnock = 1.6f;
        [Tooltip("Lanza hacia arriba al colapsar (u/s).")] public float burstLaunch = 6.5f;
        [Tooltip("Radio de captura de proyectiles (u).")] public float captureRadius = 1.375f;
        [Tooltip("Velocidad de salida de lo capturado (u/s).")] public float ejectSpeed = 22.5f;
        [Header("Elemento (build)")]
        [Tooltip("Fuego: quemadura (s).")] public float burn = 3f;
        [Header("Fauces")]
        [Tooltip("Radio de la boca (u).")] public float devourRadius = 1f;
        [Tooltip("Daño por segundo dentro.")] public float devourDps = 70f;
        [Tooltip("Hambre: + s por baja.")] public float hungerTime = 0.5f;
        [Tooltip("Hambre: + tamaño por baja.")] public float hungerGrow = 0.1f;
        [Tooltip("Hambre: tamaño máximo.")] public float hungerMax = 1.6f;
        [Tooltip("Digestión: traga por debajo de (× vida).")] public float swallowPct = 0.35f;
        [Tooltip("Regurgitar: daño de cada proyectil.")] public float regurgDamage = 45f;
        [Tooltip("Regurgitar: radio del estallido (u).")] public float regurgRadius = 1.375f;
        [Header("Errante")]
        [Tooltip("Velocidad (u/s).")] public float driftSpeed = 2f;
        [Tooltip("Cazadora: velocidad (u/s).")] public float huntSpeed = 3.25f;
        [Tooltip("Rastro: cada (s).")] public float trailEvery = 0.35f;
        [Tooltip("Rastro: duración (s).")] public float trailLife = 1.2f;
        [Tooltip("Rastro: daño/s.")] public float trailDps = 10f;
        [Tooltip("Masa: escala.")] public float massScale = 1.4f;
        [Tooltip("Masa: × tiempo atrayendo.")] public float massTime = 1.35f;
        [Header("Arsenal")]
        [Tooltip("× radio de captura.")] public float arsenalRadius = 1.25f;
        [Tooltip("× daño de lo capturado.")] public float arsenalMul = 1.5f;
        [Tooltip("Recarga: s por captura.")] public float reloadRefund = 0.2f;
        [Tooltip("Escudo: daño de una bala devuelta.")] public float shotDamage = 12f;
        [Header("Duplicador")]
        [Tooltip("Tiempo de fusión de los dos orbes (s).")] public float mergeTime = 0.3f;
        [Tooltip("Escala de la grieta fusionada.")] public float mergeScale = 1.35f;
        [Tooltip("× daño de la fusionada.")] public float mergeDamage = 1.7f;

        public bool Hambre => Up(Fauces, 0);
        public bool Digestion => Up(Fauces, 1);
        public bool Regurgitar => Up(Fauces, 2);
        public bool Cazadora => Up(Errante, 0);
        public bool Rastro => Up(Errante, 1);
        public bool Masa => Up(Errante, 2);
        public bool Recarga => Up(Arsenal, 0);
        public bool Multiplicar => Up(Arsenal, 1);
        public bool EscudoVacio => Up(Arsenal, 2);
    }

    public sealed class SingularityController : ArsenalController<SingularityKit>
    {
        private float _cd, _cdFull = 1f;
        private ArsenalAim _lastAim;

        public ArsenalAim CurrentAim => User != null ? User.BuildArsenalAim() : _lastAim;

        public override void OnPress(ArsenalAim aim)
        {
            if (_cd > 0f) return;
            var k = K; _lastAim = aim;
            _cd = _cdFull = k.cooldown;
            User?.PlayAttackAnimation();
            float d = Mathf.Min(k.range, Vector2.Distance(aim.target, aim.origin));
            Vector2 to = aim.origin + aim.dir * d;
            float gy = ArsenalTargets.GroundY(to.x, to.y + 2f);
            to.y = Mathf.Max(to.y, gy + 0.75f);
            ArsenalWorld.Add(new VoidRift(this, aim.origin, to, ArsenalBuild.Copies > 1));
            ArsenalSounds.Play("sing_launch", aim.origin);
        }

        public override void Tick(float dt, bool current) => _cd -= dt;
        public override float Ready => 1f - Mathf.Clamp01(_cd / Mathf.Max(0.01f, _cdFull));
        public override void Refund(float seconds) => _cd -= seconds;
        public void RefundOthers(float s) { foreach (var o in Others()) o.Refund(s); }
    }

    /// <summary>La Grieta: viaja, se abre, atrae, devora/arrastra/captura y colapsa.</summary>
    public sealed class VoidRift : ArsenalEntity
    {
        private struct Cap { public ArsenalProjectile q; public float a, r, r0, tc, spin; }

        private readonly SingularityController _ctrl;
        private readonly SingularityKit _k;
        private readonly ArsenalElement _el;
        private readonly bool _twin, _mass;
        private readonly float _s0, _t0, _ta, _tm, _dmgMul;
        private float _tc, _tb, _extra, _grow = 1f, _devourT, _trailT, _capSfx = -9f;
        private int _feeds;
        private bool _opened, _merged, _burst, _collapseSfx;
        private readonly Vector2 _from, _to, _perp;
        private Vector2 _c, _pos;
        private readonly List<Cap> _caps = new List<Cap>();
        private readonly List<Vector2>[] _hist = { new List<Vector2>(), new List<Vector2>() };
        private readonly List<(Body b, float k)> _suck = new List<(Body, float)>();
        private readonly List<Collider2D> _overlap = new List<Collider2D>();
        private readonly ArsenalPal _bp;

        private float S => _s0 * (_twin ? _k.mergeScale : 1f) * _grow;

        public VoidRift(SingularityController ctrl, Vector2 from, Vector2 to, bool twin)
        {
            _ctrl = ctrl; _k = (SingularityKit)ctrl.KitBase; _el = ArsenalBuild.Element; _twin = twin;
            _mass = _k.Masa; _s0 = _mass ? _k.massScale : 1f;
            _t0 = ArsenalWorld.T; _ta = _t0 + _k.travelTime; _tm = _ta + (twin ? _k.mergeTime : 0f);
            _tc = _tm + _k.pullTime * (_mass ? _k.massTime : 1f); _tb = _tc + 0.12f;
            _dmgMul = twin ? _k.mergeDamage : 1f;
            _from = from; _to = to; _c = to; _pos = from;
            Vector2 d = to - from; float L = d.magnitude; _perp = L > 1e-4f ? new Vector2(-d.y, d.x) / L : Vector2.up;
            _bp = _el == ArsenalElement.None ? ArsenalPal.Set : ArsenalPal.ForElement(_el, ArsenalPal.Set);
        }

        private float T => ArsenalWorld.T;
        private bool Live => T >= _tm && T < _tc;

        private Vector2 OrbPos(int k)
        {
            float u = EaseOut(Seg(T, _t0, _ta)), bow = 46f * PX * k * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.1f));
            if (T < _ta) return Vector2.Lerp(_from, _to, u) + _perp * bow + Vector2.up * (26f * PX * Bump(Seg(T, _t0, _ta)));
            if (T < _tm)
            {
                float m = Seg(T, _ta, _tm), a = (T - _ta) * 16f + (k > 0 ? 0f : Mathf.PI), r = 30f * PX * (1f - EaseIn(m));
                return _to + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f);
            }
            return _c + new Vector2(0f, Mathf.Sin(T * 5f) * 2f * PX);
        }

        private void OnFeed(Body e)
        {
            _feeds++;
            if (_k.Hambre && _extra < 2f) { float add = Mathf.Min(_k.hungerTime, 2f - _extra); _extra += add; _tc += add; _tb += add; _grow = Mathf.Min(_k.hungerMax, _grow + _k.hungerGrow); }
            Vector2 f = e.c; var self = this;
            ArsenalWorld.AddFx(0.5f, (l, tf) =>
            {
                if (l != L.Front) return;
                float u = Seg(ArsenalWorld.T, tf, tf + 0.4f); Vector2 c = self._pos;
                Glow(c, 60f * PX * self.S * (1f - u), ArsenalPal.Void.g1, 0.8f * (1f - u));
                for (int i = 0; i < 6; i++) { float a = i / 6f * TAU, r = 30f * PX * (1f - u); Disc(l, Vector2.Lerp(f, c, u) + Dir(a) * r, 2.5f * PX * (1f - u), ArsenalPal.Void.light, 1f - u); }
            });
            ArsenalSounds.Play("sing_feed", e.c, 0.1f);
        }

        public override bool Tick(float dt)
        {
            var k = _k; float s = S;
            // Errante: se desplaza hacia el ratón (o, con Cazadora, al enemigo más cercano) mientras atrae
            if (k.Evo(SingularityKit.Errante) && Live)
            {
                var tgt = k.Cazadora ? ArsenalTargets.Nearest(_c, 15f) : null;
                Vector2 goal = tgt != null ? tgt.c : _ctrl.CurrentAim.target;
                float sp = (tgt != null ? k.huntSpeed : k.driftSpeed) * dt; Vector2 dd = goal - _c; float dist = dd.magnitude;
                if (dist > 0.1f) _c += dd / dist * Mathf.Min(sp, dist);
                float gy = ArsenalTargets.GroundY(_c.x, _c.y + 1f); _c.y = Mathf.Max(_c.y, gy + 0.75f);
                if (k.Rastro) { _trailT -= dt; if (_trailT <= 0f) { _trailT = k.trailEvery; ArsenalWorld.Add(new MiniRift(_c, k.trailLife, k.trailDps)); } }
            }
            _pos = OrbPos(0);
            if (!_opened && T >= _tm) { _opened = true; ArsenalSounds.Play("sing_tear", _pos); }
            if (T < _ta)
            {
                int n = _twin ? 2 : 1;
                for (int i = 0; i < n; i++) { var h = _hist[i]; h.Insert(0, OrbPos(_twin ? (i == 0 ? -1 : 1) : 0)); if (h.Count > 8) h.RemoveAt(h.Count - 1); }
            }
            if (_twin && !_merged && T >= _tm)
            {   // fusión: destello y onda corta
                _merged = true; ArsenalFeel.Shake(0.1f);
                Vector2 c = _to; float sc = s; var bp = _bp;
                ArsenalWorld.AddFx(0.6f, (l, tm) => { if (l != L.Front) return; float u = Seg(ArsenalWorld.T, tm, tm + 0.4f); Glow(c, 90f * PX * sc, bp.g2, 0.8f * (1f - u)); Star4(l, c, 50f * PX * sc * (1f - u * 0.5f), 0.3f, Color.white, 1f - u); });
                ArsenalSounds.Play("sing_merge", c);
            }
            _suck.Clear();
            if (Live)
            {
                float R = k.pullRadius * s; bool drift = k.Evo(SingularityKit.Errante);
                foreach (var e in ArsenalTargets.Alive())
                {
                    float d = Vector2.Distance(e.c, _pos); if (d > R) continue;
                    ArsenalCombat.Pull(e, _pos, k.pullSpeed * (1f - 0.4f * d / R) * (drift ? 1.25f : 1f), (_mass || drift) && e.flies);
                    _suck.Add((e, Mathf.Min(1f, (1f - d / R) * 1.4f) * Seg(T, _tm, _tm + 0.25f)));
                }
                if (k.Evo(SingularityKit.Fauces)) Devour(dt, s);
                Capture(s);
            }
            if (T >= _tc && !_collapseSfx) { _collapseSfx = true; ArsenalSounds.Play("sing_collapse", _pos); }
            for (int i = 0; i < _caps.Count; i++)
            {
                var c = _caps[i];
                float shrink = 1f - 0.6f * Seg(T, _tc, _tb), inU = EaseOut(Seg(T, c.tc, c.tc + 0.18f)), r = Mathf.Lerp(c.r0, c.r, inU) * shrink;
                c.a += c.spin * dt * inU; _caps[i] = c;
                c.q.Orbit(_pos + new Vector2(Mathf.Cos(c.a) * r, Mathf.Sin(c.a) * r * Mathf.Lerp(1f, 0.6f, inU)), c.a + Mathf.PI * 0.5f, -Mathf.Sin(c.a));
            }
            if (T >= _tb && !_burst) { Collapse(s); return false; }
            return true;
        }

        private void Devour(float dt, float s)
        {
            _devourT -= dt; if (_devourT > 0f) return;
            _devourT = 0.2f;
            var list = ArsenalTargets.Alive();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (Vector2.Distance(e.c, _pos) > _k.devourRadius * s + Mathf.Max(e.rx, e.ry) * 0.5f) continue;
                if (_k.Digestion && e.health.CurrentHealth < e.health.MaxHealth * _k.swallowPct)
                {
                    ArsenalCombat.Label(e.c + Vector2.up * (e.ry + 0.75f), "arsenal.swallowed", ArsenalPal.Void.light);
                    ArsenalCombat.Hit(e, new ArsenalHit { dmg = e.health.CurrentHealth + 1f, dot = true, noReact = true, kind = HitKind.Burst, onKill = OnFeed });
                    continue;
                }
                ArsenalCombat.Hit(e, new ArsenalHit { dmg = _k.devourDps * 0.2f, dot = true, kind = HitKind.Burst, onKill = OnFeed });
            }
        }

        private void Capture(float s)
        {
            bool arsenal = _k.Evo(SingularityKit.Arsenal), shield = arsenal && _k.EscudoVacio;
            float capR = _k.captureRadius * s * (arsenal ? _k.arsenalRadius : 1f);
            var ents = ArsenalWorld.Entities;
            for (int i = 0; i < ents.Count; i++)
            {
                if (!(ents[i] is ArsenalProjectile q) || !q.Capturable || q.captured || q.lastCapturer == this) continue;
                if (q.hostile && !shield) continue;
                float d0 = Vector2.Distance(q.pos, _pos); if (d0 >= capR) continue;
                Grab(q, d0, arsenal, s);
            }
            if (!shield) return;
            // Escudo de vacío: las balas enemigas de verdad (Projectile) se paran y pasan a ser un orbe del player en órbita
            var player = _ctrl.User != null ? _ctrl.User.GetComponent<Health>() : null;
            _overlap.Clear();
            Physics2D.OverlapCircle(_pos, capR, new ContactFilter2D { useTriggers = true, useLayerMask = false }, _overlap);
            foreach (var col in _overlap)
            {
                if (col == null || !col.TryGetComponent(out Projectile p)) continue;
                if (player != null && !p.CanHit(player)) continue;
                Vector2 at = p.transform.position; p.Deflect();
                var shot = VoidShot.Get(_k.shotDamage); shot.pos = at; shot.hostile = true;
                ArsenalWorld.Add(shot);
                Grab(shot, Vector2.Distance(at, _pos), arsenal, s);
            }
        }

        private void Grab(ArsenalProjectile q, float d0, bool arsenal, float s)
        {
            q.captured = true; q.lastCapturer = this;
            _caps.Add(new Cap { q = q, a = Mathf.Atan2((q.pos.y - _pos.y) / 0.6f, q.pos.x - _pos.x), r = (40f + UnityEngine.Random.value * 18f) * PX * Mathf.Min(1.4f, s), r0 = d0, tc = T, spin = 9f + UnityEngine.Random.value * 3f });
            Vector2 at = q.pos;
            ArsenalWorld.AddFx(0.3f, (l, tf) => { if (l != L.Front) return; float u = Seg(ArsenalWorld.T, tf, tf + 0.25f); Star4(l, at, 12f * PX * (1f - u), 0.6f, ArsenalPal.Void.light, 1f - u); Glow(at, 18f * PX * (1f - u), ArsenalPal.Void.g1, 0.6f * (1f - u)); });
            if (arsenal && _k.Recarga) _ctrl.RefundOthers(_k.reloadRefund);
            if (T - _capSfx > 0.3f) { _capSfx = T; ArsenalSounds.Play("sing_capture", at); }
        }

        private void Collapse(float s)
        {
            _burst = true;
            var k = _k; Vector2 p = _pos;
            var hit = ArsenalCombat.Area(p, k.burstRadius * s, new ArsenalHit
            {
                dmg = k.burstDamage * _dmgMul, knock = k.burstKnock, launch = k.burstLaunch, heavy = true, elem = _el, kind = HitKind.Burst,
                burn = _el == ArsenalElement.Fire ? k.burn : 0f, freeze = _el == ArsenalElement.Ice ? 1f : 0f, hitstop = 0.07f, shake = 0.22f * s,
            });
            if (_mass) foreach (var e in hit) if (e.flies && !e.Dead && e.knockback != null) e.knockback.ApplyVelocity(new Vector2(0f, -17.5f), 0.5f);   // Masa: los estampa

            // lo capturado sale: en todas direcciones (base) o apuntado a los enemigos (Arsenal), reforzado y, con Multiplicar, doble
            bool arsenal = k.Evo(SingularityKit.Arsenal);
            List<Body> targets = null;
            if (arsenal) { targets = new List<Body>(ArsenalTargets.Alive()); targets.Sort((a, b) => Vector2.Distance(a.c, p).CompareTo(Vector2.Distance(b.c, p))); }
            for (int i = 0; i < _caps.Count; i++)
            {
                var c = _caps[i];
                var clone = arsenal && k.Multiplicar ? c.q.CloneForCapture() : null;    // la copia antes de reforzar
                Launch(c.q, i, clone != null ? -0.12f : 0f, targets, arsenal);
                if (clone != null) { clone.lastCapturer = this; ArsenalWorld.Add(clone); Launch(clone, i, 0.12f, targets, arsenal); }
            }
            if (k.Evo(SingularityKit.Fauces) && k.Regurgitar)
                for (int i = 0; i < _feeds; i++) ArsenalWorld.Add(new VoidBolt(p, (i / (float)Mathf.Max(1, _feeds)) * TAU, ArsenalTargets.Nearest(p, 17.5f), k.regurgDamage, k.regurgRadius));

            float sc = s; var bp = _bp; bool fire = _el == ArsenalElement.Fire, mass = _mass;
            float gy = ArsenalTargets.GroundY(p.x, p.y + 0.5f);
            ArsenalWorld.AddFx(1.6f, (l, tb) => SingDraw.BurstRift(l, ArsenalWorld.T, tb, p, sc, bp, gy, mass, fire));
            if (_el == ArsenalElement.Ice) { float nr = k.burstRadius * s * 0.8f; ArsenalWorld.AddFx(1.6f, (l, tb) => FrostNova(l, ArsenalWorld.T, tb, p.x, gy, nr)); }
            ArsenalSounds.Play(fire ? "sing_burst_fire" : _el == ArsenalElement.Ice ? "seed_nova" : "sing_burst", p);
            if (_caps.Count > 0) ArsenalSounds.Play("sing_eject", p);
        }

        private void Launch(ArsenalProjectile q, int i, float aOff, List<Body> targets, bool arsenal)
        {
            Body tg = targets != null && targets.Count > 0 ? targets[i % targets.Count] : null;
            float a = (tg != null ? Mathf.Atan2(tg.c.y - q.pos.y, tg.c.x - q.pos.x) : _caps[i % _caps.Count].a) + aOff;
            q.Eject(Dir(a) * _k.ejectSpeed, arsenal ? _k.arsenalMul : 1f);
        }

        public override void Draw(L l)
        {
            float s = S, t = T;
            if (T < _tm)
            {
                if (_twin)
                {
                    for (int i = 0; i < 2; i++) SingDraw.RiftSeed(l, t, OrbPos(i == 0 ? -1 : 1), _s0, T < _ta ? _hist[i] : null);
                    if (T >= _ta && l == L.Front) { float m = Seg(T, _ta, _tm); Glow(_to, 50f * PX * s * m, _bp.g2, 0.6f * m); }
                }
                else SingDraw.RiftSeed(l, t, _pos, s, T < _ta ? _hist[0] : null);
                return;
            }
            // tirón: hebras de luz del enemigo atrapado hacia la grieta (en la web el enemigo se estiraba)
            if (l == L.Back)
                foreach (var (b, k) in _suck)
                {
                    if (b.Dead || k <= 0.02f) continue;
                    Vector2 d = (_pos - b.c); float len = d.magnitude; if (len < 0.2f) continue; d /= len;
                    var R = new Rng(b.c.x * 3f + Mathf.Floor(t * 12f));
                    for (int i = 0; i < 3; i++)
                    {
                        float u = (t * 1.6f + i / 3f + R.Next() * 0.2f) % 1f; Vector2 n = new Vector2(-d.y, d.x) * ((R.Next() - 0.5f) * b.ry);
                        Vector2 a0 = b.c + n + d * len * u * 0.6f, a1 = a0 + d * Mathf.Min(len * 0.4f, 1f);
                        Needle(l, a0, a1, 2.2f * PX, ArsenalPal.Void.light, 0.5f * k * Bump(u));
                    }
                }
            if (l == L.Front) foreach (var (b, k) in _suck) if (!b.Dead) Glow(b.c, Mathf.Max(b.rx, b.ry) * 1.3f, ArsenalPal.Void.g1, 0.35f * k);
            SingDraw.LookRift(l, t, _pos, s, T - _tm, Seg(T, _tc, _tb));
            if (_k.Evo(SingularityKit.Fauces) && Live && l == L.Front)
            {   // Fauces: la boca (zona que devora) late
                float r = _k.devourRadius * s * (1f + 0.06f * Mathf.Sin(t * 12f));
                EllArc(l, _pos, r, r, 0f, TAU, 1.6f * PX, ArsenalPal.Void.light, 0.35f + 0.15f * Mathf.Sin(t * 12f), new ArcOpt { m = 40, wob = 0.06f, seed = 2, noTaper = true });
                Glow(_pos, r * 1.4f, ArsenalPal.Void.g1, 0.25f);
            }
        }
    }

    /// <summary>Errante · Rastro: grieta pequeña que atrae y daña un momento.</summary>
    public sealed class MiniRift : ArsenalEntity
    {
        private readonly Vector2 _p; private readonly float _t0, _life, _dps; private float _tick;
        public MiniRift(Vector2 p, float life, float dps) { _p = p; _life = life; _dps = dps; _t0 = ArsenalWorld.T; }

        public override bool Tick(float dt)
        {
            _tick -= dt;
            foreach (var e in ArsenalTargets.Alive())
            {
                if (Vector2.Distance(e.c, _p) >= 1.75f) continue;
                ArsenalCombat.Pull(e, _p, 1.5f);
                if (_tick <= 0f) ArsenalCombat.Hit(e, new ArsenalHit { dmg = _dps * 0.25f, dot = true, kind = HitKind.Burst });
            }
            if (_tick <= 0f) _tick = 0.25f;
            return ArsenalWorld.T - _t0 < _life;
        }

        public override void Draw(L l)
        {
            float u = Seg(ArsenalWorld.T, _t0 + _life - 0.25f, _t0 + _life);
            SingDraw.LookRift(l, ArsenalWorld.T, _p, 0.38f, ArsenalWorld.T - _t0, u);
        }
    }

    /// <summary>Fauces · Regurgitar: orbe de vacío grande que sale en abanico, persigue y estalla en área al impactar.</summary>
    public sealed class VoidBolt : ArsenalEntity
    {
        private Vector2 _p, _v; private Body _target; private float _life = 3f, _age; private readonly float _dmg, _radius;
        private readonly List<Vector2> _trail = new List<Vector2>(16);
        private const float R0 = 11f * PX;

        public VoidBolt(Vector2 p, float a0, Body target, float dmg, float radius)
        {
            _p = p; _v = Dir(a0) * 9.5f + Vector2.up * 3f; _target = target; _dmg = dmg; _radius = radius;
            ArsenalSounds.Play("sing_bolt", p, 0.05f);
        }

        private void Boom()
        {
            ArsenalCombat.Area(_p, _radius, new ArsenalHit { dmg = _dmg, heavy = true, knock = 0.9f, launch = 3.75f, shake = 0.12f, hitstop = 0.04f, kind = HitKind.Burst }, false);
            Vector2 b = _p; float gy = ArsenalTargets.GroundY(b.x, b.y + 0.5f);
            ArsenalWorld.AddFx(1.4f, (l, tb) => SingDraw.BurstRefined(l, ArsenalWorld.T, tb, b, 0.45f, ArsenalPal.Void, gy, false, false));
            ArsenalSounds.Play("sing_burst", b);
        }

        public override bool Tick(float dt)
        {
            _life -= dt; _age += dt; if (_life <= 0f) { Boom(); return false; }
            if (_target == null || _target.Dead) _target = ArsenalTargets.Nearest(_p, 20f);
            float turn = Mathf.Min(1f, dt * (_age < 0.15f ? 1f : 6f));
            if (_target != null) _v = Vector2.Lerp(_v, (_target.c - _p).normalized * 18f, turn);
            _p += _v * dt; _trail.Insert(0, _p); if (_trail.Count > 16) _trail.RemoveAt(16);
            if ((_target != null && Vector2.Distance(_target.c, _p) < _target.rx + R0 * 0.5f) || ArsenalTargets.InGround(_p)) { Boom(); return false; }
            return true;
        }

        public override void Draw(L l)
        {
            var V = ArsenalPal.Void;
            if (l == L.Back && _trail.Count > 1)
            {
                Ribbon(l, _trail, q => 22f * PX * Mathf.Pow(1f - q, 1.2f), V.deep, 0.45f, true);
                Ribbon(l, _trail, q => 11f * PX * (1f - q), V.mid, 0.8f);
                Ribbon(l, _trail, q => 4f * PX * (1f - q), Color.white, 0.9f);
            }
            if (l != L.Front) return;
            Glow(_p, 40f * PX, V.g1, 0.75f); Glow(_p, 18f * PX, new Color(0.86f, 0.82f, 1f), 0.6f);
            Disc(l, _p, R0 + 3f * PX, new Color(0.35f, 0.18f, 0.82f), 0.9f); Disc(l, _p, R0, new Color(0.02f, 0.01f, 0.05f), 1f);
            EllArc(l, _p, R0 + 0.8f * PX, R0 + 0.8f * PX, 0f, TAU, 2f * PX, Color.white, 0.95f, new ArcOpt { m = 30, noTaper = true, seed = 1 });
            var B = new Rng(Mathf.Floor(ArsenalWorld.T * 22f) + Mathf.Floor(_p.x * 10f));
            for (int i = 0; i < 2; i++) { float a = B.Next() * TAU; Bolt(l, _p, _p + Dir(a) * 24f * PX, 3, ref B, 1.1f * PX, ArsenalPal.Set, 0.8f); }
        }
    }

    /// <summary>Bala enemiga atrapada por el Escudo de vacío: orbita y, al soltarla, es un orbe del player.</summary>
    public sealed class VoidShot : ArsenalProjectile
    {
        private static readonly Stack<VoidShot> Pool = new Stack<VoidShot>(8);
        private float _dmg, _life;
        private readonly HashSet<Health> _hit = new HashSet<Health>();

        public static VoidShot Get(float dmg)
        {
            var s = Pool.Count > 0 ? Pool.Pop() : new VoidShot();
            s._dmg = dmg; s._life = 3f; s.dmgMul = 1f; s.captured = false; s.ejected = false; s.lastCapturer = null; s._hit.Clear(); s.vel = Vector2.zero;
            return s;
        }

        public override bool Capturable => !captured;
        public override void OnRemoved() => Pool.Push(this);

        public override bool Tick(float dt)
        {
            if (captured) return true;
            if (!ejected) return false;                       // nunca vuela sola sin haber sido soltada
            _life -= dt; pos += vel * dt;
            var e = ArsenalTargets.Inside(pos, 0.1f);
            if (e != null && !_hit.Contains(e.health))
            {
                _hit.Add(e.health);
                ArsenalCombat.Hit(e, new ArsenalHit { dmg = _dmg * dmgMul, from = pos, knock = 0.6f, kind = HitKind.Projectile });
                Vector2 hp = pos; ArsenalWorld.AddFx(0.6f, (l, t0) => Impact(l, ArsenalWorld.T, t0, hp, 0.4f, ArsenalPal.Void, ang + Mathf.PI, 2f, hp.x * 9f, 6, 3));
                return false;
            }
            return _life > 0f && !ArsenalTargets.InGround(pos);
        }

        public override void Draw(L l)
        {
            if (l != L.Front && !(captured && depth < 0f && l == L.Back)) return;
            Glow(pos, 16f * PX, ArsenalPal.Void.g1, 0.7f, l);
            Disc(l, pos, 5f * PX, ArsenalPal.Void.mid, 1f); Disc(l, pos, 2.4f * PX, Color.white, 1f);
        }
    }

    /// <summary>Dibujo de la Grieta (<c>fx-sing.js</c>: riftSeed, lookRift, burstRift, burstRefined) en Unity.</summary>
    public static class SingDraw
    {
        private static readonly Color VoidInk = new Color(0.016f, 0.004f, 0.047f), Neb = new Color(0.47f, 0.24f, 1f), RockA = new Color(0.16f, 0.18f, 0.27f), RockB = new Color(0.54f, 0.58f, 0.72f);
        private static readonly List<Vector2> LeftEdge = new List<Vector2>(20), RightEdge = new List<Vector2>(20), Mid = new List<Vector2>(20);

        /// <summary>Orbe que viaja: núcleo violeta, línea vertical que anuncia la grieta y chispas eléctricas; estela.</summary>
        public static void RiftSeed(L l, float t, Vector2 p, float s, List<Vector2> trail)
        {
            var V = ArsenalPal.Void;
            if (l == L.Back)
            {
                if (trail != null && trail.Count > 1)
                {
                    var pts = TempPoints(); pts.Add(p); pts.AddRange(trail);
                    StrokeTaper(l, pts, 7f * PX * s, ArsenalPal.Set.dark, 0.45f);
                    StrokeTaper(l, pts, 2.6f * PX * s, V.light, 0.9f);
                }
                Glow(p, 34f * PX * s, new Color(0.43f, 0.2f, 0.94f), 0.6f, L.Back);
                return;
            }
            Glow(p, 16f * PX * s, new Color(0.82f, 0.9f, 1f), 0.8f);
            Disc(l, p, 6.5f * PX * s, new Color(0.35f, 0.18f, 0.82f), 1f); Disc(l, p, 4.5f * PX * s, new Color(0.79f, 0.71f, 1f), 1f); Disc(l, p, 2.4f * PX * s, Color.white, 1f);
            Lens(l, p, Mathf.PI * 0.5f, 22f * PX * s * (1f + 0.25f * Mathf.Sin(t * 30f)), 2.2f * PX * s, Color.white, 0.85f);
            var B = new Rng(Mathf.Floor(t * 20f) + 3f);
            for (int i = 0; i < 2; i++) { float a = B.Next() * TAU; Bolt(l, p, p + Dir(a) * 15f * PX * s, 3, ref B, 0.9f * PX, ArsenalPal.Set, 0.75f); }
        }

        /// <summary>La grieta abierta en el sitio: línea de luz que crece y se rasga; dentro, el vacío con estrellas y una nebulosa;
        /// bordes que arden, chispas eléctricas, escombros que orbitan y se cuelan. <paramref name="ot"/> = s desde que se abrió.</summary>
        public static void LookRift(L l, float t, Vector2 p, float s, float ot, float collapse)
        {
            float lineU = EaseOut(Seg(ot, 0f, 0.14f)), openU = EaseOut(Seg(ot, 0.1f, 0.45f)), cU = EaseIn(collapse);
            float op = (0.08f + 0.92f * openU) * (1f - cU), H = 84f * PX * s * (0.2f + 0.8f * lineU) * (1f - 0.6f * cU);
            float Wd = 15f * PX * s * openU * (1f - cU) * (1f + 0.08f * Mathf.Sin(t * 9f));
            if (op <= 0.01f && lineU >= 1f) return;
            if (l == L.Front && ot < 0.5f)
            {   // apertura: línea vertical de luz y destello al rasgarse
                float lf = 1f - Seg(ot, 0.3f, 0.5f);
                Glow(p, 50f * PX * s * lineU, new Color(0.78f, 0.86f, 1f), 0.7f * lf);
                Lens(l, p, Mathf.PI * 0.5f, H * 1.15f, 4f * PX * s * (1f - openU * 0.5f), Color.white, lf);
                float fu = Seg(ot, 0.1f, 0.3f);
                if (fu > 0f && fu < 1f) { Star4(l, p, 46f * PX * s * (1f - fu * 0.4f), 0f, Color.white, 1f - fu, 0.12f); Lens(l, p, 0f, 120f * PX * s * (1f - fu * 0.3f), 4f * PX * s * (1f - fu), ArsenalPal.Set.light, 1f - fu); }
            }
            if (ot >= 0.1f) Sparks(l, t, t - ot + 0.1f, p, new SparkOpt { n = 14, a0 = -Mathf.PI, a1 = Mathf.PI, sp = 260f * s, pal = ArsenalPal.Set, seed = 31, len = 14, life = 0.45f, w = 2f, grav = 520, backShare = 0.45f });
            if (op <= 0.01f) return;
            if (l == L.Back) { Glow(p, 95f * PX * s, new Color(0.35f, 0.16f, 0.86f), 0.45f * op, L.Back); return; }

            const int N = 18; var R = new Rng(Mathf.Floor(t * 16f));
            LeftEdge.Clear(); RightEdge.Clear(); Mid.Clear();
            for (int i = 0; i <= N; i++)
            {
                float v = i / (float)N, w = Wd * Mathf.Pow(Mathf.Sin(Mathf.PI * v), 0.85f), y = p.y + H * 0.5f - H * v;
                float j = i == 0 || i == N ? 0f : (R.Next() - 0.5f) * 3.2f * PX * s * op, j2 = i == 0 || i == N ? 0f : (R.Next() - 0.5f) * 3.2f * PX * s * op;
                LeftEdge.Add(new Vector2(p.x - w + j, y)); RightEdge.Add(new Vector2(p.x + w + j2, y)); Mid.Add(new Vector2(p.x + (j + j2) * 0.5f, y));
            }
            // dentro: el vacío (tinta negra en los bordes, nebulosa violeta en el centro) — tira de triángulos
            var cv = Solid(l);
            Color ink = A(VoidInk, op);
            for (int i = 0; i < N; i++)
            {
                Color m0 = Color.Lerp(VoidInk, Neb, 0.5f * Mathf.Sin(Mathf.PI * i / N)), m1 = Color.Lerp(VoidInk, Neb, 0.5f * Mathf.Sin(Mathf.PI * (i + 1) / N));
                m0.a = op; m1.a = op;
                cv.Triangle(LeftEdge[i], LeftEdge[i + 1], Mid[i], ink, ink, m0); cv.Triangle(LeftEdge[i + 1], Mid[i + 1], Mid[i], ink, m1, m0);
                cv.Triangle(Mid[i], Mid[i + 1], RightEdge[i], m0, m1, ink); cv.Triangle(Mid[i + 1], RightEdge[i + 1], RightEdge[i], m1, ink, ink);
            }
            var S = new Rng(9);
            for (int i = 0; i < 26; i++)
            {   // estrellas dentro (siempre en la parte abierta: x dentro del perfil a esa altura)
                float v = 0.08f + S.Next() * 0.84f, w = Wd * Mathf.Pow(Mathf.Sin(Mathf.PI * v), 0.85f), sx = p.x + (S.Next() - 0.5f) * w * 1.6f, sy = p.y + H * 0.5f - H * v;
                float tw = 0.5f + 0.5f * Mathf.Sin(t * (3f + S.Next() * 5f) + i), r = (0.5f + S.Next() * 1.1f) * PX;
                Disc(l, new Vector2(sx, sy), r, i % 4 != 0 ? Color.white : ArsenalPal.Set.light, tw * op);
            }
            // bordes que arden (tres pasadas) + resplandor aditivo a lo largo del corte
            foreach (var side in new[] { LeftEdge, RightEdge })
            {
                cv.Stroke(side, 7f * PX * s, A(ArsenalPal.Set.dark, 0.45f * op));
                cv.Stroke(side, 3f * PX * s, A(ArsenalPal.Set.mid, 0.9f * op));
                cv.Stroke(side, 1.3f * PX, A(Color.white, op));
            }
            for (int i = 2; i < N; i += 4) Glow(Mid[i], (10f + Wd / PX) * PX * 0.9f, ArsenalPal.Set.g2, 0.18f * op);
            // chispas eléctricas que saltan del borde
            var B = new Rng(Mathf.Floor(t * 16f) * 3f + 1f);
            for (int i = 0; i < 3; i++)
            {
                bool left = B.Next() < 0.5f; var side = left ? LeftEdge : RightEdge; Vector2 q = side[1 + Mathf.FloorToInt(B.Next() * (N - 1))]; float d = left ? -1f : 1f;
                Bolt(l, q, q + new Vector2(d * (14f + B.Next() * 22f) * PX * s, (B.Next() - 0.5f) * 20f * PX * s), 4, ref B, 1.4f * PX, ArsenalPal.Set, 0.8f * op);
            }
            // escombros que orbitan y se cuelan por la grieta
            var D = new Rng(77);
            for (int i = 0; i < 12; i++)
            {
                float ph = D.Next(), rate = 0.35f + D.Next() * 0.3f, age = (t * rate + ph) % 1f, r = (130f * Mathf.Pow(1f - age, 1.3f) + 4f) * PX * s, th = D.Next() * TAU + age * 5f;
                Vector2 q = p + new Vector2(Mathf.Cos(th) * r, Mathf.Sin(th) * r * 0.6f); float sz = (2.5f + D.Next() * 3.5f) * PX * s * (0.4f + 0.6f * (1f - age));
                Chunk(l, q, sz, t * 4f + i, i, RockA, RockB, Mathf.Clamp01((1f - age) * 3f) * op);
            }
            EllArc(l, p, 22f * PX * s, H * 0.5f + 6f * PX * s, 0f, TAU, 1.2f * PX, ArsenalPal.Void.light, 0.25f * op, new ArcOpt { m = 40, noTaper = true, seed = 1 });
        }

        /// <summary>Colapso de la grieta: se cierra de golpe en una línea horizontal, escupe los escombros y estalla.</summary>
        public static void BurstRift(L l, float t, float t0, Vector2 p, float s, ArsenalPal pal, float gy, bool mass, bool fire)
        {
            float u = t - t0; if (u < 0f || u > 1.4f) return;
            float fu = Seg(u, 0f, 0.25f);
            if (l == L.Front)
            {
                Lens(l, p, 0f, 300f * PX * s * EaseOut(Seg(u, 0f, 0.1f)) * (1f - fu * 0.5f), 9f * PX * s * (1f - fu), Color.white, 1f - fu);
                Lens(l, p, 0f, 200f * PX * s, 3f * PX * s * (1f - fu), ArsenalPal.Set.light, 1f - fu);
                Glow(p, 120f * PX * s, pal.g1, 0.6f * (1f - fu));
            }
            var D = new Rng(78);
            for (int i = 0; i < 14; i++)
            {
                float a = D.Next() * TAU, v = (200f + D.Next() * 260f) * PX; bool back = D.Next() < 0.4f; float lf = 0.7f + D.Next() * 0.4f, sz = (3f + D.Next() * 4f) * PX * s;
                if ((back ? L.Back : L.Front) != l || u > lf) continue;
                Vector2 q = p + new Vector2(Mathf.Cos(a) * v * u, Mathf.Sin(a) * v * u * 0.7f - 300f * PX * u * u);
                Chunk(l, q, sz, u * 9f + i, i, RockA, RockB, 1f - Seg(u, lf * 0.7f, lf));
            }
            BurstRefined(l, t, t0 + 0.03f, p, s * 0.8f, pal, gy, mass, fire);
        }

        /// <summary>Estallido refinado: destellos anamórficos, dos ondas en anillo, onda de suelo si está cerca, chispas;
        /// con Masa grietas y escombros; con fuego, llamas que corren por el suelo.</summary>
        public static void BurstRefined(L l, float t, float t0, Vector2 p, float s, ArsenalPal pal, float gy, bool mass, bool fire)
        {
            float u = t - t0; if (u < 0f || u > 1.5f) return;
            float ru = Seg(u, 0f, 0.45f), fu = Seg(u, 0f, 0.2f); bool near = p.y - gy < 170f * PX * s;
            if (l == L.Front)
            {
                Glow(p, 150f * PX * s * (1f - fu * 0.4f), pal.g1, 0.75f * (1f - fu));
                Glow(p, 55f * PX * s, pal.g2, 0.9f * (1f - fu));
                Lens(l, p, 0f, 260f * PX * s * (1f - fu * 0.3f), 6f * PX * s * (1f - fu), Color.white, 1f - fu);          // destello anamórfico
                Lens(l, p, Mathf.PI * 0.5f, 90f * PX * s * (1f - fu * 0.4f), 4f * PX * s * (1f - fu), Color.white, 0.8f * (1f - fu));
                Disc(l, p, 16f * PX * s * (1f - fu), Color.white, 1f - fu);
                for (int k = 0; k < 2; k++)
                {
                    float d = k == 0 ? 0f : 0.08f, kk = k == 0 ? 1f : 0.75f, al = k == 0 ? 1f : 0.55f, v = Seg(u, d, d + 0.45f);
                    if (v <= 0f || v >= 1f) continue;
                    float r = (16f + 175f * EaseOut(v)) * PX * s * kk, th = (24f * (1f - v) + 3f) * PX * s, a = al * (1f - v);
                    GlowCanvas(l).Annulus(p, Mathf.Max(0.01f, r - th), r, 0.95f, A(pal.g1, 0f), A(pal.g1, 0.35f * a), A(pal.g2, 0.85f * a));
                    Solid(l).Annulus(p, r - th * 0.25f, r, 0.95f, A(Color.white, 0f), A(pal.light, 0.5f * a), A(Color.white, 0f));
                }
            }
            if (near)
            {
                float r = (20f + 150f * EaseOut(ru)) * PX * s, th = 18f * PX * s * (1f - ru) + 2f * PX, a = 1f - ru;
                Vector2 g = new Vector2(p.x, gy);
                if (l == L.Back) GlowCanvas(l).Annulus(g, Mathf.Max(0.01f, r - th), r, 0.2f, A(pal.g1, 0f), A(pal.g1, 0.35f * a), A(pal.g2, 0.8f * a), 0f, Mathf.PI);
                else GlowCanvas(l).Annulus(g, Mathf.Max(0.01f, r - th), r, 0.2f, A(pal.g1, 0f), A(pal.g1, 0.35f * a), A(pal.g2, 0.8f * a), Mathf.PI, TAU);
                Dust(l, t, t0, p.x, gy, 7, 10, 100f * s, 1.1f * s);
            }
            Sparks(l, t, t0, p, new SparkOpt { n = 10, pal = pal, sp = 420f * s, seed = 12, len = 22, life = 0.5f, w = 1.8f, a0 = 0, a1 = TAU, grav = 520, backShare = 0.45f });
            if (mass && near) { Cracks(l, t, t0, p.x, gy, pal, 41, 8, 110f); Debris(l, t, t0, p.x, gy, 14, 12, 340f); }
            if (fire && near)
                for (int i = 0; i < 12; i++)
                {
                    var Rf = new Rng(i * 5 + 2); int side = i % 2 == 1 ? 1 : -1; float d = (14f + Rf.Next() * 120f) * PX * s, tOn = t0 + d / (500f * PX * s), lf = 0.8f + Rf.Next() * 0.35f;
                    float a = Seg(t, tOn, tOn + 0.08f) * (1f - Seg(t, tOn + lf * 0.7f, tOn + lf)); bool back = Rf.Next() < 0.45f; float h = (22f + Rf.Next() * 22f) * PX, w = (6f + Rf.Next() * 5f) * PX;
                    if ((back ? L.Back : L.Front) == l) Flame(l, new Vector2(p.x + side * d, gy - PX), h * a, w * Mathf.Max(0.3f, a), t, i, a);
                }
        }
    }
}
