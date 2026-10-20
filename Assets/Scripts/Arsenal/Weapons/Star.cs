using System;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Items;
using UnityEngine;
using static RedMagic.Arsenal.ArsenalDraw;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// <b>6 · Golpe estelar</b> (<c>g-golpe-estelar.js</c>, ficha <c>docs/armas/golpe-estelar.md</c>): marca un punto y una lanza
    /// de luz cae del cielo un instante después; golpea toda la columna. Evoluciones: <b>Bombardeo</b> (fila de lanzas),
    /// <b>Cazador</b> (tus otras armas marcan; las lanzas caen solas sobre los marcados), <b>Tormenta</b> (lluvia de agujas).
    /// </summary>
    [Serializable]
    public sealed class StarKit : ArsenalKit
    {
        public const int Bombardeo = 1, Cazador = 2, Tormenta = 3;

        private static readonly ArsenalTree TreeData = new ArsenalTree("Marca un punto: una lanza de luz cae del cielo un instante después y golpea toda la columna.")
            .Add("Bombardeo", "Lanzas más pequeñas en fila desde donde apuntas hacia delante.",
                 "Salva", "+2 lanzas en la fila.",
                 "Final", "La última lanza es enorme (doble de ancho y de daño).",
                 "Rodante", "La fila sigue al ratón mientras va cayendo.")
            .Add("Cazador", "Tus otras armas marcan al golpear; las lanzas caen solas sobre los marcados (sin apuntar).",
                 "Acumulación", "Las marcas se apilan hasta 3: cada una suma daño a la lanza.",
                 "Ejecución", "Remata a los que quedan con poca vida y marca al enemigo más cercano.",
                 "Vigilancia", "Cada pocos segundos cae una lanza sola sobre un marcado, aunque lleves la otra arma.")
            .Add("Tormenta", "Lluvia de agujas en diagonal sobre una zona ancha.",
                 "Diluvio", "Más agujas, dura el doble y la zona sigue al ratón.",
                 "Agujas pesadas", "Cada aguja es golpe fuerte (rompe congelados) y hace más daño.",
                 "Ojo de la tormenta", "La lluvia atrae hacia el centro y acaba con una lanza en medio.");

        public override ArsenalTree Tree => TreeData;
        public override string WeaponId => "star";
        public override ArsenalController CreateController() => new StarController();

        [Header("Base")]
        [Tooltip("Recarga (s).")] [ArsenalCooldown] public float cooldown = 3f;
        [Tooltip("Aviso hasta que cae (s).")] public float delay = 0.6f;
        [Tooltip("Daño de la lanza.")] [ArsenalDamage] public float damage = 70f;
        [Tooltip("Ancho de la columna (u).")] [ArsenalSignature(0.2f, 2, true)] public float width = 1f;
        [Tooltip("Onda: daño.")] [ArsenalDamage] public float shockDamage = 15f;
        [Tooltip("Onda: radio (u).")] public float shockRadius = 3f;
        [Header("Elemento (build)")]
        [Tooltip("Hielo: radio que congela al caer (u).")] public float iceRadius = 2f;
        [Tooltip("Fuego: duración del cráter (s).")] public float craterTime = 1.3f;
        [Tooltip("Fuego: quemadura (s).")] public float craterBurn = 2f;
        [Header("Bombardeo")]
        public int barrageCount = 3;
        [Tooltip("Separación (u).")] public float barrageGap = 1.75f;
        [Tooltip("Tiempo entre lanzas (s).")] public float barrageStep = 0.12f;
        [Tooltip("Daño por lanza.")] [ArsenalDamage] public float barrageDamage = 45f;
        [Tooltip("Ancho (u).")] public float barrageWidth = 0.75f;
        [Tooltip("Final: × tamaño y daño.")] public float finaleMul = 2f;
        [Header("Cazador")]
        [Tooltip("Objetivos máx.")] public int maxTargets = 5;
        [Tooltip("Tiempo entre lanzas (s).")] public float chainGap = 0.15f;
        [Tooltip("Acumulación: + daño por marca extra.")] public float stackMul = 0.6f;
        [Tooltip("Ejecución: remata por debajo de (× vida).")] public float execThreshold = 0.25f;
        [Tooltip("Vigilancia: cada (s).")] public float watchEvery = 4f;
        [Header("Tormenta")]
        [Tooltip("Agujas.")] public int rainNeedles = 15;
        [Tooltip("Ancho (u).")] public float rainWidth = 4.25f;
        [Tooltip("Duración (s).")] public float rainTime = 0.55f;
        [Tooltip("Daño por aguja.")] [ArsenalDamage] public float needleDamage = 12f;
        [Tooltip("Diluvio: × agujas.")] public float delugeMul = 1.6f;
        [Tooltip("Agujas pesadas: × daño.")] public float heavyMul = 1.5f;
        [Tooltip("Ojo: fuerza de atracción (u/s).")] public float eyePull = 3.5f;

        public bool Salva => Up(Bombardeo, 0);
        public bool Final => Up(Bombardeo, 1);
        public bool Rodante => Up(Bombardeo, 2);
        public bool Acumulacion => Up(Cazador, 0);
        public bool Ejecucion => Up(Cazador, 1);
        public bool Vigilancia => Up(Cazador, 2);
        public bool Diluvio => Up(Tormenta, 0);
        public bool Pesadas => Up(Tormenta, 1);
        public bool Ojo => Up(Tormenta, 2);
    }

    public sealed class StarController : ArsenalController<StarKit>
    {
        private float _cd, _cdFull = 1f, _watchT;
        private ArsenalAim _lastAim;

        public ArsenalAim CurrentAim => User != null ? User.BuildArsenalAim() : _lastAim;
        public ArsenalPal Pal => ArsenalBuild.ElemPal(K.Evo(StarKit.Tormenta) ? ArsenalPal.Gold : ArsenalPal.Set);
        public bool Hunter => K.Evo(StarKit.Cazador);
        public bool Stacking => K.Acumulacion;

        public struct Opt { public float x, delay, dmg, width, scale; public Body target; public Func<float> track; public bool exec, noShock; }

        private void Signal(Vector2 from)
        {
            var P = Pal; User?.PlayAttackAnimation();
            ArsenalWorld.AddFx(0.35f, (l, tu) =>
            {
                if (l != L.Front) return;
                float u = Seg(ArsenalWorld.T, tu, tu + 0.3f), top = Mathf.Lerp(from.y, from.y + 12f, EaseOut(Seg(u, 0f, 0.4f)));
                var pts = TempPoints(); pts.Add(new Vector2(from.x, top)); pts.Add(from);
                StrokeTaper(l, pts, 6f * PX * (1f - u), P.light, 1f - u, false);
                StrokeTaper(l, pts, 2.4f * PX * (1f - u), P.core, 1f - u, false);
            });
            ArsenalSounds.Play("star_up", from);
        }

        /// <summary>Una lanza (con Duplicador, dos a los lados).</summary>
        public void LanceAt(Opt o)
        {
            int n = ArsenalBuild.Copies;
            for (int c = 0; c < n; c++) ArsenalWorld.Add(new StarStrike(this, o, n > 1 ? (c == 1 ? 1f : -1f) * o.width * 0.9f : 0f));
        }

        private void StrikeMarked(List<Body> list, float baseDelay)
        {
            var k = K;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i]; int marks = ArsenalMarks.Count(e.health);
                float mult = k.Acumulacion ? 1f + k.stackMul * (Mathf.Max(1, marks) - 1) : 1f;
                ArsenalMarks.Clear(e.health);
                LanceAt(new Opt { target = e, x = e.c.x, delay = baseDelay + i * k.chainGap, dmg = k.damage * mult, width = k.width, exec = k.Ejecucion, scale = 1f });
            }
        }

        public override void OnPress(ArsenalAim aim)
        {
            if (_cd > 0f) return;
            var k = K; _lastAim = aim; Signal(aim.origin);
            if (k.Evo(StarKit.Bombardeo))
            {
                _cd = _cdFull = k.cooldown * 1.15f;
                int n = k.barrageCount + (k.Salva ? 2 : 0); float dir = Mathf.Sign(aim.target.x - ArsenalCombat.PlayerCenter().x); if (dir == 0f) dir = 1f;
                for (int i = 0; i < n; i++)
                {
                    bool big = i == n - 1 && k.Final; float kk = big ? k.finaleMul : 1f, off = i * k.barrageGap * dir;
                    Func<float> track = k.Rodante ? (Func<float>)(() => CurrentAim.target.x + off) : null;
                    LanceAt(new Opt { x = aim.target.x + off, track = track, delay = k.delay + i * k.barrageStep, dmg = k.barrageDamage * kk, width = k.barrageWidth * kk, scale = 0.75f * kk });
                }
            }
            else if (k.Evo(StarKit.Cazador))
            {
                _cd = _cdFull = k.cooldown;
                var pc = ArsenalCombat.PlayerCenter();
                var marked = new List<Body>();
                foreach (var e in ArsenalTargets.Alive()) if (ArsenalMarks.IsMarked(e.health)) marked.Add(e);
                marked.Sort((a, b) => Mathf.Abs(a.c.x - pc.x).CompareTo(Mathf.Abs(b.c.x - pc.x)));
                if (marked.Count > k.maxTargets) marked.RemoveRange(k.maxTargets, marked.Count - k.maxTargets);
                if (marked.Count > 0) StrikeMarked(marked, k.delay * 0.6f);
                else LanceAt(new Opt { x = aim.target.x, delay = k.delay, dmg = k.damage, width = k.width, scale = 1f });
            }
            else if (k.Evo(StarKit.Tormenta))
            {
                _cd = _cdFull = k.cooldown * 1.15f;
                ArsenalWorld.Add(new StarRain(this, aim.target.x));
            }
            else
            {
                _cd = _cdFull = k.cooldown;
                LanceAt(new Opt { x = aim.target.x, delay = k.delay, dmg = k.damage, width = k.width, scale = 1f });
            }
        }

        public override void Tick(float dt, bool current)
        {
            _cd -= dt;
            var k = K;
            if (k.Evo(StarKit.Cazador) && k.Vigilancia)
            {   // Vigilancia: aunque no sea el arma activa
                _watchT -= dt;
                if (_watchT <= 0f)
                {
                    var m = new List<Body>(); foreach (var e in ArsenalTargets.Alive()) if (ArsenalMarks.IsMarked(e.health)) m.Add(e);
                    if (m.Count > 0) { _watchT = k.watchEvery; StrikeMarked(new List<Body> { m[UnityEngine.Random.Range(0, m.Count)] }, 0.35f); }
                    else _watchT = 0.5f;
                }
            }
        }

        public override float Ready => 1f - Mathf.Clamp01(_cd / Mathf.Max(0.01f, _cdFull));
        public override void Refund(float seconds) => _cd -= seconds;
    }

    /// <summary>Una lanza: aviso (círculo rúnico y destello arriba), cae, golpea toda la columna y deja onda, grietas y elemento.</summary>
    public sealed class StarStrike : ArsenalEntity
    {
        private readonly StarController _ctrl; private readonly StarKit _k; private readonly StarController.Opt _o;
        private readonly ArsenalElement _el; private readonly ArsenalPal _pal; private readonly float _off, _t0, _ts, _ti, _sc;
        private float _x, _lastX, _gy; private bool _done, _fallPlayed;
        private const float Fall = 0.07f;

        public StarStrike(StarController ctrl, StarController.Opt o, float offset)
        {
            _ctrl = ctrl; _k = (StarKit)ctrl.KitBase; _o = o; _off = offset; _el = ArsenalBuild.Element; _pal = ctrl.Pal; _sc = o.scale <= 0f ? 1f : o.scale;
            _t0 = ArsenalWorld.T; _ts = _t0 + o.delay; _ti = _ts + Fall;
            _lastX = o.target != null ? o.target.c.x : o.x;
            _x = XAt(); _gy = Ground(_x);
            if (!o.noShock) ArsenalSounds.Play("star_mark", new Vector2(_x, _gy));
        }

        private float T => ArsenalWorld.T;
        private static float Ground(float x) => ArsenalTargets.GroundY(x, ArsenalWorld.ViewTop(30f) + 2f, 80f);
        private float Top => ArsenalWorld.ViewTop(_gy + 10f) + 1f;

        // si el objetivo muere antes de caer, la lanza cae donde estaba
        private float XAt()
        {
            _lastX = _o.target != null ? (_o.target.Dead ? _lastX : (ArsenalTargets.Of(_o.target.health)?.c.x ?? _lastX)) : _o.track != null ? _o.track() : _o.x;
            return _lastX + _off;
        }

        public override bool Tick(float dt)
        {
            if (T < _ts) { _x = XAt(); _gy = Ground(_x); }
            if (!_fallPlayed && T >= _ts - 0.42f) { _fallPlayed = true; if (!_o.noShock) ArsenalSounds.Play("star_fall", new Vector2(_x, _gy)); }
            if (!_done && T >= _ti) { _done = true; Land(); }
            return T < _ti + 0.32f;
        }

        private void Land()
        {
            var k = _k; float x = _x;
            ArsenalMarks.Suppress = true;     // los golpes de la propia lanza no marcan
            var list = new List<Body>(ArsenalTargets.Alive());
            foreach (var e in list)
            {
                if (Mathf.Abs(e.c.x - x) < _o.width * 0.5f + e.rx)
                {
                    bool exec = _o.exec;
                    ArsenalCombat.Hit(e, new ArsenalHit
                    {
                        dmg = _o.dmg, from = new Vector2(x, e.c.y), knock = 0.35f, launch = e.flies ? 0f : 3f, heavy = true, elem = _el, kind = HitKind.Burst,
                        freeze = _el == ArsenalElement.Ice ? 1f : 0f, burn = _el == ArsenalElement.Fire ? k.craterBurn : 0f, hitstop = 0.08f * _sc, shake = 0.25f * _sc,
                        onKill = exec ? (Action<Body>)(dead => MarkNearest(dead.c)) : null,
                    });
                    if (exec && !e.Dead && e.health.CurrentHealth < e.health.MaxHealth * k.execThreshold)
                    {   // Ejecución: remata y marca al siguiente
                        ArsenalCombat.Hit(e, new ArsenalHit { dmg = e.health.CurrentHealth + 1f, dot = true, noReact = true, kind = HitKind.Burst });
                        MarkNearest(e.c);
                    }
                    if (e.flies && !e.Dead && e.knockback != null) e.knockback.ApplyVelocity(new Vector2(0f, -15f), 0.4f);   // estampa a los voladores
                }
                else if (!_o.noShock && Mathf.Abs(e.c.x - x) < k.shockRadius * _sc && e.c.y - _gy < 2.25f)
                {
                    ArsenalCombat.Hit(e, new ArsenalHit { dmg = k.shockDamage * _sc, from = new Vector2(x, e.c.y), knock = 0.7f, noReact = true, elem = _el, kind = HitKind.Burst, freeze = _el == ArsenalElement.Ice && Mathf.Abs(e.c.x - x) < k.iceRadius ? 0.5f : 0f });
                }
            }
            ArsenalMarks.Suppress = false;
            ArsenalSounds.Play(_o.noShock ? "star_needle" : "star_impact", new Vector2(x, _gy));
            var P = _pal; float gy = _gy, sc = _sc;
            ArsenalWorld.AddFx(1.4f, (l, ti) =>
            {
                GroundImpact(l, ArsenalWorld.T, ti, x, gy, 1.35f * sc, P, Mathf.Floor(x * 10f));
                Cracks(l, ArsenalWorld.T, ti, x, gy, P, 41, 8, 110f * sc);
                if (l == L.Front) { float u = Seg(ArsenalWorld.T, ti, ti + 0.2f); if (u < 1f) Glow(new Vector2(x, gy + 0.75f), 150f * PX * sc, P.g1, 0.7f * (1f - u)); }
            });
            if (_el == ArsenalElement.Ice) { float r = k.iceRadius * _sc; ArsenalWorld.AddFx(1.6f, (l, ti) => FrostNova(l, ArsenalWorld.T, ti, x, gy, r)); }
            if (_el == ArsenalElement.Fire) ArsenalWorld.Add(new FireZone(x, _gy, 1.75f * _sc, k.craterTime, k.craterBurn));
        }

        private static void MarkNearest(Vector2 at)
        {
            var n = ArsenalTargets.Nearest(at, 7.5f);
            if (n != null) ArsenalMarks.Mark(n.health, false, force: true);
        }

        public override void Draw(L l)
        {
            float t = T;
            if (t < _ts + 0.02f)
            {   // círculo rúnico en el suelo y destello arriba
                float a = EaseOut(Seg(t, _t0, _t0 + 0.12f)), r = Mathf.Lerp(74f, 46f, EaseInOut(Seg(t, _t0, _ts))) * PX * Mathf.Max(0.6f, _sc);
                StarDraw.RuneCircle(l, t, _x, _gy, r, a, _pal, Top);
                if (l == L.Front) { float k = Seg(t, _t0, _ts); Vector2 sky = new Vector2(_x, Top - 1.2f); Star4(l, sky, (8f + 16f * k) * PX * _sc * (0.8f + 0.2f * Mathf.Sin(t * 40f)), t, _pal.core, a); Glow(sky, 40f * PX * k * _sc, _pal.g2, a * k); }
            }
            if (t >= _ts)
            {
                float fallU = Seg(t, _ts, _ti), y = Mathf.Lerp(Top, _gy, EaseIn(fallU)), after = Seg(t, _ti + 0.12f, _ti + 0.3f), w = 18f * PX * _sc * (1f - after);
                if (after < 1f) StarDraw.Lance(l, t, _x, y, w, 1f - after * after, _pal, Top);
                if (t < _ti && l == L.Front)
                    for (int i = 0; i < 6; i++)
                    {
                        var R = new Rng(i + 70); float ox = (R.Next() - 0.5f) * 60f * PX * _sc, len = (40f + R.Next() * 50f) * PX, yy = Mathf.Lerp(Top + 1.5f, _gy, EaseIn(Seg(t, _ts - 0.02f * i, _ti)));
                        Needle(l, new Vector2(_x + ox, yy + len), new Vector2(_x + ox, yy), 2f * PX, _pal.light, 0.7f);
                    }
            }
        }

        private static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
    }

    /// <summary>Tormenta: agujas en diagonal repartidas en una franja; cada una golpea lo primero que corta.</summary>
    public sealed class StarRain : ArsenalEntity
    {
        private struct Needle1 { public float rel, t0, t1; public bool done, set; public float tx, gy; }
        private readonly StarController _ctrl; private readonly StarKit _k; private readonly ArsenalElement _el; private readonly ArsenalPal _pal;
        private readonly bool _deluge, _heavy, _eye; private readonly float _t0, _ts, _dur;
        private readonly List<Needle1> _needles = new List<Needle1>();
        private float _cx, _gy; private bool _lanceDone, _sfx; private float _lastNeedleSfx = -9f;
        private static readonly float Ang = -(Mathf.PI * 0.5f - 0.42f);   // hacia abajo y a la derecha
        private const float Len = 420f * PX;

        public StarRain(StarController ctrl, float cx)
        {
            _ctrl = ctrl; _k = (StarKit)ctrl.KitBase; _el = ArsenalBuild.Element; _pal = ctrl.Pal;
            _deluge = _k.Diluvio; _heavy = _k.Pesadas; _eye = _k.Ojo;
            _t0 = ArsenalWorld.T; _ts = _t0 + _k.delay; _dur = _k.rainTime * (_deluge ? 2f : 1f); _cx = cx; _gy = ArsenalTargets.GroundY(cx, ArsenalCombat.PlayerCenter().y + 10f, 60f);
            int count = Mathf.RoundToInt(_k.rainNeedles * (_deluge ? _k.delugeMul : 1f) * ArsenalBuild.Copies);
            for (int i = 0; i < count; i++)
            {
                var R = new Rng(i + 900 + Mathf.Floor(_t0 * 10f));
                float rel = -_k.rainWidth * 0.5f + ((i * 7) % count) / (float)count * _k.rainWidth + (R.Next() - 0.5f) * 10f * PX, t1 = _ts + i / (float)count * _dur + R.Next() * 0.02f;
                _needles.Add(new Needle1 { rel = rel, t1 = t1, t0 = t1 - 0.12f });
            }
            ArsenalSounds.Play("star_mark", new Vector2(cx, _gy));
        }

        private float T => ArsenalWorld.T;
        private static Vector2 Dir0 => Dir(Ang);

        public override bool Tick(float dt)
        {
            var k = _k;
            if (_deluge && T < _ts + _dur) _cx = Mathf.Lerp(_cx, _ctrl.CurrentAim.target.x, 0.06f);
            if (_eye && T >= _ts && T < _ts + _dur) foreach (var e in ArsenalTargets.Alive()) if (Mathf.Abs(e.c.x - _cx) < k.rainWidth * 0.5f + 1f) ArsenalCombat.Pull(e, new Vector2(_cx, e.c.y), k.eyePull);
            for (int i = 0; i < _needles.Count; i++)
            {
                var n = _needles[i];
                if (!n.set && T >= n.t0) { n.set = true; n.tx = _cx + n.rel; n.gy = ArsenalTargets.GroundY(n.tx, _gy + 6f, 20f); }   // la aguja fija su sitio al aparecer
                if (n.done || T < n.t1) { _needles[i] = n; continue; }
                n.done = true; _needles[i] = n;
                Vector2 end = new Vector2(n.tx, n.gy), start = end - Dir0 * Len;
                bool onE = ArsenalTargets.Raycast(start, end, out var h);
                if (onE) ArsenalCombat.Hit(h.body, new ArsenalHit { dmg = k.needleDamage * (_heavy ? k.heavyMul : 1f), from = h.p - Dir0, knock = 0.2f, heavy = _heavy, elem = _el, kind = HitKind.Projectile, freeze = _el == ArsenalElement.Ice ? 0.2f : 0f, burn = _el == ArsenalElement.Fire ? 1f : 0f, shake = 0.05f, hitstop = 0.015f });
                Vector2 ip = onE ? h.p : end; var P = _pal; float a = Ang;
                ArsenalWorld.AddFx(1.2f, (l, ti) =>
                {
                    if (onE) Impact(l, ArsenalWorld.T, ti, ip, 0.7f, P, a + Mathf.PI, 2.8f, Mathf.Floor(ip.x * 10f));
                    else
                    {
                        GroundImpact(l, ArsenalWorld.T, ti, ip.x, ip.y, 0.6f, P, Mathf.Floor(ip.x * 10f), true, false);
                        if (l == L.Front && ArsenalWorld.T < ti + 0.5f) Lens(l, ip - Dir(a) * 8f * PX, a, 22f * PX, 3f * PX, P.light, 1f - Seg(ArsenalWorld.T, ti + 0.2f, ti + 0.5f));   // la aguja clavada
                    }
                });
                if (T - _lastNeedleSfx > 0.03f) { _lastNeedleSfx = T; ArsenalSounds.Play("star_needle", ip, 0.03f); }
            }
            if (_eye && !_lanceDone && T >= _ts + _dur - 0.1f)
            {
                _lanceDone = true;
                _ctrl.LanceAt(new StarController.Opt { x = _cx, delay = 0.1f, dmg = k.damage, width = k.width, scale = 1f });
            }
            return T < _ts + _dur + 0.3f;
        }

        public override void Draw(L l)
        {
            float t = T;
            float top = ArsenalWorld.ViewTop(_gy + 10f) + 1f;
            if (t < _ts + 0.02f || (_eye && t < _ts + _dur))
            {
                float a = EaseOut(Seg(t, _t0, _t0 + 0.12f)) * (t > _ts ? 0.6f : 1f), r = Mathf.Lerp(110f, 88f, Seg(t, _t0, _ts)) * PX;
                StarDraw.RuneCircle(l, t, _cx, _gy, r, a, _pal, top);
                if (l == L.Front && t < _ts + 0.02f) { float k = Seg(t, _t0, _ts); Vector2 sky = new Vector2(_cx - 90f * PX, top - 1.2f); Star4(l, sky, (8f + 16f * k) * PX * (0.8f + 0.2f * Mathf.Sin(t * 40f)), t, _pal.core, a); Glow(sky, 40f * PX * k, _pal.g2, a * k); }
            }
            foreach (var n in _needles)
            {
                if (!n.set || t < n.t0 || t >= n.t1) continue;
                Vector2 end = new Vector2(n.tx, n.gy), start = end - Dir0 * Len;
                float hu = ArsenalTargets.Raycast(start, end, out var h) ? h.u : 1f, u = Seg(t, n.t0, n.t1) * hu;
                StarDraw.RainNeedle(l, Vector2.Lerp(start, end, u), Ang, _pal);
            }
        }
    }

    /// <summary>
    /// Marcas del Cazador (regla común, no de un arma): con un Golpe estelar en evolución Cazador equipado (activo o de
    /// reserva), cualquier golpe del jugador de otra arma —incluida la espada— marca al enemigo 8 s; con Acumulación se apilan
    /// hasta 3 (un golpe por ataque). Las dibuja <see cref="ArsenalStatusDrawer"/>.
    /// </summary>
    public static class ArsenalMarks
    {
        private struct MarkData { public float until, lastHit; public int n; }
        private static readonly Dictionary<Health, MarkData> Marks = new Dictionary<Health, MarkData>();
        private static readonly List<Health> Dead = new List<Health>();
        public const float Duration = 8f;
        /// <summary>Mientras cae una lanza: sus propios golpes no marcan.</summary>
        public static bool Suppress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Marks.Clear(); Suppress = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook() { PlayerHit.Landed -= OnLanded; PlayerHit.Landed += OnLanded; }

        private static StarController Hunter()
        {
            var u = WeaponUser.Current; if (u == null) return null;
            foreach (var c in u.OtherArsenal(null)) if (c is StarController s && s.Hunter) return s;
            return null;
        }

        private static void OnLanded(HitInfo hit)
        {
            if (Suppress || hit.Target == null || hit.Kind == HitKind.Burst) return;
            var hunter = Hunter(); if (hunter == null) return;
            Mark(hit.Target, hunter.Stacking);
        }

        public static void Mark(Health h, bool stack, bool force = false)
        {
            if (h == null || h.IsDead) return;
            float now = ArsenalWorld.T;
            Marks.TryGetValue(h, out var m);
            bool live = m.until > now, fresh = now - m.lastHit > 0.25f;
            m.n = stack ? Mathf.Clamp((live ? m.n : 0) + (fresh || force ? 1 : 0), 1, 3) : 1;
            m.lastHit = now; m.until = now + Duration;
            Marks[h] = m;
        }

        public static bool IsMarked(Health h) => h != null && !h.IsDead && Marks.TryGetValue(h, out var m) && m.until > ArsenalWorld.T;
        public static int Count(Health h) => IsMarked(h) ? Marks[h].n : 0;
        public static void Clear(Health h) { if (h != null) Marks.Remove(h); }

        /// <summary>Rombo que gira encima de cada marcado (uno por marca apilada).</summary>
        public static void Draw(L l)
        {
            if (l != L.Front || Marks.Count == 0) return;
            float t = ArsenalWorld.T; Dead.Clear();
            foreach (var kv in Marks)
            {
                if (kv.Key == null || kv.Key.IsDead || kv.Value.until <= t) { Dead.Add(kv.Key); continue; }
                var b = ArsenalTargets.Of(kv.Key); if (b == null) continue;
                float a = Mathf.Min(1f, kv.Value.until - t); int n = kv.Value.n;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = new Vector2(b.c.x + (i - (n - 1) * 0.5f) * 0.32f, b.c.y + b.ry + 0.55f);
                    StarDraw.Diamond(l, p, 0.16f, t * 2.5f + i, ArsenalPal.Set, a);
                }
            }
            foreach (var h in Dead) Marks.Remove(h);
        }
    }

    /// <summary>Dibujo del Golpe estelar (<c>fx.js ▸ runeCircle, starLance, rainNeedle</c>).</summary>
    public static class StarDraw
    {
        /// <summary>Círculo rúnico en el suelo: arcos que giran en sentidos contrarios, marcas radiales y columna guía tenue hacia arriba.</summary>
        public static void RuneCircle(L l, float t, float x, float gy, float r, float a, ArsenalPal pal, float top)
        {
            if (a <= 0f) return;
            for (int i = 0; i < 4; i++)
            {
                float a0 = (i / 4f) * TAU + t * (i % 2 == 1 ? 1.6f : -1.2f);
                bool back = Mathf.Sin(a0 + 0.6f) > 0f;           // mitad lejana (arriba) detrás
                if ((back ? L.Back : L.Front) == l) EllArc(l, new Vector2(x, gy - PX), r, r * 0.24f, a0, a0 + 1.2f, 3f * PX, i % 2 == 1 ? pal.mid : pal.light, a, new ArcOpt { m = 12, seed = 1, taper = 1 });
            }
            GroundRing(l, x, gy - PX, r * 0.7f, 1.4f * PX, pal.core, a * 0.8f, new ArcOpt { m = 32, noTaper = true, seed = 4 });
            for (int i = 0; i < 12; i++)
            {
                float th = i / 12f * TAU - t * 0.8f; bool back = Mathf.Sin(th) > 0f;
                if ((back ? L.Back : L.Front) != l) continue;
                Vector2 c = new Vector2(x + Mathf.Cos(th) * r * 0.85f, gy - PX + Mathf.Sin(th) * r * 0.85f * 0.24f);
                Needle(l, c + new Vector2(0f, 3f * PX), c - new Vector2(0f, 3f * PX), 2f * PX, pal.light, a * 0.9f);
            }
            if (l == L.Back)
            {   // columna guía: luz que sube del círculo, más fuerte en el centro y abajo, bordes y cima a 0
                var cv = GlowCanvas(l); Color lo = A(pal.g2, 0.16f * a), clear = A(pal.g2, 0f);
                float hw = r * 0.35f, h = Mathf.Min(top - gy, 9f);
                Vector2 bl = new Vector2(x - hw, gy), bc = new Vector2(x, gy), br = new Vector2(x + hw, gy), tl = new Vector2(x - hw * 0.6f, gy + h), tc = new Vector2(x, gy + h), tr = new Vector2(x + hw * 0.6f, gy + h);
                cv.Triangle(bl, bc, tc, clear, lo, clear); cv.Triangle(bl, tc, tl, clear, clear, clear);
                cv.Triangle(bc, br, tr, lo, clear, clear); cv.Triangle(bc, tr, tc, lo, clear, clear);
            }
        }

        /// <summary>Lanza de luz que cae: columna por capas desde arriba hasta la cabeza (<paramref name="hy"/>); tras el impacto se afina.</summary>
        public static void Lance(L l, float t, float x, float hy, float w, float a, ArsenalPal pal, float top)
        {
            if (l != L.Front || a <= 0f) return;
            var pts = TempPoints(); for (int i = 0; i <= 14; i++) pts.Add(new Vector2(x + Mathf.Sin(i * 1.3f + t * 40f) * 1.2f * PX, Mathf.Lerp(hy, top, i / 14f)));
            Ribbon(l, pts, s => w * 2.4f * (1f - 0.35f * s), pal.g1, 0.12f * a, true);   // halo suave
            Ribbon(l, pts, s => w * 1.7f * (1f - 0.35f * s), pal.dark, 0.3f * a);
            Ribbon(l, pts, s => w * 1.15f * (1f - 0.35f * s), pal.deep, 0.55f * a);
            Ribbon(l, pts, s => w * 0.8f * (1f - 0.35f * s), pal.mid, 0.9f * a);
            Ribbon(l, pts, s => w * 0.5f * (1f - 0.35f * s), pal.light, a);
            Ribbon(l, pts, s => w * 0.22f * (1f - 0.35f * s), pal.core, a);
            Glow(new Vector2(x, hy), w * 2.6f, pal.g1, 0.7f * a);
            Lens(l, new Vector2(x, hy + w * 0.6f), Mathf.PI * 0.5f, w * 2.6f, w * 0.7f, pal.core, a);
        }

        /// <summary>Aguja de la lluvia en vuelo (punta en <paramref name="p"/>, <paramref name="ang"/> = dirección).</summary>
        public static void RainNeedle(L l, Vector2 p, float ang, ArsenalPal pal)
        {
            if (l != L.Front) return;
            Vector2 d = Dir(ang);
            var pts = TempPoints(); pts.Add(p); pts.Add(p - d * 120f * PX);
            StrokeTaper(l, pts, 9f * PX, pal.deep, 0.5f);
            pts = TempPoints(); pts.Add(p); pts.Add(p - d * 100f * PX);
            StrokeTaper(l, pts, 5f * PX, pal.mid, 0.9f);
            Lens(l, p - d * 14f * PX, ang, 38f * PX, 6f * PX, pal.light, 1f);
            Lens(l, p - d * 12f * PX, ang, 30f * PX, 3f * PX, pal.core, 1f);
            Glow(p, 26f * PX, pal.g1, 0.7f);
        }

        /// <summary>Rombo de marca que gira (con brillo).</summary>
        public static void Diamond(L l, Vector2 p, float r, float rot, ArsenalPal pal, float a)
        {
            if (l != L.Front || a <= 0f) return;
            float sx = Mathf.Cos(rot);
            var cv = Solid(l);
            Glow(p, r * 2.2f, pal.g1, 0.5f * a);
            foreach (var (k, c) in new[] { (1f, pal.deep), (0.7f, pal.mid), (0.38f, pal.core) })
            {
                float rr = r * k; var pts = TempPoints();
                pts.Add(p + new Vector2(0f, rr * 1.35f)); pts.Add(p + new Vector2(rr * sx, 0f)); pts.Add(p + new Vector2(0f, -rr * 1.35f)); pts.Add(p + new Vector2(-rr * sx, 0f));
                cv.Polygon(pts, A(c, a));
            }
        }
    }
}
