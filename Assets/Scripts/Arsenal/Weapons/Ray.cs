using System;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Items;
using UnityEngine;
using static RedMagic.Arsenal.ArsenalDraw;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// <b>3 · Rayo</b> (<c>g-rayo.js</c>, ficha <c>docs/armas/rayo.md</c>): mantener = canalizar un haz con calor (al máximo se
    /// corta solo). Evoluciones: <b>Sobrecarga</b> (el daño crece con el calor y al soltar descarga por toda la línea),
    /// <b>Prisma</b> (se divide en haces dorados; Corona), <b>Faro</b> (al soltar, el cristal se queda disparando solo).
    /// </summary>
    [Serializable]
    public sealed class RayKit : ArsenalKit
    {
        public const int Sobrecarga = 1, Prisma = 2, Faro = 3;

        private static readonly ArsenalTree TreeData = new ArsenalTree("Mantén para canalizar un haz que quema al primero que toca. Si te pasas, se corta.")
            .Add("Sobrecarga", "El daño crece mientras canalizas y al soltar descarga a lo largo de todo el haz. Si llegas al máximo se recalienta y tarda más en recargar.",
                 "Punto justo", "Soltar en el último tramo del calor, sin llegar al máximo: descarga doble y golpe fuerte.",
                 "Perforante", "Con más de medio calor el haz atraviesa a todos los de la línea.",
                 "Onda", "La descarga estalla también en el punto de contacto, en área.")
            .Add("Prisma", "Atraviesa al primero y se divide en haces dorados hacia los enemigos más cercanos.",
                 "Cadena", "Cada haz dorado salta a un enemigo más.",
                 "Foco", "Cada haz que no encuentra a quién ir se suma al principal (bueno contra uno solo).",
                 "Corona", "Haces dorados cortos giran alrededor de la gema y cortan a todo lo que rodea al primero.")
            .Add("Faro", "Al soltar, el cristal se queda flotando donde estabas y sigue disparando solo al enemigo más cercano. Premia cambiar de arma.",
                 "Vínculo", "Un hilo de luz une el faro contigo: daña lo que lo cruza y parte del daño del faro te cura.",
                 "Estallido", "Al apagarse, el faro lo descarga todo de golpe: estallido en área donde apunta (golpe fuerte).",
                 "Vigía", "Cada baja del faro alarga su vida.");

        public override ArsenalTree Tree => TreeData;
        public override string WeaponId => "ray";
        public override ArsenalController CreateController() => new RayController();

        [Header("Base")]
        [Tooltip("Canalización máxima (s).")] public float maxChannel = 2f;
        [Tooltip("Recarga tras soltar (s).")] public float cooldown = 0.8f;
        [Tooltip("Alcance (u).")] public float range = 13f;
        [Tooltip("Giro hacia el ratón (rad/s).")] public float turnRate = 9f;
        [Tooltip("Cada cuánto quema (s).")] public float tick = 0.1f;
        [Tooltip("Daño por tick.")] public float tickDamage = 6f;
        [Tooltip("Empujón por tick (× el del enemigo).")] public float push = 0.12f;
        [Header("Elemento y Duplicador (build)")]
        [Tooltip("Hielo: congelación por tick.")] public float freezeTick = 0.07f;
        [Tooltip("Fuego: quemadura por tick (s).")] public float burnTick = 0.6f;
        [Tooltip("Duplicador: separación de los dos haces (rad).")] public float dupAngle = 0.07f;
        [Header("Sobrecarga")]
        [Tooltip("× daño por tick con el calor al máximo.")] public float overMax = 2.5f;
        [Tooltip("Daño de la descarga (calor al máximo).")] public float overBurst = 40f;
        [Tooltip("Calor mínimo para descargar (fracción).")] public float overMin = 0.2f;
        [Tooltip("Calor para que la descarga sea golpe fuerte.")] public float overHeavyAt = 0.5f;
        [Tooltip("× recarga si se recalienta.")] public float overHeatCd = 2f;
        [Tooltip("Punto justo: empieza la franja (fracción del calor).")] public float sweetFrom = 0.75f;
        [Tooltip("Punto justo: × descarga.")] public float sweetMul = 2f;
        [Tooltip("Perforante: calor a partir del que atraviesa.")] public float pierceAt = 0.5f;
        [Tooltip("Onda: radio (u).")] public float waveRadius = 2.25f;
        [Tooltip("Onda: daño (calor al máximo).")] public float waveDamage = 30f;
        [Header("Prisma")]
        public int prismCount = 2;
        [Tooltip("Alcance (u).")] public float prismRange = 7.5f;
        [Tooltip("Daño de cada haz (×).")] public float prismMul = 0.6f;
        [Tooltip("Cadena: saltos extra por haz.")] public int chainJumps = 1;
        [Tooltip("Foco: + daño del principal por haz sin usar.")] public float focusMul = 0.6f;
        [Tooltip("Corona: haces que giran.")] public int crownCount = 3;
        [Tooltip("Corona: largo (u).")] public float crownLen = 4.25f;
        [Tooltip("Corona: giro (rad/s).")] public float crownSpin = 5f;
        [Tooltip("Corona: daño de cada haz (×).")] public float crownMul = 0.9f;
        [Header("Faro")]
        [Tooltip("Duración (s).")] public float beaconTime = 3f;
        [Tooltip("× daño por tick.")] public float beaconMul = 0.8f;
        [Tooltip("Alcance para buscar enemigo (u).")] public float beaconRange = 10.5f;
        [Tooltip("Vínculo: daño/s del hilo.")] public float linkDps = 20f;
        [Tooltip("Vínculo: fracción del daño del faro que cura.")] public float lifesteal = 0.3f;
        [Tooltip("Estallido: daño.")] public float novaDamage = 50f;
        [Tooltip("Estallido: radio (u).")] public float novaRadius = 2.75f;
        [Tooltip("Vigía: + s de vida por baja.")] public float watchTime = 1f;

        public bool PuntoJusto => Up(Sobrecarga, 0);
        public bool Perforante => Up(Sobrecarga, 1);
        public bool Onda => Up(Sobrecarga, 2);
        public bool Cadena => Up(Prisma, 0);
        public bool Foco => Up(Prisma, 1);
        public bool Corona => Up(Prisma, 2);
        public bool Vinculo => Up(Faro, 0);
        public bool Estallido => Up(Faro, 1);
        public bool Vigia => Up(Faro, 2);
    }

    public sealed class RayController : ArsenalController<RayKit>
    {
        private float _cd, _cdFull = 1f;
        private List<RayBeam> _beam, _beacons = new List<RayBeam>();
        private ArsenalAim _lastAim;

        public ArsenalAim CurrentAim => User != null ? User.BuildArsenalAim() : _lastAim;

        public override void OnPress(ArsenalAim aim)
        {
            if (_cd > 0f || _beam != null) return;
            _lastAim = aim;
            int n = ArsenalBuild.Copies; _beam = new List<RayBeam>(n);
            for (int c = 0; c < n; c++)
            {
                var b = new RayBeam(this, aim, n > 1 ? (c == 1 ? 1f : -1f) * K.dupAngle : 0f, c > 0, c);
                _beam.Add(b); ArsenalWorld.Add(b);
            }
        }

        public override void OnHold(ArsenalAim aim, float dt)
        {
            if (_beam == null) return;
            _lastAim = aim;
            foreach (var b in _beam) b.Aim(aim, dt);
            if (_beam[0].Heat >= K.maxChannel) Stop(true);
        }

        public override void OnRelease(ArsenalAim aim) => Stop(false);

        /// <summary><paramref name="maxed"/> = se cortó solo al llegar al máximo (Sobrecarga: recalentado).</summary>
        private void Stop(bool maxed)
        {
            if (_beam == null) return;
            var k = K;
            if (k.Evo(RayKit.Faro)) { foreach (var b in _beacons) b.End(); _beacons = _beam; }   // los faros nuevos sustituyen a los viejos
            foreach (var b in _beam) b.Release(maxed);
            _beam = null;
            _cd = _cdFull = k.cooldown * (k.Evo(RayKit.Sobrecarga) && maxed ? k.overHeatCd : 1f);
        }

        public override void Tick(float dt, bool current)
        {
            _cd -= dt;
            if (_beam != null && !current) Stop(false);
        }

        public override float Ready => _beam != null ? 1f - _beam[0].Heat / K.maxChannel : 1f - Mathf.Clamp01(_cd / Mathf.Max(0.01f, _cdFull));
        public override void Refund(float seconds) => _cd -= seconds;

        public override void Dispose()
        {
            if (_beam != null) foreach (var b in _beam) b.Release(false);
            foreach (var b in _beacons) b.End();
            _beam = null; _beacons.Clear();
        }
    }

    /// <summary>
    /// Haz: lo sostiene el player; con Faro, al soltar se planta y dispara solo; al final se apaga. <c>idx</c> = copia del
    /// Duplicador (el faro de la copia busca al segundo más cercano).
    /// </summary>
    public sealed class RayBeam : ArsenalEntity
    {
        private enum Mode { Held, Beacon }
        private struct Sub { public Body e; public Vector2 from; public bool chain; }
        private struct Seg2 { public Vector2 a, b; public float ang; }

        private readonly RayController _ctrl;
        private readonly RayKit _k;
        private readonly ArsenalElement _el;
        private readonly bool _quiet, _over, _prism, _beacon;
        private readonly int _idx;
        private readonly float _angOff, _t0;
        private readonly ArsenalPal _pal, _gp = ArsenalPal.Gold, _lp = ArsenalPal.Life;
        private Mode _mode = Mode.Held;
        private float _ang, _tickT, _linkT, _released = -1f, _healAcc, _crownA, _plantT, _endT, _on;
        private int _unused;
        private Vector2 _o, _bpos, _px;
        private readonly List<Vector2> _path = new List<Vector2>(3);
        private readonly List<RayHit> _hits = new List<RayHit>(8);
        private readonly List<Sub> _subs = new List<Sub>(8);
        private readonly List<Seg2> _crown = new List<Seg2>(6);
        private readonly HashSet<Health> _touched = new HashSet<Health>(), _skip = new HashSet<Health>();
        private Body _first;
        private SoundLoop _loop;

        public float Heat { get; private set; }

        public RayBeam(RayController ctrl, ArsenalAim aim, float angOff, bool quiet, int idx)
        {
            _ctrl = ctrl; _k = (RayKit)ctrl.KitBase; _el = ArsenalBuild.Element; _quiet = quiet; _idx = idx; _angOff = angOff;
            _over = _k.Evo(RayKit.Sobrecarga); _prism = _k.Evo(RayKit.Prisma); _beacon = _k.Evo(RayKit.Faro);
            _pal = ArsenalBuild.ElemPal(ArsenalPal.Set); _t0 = ArsenalWorld.T; _ang = aim.Ang + angOff; _o = aim.origin;
            _path.Add(aim.origin); _path.Add(aim.origin);
            if (!quiet) { ArsenalSounds.Play("ray_ignite", aim.origin); if (ctrl.User != null) _loop = ArsenalSounds.StartLoop("ray_loop", ctrl.User, ctrl.User.transform); }
        }

        private float T => ArsenalWorld.T;
        private float Hf => Mathf.Clamp01(Heat / _k.maxChannel);
        private bool Piercing => _mode == Mode.Held && _over && _k.Perforante && Hf >= _k.pierceAt;

        public void Aim(ArsenalAim aim, float dt)
        {
            if (_mode != Mode.Held) return;
            float d = Mathf.DeltaAngle(_ang * Mathf.Rad2Deg, (aim.Ang + _angOff) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            _ang += Mathf.Clamp(d, -_k.turnRate * dt, _k.turnRate * dt);
        }

        // recorrido: hasta el primer enemigo, el suelo o el alcance; pierce = no se para en los enemigos (los da todos)
        private void Trace(Vector2 from, float a, bool pierce, List<Vector2> path, List<RayHit> hits)
        {
            path.Clear(); hits.Clear(); path.Add(from);
            Vector2 end = from + Dir(a) * _k.range;
            float ug = 2f;
            if (ArsenalTargets.GroundHit(from, end, out var gp, out _)) ug = Vector2.Distance(from, gp) / _k.range;
            if (pierce)
            {
                _skip.Clear();
                while (ArsenalTargets.Raycast(from, end, out var h, _skip) && h.u < ug) { hits.Add(h); _skip.Add(h.body.health); }
            }
            else if (ArsenalTargets.Raycast(from, end, out var h, null) && h.u < ug) { hits.Add(h); path.Add(h.p); return; }
            path.Add(ug <= 1f ? Vector2.Lerp(from, end, ug) : end);
        }

        private void HitOne(Body e, float mul, float a)
        {
            _touched.Add(e.health);
            bool watch = _mode == Mode.Beacon && _k.Vigia;
            float d = ArsenalCombat.Hit(e, new ArsenalHit
            {
                dmg = _k.tickDamage * mul, from = e.c - Dir(a), knock = _k.push, elem = _el, kind = HitKind.Beam,
                freeze = _el == ArsenalElement.Ice ? _k.freezeTick : 0f, burn = _el == ArsenalElement.Fire ? _k.burnTick : 0f,
                onKill = watch ? (Action<Body>)(_ => { _endT += _k.watchTime; ArsenalCombat.Label(_bpos + Vector2.up * 0.85f, "arsenal.extra_seconds", ArsenalPal.Gold.light, _k.watchTime); }) : null,
            });
            if (_mode == Mode.Beacon && _k.Vinculo && d > 0f)
            {
                _healAcc += d * _k.lifesteal;
                if (_healAcc >= 1f) { float hv = Mathf.Floor(_healAcc); _healAcc -= hv; var ph = _ctrl.User != null ? _ctrl.User.GetComponent<Health>() : null; ph?.Heal(hv); }
            }
        }

        /// <summary>Sobrecarga: descarga por toda la línea (atraviesa) al soltar; Punto justo = franja final sin llegar al máximo.</summary>
        private void Discharge(bool maxed)
        {
            float k = Hf; if (k < _k.overMin) return;
            bool sweet = _k.PuntoJusto && !maxed && k >= _k.sweetFrom, heavy = sweet || k >= _k.overHeavyAt;
            float mul = k * (sweet ? _k.sweetMul : 1f);
            var line = new List<Vector2>(3); var lineHits = new List<RayHit>(8);
            Trace(_o, _ang, true, line, lineHits);
            var P = sweet ? _gp : _pal; Vector2 end = _path[_path.Count - 1];
            var hit = new ArsenalHit { heavy = heavy, elem = _el, kind = HitKind.Beam, freeze = _el == ArsenalElement.Ice ? _k.freezeTick * 3f : 0f, burn = _el == ArsenalElement.Fire ? _k.burnTick * 3f : 0f, hitstop = heavy ? 0.05f : 0f, shake = heavy ? 0.12f : 0f };
            foreach (var h in lineHits) { var o = hit; o.dmg = _k.overBurst * mul; o.knock = 0.6f * k; o.from = h.p - Dir(_ang); ArsenalCombat.Hit(h.body, o); }
            bool wave = _k.Onda; float wr = _k.waveRadius;
            if (wave) { var o = hit; o.dmg = _k.waveDamage * mul; o.knock = 0.7f; ArsenalCombat.Area(end, wr, o); }
            ArsenalWorld.AddFx(0.5f, (l, f0) =>
            {
                RayDraw.Discharge(l, ArsenalWorld.T, f0, line, k, P);
                if (wave) { RayDraw.ShockRing(l, ArsenalWorld.T, f0, end, wr, P); Impact(l, ArsenalWorld.T, f0, end, 0.7f, P, 0f, TAU, 5); }
            });
            if (sweet) ArsenalCombat.Label(ArsenalCombat.PlayerCenter() + Vector2.up * 1.6f, "arsenal.ray.sweet", ArsenalPal.Gold.light);
            if (!_quiet) { ArsenalSounds.Play(sweet ? "ray_sweet" : "ray_discharge", _o); if (wave) ArsenalSounds.Play("ray_wave", end); }
        }

        public void Release(bool maxed)
        {
            if (_mode != Mode.Held || _released >= 0f) return;
            ArsenalSounds.StopLoop(_loop); _loop = default;
            if (_over)
            {
                if (maxed) ArsenalCombat.Label(ArsenalCombat.PlayerCenter() + Vector2.up * 1.6f, "arsenal.ray.overheat", new Color(1f, 0.6f, 0.35f));
                Discharge(maxed);
            }
            if (_beacon)
            {   // se planta donde estaba el cristal y sigue solo
                _mode = Mode.Beacon; _bpos = _o; _plantT = T; _endT = T + _k.beaconTime; _on = _first != null ? 1f : 0f;
                if (!_quiet) ArsenalSounds.Play("ray_plant", _bpos);
                return;
            }
            Off();
        }

        /// <summary>Fin del faro (se acaba el tiempo o lo sustituye uno nuevo).</summary>
        public void End()
        {
            if (_mode != Mode.Beacon || _released >= 0f) return;
            if (_k.Estallido)
            {   // estalla donde apunta (si no apunta a nadie, en el sitio)
                Vector2 end = _path[_path.Count - 1], at = _first != null ? end : _bpos; var pts = _first != null ? new List<Vector2>(_path) : null; float r = _k.novaRadius; var P = _pal;
                ArsenalCombat.Area(at, r, new ArsenalHit { dmg = _k.novaDamage, knock = 0.9f, heavy = true, elem = _el, kind = HitKind.Burst, freeze = _el == ArsenalElement.Ice ? 0.5f : 0f, burn = _el == ArsenalElement.Fire ? 2f : 0f, shake = 0.15f, hitstop = 0.04f });
                ArsenalWorld.AddFx(0.9f, (l, f0) => { if (pts != null) RayDraw.Discharge(l, ArsenalWorld.T, f0, pts, 0.5f, P); RayDraw.ShockRing(l, ArsenalWorld.T, f0, at, r, P); Impact(l, ArsenalWorld.T, f0, at, 0.9f, P, 0f, TAU, 11); });
                if (!_quiet) ArsenalSounds.Play("ray_nova", at);
            }
            Off();
        }

        private void Off()
        {
            _released = T; ArsenalSounds.StopLoop(_loop); _loop = default;
            if (!_quiet) ArsenalSounds.Play("ray_off", _o);
            if (_el == ArsenalElement.Ice)
                foreach (var e in ArsenalTargets.Alive())
                    if (_touched.Contains(e.health) && ElementStatus.Peek(e.health) is ElementStatus st && st.IsFrozen)
                        ArsenalCombat.Hit(e, new ArsenalHit { dmg = 0f, heavy = true, kind = HitKind.Beam });   // al soltar: añicos
        }

        public override void OnRemoved() { ArsenalSounds.StopLoop(_loop); _loop = default; }

        public override bool Tick(float dt)
        {
            if (_released >= 0f) return T - _released < 0.6f;
            var k = _k;
            if (_mode == Mode.Held) { Heat += dt; _o = _ctrl.CurrentAim.origin; }
            else
            {
                if (T >= _endT) { End(); return true; }
                _o = _bpos + new Vector2(0f, Mathf.Sin((T - _plantT) * 3f) * 3f * PX);
                Body n1 = ArsenalTargets.Nearest(_bpos, k.beaconRange), n2 = null;
                if (_idx > 0 && n1 != null) { _skip.Clear(); _skip.Add(n1.health); n2 = ArsenalTargets.Nearest(_bpos, k.beaconRange, _skip); }
                var tg = n2 ?? n1;
                _on = Mathf.Clamp01(_on + (tg != null ? 6f : -6f) * dt);
                if (tg != null) { float d = Mathf.DeltaAngle(_ang * Mathf.Rad2Deg, Mathf.Atan2(tg.c.y - _bpos.y, tg.c.x - _bpos.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad; _ang += Mathf.Clamp(d, -k.turnRate * dt, k.turnRate * dt); }
            }
            Trace(_o, _ang, Piercing, _path, _hits);
            _first = _hits.Count > 0 ? _hits[0].body : null;

            // Prisma: sale por detrás del primero y se divide (Cadena: cada haz salta a uno más)
            _subs.Clear(); _unused = 0; _crown.Clear();
            if (_prism && _first != null)
            {
                var h0 = _hits[0]; Vector2 dir = Dir(_ang);
                _px = _first.c + new Vector2(dir.x * _first.rx, dir.y * _first.ry);
                _skip.Clear(); _skip.Add(_first.health);
                for (int i = 0; i < k.prismCount; i++)
                {
                    var n = ArsenalTargets.Nearest(_px, k.prismRange, _skip); if (n == null) { _unused++; continue; }
                    _skip.Add(n.health); _subs.Add(new Sub { e = n, from = _px });
                    if (k.Cadena) { var prev = n; for (int j = 0; j < k.chainJumps; j++) { var c = ArsenalTargets.Nearest(prev.c, k.prismRange, _skip); if (c == null) break; _skip.Add(c.health); _subs.Add(new Sub { e = c, from = prev.c, chain = true }); prev = c; } }
                }
                if (k.Corona)
                {   // Corona: haces cortos que giran alrededor de la gema
                    _crownA += k.crownSpin * dt;
                    for (int i = 0; i < k.crownCount; i++) { float a = _crownA + i / (float)k.crownCount * TAU; _crown.Add(new Seg2 { a = _px, b = _px + Dir(a) * k.crownLen, ang = a }); }
                }
            }

            _tickT -= dt;
            if (_tickT <= 0f && (_mode == Mode.Held || _on > 0.5f))
            {
                _tickT = k.tick;
                float mul = _mode == Mode.Beacon ? k.beaconMul : _over ? Mathf.Lerp(1f, k.overMax, Hf) : 1f;
                if (_prism && k.Foco) mul *= 1f + k.focusMul * _unused;
                if (Piercing) foreach (var h in _hits) HitOne(h.body, mul, _ang);
                else if (_first != null) HitOne(_first, mul, _ang);
                foreach (var s in _subs) if (!s.e.Dead) HitOne(s.e, k.prismMul, Mathf.Atan2(s.e.c.y - s.from.y, s.e.c.x - s.from.x));
                if (_crown.Count > 0)
                {   // Corona: cada enemigo, un golpe por tick aunque lo crucen varios haces
                    _skip.Clear(); _skip.Add(_first.health);
                    foreach (var c in _crown) while (ArsenalTargets.Raycast(c.a, c.b, out var h, _skip)) { _skip.Add(h.body.health); HitOne(h.body, k.crownMul, c.ang); }
                }
                if (_hits.Count > 0 && !_quiet) ArsenalSounds.Play("ray_tick", _o, 0.09f);
            }
            if (_mode == Mode.Beacon && k.Vinculo)
            {   // Vínculo: el hilo faro → player daña lo que lo cruza
                _linkT -= dt;
                if (_linkT <= 0f)
                {
                    _linkT = k.tick; Vector2 pc = ArsenalCombat.PlayerCenter(); _skip.Clear();
                    while (ArsenalTargets.Raycast(_bpos, pc, out var h, _skip)) { _skip.Add(h.body.health); ArsenalCombat.Hit(h.body, new ArsenalHit { dmg = k.linkDps * k.tick, dot = true, kind = HitKind.Beam }); }
                }
            }
            return true;
        }

        public override void Draw(L l)
        {
            float t = T, fade = _released < 0f ? 1f : 1f - EaseIn(Seg(t, _released, _released + 0.14f));
            float env = EaseOut(Seg(t, _t0, _t0 + 0.08f)) * fade * (_mode == Mode.Beacon ? _on : 1f);
            float hk = _over ? Hf : 0f, sc = _over ? 1f + 0.8f * hk : _mode == Mode.Beacon ? 0.75f : 1f;
            Vector2 m = _path[0], end = _path[_path.Count - 1];
            if (_mode == Mode.Beacon)
            {
                RayDraw.BeaconCrystal(l, t, _bpos, fade, _pal, (_endT - t) / _k.beaconTime);
                if (_k.Vinculo)
                {
                    Vector2 pc = ArsenalCombat.PlayerCenter();
                    RayDraw.EnergyBeam(l, t, _bpos, pc, 0.6f * fade, _lp, 0.3f, 5f);
                    if (_first != null) { RayDraw.LifeMotes(l, t, _bpos, pc, fade); RayDraw.HealCrosses(l, t - _plantT, pc, fade); }
                }
            }
            if (env > 0.01f)
            {
                float reach = _mode == Mode.Beacon ? 1f : EaseOut(Seg(t, _t0, _t0 + 0.07f));
                RayDraw.EnergyBeam(l, t, m, Vector2.Lerp(m, end, reach), env, _pal, sc, 0f);
                if (l == L.Front && _mode == Mode.Held)
                {
                    Glow(m, 34f * PX * env * sc, _pal.g1, 0.7f * env); Star4(l, m, 16f * PX * env * sc, t * 7f, _pal.core, env);
                    if (_over && _k.PuntoJusto && hk >= _k.sweetFrom)
                    {   // ¡suelta ya!: estrella dorada que late en el cristal
                        float pu = 0.6f + 0.4f * Mathf.Sin(t * 40f); Glow(m, 50f * PX, _gp.g1, 0.8f * pu); Star4(l, m, 30f * PX * pu, -t * 5f, _gp.core, 1f);
                    }
                }
                if (reach >= 1f)
                {
                    float s0 = _t0 + 0.07f, al = s0 + Mathf.Max(0f, Mathf.Ceil((t - 0.45f - s0) / 0.035f)) * 0.035f;
                    RayDraw.BeamContact(l, t, end, env, _pal, al, _released < 0f ? t : _released, sc, 1);
                    if (Piercing) for (int i = 0; i < _hits.Count; i++) RayDraw.BeamContact(l, t, _hits[i].p, env, _pal, al, t, 0.6f, 20 + i);
                }
                for (int i = 0; i < _subs.Count; i++)
                {
                    var s = _subs[i]; if (s.e.Dead) continue;
                    Vector2 tp = new Vector2(s.e.c.x - Mathf.Sign(s.e.c.x - s.from.x) * s.e.rx * 0.8f, s.e.c.y);
                    RayDraw.EnergyBeam(l, t, s.from, tp, env * 0.95f, _gp, s.chain ? 0.4f : 0.5f, i * 2.1f);
                    RayDraw.BeamContact(l, t, tp, env, _gp, Mathf.Ceil((t - 0.4f) / 0.035f) * 0.035f, t, 0.6f, 10 + i);
                }
                for (int i = 0; i < _crown.Count; i++) RayDraw.EnergyBeam(l, t, _crown[i].a, _crown[i].b, env * 0.9f, _gp, 0.35f, 30 + i);
                if (_prism && _first != null) FanShard.PrismGem(l, t, _px, env, _gp);
            }
            if (_released >= 0f)
            {   // restos que flotan sobre la línea del haz
                var R = new Rng(61); float u = Seg(t, _released, _released + 0.6f);
                for (int i = 0; i < 16; i++)
                {
                    float kk = R.Next(); bool back = R.Next() < 0.5f; float dy = (10f + R.Next() * 26f) * PX * EaseOut(u), jx = (R.Next() - 0.5f) * 12f * PX * u, r = (1f + R.Next() * 1.8f) * PX * (1f - u);
                    if ((back ? L.Back : L.Front) == l) Disc(l, Vector2.Lerp(m, end, kk) + new Vector2(jx, dy), r, i % 3 != 0 ? _pal.light : _pal.core, 1f - u);
                }
            }
        }
    }

    /// <summary>Dibujo del Rayo (<c>fx.js</c>: energyBeam, beamContact, beaconCrystal, beamDischarge, shockRing, lifeMotes, healCrosses).</summary>
    public static class RayDraw
    {
        private static readonly List<Vector2> Pts = new List<Vector2>(40), Helix = new List<Vector2>(40);
        private static readonly List<float> Ws = new List<float>(40);
        private static readonly List<Color> Cs = new List<Color>(40);

        /// <summary>
        /// Haz de energía: cinco cintas (morado ancho → núcleo blanco) que respiran, un halo aditivo, anillos de flujo, motas que
        /// avanzan y dos hilos en hélice (por detrás y por delante del eje).
        /// </summary>
        public static void EnergyBeam(L l, float t, Vector2 a, Vector2 b, float env, ArsenalPal pal, float sc = 1f, float sd = 0f)
        {
            if (env <= 0.01f) return;
            Vector2 d = b - a; float Lw = d.magnitude; if (Lw < 1e-4f) return;
            Vector2 nrm = new Vector2(-d.y, d.x) / Lw; float Lpx = Lw / PX;
            int n = Mathf.Clamp(Mathf.CeilToInt(Lpx / 5f), 36, 140);   // muestras según el largo: la hélice no se rompe en haces largos
            Vector2 P(float s, float off) { float wv = Mathf.Sin(s * Lpx * 0.05f - t * 30f + sd) * 1.3f * PX * env; return a + d * s + nrm * (wv + off); }
            float Breathe(float s) => 1f + 0.16f * Mathf.Sin(s * Lpx * 0.09f - t * 45f + sd) + 0.08f * Noise1(s * 9f - t * 11f, sd);
            float Rise(float s) => 0.55f + 0.45f * Seg01(s, 0f, 0.1f);
            Pts.Clear(); for (int i = 0; i <= n; i++) Pts.Add(P(i / (float)n, 0f));
            if (l == L.Front)
            {
                // halo aditivo (nuevo en Unity): luz que sale del haz
                Ws.Clear(); Cs.Clear();
                for (int i = 0; i <= n; i++) { float s = i / (float)n; Ws.Add(46f * PX * Mathf.Min(sc, 1.3f) * env * Breathe(s) * Rise(s)); Cs.Add(A(pal.g1, 0.35f * env / Mathf.Max(1f, sc * 0.8f))); }
                GlowCanvas(l).StrokeVar(Pts, Ws, Cs, true);
                Band(l, 28f, pal.dark, 0.2f); Band(l, 17f, pal.deep, 0.45f); Band(l, 11f, pal.mid, 0.9f); Band(l, 6f, pal.light, 1f); Band(l, 2.6f, pal.core, 1f);
                for (int i = 0; i < 6; i++)
                {   // anillos de flujo que avanzan
                    float s = ((t * 900f / Lpx) + i / 6f) % 1f, w = 9f * PX * sc * env * Breathe(s);
                    Solid(l).EllipseArc(P(s, 0f), 2.6f * PX, w, Mathf.Atan2(d.y, d.x), 0f, TAU, 1.4f * PX, A(pal.core, Mathf.Clamp01(0.55f * env * Mathf.Sin(Mathf.PI * s))));
                }
                var R = new Rng(sd + 3f);
                for (int i = 0; i < 12; i++)
                {   // motas que corren hacia delante
                    float sp = 0.8f + R.Next() * 0.8f, s = ((t * sp * 1.6f) + R.Next()) % 1f, off = (R.Next() - 0.5f) * 16f * PX * sc * env, ln = (6f + R.Next() * 8f) * PX * sc;
                    Drop(l, P(s, off), Mathf.Atan2(d.y, d.x), ln, 1.4f * PX * sc, i % 2 == 1 ? pal.core : pal.light, env * Mathf.Sin(Mathf.PI * s));
                }
            }
            for (int k = 0; k < 2; k++)
            {   // dos hilos en hélice
                Helix.Clear();
                for (int i = 0; i <= n; i++)
                {
                    float s = i / (float)n, ph = s * Lpx * 0.07f - t * 50f + k * Mathf.PI; bool front = Mathf.Cos(ph) > 0f;
                    if ((l == L.Front) != front) { Flush(l, k, pal, sc, env); continue; }
                    Helix.Add(P(s, Mathf.Sin(ph) * 9f * PX * sc * env * Rise(s)));
                }
                Flush(l, k, pal, sc, env);
            }

            void Band(L layer, float w, Color c, float al)
            {
                Ws.Clear(); Cs.Clear();
                for (int i = 0; i <= n; i++) { float s = i / (float)n; Ws.Add(w * PX * sc * env * Breathe(s) * Rise(s)); Cs.Add(A(c, al)); }
                Solid(layer).StrokeVar(Pts, Ws, Cs);
            }
        }

        private static void Flush(L l, int k, ArsenalPal pal, float sc, float env)
        {
            if (Helix.Count > 1) Solid(l).Stroke(Helix, 1.7f * PX * sc, A(k == 1 ? pal.light : pal.core, Mathf.Clamp01(env)));
            Helix.Clear();
        }

        private static float Seg01(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

        /// <summary>Punto de contacto: luz, destello que parpadea y chispas continuas que saltan hacia atrás.</summary>
        public static void BeamContact(L l, float t, Vector2 p, float env, ArsenalPal pal, float t0, float t1, float sc = 1f, float seed = 0f)
        {
            if (l == L.Front && env > 0f)
            {
                Glow(p, 46f * PX * sc * env, pal.g1, 0.7f * env); Glow(p, 20f * PX * sc * env, pal.g2, 0.9f * env);
                Star4(l, p, (18f + 6f * Mathf.Sin(t * 70f + seed)) * PX * sc * env, t * 4f + seed, pal.core, env);
                Disc(l, p, 5f * PX * sc * env, pal.core, env);
            }
            for (float e = t0; e < Mathf.Min(t, t1); e += 0.035f)
                if (t - e < 0.4f) Sparks(l, t, e, p, new SparkOpt { n = 3, a0 = Mathf.PI - 1.1f, a1 = Mathf.PI + 1.1f, sp = 260f * sc, pal = pal, seed = Mathf.Floor(e * 1000f) + seed, len = 12, life = 0.3f, w = 2.2f, grav = 520, backShare = 0.45f });
        }

        /// <summary>Faro: cristal doble flotando con un aro que marca la vida que le queda.</summary>
        public static void BeaconCrystal(L l, float t, Vector2 p, float a, ArsenalPal pal, float k)
        {
            if (a <= 0f || l != L.Front) return;
            Vector2 c = p + new Vector2(0f, Mathf.Sin(t * 3f) * 2f * PX);
            EllArc(l, c, 20f * PX, 6f * PX, t * 2.2f, t * 2.2f + TAU * Mathf.Clamp01(k), 2f * PX, pal.light, 0.8f * a, new ArcOpt { m = 24, seed = 1, taper = 1 });
            Glow(c, 40f * PX, pal.g1, 0.5f * a); Glow(c, 18f * PX, pal.g2, 0.7f * a);
            CrystalShard(l, c - new Vector2(0f, 4f * PX), Mathf.PI * 0.5f, 0.75f, pal, a);
            CrystalShard(l, c + new Vector2(0f, 4f * PX), -Mathf.PI * 0.5f, 0.45f, pal, a);
            Star4(l, c, (9f + 3f * Mathf.Sin(t * 9f)) * PX, t * 1.5f, pal.core, a);
        }

        /// <summary>Descarga a lo largo de la línea: haz grueso que se apaga en 0,3 s y chispas por toda la línea; k = calor 0..1.</summary>
        public static void Discharge(L l, float t, float t0, List<Vector2> pts, float k, ArsenalPal pal)
        {
            float u = Seg01(t, t0, t0 + 0.3f); if (u >= 1f || pts == null) return;
            for (int i = 0; i + 1 < pts.Count; i++) EnergyBeam(l, t, pts[i], pts[i + 1], 1f - EaseIn(u), pal, 1.3f + 1.4f * k, 9 + i);
            var R = new Rng(Mathf.Floor(t0 * 100f));
            for (int i = 0; i + 1 < pts.Count; i++)
                for (int j = 0; j < 6; j++) Sparks(l, t, t0, Vector2.Lerp(pts[i], pts[i + 1], R.Next()), new SparkOpt { n = 3, pal = pal, sp = 220, seed = i * 31 + j, len = 12, life = 0.35f, a0 = 0, a1 = TAU, grav = 520, w = 2.6f, backShare = 0.45f });
        }

        /// <summary>Onda de choque circular (Onda, Estallido).</summary>
        public static void ShockRing(L l, float t, float t0, Vector2 p, float r, ArsenalPal pal)
        {
            float u = Seg01(t, t0, t0 + 0.35f); if (u >= 1f || l != L.Front) return;
            float rr = r * EaseOut(u);
            Glow(p, rr * 1.1f, pal.g1, 0.45f * (1f - u));
            EllArc(l, p, rr, rr, 0f, TAU, 5f * PX * (1f - u * 0.6f), pal.mid, 1f - u, new ArcOpt { m = 40, wob = 0.06f, seed = 2, noTaper = true });
            EllArc(l, p, rr * 0.93f, rr * 0.93f, 0f, TAU, 2f * PX * (1f - u * 0.6f), pal.core, 1f - u, new ArcOpt { m = 40, wob = 0.06f, seed = 2, noTaper = true });
        }

        /// <summary>Gotas carmesí que viajan de a hasta b ondulando (robo de vida del Vínculo).</summary>
        public static void LifeMotes(L l, float t, Vector2 a, Vector2 b, float on)
        {
            if (on <= 0f) return;
            var L2 = ArsenalPal.Life; var R = new Rng(301); Vector2 d = b - a; float ang = Mathf.Atan2(d.y, d.x); Vector2 n = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang));
            for (int i = 0; i < 14; i++)
            {
                float sp = 0.9f + R.Next() * 0.7f, s = ((t * sp * 1.3f) + R.Next()) % 1f, wob = Mathf.Sin(s * 14f + i) * 10f * PX * (1f - s);
                if ((Mathf.Sin(s * 14f + i) > 0f ? L.Front : L.Back) != l) continue;
                Vector2 p = Vector2.Lerp(a, b, s) + n * wob;
                Drop(l, p, ang, 9f * PX, 2.6f * PX, i % 3 != 0 ? L2.mid : L2.light, on * Mathf.Sin(Mathf.PI * s));
                if (i % 3 == 0) Glow(p, 9f * PX, L2.g1, 0.5f * on * Mathf.Sin(Mathf.PI * s), l);
            }
        }

        /// <summary>Cruces de curación que suben del player.</summary>
        public static void HealCrosses(L l, float tt, Vector2 player, float on)
        {
            if (l != L.Front || on <= 0f) return;
            var L2 = ArsenalPal.Life; var cv = Solid(l);
            for (int i = 0; i < 6; i++)
            {
                var R = new Rng(i + 500); float age = (tt * 1.2f + R.Next()) % 1f, x = player.x + (R.Next() - 0.5f) * 36f * PX, y = player.y + 0.4f + age * 50f * PX, a = Mathf.Sin(Mathf.PI * age) * on, r = (4f + R.Next() * 2f) * PX;
                cv.Line(new Vector2(x - r, y), new Vector2(x + r, y), 2.4f * PX, A(L2.light, a)); cv.Line(new Vector2(x, y - r), new Vector2(x, y + r), 2.4f * PX, A(L2.light, a));
            }
        }
    }
}
