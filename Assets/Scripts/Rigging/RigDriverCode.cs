using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Rigging
{
    /// <summary>
    /// Motor del rig en <b>código puro</b>: senos para el reposo, una corrutina por gesto y
    /// curvas de easing escritas a mano. Sin dependencias.
    ///
    /// Cada frame (en <c>LateUpdate</c>, después de la IA) recompone la pose desde cero:
    /// <c>reposo + balanceo + capas de gesto</c>. Los gestos no tocan transforms, sólo escriben
    /// unos pocos números (peso de apuntado, coz, inclinación…) y la composición los suma; así
    /// dos gestos a la vez (un golpe recibido en mitad de un disparo) nunca se pisan.
    /// </summary>
    [DisallowMultipleComponent]
    public class RigDriverCode : RigDriver
    {
        private struct PartPose
        {
            public RigPart data;
            public Vector3 position;
            public float angle;
            public Vector3 scale;
        }

        private struct Piece
        {
            public Transform transform;
            public Vector3 pivot;
            public Vector2 velocity;
            public float spin;
            public bool resting;
        }

        private PartPose[] _parts;
        private Vector3 _bodyPos, _bodyScale;
        private float _bodyAngle;
        private Vector3 _aimPos;
        private float _aimAngle;
        private int _aimIndex = -1;

        // Reloj propio: sólo avanza con tiempo escalado, así que la pausa lo congela sin saltos.
        private float _clock;
        private Vector3 _lastPosition;
        private float _lean;

        // Capas de gesto que la composición suma.
        private Vector2? _aimPoint;
        private float _aimWeight;
        private float _armRotation;   // anticipación + coz, grados
        private float _armPush;       // retroceso a lo largo del cañón, unidades locales
        private float _bodyTilt;      // grados hacia atrás
        private float _bodyKick;      // unidades hacia atrás
        private float _hurtTime = -1f;

        private Coroutine _attack;
        private bool _dead;
        private readonly List<Piece> _pieces = new List<Piece>();

        protected override void Awake()
        {
            base.Awake();
            CacheRest();
        }

        private void CacheRest()
        {
            var rig = Rig;
            rig.TryGetRest(rig.body, out _bodyPos, out _bodyAngle, out _bodyScale);

            _parts = new PartPose[rig.parts.Count];
            for (int i = 0; i < rig.parts.Count; i++)
            {
                var data = rig.parts[i];
                _parts[i].data = data;
                if (data?.part == null) continue;

                rig.TryGetRest(data.part, out _parts[i].position, out _parts[i].angle, out _parts[i].scale);
                if (data.part == rig.aimPart) _aimIndex = i;
            }

            if (rig.aimPart != null) rig.TryGetRest(rig.aimPart, out _aimPos, out _aimAngle, out _);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _clock = 0f;
            _lean = 0f;
            _aimPoint = null;
            _aimWeight = _armRotation = _armPush = _bodyTilt = _bodyKick = 0f;
            _hurtTime = -1f;
            _attack = null;
            _dead = false;
            _pieces.Clear();
            _lastPosition = transform.position;
        }

        protected override void OnDisable()
        {
            StopAllCoroutines();
            _attack = null;
            _pieces.Clear();
            base.OnDisable();
        }

        // ============================================================ gestos

        public override void PlayAttack(Vector2? aimPoint, float speed)
        {
            if (_dead) return;
            if (_attack != null) StopCoroutine(_attack);
            _attack = StartCoroutine(AttackRoutine(aimPoint, Mathf.Max(0.01f, speed)));
        }

        private IEnumerator AttackRoutine(Vector2? aimPoint, float speed)
        {
            var rig = Rig;
            _aimPoint = aimPoint;

            // --- carga: el brazo gira hacia el punto, baja un poco el cañón (anticipación), el
            // cuerpo se echa atrás y al final tiembla: "va a disparar".
            float windup = rig.windup / speed;
            for (float t = 0f; t < windup;)
            {
                float k = t / windup;
                _aimWeight = EaseOutCubic(Mathf.Clamp01(k / 0.6f));
                _armRotation = -rig.windupPullAngle * EaseInOut(k);
                _armPush = -rig.recoilDistance * 0.35f * EaseInOut(k);
                _bodyTilt = rig.windupBodyTilt * EaseInOut(k);
                if (k > 0.7f) _armRotation += Mathf.Sin(_clock * 90f) * 1.5f;   // temblor de carga

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            // --- suelta: el aviso sale en este frame exacto, con el brazo ya apuntando.
            _aimWeight = 1f;
            RaiseReleased();

            // --- recupera: coz de golpe y vuelta al reposo con easing.
            float recover = rig.recover / speed;
            for (float t = 0f; t < recover;)
            {
                float k = t / recover;
                float decay = 1f - EaseOutCubic(k);

                _armRotation = rig.recoilAngle * decay * (1f - Mathf.Exp(-k * 40f));   // sube en 1-2 frames
                _armPush = rig.recoilDistance * decay;
                _bodyKick = rig.bodyRecoil * decay;
                _bodyTilt = rig.windupBodyTilt * decay;
                _aimWeight = k < 0.5f ? 1f : 1f - EaseInOut((k - 0.5f) / 0.5f);

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _aimWeight = _armRotation = _armPush = _bodyTilt = _bodyKick = 0f;
            _aimPoint = null;
            _attack = null;
            RaiseFinished();
        }

        public override void PlayHurt()
        {
            if (!_dead) _hurtTime = 0f;
        }

        public override void PlayDeath()
        {
            if (_dead) return;
            _dead = true;

            if (_attack != null) StopCoroutine(_attack);
            _attack = null;

            Vector2 center = Rig.body != null ? (Vector2)Rig.body.position : (Vector2)transform.position;

            // Se desmonta: cada pieza pasa a colgar de la raíz (conservando su pose en el mundo)
            // y vuela por su cuenta. La jerarquía original se restaura en OnEnable/OnDisable.
            foreach (var pose in _parts)
            {
                var part = pose.data?.part;
                if (part == null || !pose.data.breaksOnDeath) continue;

                part.SetParent(transform, true);

                Vector2 away = (Vector2)part.TransformPoint(pose.data.pivot) - center;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized;
                float jitter = Random.Range(0.7f, 1.3f);

                _pieces.Add(new Piece
                {
                    transform = part,
                    pivot = pose.data.pivot,
                    velocity = away * Rig.breakImpulse * jitter + Vector2.up * Rig.breakUpward * jitter,
                    spin = Random.Range(-1f, 1f) * Rig.breakSpin,
                });
            }
        }

        // ============================================================ composición

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (_dead)
            {
                SimulatePieces(dt);
                return;
            }

            _clock += dt;

            // Inclinación: sale de la velocidad real, así sirve igual si lo mueve la física, la
            // IA de un jefe o un empujón.
            Vector3 position = transform.position;
            float vx = dt > 0f ? (position.x - _lastPosition.x) / dt : 0f;
            _lastPosition = position;

            var rig = Rig;
            float leanTarget = -Mathf.Clamp(vx / rig.leanFullSpeed, -1f, 1f) * rig.leanDegrees;
            _lean = Mathf.Lerp(_lean, leanTarget, 1f - Mathf.Exp(-rig.leanResponse * dt));

            ComposeBody(dt);
            ComposeParts();
        }

        private void ComposeBody(float dt)
        {
            var rig = Rig;
            var body = rig.body;
            if (body == null) return;

            // Golpe recibido: tambaleo amortiguado + aplastamiento que rebota.
            float hurtTilt = 0f, squash = 0f;
            if (_hurtTime >= 0f)
            {
                _hurtTime += dt;
                float k = _hurtTime / rig.hurtDuration;
                if (k >= 1f) _hurtTime = -1f;
                else
                {
                    float envelope = 1f - k;
                    hurtTilt = rig.hurtTilt * envelope * Mathf.Sin(k * Mathf.PI * 3f);
                    squash = rig.hurtSquash * envelope * Mathf.Cos(k * Mathf.PI * 2f);
                }
            }

            // Hacia atrás = contra el facing. La raíz visual cuelga de la raíz sin voltear, así que
            // su posición y su giro están en espacio de mundo (no reflejado).
            Vector3 bob = rig.hoverBob * Mathf.Sin(_clock * rig.hoverSpeed);
            body.localPosition = _bodyPos + bob + new Vector3(-_bodyKick * Facing, 0f, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f,
                _bodyAngle + _lean + (_bodyTilt + hurtTilt) * Facing);
            body.localScale = new Vector3(Mathf.Abs(_bodyScale.x) * Facing * (1f + squash),
                                          _bodyScale.y * (1f - squash), _bodyScale.z);
        }

        private void ComposeParts()
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                var pose = _parts[i];
                var data = pose.data;
                if (data?.part == null) continue;

                float s = Mathf.Sin(_clock * data.swaySpeed + data.swayPhase);
                float angle = pose.angle + data.swayRotate * s;
                var scale = new Vector3(pose.scale.x * (1f + data.swayScale.x * s),
                                        pose.scale.y * (1f + data.swayScale.y * s), pose.scale.z);
                if (i == _aimIndex) angle += AimRotation();

                var t = data.part;
                t.localRotation = Quaternion.Euler(0f, 0f, angle);
                t.localScale = scale;
                t.localPosition = pose.position + (Vector3)(data.swayMove * s)
                                + ProceduralRig.PivotCorrection(data.pivot, pose.angle, pose.scale, angle, scale);

                if (i == _aimIndex) ApplyRecoilPush(t, angle);
            }

            if (_aimIndex < 0 && Rig.aimPart != null)
            {
                var arm = Rig.aimPart;
                float angle = _aimAngle + AimRotation();
                arm.localRotation = Quaternion.Euler(0f, 0f, angle);
                arm.localPosition = _aimPos;
                ApplyRecoilPush(arm, angle);
            }
        }

        /// <summary>Giro hacia el objetivo + anticipación + coz, encima del balanceo.</summary>
        private float AimRotation()
        {
            float aim = _aimPoint.HasValue && _aimWeight > 0f
                ? Rig.AimOffsetTowards(_aimPoint.Value, _aimPos, _aimAngle) * _aimWeight
                : 0f;
            return aim + _armRotation;
        }

        /// <summary>Retroceso a lo largo del cañón tal como apunta ahora.</summary>
        private void ApplyRecoilPush(Transform arm, float angle)
        {
            if (Mathf.Abs(_armPush) < 0.0001f) return;

            float barrel = (angle + Rig.aimArtAngle) * Mathf.Deg2Rad;
            arm.localPosition -= new Vector3(Mathf.Cos(barrel), Mathf.Sin(barrel), 0f) * _armPush;
        }

        // ============================================================ piezas sueltas

        private void SimulatePieces(float dt)
        {
            if (dt <= 0f) return;

            var rig = Rig;
            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                if (piece.transform == null || piece.resting) continue;

                piece.velocity.y -= rig.breakGravity * dt;

                // Todo se mide en el punto de articulación de la pieza (su parte visible), no en el
                // pivote del sprite, que en un lienzo completo puede caer lejos del dibujo.
                Vector2 from = piece.transform.TransformPoint(piece.pivot);
                Vector2 to = from + piece.velocity * dt;

                var hit = Physics2D.Linecast(from, to, rig.breakGround);
                if (hit.collider != null && piece.velocity.y < 0f)
                {
                    to = hit.point;
                    piece.velocity = new Vector2(piece.velocity.x * 0.6f, -piece.velocity.y * rig.breakBounce);
                    piece.spin *= 0.5f;
                    if (piece.velocity.y < 1f) piece.resting = true;
                }

                piece.transform.position += (Vector3)(to - from);
                piece.transform.RotateAround(to, Vector3.forward, piece.spin * dt);
                _pieces[i] = piece;
            }
        }

        // ============================================================ easing

        private static float EaseOutCubic(float x)
        {
            x = 1f - Mathf.Clamp01(x);
            return 1f - x * x * x;
        }

        private static float EaseInOut(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
