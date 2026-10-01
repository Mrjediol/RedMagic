using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace RedMagic.Rigging
{
    /// <summary>
    /// Motor del rig con <b>DOTween</b>. Hereda de <see cref="RigDriverCode"/> la composición (reposo +
    /// balanceo + capas, cada frame) y cambia <b>quién mueve los números</b> de esas capas: en vez de
    /// una corrutina por gesto con curvas escritas a mano, cada gesto es una <see cref="Sequence"/>.
    ///
    /// Mismo contrato y mismos datos (<see cref="ProceduralRig"/>): <c>AttackReleased</c> sale
    /// exactamente al acabar la carga, <c>AttackFinished</c> al volver al reposo. Cambiar un prefab
    /// de motor es cambiar este componente.
    ///
    /// Lo que DOTween da de más, y aquí se usa:
    /// <list type="bullet">
    /// <item><b>Curvas como dato</b>: cada tramo tiene su <see cref="Ease"/> en el Inspector
    /// (latigazo con rebote, vuelta al reposo elástica…) en vez de una fórmula en el código.</item>
    /// <item><b>Acción solapada</b>: en un gesto con nombre cada pieza de la pose arranca un poco
    /// después que la anterior (<see cref="poseStagger"/>) y con su propio rebote, así el brazo
    /// llega antes que la mano. Con un solo peso por gesto eso no se puede hacer.</item>
    /// <item><b>Punch y shake</b>: el golpe recibido, el temblor de la carga y el estirón al soltar
    /// son <c>DOTween.Punch</c> / <c>Shake</c>, con vibración y elasticidad ajustables.</item>
    /// <item><b>Despertar elástico</b>: el cuello se despliega con la curva que se elija.</item>
    /// </list>
    ///
    /// Pausa: las secuencias llevan este componente como target y se pausan/reanudan con
    /// <c>GameStateManager.CanPlayerAct</c>, igual que las corrutinas del otro motor.
    /// </summary>
    [DisallowMultipleComponent]
    public class RigDriverTween : RigDriverCode
    {
        [Header("Ataque propio (apuntar · coz)")]
        [Tooltip("Curva con la que el brazo gira hacia el objetivo.")]
        public Ease aimEase = Ease.OutCubic;

        [Tooltip("Curva de la coz al subir. OutBack = se pasa un poco y vuelve (seca).")]
        public Ease recoilEase = Ease.OutBack;

        [Tooltip("Curva de la vuelta al reposo tras la coz. OutElastic = rebota como un muelle.")]
        public Ease settleEase = Ease.OutElastic;

        [Tooltip("El cuerpo se encoge durante la carga (anticipación), en fracción (0.06 = 6%). 0 = nada.")]
        [Range(0f, 0.3f)] public float chargeSquash = 0.05f;

        [Tooltip("Estirón del cuerpo al soltar el golpe (0.1 = 10%), que rebota hasta el reposo. 0 = nada.")]
        [Range(0f, 0.4f)] public float releasePunch = 0.1f;

        [Tooltip("Temblor del arma (grados) en el último 30% de la carga.")]
        [Min(0f)] public float chargeShake = 1.5f;

        [Header("Gestos con nombre")]
        [Tooltip("Curva de la carga (llega a la pose de 'windup').")]
        public Ease gestureWindupEase = Ease.InOutCubic;

        [Tooltip("Curva del latigazo (pose de 'strike'). OutBack = se pasa de la pose y vuelve.")]
        public Ease gestureStrikeEase = Ease.OutBack;

        [Tooltip("Cuánto se pasa el latigazo (sólo con curvas Back). 1.7 = estándar.")]
        [Min(0f)] public float strikeOvershoot = 1.7f;

        [Tooltip("Curva de la vuelta al reposo. OutElastic = las piezas rebotan al asentarse; " +
                 "OutCubic = vuelta limpia (cuerpos pesados).")]
        public Ease gestureRecoverEase = Ease.OutElastic;

        [Tooltip("Amplitud y periodo del rebote elástico de la vuelta al reposo (sólo curvas Elastic).")]
        public Vector2 recoverElastic = new Vector2(1f, 0.55f);

        [Tooltip("Segundos de retraso entre una pieza de la pose y la siguiente (acción solapada). " +
                 "0 = todas a la vez, como el motor de código.")]
        [Min(0f)] public float poseStagger = 0.035f;

        [Header("Golpe recibido (punch)")]
        [Tooltip("Vaivenes del tambaleo.")]
        [Min(1)] public int hurtVibrato = 7;

        [Tooltip("0 = sólo va hacia un lado y vuelve; 1 = oscila entero a los dos lados.")]
        [Range(0f, 1f)] public float hurtElasticity = 0.7f;

        [Header("Despertar / dormir")]
        [Tooltip("Curva del despliegue. OutBack usa 'wakeOvershoot' del rig; OutElastic rebota más.")]
        public Ease wakeEase = Ease.OutBack;

        public Ease sleepEase = Ease.InBack;

        // ---- capas propias (se suman a las del motor base)
        private float _pull, _recoil;          // anticipación y coz del brazo
        private Vector3 _armShake;             // temblor de carga (x)
        private Vector3 _hurt;                 // x = tambaleo (grados), y = aplastamiento
        private Vector3 _punch;                // x = estirón al soltar
        private float _squash;                 // encogerse durante la carga
        private Vector3 _gestureShakeAngle;    // z = temblor del gesto (grados)

        // Un peso por pieza de la pose (no uno por gesto): es lo que permite solapar la acción.
        private readonly List<float> _windupWeights = new List<float>();
        private readonly List<float> _strikeWeights = new List<float>();

        private Sequence _attackSeq, _gestureSeq, _foldSeq;
        private Tween _hurtTween;
        private bool _paused;

        private static bool _capacitySet;

        protected override float ExtraTilt => _hurt.x;
        protected override float ExtraSquash => _hurt.y + _squash - _punch.x;

        protected override void Awake()
        {
            base.Awake();

            // Varios enemigos atacando a la vez superan la reserva por defecto de DOTween (200/50).
            if (_capacitySet) return;
            _capacitySet = true;
            DOTween.SetTweensCapacity(800, 200);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResetLayers();
            _paused = false;
        }

        protected override void OnDisable()
        {
            KillAll();
            ResetLayers();
            base.OnDisable();
        }

        private void ResetLayers()
        {
            _pull = _recoil = _squash = 0f;
            _armShake = _hurt = _punch = _gestureShakeAngle = Vector3.zero;
            _windupWeights.Clear();
            _strikeWeights.Clear();
        }

        private void KillAll()
        {
            Kill(ref _attackSeq);
            Kill(ref _gestureSeq);
            Kill(ref _foldSeq);
            if (_hurtTween != null && _hurtTween.IsActive()) _hurtTween.Kill();
            _hurtTween = null;
        }

        private static void Kill(ref Sequence sequence)
        {
            if (sequence != null && sequence.IsActive()) sequence.Kill();
            sequence = null;
        }

        /// <summary>Los gestos sólo avanzan cuando el jugador puede actuar (misma regla que el motor de código).</summary>
        private void Update()
        {
            bool paused = Paused;
            if (paused == _paused) return;

            _paused = paused;
            if (paused) DOTween.Pause(this);
            else DOTween.Play(this);
        }

        private Sequence NewSequence()
        {
            var sequence = DOTween.Sequence().SetTarget(this).SetAutoKill(true);
            if (_paused) sequence.Pause();
            return sequence;
        }

        // ============================================================ ataque propio

        public override void PlayAttack(Vector2? aimPoint, float speed)
        {
            if (_dead) return;
            speed = Mathf.Max(0.01f, speed);

            Kill(ref _attackSeq);
            var rig = Rig;
            _aimPoint = aimPoint;
            rig.ShowSprites(RigSpriteState.Attacking);   // aviso: ojos de disparo desde la carga

            float windup = Mathf.Max(0.01f, rig.windup / speed);
            float hold = rig.recoilDelay / speed;
            float kick = Mathf.Max(0.01f, rig.recoilKick / speed);
            float recover = Mathf.Max(rig.recover / speed, hold + kick + 0.01f);
            float settle = recover - hold - kick;

            var s = NewSequence();
            _attackSeq = s;

            // --- carga: apunta, baja el cañón y lo sube (anticipación), el cuerpo se echa atrás y se
            // encoge, y al final tiembla. En el último 15% el cañón está ya sobre el objetivo.
            s.Insert(0f, Value(_aimWeight, 1f, windup * 0.6f, v => _aimWeight = v).SetEase(aimEase));
            s.Insert(0f, Value(0f, -rig.windupPullAngle, windup * 0.425f, v => _pull = v).SetEase(Ease.OutSine));
            s.Insert(windup * 0.425f, Value(-rig.windupPullAngle, 0f, windup * 0.425f, v => _pull = v).SetEase(Ease.InSine));
            s.Insert(0f, Value(_bodyTilt, rig.windupBodyTilt, windup, v => _bodyTilt = v).SetEase(Ease.InOutSine));
            if (chargeSquash > 0f)
                s.Insert(windup * 0.3f, Value(0f, chargeSquash, windup * 0.7f, v => _squash = v).SetEase(Ease.InQuad));
            if (chargeShake > 0f)
                s.Insert(windup * 0.7f, DOTween.Shake(() => _armShake, v => _armShake = v, windup * 0.3f,
                                                      new Vector3(chargeShake, 0f, 0f), 40, 0f, false));

            // --- suelta: en este instante exacto, con el brazo quieto y apuntando.
            s.InsertCallback(windup, () =>
            {
                _aimWeight = 1f;
                _pull = 0f;
                _armShake = Vector3.zero;
                _squash = 0f;
                RaiseReleased();
            });

            // --- estirón del cuerpo al soltar (rebota hasta el reposo) y la coz del arma.
            if (releasePunch > 0f)
                s.Insert(windup, DOTween.Punch(() => _punch, v => _punch = v, new Vector3(releasePunch, 0f, 0f),
                                               Mathf.Max(0.1f, recover * 0.7f), 6, 0.5f));
            s.Insert(windup + hold, Value(0f, 1f, kick, v => _recoil = v).SetEase(recoilEase));
            s.Insert(windup + hold + kick, Value(1f, 0f, settle, v => _recoil = v).SetEase(settleEase));
            s.Insert(windup, Value(rig.windupBodyTilt, 0f, recover, v => _bodyTilt = v).SetEase(Ease.OutCubic));
            s.Insert(windup + recover * 0.5f, Value(1f, 0f, recover * 0.5f, v => _aimWeight = v).SetEase(Ease.InOutSine));

            s.OnUpdate(WriteArm);
            s.OnComplete(() =>
            {
                _attackSeq = null;
                EndAttackLayers();
                rig.ShowSprites(RigSpriteState.Awake);
                RaiseFinished();
            });
        }

        /// <summary>
        /// Un número de <paramref name="from"/> a <paramref name="to"/>. Siempre con el origen explícito:
        /// dentro de una secuencia hay tramos encadenados sobre el mismo número, y así ninguno depende
        /// de cuándo decide DOTween leer el valor de partida.
        /// </summary>
        private static Tweener Value(float from, float to, float seconds, TweenCallback<float> set) =>
            DOVirtual.Float(from, to, Mathf.Max(0.001f, seconds), set);

        /// <summary>Las capas propias del brazo, a las tres que lee la composición del motor base.</summary>
        private void WriteArm()
        {
            var rig = Rig;
            _armRotation = _pull + _armShake.x + rig.recoilAngle * _recoil;
            _armPush = rig.recoilDistance * _recoil;
            _bodyKick = rig.bodyRecoil * _recoil;
        }

        private void EndAttackLayers()
        {
            _pull = _recoil = _squash = 0f;
            _armShake = _punch = Vector3.zero;
            _aimWeight = _armRotation = _armPush = _bodyTilt = _bodyKick = 0f;
            _aimPoint = null;
        }

        // ============================================================ golpe recibido

        public override void PlayHurt(float strength)
        {
            if (_dead) return;

            bool running = _hurtTween != null && _hurtTween.IsActive();
            // Un golpe flojo no pisa un tambaleo fuerte que aún está sonando.
            if (running && strength < _hurtStrength) return;

            if (_hurtTween != null && _hurtTween.IsActive()) _hurtTween.Kill();
            _hurtStrength = Mathf.Max(0.01f, strength);

            var rig = Rig;
            _hurt = Vector3.zero;
            var punch = new Vector3(rig.hurtTilt * _hurtStrength, rig.hurtSquash * Mathf.Min(_hurtStrength, 2f), 0f);
            _hurtTween = DOTween.Punch(() => _hurt, v => _hurt = v, punch,
                                       rig.hurtDuration * Mathf.Sqrt(_hurtStrength) * 1.6f, hurtVibrato, hurtElasticity)
                .SetTarget(this)
                .OnComplete(() => { _hurt = Vector3.zero; _hurtTween = null; });
            if (_paused) _hurtTween.Pause();
        }

        // ============================================================ gestos con nombre

        public override bool PlayGesture(string gestureName, float secondsToRelease)
        {
            if (_dead) return false;
            var gesture = Rig.FindGesture(gestureName);
            if (gesture == null) return false;
            if (!isActiveAndEnabled) return true;

            BeginGesture(gesture);
            float windup = Mathf.Max(0.01f, secondsToRelease);
            var s = NewSequence();
            _gestureSeq = s;

            // --- carga: cada pieza arranca un poco después que la anterior, pero todas llegan a la
            // vez, justo al soltar (el aviso sigue durando exactamente 'secondsToRelease').
            float maxDelay = windup * 0.4f;
            for (int i = 0; i < _windupWeights.Count; i++)
            {
                float delay = Mathf.Min(i * poseStagger, maxDelay);
                s.Insert(delay, Weight(_windupWeights, i, _windupWeights[i], 1f, windup - delay).SetEase(gestureWindupEase));
            }

            for (int i = 0; i < _strikeWeights.Count; i++)
                if (_strikeWeights[i] != 0f) s.Insert(0f, Weight(_strikeWeights, i, _strikeWeights[i], 0f, windup * 0.5f).SetEase(Ease.OutSine));

            if (gesture.shake > 0f)
                s.Insert(windup * 0.7f, DOTween.Shake(() => _gestureShakeAngle, v => _gestureShakeAngle = v, windup * 0.3f,
                                                      new Vector3(0f, 0f, gesture.shake), 40, 0f, false));

            s.InsertCallback(windup, () =>
            {
                for (int i = 0; i < _windupWeights.Count; i++) _windupWeights[i] = 1f;
                _gestureShakeAngle = Vector3.zero;
                RaiseReleased();
            });

            // --- sin pose de golpe (una carga que se mantiene): se deshace despacio, sin latigazo.
            bool hasStrike = gesture.strike != null && gesture.strike.Count > 0;
            float settleAt = windup;
            if (hasStrike)
            {
                float strikeTime = Mathf.Max(0.01f, gesture.strikeTime);
                float lag = 0f;
                for (int i = 0; i < _strikeWeights.Count; i++)
                {
                    float delay = Mathf.Min(i * poseStagger, strikeTime);
                    lag = Mathf.Max(lag, delay);
                    s.Insert(windup + delay, Weight(_strikeWeights, i, 0f, 1f, strikeTime).SetEase(gestureStrikeEase, strikeOvershoot));
                }

                for (int i = 0; i < _windupWeights.Count; i++)
                    s.Insert(windup, Weight(_windupWeights, i, 1f, 0f, strikeTime).SetEase(Ease.OutCubic));

                settleAt = windup + strikeTime + lag;
            }

            // Tras el latigazo: la carga ya está a 0 y el golpe a 1. Sin latigazo: la carga sigue a 1.
            InsertSettle(s, settleAt, Mathf.Max(0.01f, gesture.recover), gestureRecoverEase, true,
                         hasStrike ? 0f : 1f, hasStrike ? 1f : 0f);
            s.OnComplete(() =>
            {
                _gestureSeq = null;
                EndGesture();
                RaiseFinished();
            });
            return true;
        }

        public override void SetMoving(bool moving)
        {
            if (_dead) return;
            var move = Rig.FindGesture(Rig.moveGesture);
            if (move == null) return;

            if (moving && (_gesture == null || _gesture == move)) Hold(move);
            else if (!moving && _gesture == move) Settle(0.3f);
        }

        public override bool HoldGesture(string gestureName)
        {
            if (_dead) return false;
            var gesture = Rig.FindGesture(gestureName);
            if (gesture == null) return false;

            Hold(gesture);
            return true;
        }

        public override void ClearHold()
        {
            if (_dead || _gesture == null) return;
            Settle(0.25f);
        }

        /// <summary>Llega a la pose de carga y se queda ahí hasta <see cref="ClearHold"/>.</summary>
        private void Hold(RigGesture gesture)
        {
            if (!isActiveAndEnabled) return;

            BeginGesture(gesture);
            var s = NewSequence();
            _gestureSeq = s;
            for (int i = 0; i < _windupWeights.Count; i++)
                s.Insert(0f, Weight(_windupWeights, i, _windupWeights[i], 1f, GestureCrossfade).SetEase(Ease.InOutSine));
            for (int i = 0; i < _strikeWeights.Count; i++)
                s.Insert(0f, Weight(_strikeWeights, i, _strikeWeights[i], 0f, GestureCrossfade).SetEase(Ease.InOutSine));
            s.OnComplete(() => _gestureSeq = null);
        }

        /// <summary>De los pesos actuales a cero, y el gesto termina.</summary>
        private void Settle(float seconds)
        {
            if (!isActiveAndEnabled) return;

            Kill(ref _gestureSeq);
            _gestureShakeAngle = Vector3.zero;
            var s = NewSequence();
            _gestureSeq = s;
            InsertSettle(s, 0f, seconds, Ease.InOutSine, false);   // desde donde esté cada pieza
            s.OnComplete(() =>
            {
                _gestureSeq = null;
                EndGesture();
            });
        }

        /// <param name="windupFrom">Peso de la carga al empezar el tramo; negativo = el que tenga ahora cada pieza.</param>
        /// <param name="strikeFrom">Lo mismo para el golpe.</param>
        private void InsertSettle(Sequence s, float at, float seconds, Ease ease, bool staggered,
                                  float windupFrom = -1f, float strikeFrom = -1f)
        {
            float maxDelay = seconds * 0.3f;
            for (int i = 0; i < _windupWeights.Count; i++)
            {
                float from = windupFrom >= 0f ? windupFrom : _windupWeights[i];
                if (from != 0f) s.Insert(at, Weight(_windupWeights, i, from, 0f, seconds).SetEase(Ease.OutCubic));
            }

            for (int i = 0; i < _strikeWeights.Count; i++)
            {
                float from = strikeFrom >= 0f ? strikeFrom : _strikeWeights[i];
                if (from == 0f) continue;

                float delay = staggered ? Mathf.Min(i * poseStagger, maxDelay) : 0f;
                var tween = Weight(_strikeWeights, i, from, 0f, seconds - delay);
                if (ease == Ease.OutElastic || ease == Ease.InElastic || ease == Ease.InOutElastic)
                    tween.SetEase(ease, recoverElastic.x, recoverElastic.y);
                else tween.SetEase(ease);
                s.Insert(at + delay, tween);
            }
        }

        /// <summary>
        /// Arranca <paramref name="gesture"/>: otro gesto distinto pasa a la capa de fundido con el peso
        /// que llevaba; el mismo sigue desde donde estaba.
        /// </summary>
        private void BeginGesture(RigGesture gesture)
        {
            Kill(ref _gestureSeq);
            _gestureShakeAngle = Vector3.zero;

            if (_gesture != gesture)
            {
                float windup = Max(_windupWeights), strike = Max(_strikeWeights);
                if (_gesture != null && (windup > 0.001f || strike > 0.001f))
                {
                    _fadeGesture = _gesture;
                    _fadeWindup = windup;
                    _fadeStrike = strike;
                    _fadeTime = 0f;
                }

                Resize(_windupWeights, gesture.windup != null ? gesture.windup.Count : 0);
                Resize(_strikeWeights, gesture.strike != null ? gesture.strike.Count : 0);
            }

            _gesture = gesture;
        }

        private void EndGesture()
        {
            _gesture = null;
            _gestureShakeAngle = Vector3.zero;
            _windupWeights.Clear();
            _strikeWeights.Clear();
        }

        private static void Resize(List<float> weights, int count)
        {
            weights.Clear();
            for (int i = 0; i < count; i++) weights.Add(0f);
        }

        private static float Max(List<float> weights)
        {
            float max = 0f;
            foreach (float w in weights) max = Mathf.Max(max, Mathf.Clamp01(w));
            return max;
        }

        private static Tweener Weight(List<float> weights, int index, float from, float to, float seconds) =>
            Value(from, to, seconds, v => { if (index < weights.Count) weights[index] = v; });

        /// <summary>Cada pieza de la pose con su propio peso (y el temblor de la carga).</summary>
        protected override void ApplyGesture()
        {
            ApplyFadeGesture();

            var gesture = _gesture;
            if (gesture == null) return;

            for (int i = 0; i < _windupWeights.Count && i < gesture.windup.Count; i++) ApplyPose(gesture.windup[i], _windupWeights[i]);
            for (int i = 0; i < _strikeWeights.Count && i < gesture.strike.Count; i++) ApplyPose(gesture.strike[i], _strikeWeights[i]);

            var body = Rig.body;
            if (_gestureShakeAngle.z != 0f && body != null)
                body.localRotation *= Quaternion.Euler(0f, 0f, _gestureShakeAngle.z);
        }

        // ============================================================ plegar / desplegar

        public override void PlayWake(float speed)
        {
            if (_dead) return;
            var rig = Rig;
            if (!rig.CanFold)
            {
                rig.ShowSprites(RigSpriteState.Awake);
                RaiseWakeFinished();
                return;
            }

            Kill(ref _foldSeq);
            float duration = Mathf.Max(0.01f, rig.wakeDuration / Mathf.Max(0.01f, speed));
            float flickerFrom = 1f - rig.wakeFlickerPortion;

            var unfold = Value(_unfold, 1f, duration, v => _unfold = v);
            if (wakeEase == Ease.OutBack || wakeEase == Ease.InOutBack) unfold.SetEase(wakeEase, rig.wakeOvershoot);
            else unfold.SetEase(wakeEase);

            var s = NewSequence();
            _foldSeq = s;
            s.Append(unfold);
            s.OnUpdate(() =>
            {
                float k = s.Elapsed() / duration;
                if (k < flickerFrom) return;

                // 0 parpadeos = los ojos se abren una vez, limpio, al entrar en este tramo.
                float f = (k - flickerFrom) / Mathf.Max(0.0001f, rig.wakeFlickerPortion);
                bool on = rig.wakeFlickers <= 0 || (int)(f * rig.wakeFlickers * 2f) % 2 == 1;
                rig.ShowSprites(on ? RigSpriteState.Awake : RigSpriteState.Asleep);
            });
            s.OnComplete(() =>
            {
                _foldSeq = null;
                _unfold = 1f;
                rig.ShowSprites(RigSpriteState.Awake);
                RaiseWakeFinished();
            });
        }

        public override void PlaySleep(bool instant)
        {
            if (_dead) return;

            Kill(ref _attackSeq);
            EndAttackLayers();
            Kill(ref _foldSeq);

            var rig = Rig;
            rig.ShowSprites(RigSpriteState.Asleep);   // los ojos se apagan antes de plegarse
            if (instant || !rig.CanFold)
            {
                _unfold = 0f;
                return;
            }

            var fold = Value(_unfold, 0f, Mathf.Max(0.01f, rig.sleepDuration), v => _unfold = v);
            if (sleepEase == Ease.InBack || sleepEase == Ease.InOutBack) fold.SetEase(sleepEase, rig.wakeOvershoot * 0.5f);
            else fold.SetEase(sleepEase);

            var s = NewSequence();
            _foldSeq = s;
            s.Append(fold);
            s.OnComplete(() => { _foldSeq = null; _unfold = 0f; });
        }

        // ============================================================ muerte

        public override void PlayDeath()
        {
            if (_dead) return;

            KillAll();
            ResetLayers();
            base.PlayDeath();   // se desmonta: la física de las piezas es la del motor base
        }
    }
}
