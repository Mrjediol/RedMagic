using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Fx;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// El proyectil del arma «Cometa» (<see cref="ShotDelivery.Comet"/>): sin sprite, sólo efectos
    /// (<see cref="CometBodyFx"/>, <see cref="CometHitFx"/>), y <b>pooled</b>. Se mueve por su cuenta (no usa
    /// Rigidbody): cada frame barre su recorrido con un CircleCast y reacciona según el comportamiento de
    /// <see cref="CometSettings"/>:
    /// <list type="bullet">
    /// <item><b>Base</b>: recto; impacta en el primer enemigo.</item>
    /// <item><b>Perforación</b>: atraviesa <see cref="CometSettings.pierceCount"/> enemigos (agujas de entrada y
    /// salida, hilo a través del cuerpo) e impacta en el siguiente.</item>
    /// <item><b>Cadena</b>: si queda un enemigo dentro de <see cref="CometSettings.chainRange"/> y saltos
    /// disponibles, se pega <see cref="CometSettings.chainStickTime"/> s, apunta y sale con fuerza hacia él;
    /// el último impacta normal.</item>
    /// <item><b>Homing</b>: al salir fija al enemigo más cercano al cursor y lo persigue (giro limitado);
    /// todo en ámbar y con esquinas de fijación sobre el objetivo.</item>
    /// </list>
    /// El terreno (capa Ground) lo detiene siempre. El daño pasa por <see cref="PlayerHit.Deal"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CometProjectile : MonoBehaviour, IPooled
    {
        private const int GroundMask = 1 << 6;

        /// <summary>Homing: diferencia de altura (unidades) por debajo de la cual se considera «a la misma altura».</summary>
        private const float SameHeight = 1.5f;
        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[24];
        private static readonly Collider2D[] Overlaps = new Collider2D[48];
        private static Pool<CometProjectile> _pool;

        private enum State { Fly, Stuck, Done }

        private CometBodyFx _fx;
        private WeaponShot _shot;
        private ShotContext _ctx;
        private CometSettings _s;
        private CometStats _stats;
        private CometPalette _pal;

        private State _state;
        private Vector2 _pos, _dir;
        private float _age, _speed, _baseSpeed, _speedBoost, _radius, _damage, _knockback;
        private int _pierceLeft, _jumpsLeft;
        private readonly HashSet<Health> _hit = new HashSet<Health>();

        private Health _target;               // homing / siguiente de la cadena
        private int _turnSign;                // homing: sentido de giro decidido cuando el objetivo queda detrás (0 = libre)
        private Collider2D _targetBody;
        private Collider2D _insideBody;       // perforación: cuerpo que estamos cruzando (impact frame al salir)
        private Health _insideHealth;

        private Transform _stickTo;
        private Vector2 _stickOffset;
        private float _stickTimer;
        private CometHitFx _chainFx;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _pool = null;

        /// <summary>Lanza un cometa con el disparo ya resuelto (lo llama <see cref="ShotResolver.Fire"/>).</summary>
        public static CometProjectile Launch(WeaponShot shot, in ShotContext ctx)
        {
            var settings = CometRuntime.For(shot.source) ?? new CometSettings();
            _pool ??= new Pool<CometProjectile>(Build, prewarm: 4);
            var p = _pool.Get();
            p.Init(shot, ctx, settings);
            return p;
        }

        private static CometProjectile Build()
        {
            var go = new GameObject("Comet Projectile");
            return go.AddComponent<CometProjectile>();
        }

        private void Init(WeaponShot shot, in ShotContext ctx, CometSettings settings)
        {
            _shot = shot;
            _ctx = ctx;
            _s = settings;
            _stats = settings.Stats;
            bool homing = settings.behaviour == CometBehaviour.Homing;
            _pal = homing ? CometPalette.Amber : CometPalette.Normal;

            if (_fx == null)
            {
                var mat = settings.fxMaterial != null ? settings.fxMaterial : new Material(Shader.Find("RedMagic/Fx Vertex Color"));
                _fx = new CometBodyFx(transform, mat);
            }

            _pos = ctx.Muzzle(shot.muzzleOffset);
            // base: recto hacia donde mira el jugador; con la mejora «apuntar con el ratón», hacia el cursor
            Vector2? cursor = ProjectileAim.MouseWorld();
            _dir = new Vector2(ctx.Facing, 0f);
            if (settings.aimAtCursor && cursor.HasValue && (cursor.Value - _pos).sqrMagnitude > 0.01f) _dir = (cursor.Value - _pos).normalized;
            _hit.Clear();
            _target = null; _targetBody = null;
            _turnSign = 0;
            _insideBody = null; _insideHealth = null;
            _stickTo = null; _chainFx = null;

            if (homing)
            {
                // fija al enemigo más cercano al cursor; sale recto (o hacia el cursor con la mejora) y gira hacia él
                Vector2 seek = cursor ?? _pos + _dir * 6f;
                _target = FindNearest(seek, settings.homingCursorRadius, out _targetBody);
                if (_target == null) _target = FindNearest(_pos, ProjectileAim.NearestEnemySearchRadius, out _targetBody);
            }

            _state = State.Fly;
            _age = 0f;
            _baseSpeed = Mathf.Max(0.5f, shot.speed * _stats.speedMultiplier);
            _speedBoost = 0f;
            _speed = _baseSpeed * 0.55f;
            _radius = Mathf.Max(0.12f, shot.size.y * 0.5f) * _stats.sizeScale;
            _damage = shot.damage * _stats.damageMultiplier;
            _knockback = (shot.source != null ? shot.source.BaseKnockbackMultiplier : 1f) * _stats.knockbackMultiplier;
            _pierceLeft = settings.behaviour == CometBehaviour.Pierce ? Mathf.Max(1, settings.pierceCount) : 0;
            _jumpsLeft = settings.behaviour == CometBehaviour.Chain ? Mathf.Max(1, settings.chainCount) : 0;

            transform.position = _pos;
            _fx.Px = settings.pixel;
            _fx.Scale = _stats.sizeScale;
            _fx.Pow = settings.upgraded;
            _fx.Lance = settings.behaviour == CometBehaviour.Pierce;
            _fx.Span = settings.upgraded ? 0.3f : 0.23f;
            _fx.Pal = _pal;
            _fx.Dens = settings.upgraded ? 2f : 1f;
            _fx.Satellites = settings.upgraded ? 5 : 3;
            _fx.ShockScale = 1.2f;
            _fx.Begin(_pos, _dir);
            _fx.Mark = homing ? _targetBody : null;
        }

        void IPooled.OnReturnedToPool()
        {
            _fx?.Clear();
            _chainFx = null;
            _hit.Clear();
        }

        private void OnDestroy() => _fx?.Destroy();

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            switch (_state)
            {
                case State.Fly: Fly(dt); break;
                case State.Stuck: Stuck(dt); break;
            }

            transform.position = _pos;
            _fx.Tick(dt, _pos, _dir);

            if (_state == State.Done && _fx.Finished) _pool?.Release(this);
        }

        // ------------------------------------------------------------------ vuelo

        private void Fly(float dt)
        {
            _age += dt;
            if (_age > _shot.lifetime) { Finish(); return; }

            // arranca algo lento y acelera (el boceto sale de la boca y coge velocidad); tras saltar en la
            // cadena sale disparado y se calma
            float ramp = Mathf.Lerp(0.55f, 1.25f, CometDraw.EaseOut(Mathf.Clamp01(_age / 0.22f)));
            _speedBoost = Mathf.MoveTowards(_speedBoost, 0f, dt * 2.5f);
            _speed = _baseSpeed * (ramp + _speedBoost);

            Steer(dt);

            Vector2 step = _dir * (_speed * dt);
            float dist = step.magnitude;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _ctx.HitLayers, useTriggers = true };
            int count = Physics2D.CircleCast(_pos, _radius, _dir, filter, Hits, dist);
            SortByDistance(count);

            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                var col = hit.collider;
                if (col == null || !col.enabled) continue;
                if (col.GetComponentInParent<CometProjectile>() != null) continue;

                var health = col.GetComponentInParent<Health>();
                if (health != null)
                {
                    if (!IsTarget(health) || _hit.Contains(health)) continue;
                    _pos += _dir * hit.distance;
                    if (HitEnemy(health, col, hit.point)) return;   // se paró (pegado o fin)
                    continue;
                }

                if (((1 << col.gameObject.layer) & GroundMask) != 0)
                {
                    _pos += _dir * hit.distance;
                    var dir = _dir;
                    CometHitFx.PlayImpact(hit.point, dir, false, _pal, _s.pixel, _s.fxMaterial);
                    Play(_shot.source != null ? _shot.source.TerrainImpactSound : null, hit.point);
                    Finish();
                    return;
                }
            }

            _pos += step;
            CheckLeftBody();
        }

        private void Steer(float dt)
        {
            bool homing = _s.behaviour == CometBehaviour.Homing;
            bool chasing = _target != null && (homing || _s.behaviour == CometBehaviour.Chain);
            if (!chasing) return;

            if (_target.IsDead || _targetBody == null || !_targetBody.enabled)
            {
                // el fijado murió: el homing busca otro cerca; la cadena sigue recta
                _target = homing ? FindNearest(_pos, 10f, out _targetBody) : null;
                if (homing) _fx.Mark = _targetBody;
                if (_target == null) return;
            }

            Vector2 toTarget = (Vector2)_targetBody.bounds.center - _pos;
            Vector2 desired = toTarget.normalized;
            float rate = homing ? _s.homingTurnRate : 720f;
            float step = rate * dt;
            float angle = Vector2.SignedAngle(_dir, desired);

            // Objetivo casi justo detrás y a una altura parecida: el giro más corto es ambiguo y a veces
            // bajaba contra el suelo. Se decide una vez girar hacia ARRIBA y se mantiene hasta encararlo.
            if (homing && Mathf.Abs(angle) > 120f && Mathf.Abs(toTarget.y) < SameHeight)
            {
                if (_turnSign == 0) _turnSign = _dir.x < 0f ? -1 : 1;   // girar con este signo sube la punta
            }
            else if (Mathf.Abs(angle) < 90f) _turnSign = 0;

            if (_turnSign != 0)
            {
                float turn = Mathf.Min(step, Mathf.Abs(angle)) * _turnSign;
                _dir = ((Vector2)(Quaternion.Euler(0f, 0f, turn) * _dir)).normalized;
                return;
            }
            _dir = ((Vector2)Vector3.RotateTowards(_dir, desired, step * Mathf.Deg2Rad, 0f)).normalized;
        }

        /// <summary>Devuelve true si el cometa deja de volar (se pega o termina).</summary>
        private bool HitEnemy(Health health, Collider2D col, Vector2 point)
        {
            _hit.Add(health);
            Vector2 body = col.bounds.center;
            var weapon = _shot.source;
            bool doPierce = _s.behaviour == CometBehaviour.Pierce && _pierceLeft > 0;
            Health next = null;
            Collider2D nextBody = null;
            bool doChain = _s.behaviour == CometBehaviour.Chain && _jumpsLeft > 0 &&
                           (next = FindChainTarget(body, out nextBody)) != null;

            float dmg = _damage * _ctx.DamageScale;
            if (dmg > 0f) PlayerHit.Deal(health, dmg, _pos - _dir, _knockback, HitKind.Projectile);
            _ctx.Impact?.Fire(body);

            if (doPierce)
            {
                _pierceLeft--;
                Vector2 exit = ExitPoint(col.bounds, point, _dir);
                float travel = Vector2.Distance(point, exit) / Mathf.Max(0.5f, _speed);
                CometHitFx.PlayPierce(point, exit, _dir, travel, _s.upgraded, _pal, _s.pixel, _s.fxMaterial);
                Play(_s.pierceEntrySound, point);
                Play(weapon != null ? weapon.ImpactSound : null, body);
                ImpactFrame(health);
                Shake(0.5f);
                _insideBody = col;
                _insideHealth = health;
                _fx.Inside = true;
                return false;
            }

            if (doChain)
            {
                _jumpsLeft--;
                Vector2 stick = Vector2.Lerp(point, body, 0.65f);
                _pos = stick;
                _stickTo = health.transform;
                _stickOffset = stick - (Vector2)_stickTo.position;
                _stickTimer = 0f;
                _target = next;
                _targetBody = nextBody;
                _chainFx = CometHitFx.PlayChain(point, stick, _dir, _stickTo, col, _s.chainStickTime, _s.upgraded, _pal, _s.pixel, _s.fxMaterial);
                Play(_s.chainStickSound, point);
                Play(weapon != null ? weapon.ImpactSound : null, body);
                ImpactFrame(health);
                Shake(0.6f);
                _fx.Inside = true;
                _state = State.Stuck;
                return true;
            }

            // impacto final
            CometHitFx.PlayImpact(point, _dir, _s.upgraded, _pal, _s.pixel, _s.fxMaterial);
            Play(weapon != null ? weapon.ImpactSound : null, body);
            ImpactFrame(health);
            Shake(1f);
            Finish();
            return true;
        }

        // ------------------------------------------------------------------ pegado (cadena)

        private void Stuck(float dt)
        {
            _stickTimer += dt;
            if (_stickTo != null) _pos = (Vector2)_stickTo.position + _stickOffset;

            // la punta gira hacia el siguiente mientras se acumula la energía
            if (_target != null && _targetBody != null && _targetBody.enabled)
            {
                Vector2 desired = ((Vector2)_targetBody.bounds.center - _pos).normalized;
                _dir = ((Vector2)Vector3.RotateTowards(_dir, desired, 900f * Mathf.Deg2Rad * dt, 0f)).normalized;
            }
            _chainFx?.AimChain(_dir);

            if (_stickTimer < _s.chainStickTime) return;

            if (_target == null || _target.IsDead || _targetBody == null || !_targetBody.enabled)
            {
                // el siguiente desapareció mientras estaba pegado: busca otro; si no hay, termina aquí
                _target = FindChainTarget(_pos, out _targetBody);
                if (_target == null)
                {
                    _chainFx?.ReleaseChain(_dir);
                    CometHitFx.PlayImpact(_pos, _dir, _s.upgraded, _pal, _s.pixel, _s.fxMaterial);
                    Finish();
                    return;
                }
            }

            _dir = ((Vector2)_targetBody.bounds.center - _pos).normalized;
            _chainFx?.ReleaseChain(_dir);
            _chainFx = null;
            Play(_s.chainReleaseSound, _pos);
            ImpactFrameOn(_stickTo);
            Shake(0.6f);
            _stickTo = null;
            _fx.Inside = false;
            _speedBoost = 0.6f;
            _age = Mathf.Max(_age, 0.22f);   // sale ya a toda velocidad
            _state = State.Fly;
        }

        // ------------------------------------------------------------------ utilidades

        private void CheckLeftBody()
        {
            if (_insideBody == null) return;
            if (_insideBody.enabled && _insideBody.OverlapPoint(_pos)) return;
            Play(_s.pierceExitSound, _pos);
            ImpactFrame(_insideHealth);
            _insideBody = null;
            _insideHealth = null;
            _fx.Inside = false;
        }

        private void Finish()
        {
            if (_state == State.Done) return;
            _state = State.Done;
            _fx.Inside = false;
            _fx.End();
        }

        private void ImpactFrame(Health health)
        {
            if (health != null) ImpactFrameOn(health.transform);
        }

        private void ImpactFrameOn(Transform target)
        {
            if (_s.impactFrames && target != null) ImpactFrames.Play(target, _s.impactFrameDuration, _s.silhouetteMaterial);
        }

        private void Shake(float part)
        {
            if (_stats.shakeAmplitude > 0f) CameraFollow.ShakeAll(_stats.shakeAmplitude * part, _stats.shakeDuration);
        }

        private static void Play(SoundCue cue, Vector2 at)
        {
            if (cue != null) AudioManager.Instance?.Play(cue, at);
        }

        /// <summary>Por dónde sale una línea que entra en <paramref name="bounds"/> por <paramref name="entry"/> con dirección <paramref name="dir"/>.</summary>
        private static Vector2 ExitPoint(Bounds bounds, Vector2 entry, Vector2 dir)
        {
            var ray = new Ray(new Vector3(entry.x, entry.y, 0f) + (Vector3)(dir * 50f), -(Vector3)dir);
            var b = new Bounds(new Vector3(bounds.center.x, bounds.center.y, 0f), new Vector3(bounds.size.x, bounds.size.y, 1f));
            return b.IntersectRay(ray, out float d) ? (Vector2)ray.GetPoint(d) : entry + dir * bounds.size.x;
        }

        private Health FindChainTarget(Vector2 from, out Collider2D body)
        {
            body = null;
            Health best = null;
            float bestD = _s.chainRange * _s.chainRange;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _ctx.HitLayers, useTriggers = true };
            int n = Physics2D.OverlapCircle(from, _s.chainRange, filter, Overlaps);
            for (int i = 0; i < n; i++)
            {
                var col = Overlaps[i];
                if (col == null || !col.enabled) continue;
                var h = col.GetComponentInParent<Health>();
                if (!IsTarget(h) || _hit.Contains(h)) continue;
                float d = ((Vector2)col.bounds.center - from).sqrMagnitude;
                if (d >= bestD) continue;
                bestD = d; best = h; body = col;
            }
            return best;
        }

        private Health FindNearest(Vector2 from, float radius, out Collider2D body)
        {
            body = null;
            Health best = null;
            float bestD = float.MaxValue;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _ctx.HitLayers, useTriggers = true };
            int n = Physics2D.OverlapCircle(from, radius, filter, Overlaps);
            for (int i = 0; i < n; i++)
            {
                var col = Overlaps[i];
                if (col == null || !col.enabled) continue;
                var h = col.GetComponentInParent<Health>();
                if (!IsTarget(h)) continue;
                float d = ((Vector2)col.bounds.center - from).sqrMagnitude;
                if (d >= bestD) continue;
                bestD = d; best = h; body = col;
            }
            return best;
        }

        private bool IsTarget(Health health)
        {
            if (health == null || health.IsDead || !health.isActiveAndEnabled) return false;
            if (_ctx.CasterHealth != null && health == _ctx.CasterHealth) return false;
            if (_ctx.Caster != null && health.transform.IsChildOf(_ctx.Caster.transform)) return false;
            if (!string.IsNullOrEmpty(_ctx.FriendlyTag) && health.CompareTag(_ctx.FriendlyTag)) return false;
            return !Teams.Allied(_ctx.Caster, health);
        }

        private static void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                var key = Hits[i];
                int j = i - 1;
                while (j >= 0 && Hits[j].distance > key.distance) { Hits[j + 1] = Hits[j]; j--; }
                Hits[j + 1] = key;
            }
        }
    }
}
