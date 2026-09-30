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
        private float _hurtStrength = 1f;

        // Gesto con nombre (jefes): pesos de la pose de carga y de la de golpe.
        private RigGesture _gesture;
        private float _gestureWindup, _gestureStrike, _gestureShake;
        private Coroutine _gestureRoutine;

        // Despliegue: 0 plegado (dormido), 1 pose del prefab, >1 durante el rebote.
        private float _unfold = 1f;
        private Coroutine _fold;
        private Vector3 _stretchScale;

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
            if (rig.stretchPart != null) rig.TryGetRest(rig.stretchPart, out _, out _, out _stretchScale);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _clock = 0f;
            _lean = 0f;
            _aimPoint = null;
            _aimWeight = _armRotation = _armPush = _bodyTilt = _bodyKick = 0f;
            _hurtTime = -1f;
            _hurtStrength = 1f;
            _gesture = null;
            _gestureWindup = _gestureStrike = _gestureShake = 0f;
            _gestureRoutine = null;
            _attack = null;
            _fold = null;
            _unfold = 1f;
            _dead = false;
            _pieces.Clear();
            _lastPosition = transform.position;
        }

        protected override void OnDisable()
        {
            StopAllCoroutines();
            _attack = null;
            _fold = null;
            _gestureRoutine = null;
            _gesture = null;
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
            rig.ShowSprites(RigSpriteState.Attacking);   // aviso: ojos de disparo desde la carga

            // --- carga: el brazo gira hacia el punto, baja el cañón (anticipación) y lo vuelve a
            // subir, el cuerpo se echa atrás y al final tiembla. En el último 15% el cañón está
            // ya exactamente sobre el objetivo: el disparo sale de donde apunta.
            float windup = rig.windup / speed;
            for (float t = 0f; t < windup;)
            {
                float k = t / windup;
                float bell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k / 0.85f));
                _aimWeight = EaseOutCubic(Mathf.Clamp01(k / 0.6f));
                _armRotation = -rig.windupPullAngle * bell;
                _bodyTilt = rig.windupBodyTilt * EaseInOut(k);
                if (k > 0.7f) _armRotation += Mathf.Sin(_clock * 90f) * 1.5f;   // temblor de carga

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            // --- suelta: el aviso sale en este frame exacto, con el brazo quieto y apuntando.
            _aimWeight = 1f;
            _armRotation = 0f;
            RaiseReleased();

            // --- recupera: el arma aguanta 'recoilDelay' (el proyectil sale del cañón a la vista),
            // la coz sube en 'recoilKick' con easing y luego todo vuelve al reposo.
            float hold = rig.recoilDelay / speed;
            float kick = rig.recoilKick / speed;
            float recover = Mathf.Max(rig.recover / speed, hold + kick + 0.01f);
            float settle = recover - hold - kick;
            for (float t = 0f; t < recover;)
            {
                float k = t / recover;
                float strength = t < hold ? 0f
                    : t < hold + kick ? EaseOutCubic((t - hold) / kick)
                    : 1f - EaseInOut((t - hold - kick) / settle);

                _armRotation = rig.recoilAngle * strength;
                _armPush = rig.recoilDistance * strength;
                _bodyKick = rig.bodyRecoil * strength;
                _bodyTilt = rig.windupBodyTilt * (1f - EaseOutCubic(k));
                _aimWeight = k < 0.5f ? 1f : 1f - EaseInOut((k - 0.5f) / 0.5f);

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _aimWeight = _armRotation = _armPush = _bodyTilt = _bodyKick = 0f;
            _aimPoint = null;
            _attack = null;
            rig.ShowSprites(RigSpriteState.Awake);
            RaiseFinished();
        }

        public override void PlayHurt(float strength)
        {
            if (_dead) return;
            // Un golpe flojo no pisa un tambaleo fuerte que aún está sonando.
            if (_hurtTime >= 0f && strength < _hurtStrength) return;
            _hurtTime = 0f;
            _hurtStrength = Mathf.Max(0.01f, strength);
        }

        // ============================================================ gestos con nombre (jefes)

        public override bool PlayGesture(string gestureName, float secondsToRelease)
        {
            if (_dead) return false;
            var gesture = Rig.FindGesture(gestureName);
            if (gesture == null) return false;

            StartGesture(GestureRoutine(gesture, Mathf.Max(0.01f, secondsToRelease)), gesture);
            return true;
        }

        public override bool HoldGesture(string gestureName)
        {
            if (_dead) return false;
            var gesture = Rig.FindGesture(gestureName);
            if (gesture == null) return false;

            StartGesture(HoldRoutine(), gesture);
            return true;
        }

        public override void ClearHold()
        {
            if (_dead || _gesture == null) return;
            StartGesture(SettleRoutine(0.25f), _gesture);
        }

        private void StartGesture(IEnumerator routine, RigGesture gesture)
        {
            if (_gestureRoutine != null) StopCoroutine(_gestureRoutine);

            // Otro gesto encima: la pose nueva arranca desde cero, pero la vieja no desaparece de
            // golpe si se estaba usando la misma — se reaprovechan los pesos actuales.
            if (_gesture != gesture) _gestureWindup = _gestureStrike = 0f;
            _gesture = gesture;
            _gestureShake = 0f;
            _gestureRoutine = StartCoroutine(routine);
        }

        /// <summary>Carga (easing) → suelta (evento) → golpe (strikeTime) → vuelta al reposo.</summary>
        private IEnumerator GestureRoutine(RigGesture gesture, float windup)
        {
            float from = _gestureWindup;
            for (float t = 0f; t < windup;)
            {
                float k = t / windup;
                _gestureWindup = Mathf.Lerp(from, 1f, EaseInOut(k));
                _gestureStrike = Mathf.Lerp(_gestureStrike, 0f, k);
                _gestureShake = k > 0.7f ? gesture.shake : 0f;

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _gestureWindup = 1f;
            _gestureShake = 0f;
            RaiseReleased();

            for (float t = 0f; t < gesture.strikeTime;)
            {
                float k = EaseOutCubic(t / gesture.strikeTime);
                _gestureStrike = k;
                _gestureWindup = 1f - k;

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _gestureStrike = 1f;
            _gestureWindup = 0f;
            yield return SettleRoutine(gesture.recover);
            RaiseFinished();
        }

        /// <summary>Llega a la pose de carga y se queda ahí hasta <see cref="ClearHold"/>.</summary>
        private IEnumerator HoldRoutine()
        {
            float from = _gestureWindup;
            const float blend = 0.15f;
            for (float t = 0f; t < blend;)
            {
                _gestureWindup = Mathf.Lerp(from, 1f, EaseInOut(t / blend));
                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _gestureWindup = 1f;
            _gestureRoutine = null;
        }

        /// <summary>De los pesos actuales a cero.</summary>
        private IEnumerator SettleRoutine(float seconds)
        {
            float windup = _gestureWindup, strike = _gestureStrike;
            _gestureShake = 0f;
            for (float t = 0f; t < seconds;)
            {
                float e = 1f - EaseInOut(t / seconds);
                _gestureWindup = windup * e;
                _gestureStrike = strike * e;
                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _gestureWindup = _gestureStrike = 0f;
            _gesture = null;
            _gestureRoutine = null;
        }

        /// <summary>Suma las poses del gesto encima de todo lo compuesto este frame.</summary>
        private void ApplyGesture()
        {
            if (_gesture == null) return;

            ApplyPoses(_gesture.windup, _gestureWindup);
            ApplyPoses(_gesture.strike, _gestureStrike);

            var body = Rig.body;
            if (_gestureShake > 0f && body != null)
                body.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(_clock * 80f) * _gestureShake);
        }

        private void ApplyPoses(List<RigPose> poses, float weight)
        {
            if (weight <= 0.0001f || poses == null) return;

            var body = Rig.body;
            foreach (var pose in poses)
            {
                var t = pose?.part;
                if (t == null || Driven.ContainsKey(t)) continue;

                // Sólo lo que se recompone cada frame: sumar sobre algo que no se reinicia lo haría derivar.
                if (t != body && Rig.FindPart(t) == null) continue;

                // La raíz visual cuelga de la raíz sin voltear: 'delante' y 'atrás' dependen del facing.
                // Las partes cuelgan de ella ya volteadas, así que sus números valen tal cual.
                float side = t == body ? Facing : 1f;
                t.localPosition += new Vector3(pose.move.x * side, pose.move.y, 0f) * weight;
                t.localRotation *= Quaternion.Euler(0f, 0f, pose.rotate * side * weight);
                var s = t.localScale;
                t.localScale = new Vector3(s.x * (1f + pose.scale.x * weight), s.y * (1f + pose.scale.y * weight), s.z);
            }
        }

        // ============================================================ plegar / desplegar

        public override void PlayWake(float speed)
        {
            if (_dead) return;
            if (!Rig.CanFold)
            {
                Rig.ShowSprites(RigSpriteState.Awake);
                RaiseWakeFinished();
                return;
            }

            if (_fold != null) StopCoroutine(_fold);
            _fold = StartCoroutine(WakeRoutine(Mathf.Max(0.01f, speed)));
        }

        public override void PlaySleep(bool instant)
        {
            if (_dead) return;
            if (_attack != null) StopCoroutine(_attack);
            _attack = null;
            if (_fold != null) StopCoroutine(_fold);
            _fold = null;

            Rig.ShowSprites(RigSpriteState.Asleep);   // los ojos se apagan antes de plegarse
            if (instant || !Rig.CanFold) _unfold = 0f;
            else _fold = StartCoroutine(SleepRoutine());
        }

        /// <summary>
        /// El cuello sale de la base con rebote (OutBack) llevando la cabeza en la punta; en el
        /// tramo final los ojos parpadean hasta quedarse encendidos. Arranca desde donde esté (un
        /// despertar a mitad de plegarse no salta).
        /// </summary>
        private IEnumerator WakeRoutine(float speed)
        {
            var rig = Rig;
            float from = _unfold;
            float duration = rig.wakeDuration / speed;
            float flickerFrom = 1f - rig.wakeFlickerPortion;

            for (float t = 0f; t < duration;)
            {
                float k = t / duration;
                _unfold = Mathf.LerpUnclamped(from, 1f, ProceduralRig.OutBack(k, rig.wakeOvershoot));

                if (k >= flickerFrom)
                {
                    // 0 parpadeos = los ojos se abren una vez, limpio, al entrar en este tramo.
                    float f = (k - flickerFrom) / Mathf.Max(0.0001f, rig.wakeFlickerPortion);
                    bool on = rig.wakeFlickers <= 0 || (int)(f * rig.wakeFlickers * 2f) % 2 == 1;
                    rig.ShowSprites(on ? RigSpriteState.Awake : RigSpriteState.Asleep);
                }

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _unfold = 1f;
            rig.ShowSprites(RigSpriteState.Awake);
            _fold = null;
            RaiseWakeFinished();
        }

        /// <summary>El despertar al revés: se estira un poco (InBack) y se recoge sobre la base.</summary>
        private IEnumerator SleepRoutine()
        {
            var rig = Rig;
            float from = _unfold;
            float duration = rig.sleepDuration;

            for (float t = 0f; t < duration;)
            {
                _unfold = Mathf.LerpUnclamped(from, 0f, ProceduralRig.InBack(t / duration, rig.wakeOvershoot * 0.5f));
                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            _unfold = 0f;
            _fold = null;
        }

        public override void PlayDeath()
        {
            if (_dead) return;
            _dead = true;

            if (_attack != null) StopCoroutine(_attack);
            _attack = null;
            if (_fold != null) StopCoroutine(_fold);
            _fold = null;
            Rig.ShowSprites(RigSpriteState.Asleep);   // se apagan los ojos
            if (_gestureRoutine != null) StopCoroutine(_gestureRoutine);
            _gestureRoutine = null;
            _gesture = null;
            Driven.Clear();

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

            RepairHierarchy();
            _clock += dt;
            TickDriven(dt);

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
            ApplyGesture();
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
                float k = _hurtTime / (rig.hurtDuration * Mathf.Sqrt(_hurtStrength));
                if (k >= 1f) _hurtTime = -1f;
                else
                {
                    float envelope = 1f - k;
                    hurtTilt = rig.hurtTilt * _hurtStrength * envelope * Mathf.Sin(k * Mathf.PI * 3f);
                    squash = rig.hurtSquash * Mathf.Min(_hurtStrength, 2f) * envelope * Mathf.Cos(k * Mathf.PI * 2f);
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
            var rig = Rig;
            float stretch = rig.CanFold ? rig.StretchAt(_unfold) : 1f;
            if (rig.CanFold)
                rig.stretchPart.localScale = new Vector3(_stretchScale.x, _stretchScale.y * stretch, _stretchScale.z);

            for (int i = 0; i < _parts.Length; i++)
            {
                var pose = _parts[i];
                var data = pose.data;
                if (data?.part == null) continue;

                var t = data.part;
                bool rider = rig.CanFold && rig.stretchRiders.Contains(t);

                // Plegado no respira: el balanceo de los pasajeros escala con el despliegue, así
                // la cabeza dormida se queda quieta y pegada a la base.
                float s = Mathf.Sin(_clock * data.swaySpeed + data.swayPhase) *
                          (rider ? Mathf.Clamp01(_unfold) : 1f);
                // La inclinación es de mundo y la parte cuelga de la raíz volteada: se refleja con el facing.
                float angle = pose.angle + data.swayRotate * s + _lean * data.leanFollow * Facing;
                var scale = new Vector3(pose.scale.x * (1f + data.swayScale.x * s),
                                        pose.scale.y * (1f + data.swayScale.y * s), pose.scale.z);
                if (i == _aimIndex) angle += AimRotation();
                if (rider) scale.y *= rig.RiderScaleY(_unfold);

                var position = pose.position + (Vector3)(data.swayMove * s)
                             + ProceduralRig.PivotCorrection(data.pivot, pose.angle, pose.scale, angle, scale);
                if (rider) position += rig.RiderOffset(pose.position, stretch, _unfold);
                var rotation = Quaternion.Euler(0f, 0f, angle);

                // Prestada a un ataque: la mueve él. Volviendo: mezcla desde donde la soltó hasta la
                // pose que le tocaría ahora (que sigue viva: el cuerpo se mueve mientras vuelve).
                if (Driven.TryGetValue(t, out var driven))
                {
                    if (!driven.returning) continue;

                    float e = EaseInOut(driven.returnTime / driven.returnDuration);
                    t.localPosition = Vector3.LerpUnclamped(driven.fromPosition, position, e);
                    t.localRotation = Quaternion.SlerpUnclamped(driven.fromRotation, rotation, e);
                    // Un puño reflejado (ver BossLimbs) no se "desvoltea" de golpe al soltarlo: a mitad de vuelta.
                    t.localScale = e < 0.5f ? driven.fromScale : scale;
                    continue;
                }

                t.localRotation = rotation;
                t.localScale = scale;
                t.localPosition = position;

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
