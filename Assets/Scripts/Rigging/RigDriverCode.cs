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
    ///
    /// La composición (este <c>LateUpdate</c>) es independiente de <b>quién mueve los números</b>:
    /// aquí son corrutinas; <see cref="RigDriverTween"/> hereda la composición tal cual y los mueve
    /// con DOTween. Por eso las capas de gesto son <c>protected</c>.
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
        protected float _clock;
        private Vector3 _lastPosition;
        private float _lean;

        // Inercia: desplazamiento (mundo) del cuerpo respecto a la raíz y su velocidad (muelle).
        private Vector2 _followOffset, _followVelocity;

        // Capas de gesto que la composición suma.
        protected Vector2? _aimPoint;
        protected float _aimWeight;
        protected float _armRotation;   // anticipación + coz, grados
        protected float _armPush;       // retroceso a lo largo del cañón, unidades locales
        protected float _bodyTilt;      // grados hacia atrás
        protected float _bodyKick;      // unidades hacia atrás
        private float _hurtTime = -1f;
        protected float _hurtStrength = 1f;

        /// <summary>Tambaleo (grados) y aplastamiento extra que suma un motor derivado al cuerpo.</summary>
        protected virtual float ExtraTilt => 0f;
        protected virtual float ExtraSquash => 0f;

        // Gesto con nombre: el peso de la pose de cada uno de sus pasos (el cuerpo está en la mezcla).
        protected RigGesture _gesture;
        protected readonly List<float> _weights = new List<float>();
        protected float _gestureShake;
        private Coroutine _gestureRoutine;
        private readonly List<float> _from = new List<float>();        // pesos al empezar el tramo en curso
        private readonly List<float> _durations = new List<float>();   // segundos de cada paso de este lanzamiento

        // Volviendo al reposo: los giros de las poses se toman por el camino corto. Un paso que deja
        // una pieza a 360° (un giro completo) ya está, a la vista, en reposo — no se desenrosca.
        private bool _settling;

        // Gesto anterior desvaneciéndose: al encadenar gestos (la carga de la embestida y su pose de
        // viaje) el primero no desaparece de golpe, se funde con el nuevo.
        protected const float GestureCrossfade = 0.2f;
        protected RigGesture _fadeGesture;
        protected readonly List<float> _fadeWeights = new List<float>();
        protected float _fadeTime;

        // Despliegue: 0 plegado (dormido), 1 pose del prefab, >1 durante el rebote.
        protected float _unfold = 1f;
        private Coroutine _fold;
        private Vector3 _stretchScale;

        // Gestos en reposo (ProceduralRig.idle): cuenta atrás hasta el siguiente intento, y por dónde va.
        private float _idleTimer;
        private int _idleLast = -1;
        private bool _moving, _asleep;

        private Coroutine _attack;
        protected bool _dead;
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
            _followOffset = _followVelocity = Vector2.zero;
            _aimPoint = null;
            _aimWeight = _armRotation = _armPush = _bodyTilt = _bodyKick = 0f;
            _hurtTime = -1f;
            _hurtStrength = 1f;
            _gesture = null;
            _weights.Clear();
            _gestureShake = 0f;
            _gestureRoutine = null;
            _fadeGesture = null;
            _attack = null;
            _fold = null;
            _unfold = 1f;
            _dead = false;
            _moving = _asleep = false;
            _idleLast = -1;
            _idleTimer = NextIdleDelay();
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
            Rig.ClearCastPoint();   // el ataque propio sale de la boca
            AimTarget = aimPoint;
            ReleasesFired = 0;
            ReleaseTotal = 1;
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
            Rig.SetHurtSprites(true);
            _hurtStrength = Mathf.Max(0.01f, strength);
        }

        // ============================================================ gestos con nombre (jefes)

        public override bool PlayGesture(string gestureName, float secondsToRelease)
        {
            if (_dead) return false;
            var gesture = Rig.FindGesture(gestureName);
            if (gesture == null) return false;

            StartGesture(GestureRoutine(gesture, Mathf.Max(0.01f, secondsToRelease)), gesture);
            Rig.ShowGestureSprites(gesture.name, isAttack: true);
            return true;
        }

        /// <summary>
        /// Desplazándose: mantiene el gesto de moverse (<see cref="ProceduralRig.moveGesture"/>) fundido
        /// encima; al parar lo suelta. Nunca pisa otro gesto (un ataque manda).
        /// </summary>
        public override void SetMoving(bool moving)
        {
            _moving = moving;
            if (_dead) return;
            var move = Rig.FindGesture(Rig.moveGesture);
            if (move == null) return;

            if (moving && (_gesture == null || _gesture == move))
            {
                StartGesture(HoldRoutine(), move, hold: true);
                Rig.ShowGestureSprites(move.name, isAttack: false);
            }
            else if (!moving && _gesture == move)
                StartGesture(SettleRoutine(0.3f), move);
        }

        public override bool HoldGesture(string gestureName)
        {
            if (_dead) return false;
            var gesture = Rig.FindGesture(gestureName);
            if (gesture == null) return false;

            StartGesture(HoldRoutine(), gesture, hold: true);
            Rig.ShowGestureSprites(gesture.name, isAttack: false);
            return true;
        }

        public override void ClearHold()
        {
            if (_dead || _gesture == null) return;
            StartGesture(SettleRoutine(0.25f), _gesture);
        }

        /// <param name="hold">Pose mantenida (desplazarse, viaje de una embestida): se llama cada vez
        /// que se reafirma, así que sus efectos sólo arrancan cuando el gesto es nuevo.</param>
        private void StartGesture(IEnumerator routine, RigGesture gesture, bool hold = false)
        {
            // Apagándose (p. ej. Mover.OnDisable → SetMoving(false)): no hay corrutinas, y
            // OnDisable/OnEnable ya dejan el gesto en reposo.
            if (!isActiveAndEnabled) return;

            if (_gestureRoutine != null) StopCoroutine(_gestureRoutine);

            // Efectos de gesto: lo que hubiera en bucle se apaga, salvo que sea la misma pose mantenida.
            bool fresh = _gesture != gesture;
            if (fresh || !hold) StopFx();

            // Otro gesto encima: el viejo pasa a la capa de fundido con los pesos que tenía y el nuevo
            // arranca de cero; el mismo gesto sigue desde donde estaba.
            if (_gesture != gesture)
            {
                if (_gesture != null && AnyWeight())
                {
                    _fadeGesture = _gesture;
                    _fadeWeights.Clear();
                    _fadeWeights.AddRange(_weights);
                    _fadeTime = 0f;
                }

                _weights.Clear();
            }
            _gesture = gesture;
            _settling = false;
            Fit(_weights, gesture.steps != null ? gesture.steps.Count : 0);
            _gestureShake = 0f;
            // Una pose mantenida no acaba su primer paso: sólo salen los efectos del arranque.
            if (hold && fresh) FireFx(gesture, 0, -1f, 0f);
            _gestureRoutine = StartCoroutine(routine);
        }

        private static void Fit(List<float> weights, int count)
        {
            while (weights.Count < count) weights.Add(0f);
            if (weights.Count > count) weights.RemoveRange(count, weights.Count - count);
        }

        private bool AnyWeight()
        {
            foreach (float w in _weights) if (Mathf.Abs(w) > 0.001f) return true;
            return false;
        }

        /// <summary>Los pesos de ahora: el punto de partida del tramo que empieza.</summary>
        private void Snapshot()
        {
            _from.Clear();
            _from.AddRange(_weights);
        }

        /// <summary>De donde estaba a "sólo la pose de <paramref name="step"/>" (-1 = el reposo), en <paramref name="k"/> (0–1).</summary>
        private void BlendTo(int step, float k)
        {
            for (int i = 0; i < _weights.Count && i < _from.Count; i++)
                _weights[i] = Mathf.LerpUnclamped(_from[i], i == step ? 1f : 0f, k);
        }

        private static float Ease(RigEase ease, float x)
        {
            x = Mathf.Clamp01(x);
            switch (ease)
            {
                case RigEase.Out: return EaseOutCubic(x);
                case RigEase.In: return x * x * x;
                case RigEase.Linear: return x;
                default: return EaseInOut(x);
            }
        }

        /// <summary>
        /// La línea de tiempo del gesto: paso a paso (cada uno llega a su pose con su easing), luego la
        /// vuelta al reposo. Por el camino salen sus efectos y, en cada <see cref="RigRelease"/>, el
        /// ataque: se fija de dónde sale y se avisa (<c>AttackReleased</c>), tantas veces como sueltas haya.
        /// <paramref name="lead"/> = el aviso de quien lo lanza: lo que duran sus pasos 'stretch'.
        /// <paramref name="silent"/> = un gesto de reposo: mismas poses, efectos y sonido, pero no avisa
        /// de nada (ni <c>AttackReleased</c> ni <c>AttackFinished</c> ni fogonazo): no es un ataque.
        /// </summary>
        private IEnumerator GestureRoutine(RigGesture gesture, float lead, bool silent = false)
        {
            PlayGestureSound(gesture, GestureSoundMoment.Start);
            Rig.ClearCastPoint();

            var steps = gesture.steps;
            int count = steps != null ? steps.Count : 0;
            var releases = gesture.EffectiveReleases;
            gesture.Durations(lead, _durations);
            ReleasesFired = 0;
            ReleaseTotal = silent ? 0 : Mathf.Max(1, releases.Count);
            bool sounded = false;

            // Sin pasos no hay pose que recorrer: espera el aviso y suelta.
            if (count == 0)
            {
                for (float t = 0f; t < lead;)
                {
                    yield return null;
                    if (!Paused) t += Time.deltaTime;
                }

                Release(gesture, null, silent, ref sounded);
            }

            for (int s = 0; s < count; s++)
            {
                var step = steps[s];
                float duration = _durations[s];
                float done = -1f;   // avance del paso ya recorrido por sus efectos y sueltas
                Snapshot();
                for (float t = 0f; t < duration;)
                {
                    float progress = t / duration;
                    BlendTo(s, Ease(step != null ? step.ease : RigEase.InOut, progress));
                    _gestureShake = step != null && progress > 0.7f ? step.shake : 0f;
                    FireStep(gesture, releases, s, count, done, progress, silent, ref sounded);
                    done = progress;
                    Rig.SetGestureMoment(s + progress);

                    yield return null;
                    if (!Paused) t += Time.deltaTime;
                }

                BlendTo(s, 1f);
                _gestureShake = 0f;
                FireStep(gesture, releases, s, count, done, 1f, silent, ref sounded);
                EndFxStep(s);
                Rig.SetGestureMoment(s + 1f);
            }

            yield return SettleRoutine(gesture.recover, gesture);
            if (!silent) RaiseFinished();
        }

        /// <summary>Lo que cae en el tramo (<paramref name="from"/>, <paramref name="to"/>] del paso: efectos y sueltas.</summary>
        private void FireStep(RigGesture gesture, List<RigRelease> releases, int step, int count,
                              float from, float to, bool silent, ref bool sounded)
        {
            FireFx(gesture, step, from, to);
            for (int i = 0; i < releases.Count; i++)
            {
                var r = releases[i];
                if (r == null || Mathf.Min(r.step, count - 1) != step || r.at <= from || r.at > to) continue;
                Release(gesture, r, silent, ref sounded);
            }
        }

        private void Release(RigGesture gesture, RigRelease release, bool silent, ref bool sounded)
        {
            if (!sounded)
            {
                sounded = true;
                PlayGestureSound(gesture, GestureSoundMoment.Release);
            }

            if (silent) return;

            if (release != null) Rig.SetCastPoint(release.point, release.offset);
            ReleasesFired++;
            RaiseReleased(gesture);
        }

        // ============================================================ gestos en reposo

        private float NextIdleDelay() => Mathf.Max(0.2f, Rig.idle != null ? Rig.idle.interval : 4f) * Random.Range(0.75f, 1.25f);

        /// <summary>
        /// En reposo de verdad — vivo, despierto y desplegado, sin objetivo, sin moverse, sin gesto ni
        /// ataque en curso, sin piezas prestadas a un ataque — cada <c>idle.interval</c> segundos tira
        /// el dado y, si sale, hace uno de sus gestos de reposo (nunca el mismo dos veces seguidas si
        /// hay más). En cuanto deja de estar en reposo la cuenta vuelve a empezar; un gesto de verdad
        /// (ataque, moverse) lo sustituye con el fundido de siempre.
        /// </summary>
        private void TickIdle(float dt)
        {
            var idle = Rig.idle;
            if (idle?.gestures == null || idle.gestures.Count == 0) return;

            bool resting = !Paused && !Alert && !_moving && !_asleep && _fold == null && _gesture == null &&
                           _attack == null && _hurtTime < 0f && Driven.Count == 0;
            if (!resting)
            {
                _idleTimer = NextIdleDelay();
                return;
            }

            _idleTimer -= dt;
            if (_idleTimer > 0f) return;
            _idleTimer = NextIdleDelay();
            if (Random.value > idle.chance) return;

            int index = Random.Range(0, idle.gestures.Count);
            if (index == _idleLast && idle.gestures.Count > 1) index = (index + 1) % idle.gestures.Count;
            var gesture = Rig.FindGesture(idle.gestures[index]);
            if (gesture == null) return;

            _idleLast = index;
            StartGesture(GestureRoutine(gesture, Mathf.Max(0.05f, idle.windup), silent: true), gesture);
            Rig.ShowGestureSprites(gesture.name, isAttack: false);
        }

        /// <summary>Llega a la pose del primer paso y se queda ahí hasta <see cref="ClearHold"/>.</summary>
        private IEnumerator HoldRoutine()
        {
            Snapshot();
            const float blend = GestureCrossfade;
            for (float t = 0f; t < blend;)
            {
                Rig.SetGestureMoment(t / blend);   // una pose mantenida recorre su primer paso y se queda en su final
                BlendTo(0, EaseInOut(t / blend));
                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            BlendTo(0, 1f);
            Rig.SetGestureMoment(1f);
            _gestureRoutine = null;
        }

        /// <summary>
        /// De los pesos actuales a cero. <paramref name="recovering"/> = es la vuelta al reposo de ese
        /// gesto (su fase Recover): salen sus efectos; null = un corte, sin efectos.
        /// </summary>
        private IEnumerator SettleRoutine(float seconds, RigGesture recovering = null)
        {
            Snapshot();
            _settling = true;
            // La vuelta es el tramo que sigue al último paso: ahí caen sus efectos y sus sprites.
            var current = recovering ?? _gesture;
            int last = current?.steps != null ? current.steps.Count : 0;
            float fxFrom = -1f;
            _gestureShake = 0f;
            for (float t = 0f; t < seconds;)
            {
                float k = t / seconds;
                BlendTo(-1, EaseInOut(k));
                Rig.SetGestureMoment(last + k);   // la vuelta al reposo, sea el final del gesto o un corte
                if (recovering != null)
                {
                    FireFx(recovering, last, fxFrom, k);
                    fxFrom = k;
                }

                yield return null;
                if (!Paused) t += Time.deltaTime;
            }

            if (recovering != null)
            {
                FireFx(recovering, last, fxFrom, 1f);
                EndFxStep(last);
            }

            _weights.Clear();
            _settling = false;
            Rig.ClearCastPoint();
            _gesture = null;
            _gestureRoutine = null;
            Rig.EndGestureSprites();   // los sprites propios del gesto duran hasta que el cuerpo vuelve al reposo
        }

        /// <summary>Suma las poses del gesto encima de todo lo compuesto este frame.</summary>
        protected virtual void ApplyGesture()
        {
            ApplyFadeGesture();

            if (_gesture == null) return;

            ApplySteps(_gesture, _weights, 1f, _settling);

            var body = Rig.body;
            if (_gestureShake > 0f && body != null)
                body.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(_clock * 80f) * _gestureShake);
        }

        /// <summary>El gesto anterior, desvaneciéndose debajo del nuevo.</summary>
        protected void ApplyFadeGesture()
        {
            if (_fadeGesture == null) return;

            _fadeTime += Time.deltaTime;
            float f = 1f - EaseInOut(_fadeTime / GestureCrossfade);
            if (f <= 0f) _fadeGesture = null;
            else
            {
                ApplySteps(_fadeGesture, _fadeWeights, f, true);
            }
        }

        /// <summary>La pose de cada paso a su peso (× <paramref name="scale"/>): la mezcla en la que está el cuerpo.</summary>
        /// <param name="shortTurns">Los giros, por el camino corto (ver <see cref="_settling"/>).</param>
        protected void ApplySteps(RigGesture gesture, List<float> weights, float scale, bool shortTurns = false)
        {
            var steps = gesture.steps;
            if (steps == null) return;
            for (int i = 0; i < steps.Count && i < weights.Count; i++)
                if (steps[i] != null) ApplyPoses(steps[i].poses, weights[i] * scale, shortTurns);
        }

        protected void ApplyPoses(List<RigPose> poses, float weight, bool shortTurns = false)
        {
            if (poses == null) return;
            foreach (var pose in poses) ApplyPose(pose, weight, shortTurns);
        }

        /// <summary>Una pose a un peso. El peso puede pasarse de 1 o bajar de 0 (un easing con rebote).</summary>
        protected void ApplyPose(RigPose pose, float weight, bool shortTurns = false)
        {
            if (Mathf.Abs(weight) <= 0.0001f) return;

            var body = Rig.body;
            var t = pose?.part;
            if (t == null || Driven.ContainsKey(t)) return;

            // Sólo lo que se recompone cada frame: sumar sobre algo que no se reinicia lo haría derivar.
            if (t != body && Rig.FindPart(t) == null) return;

            // La raíz visual cuelga de la raíz sin voltear: 'delante' y 'atrás' dependen del facing.
            // Las partes cuelgan de ella ya volteadas, así que sus números valen tal cual.
            float side = t == body ? Facing : 1f;
            t.localPosition += new Vector3(pose.move.x * side, pose.move.y, 0f) * weight;
            float turn = shortTurns ? Mathf.DeltaAngle(0f, pose.rotate) : pose.rotate;
            t.localRotation *= Quaternion.Euler(0f, 0f, turn * side * weight);
            var s = t.localScale;
            t.localScale = new Vector3(s.x * (1f + pose.scale.x * weight), s.y * (1f + pose.scale.y * weight), s.z);
        }

        // ============================================================ plegar / desplegar

        public override void PlayWake(float speed)
        {
            if (_dead) return;
            _asleep = false;
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

            _asleep = true;
            if (_gesture != null) StartGesture(SettleRoutine(0.2f), _gesture);   // un gesto de reposo a medias no sigue dormido
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
            Rig.ShowDeadSprites();   // su sprite de muerto, o se apagan los ojos (el de dormido)
            if (_gestureRoutine != null) StopCoroutine(_gestureRoutine);
            _gestureRoutine = null;
            _gesture = null;
            _aimWeight = _armRotation = _armPush = _bodyTilt = _bodyKick = 0f;
            Driven.Clear();
            StopFx();
            _fadeGesture = null;

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
            TickIdle(dt);

            // Inclinación: sale de la velocidad real, así sirve igual si lo mueve la física, la
            // IA de un jefe o un empujón.
            Vector3 position = transform.position;
            float vx = dt > 0f ? (position.x - _lastPosition.x) / dt : 0f;
            UpdateFollow((Vector2)(position - _lastPosition), dt);
            _lastPosition = position;

            var rig = Rig;
            float leanTarget = -Mathf.Clamp(vx / rig.leanFullSpeed, -1f, 1f) * rig.leanDegrees;
            _lean = Mathf.Lerp(_lean, leanTarget, 1f - Mathf.Exp(-rig.leanResponse * dt));

            ComposeBody(dt);
            ComposeParts();
            ApplyGesture();
            TickFx(dt);   // después de componer: los efectos de gesto siguen a su ancla ya colocada
        }

        /// <summary>
        /// El cuerpo como una masa colgada de la raíz por un muelle: cuando la raíz se mueve el cuerpo
        /// se queda atrás (el desplazamiento absorbe el paso), el muelle lo arrastra y, al parar, se
        /// pasa un poco y vuelve. Un salto enorme en un frame (teletransporte, reaparecer) no se
        /// interpreta como movimiento.
        /// </summary>
        private void UpdateFollow(Vector2 rootDelta, float dt)
        {
            var rig = Rig;
            if (rig.followStiffness <= 0f || dt <= 0f || rootDelta.sqrMagnitude > 9f)
            {
                _followOffset = _followVelocity = Vector2.zero;
                return;
            }

            _followOffset -= rootDelta;
            Vector2 accel = -rig.followStiffness * _followOffset - rig.followDamping * _followVelocity;
            _followVelocity += accel * dt;
            _followOffset += _followVelocity * dt;

            if (rig.followMaxOffset > 0f && _followOffset.sqrMagnitude > rig.followMaxOffset * rig.followMaxOffset)
            {
                _followOffset = _followOffset.normalized * rig.followMaxOffset;
                _followVelocity = Vector2.zero;
            }
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
                if (k >= 1f)
                {
                    _hurtTime = -1f;
                    Rig.SetHurtSprites(false);
                }
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
            body.localPosition = _bodyPos + bob + new Vector3(-_bodyKick * Facing, 0f, 0f)
                               + transform.InverseTransformVector(_followOffset);
            hurtTilt += ExtraTilt;
            squash += ExtraSquash;
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
            // El ataque propio apunta con su peso; un gesto, con el de sus pasos marcados 'aim' (hacia el
            // objetivo que le dio quien lo lanzó, AimTarget).
            float weight = _aimWeight;
            Vector2? point = _aimPoint;
            float gesture = GestureAimWeight();
            if (gesture > weight && AimTarget.HasValue)
            {
                weight = gesture;
                point = AimTarget;
            }

            float aim = point.HasValue && weight > 0f
                ? Rig.AimOffsetTowards(point.Value, _aimPos, _aimAngle) * weight
                : 0f;
            return aim + _armRotation;
        }

        /// <summary>Cuánto apunta el gesto en curso: la suma de los pesos de sus pasos <see cref="RigGestureStep.aim"/>.</summary>
        private float GestureAimWeight()
        {
            var steps = _gesture?.steps;
            if (steps == null) return 0f;

            float weight = 0f;
            for (int i = 0; i < steps.Count && i < _weights.Count; i++)
                if (steps[i] != null && steps[i].aim) weight += _weights[i];
            return Mathf.Clamp01(weight);
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

        protected static float EaseOutCubic(float x)
        {
            x = 1f - Mathf.Clamp01(x);
            return 1f - x * x * x;
        }

        protected static float EaseInOut(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
