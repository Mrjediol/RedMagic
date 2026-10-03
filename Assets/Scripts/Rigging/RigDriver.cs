using System;
using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Rigging
{
    /// <summary>
    /// El <b>motor</b> que anima un <see cref="ProceduralRig"/>. Hay uno por técnica (código puro,
    /// DOTween…) y todos cumplen el mismo contrato, así que quien los usa
    /// (<c>EnemyAnimation</c>, el animador de un jefe) no sabe cuál tiene delante.
    ///
    /// El contrato importante son los eventos: <see cref="AttackReleased"/> sale <b>en el
    /// instante</b> de cada suelta del gesto (<see cref="RigRelease"/>: una o varias por gesto), igual
    /// que el <c>AnimationEvent</c> de un clip de sprites; <see cref="AttackFinished"/> al volver al
    /// reposo; <see cref="WakeFinished"/> al acabar de despertar.
    ///
    /// Pausa: los gestos sólo avanzan con <see cref="GameStateManager.CanPlayerAct"/> y con tiempo
    /// escalado, igual que el cerebro del enemigo — un disparo no puede salir con el juego parado.
    /// </summary>
    [RequireComponent(typeof(ProceduralRig))]
    public abstract class RigDriver : MonoBehaviour
    {
        public event Action AttackReleased;
        public event Action AttackFinished;
        public event Action WakeFinished;

        protected ProceduralRig Rig { get; private set; }

        /// <summary>
        /// El punto al que va el ataque en curso (lo dice quien lo lanza). Los efectos de la boca que
        /// miran "hacia la acción" apuntan ahí — por donde sale el tiro —, no hacia donde esté girada
        /// el arma en ese instante del gesto. Null = sin objetivo: hacia donde apunta el arma.
        /// </summary>
        public Vector2? AimTarget { get; set; }

        /// <summary>Sueltas que ha dado ya el gesto en curso, y cuántas tiene en total.</summary>
        public int ReleasesFired { get; protected set; }
        public int ReleaseTotal { get; protected set; }

        /// <summary>Segundos que dura ese gesto entero (pasos + vuelta), lanzado con ese aviso. -1 = no existe.</summary>
        public float GestureSeconds(string gestureName, float lead)
        {
            var gesture = GetComponent<ProceduralRig>().FindGesture(gestureName);
            if (gesture == null) return -1f;

            gesture.Durations(lead, TotalScratch);
            float total = gesture.recover;
            foreach (float d in TotalScratch) total += d;
            return total;
        }

        private static readonly List<float> TotalScratch = new List<float>();

        /// <summary>Segundos desde que arranca ese gesto hasta su primera suelta, lanzado con ese aviso. -1 = no existe.</summary>
        public float SecondsToRelease(string gestureName, float lead)
        {
            var gesture = GetComponent<ProceduralRig>().FindGesture(gestureName);
            return gesture != null ? gesture.SecondsToFirstRelease(lead) : -1f;
        }

        /// <summary>1 mira a la derecha, -1 a la izquierda.</summary>
        protected int Facing { get; private set; } = 1;

        /// <summary>True mientras los gestos deben quedarse congelados (pausa).</summary>
        protected static bool Paused => !GameStateManager.CanPlayerAct;

        protected virtual void Awake()
        {
            Rig = GetComponent<ProceduralRig>();
            Rig.FixConvertedWindows();
            Rig.CaptureRest();
            CacheFxContext();
        }

        protected virtual void OnEnable()
        {
            Facing = 1;
            Alert = false;
            AimTarget = null;
            Rig.RestorePose();
        }

        protected virtual void OnDisable()
        {
            StopFx();
            Driven.Clear();
            Rig.RestorePose();
        }

        /// <summary>
        /// Rearma las piezas que la muerte desmontó (reutilización por pool). Se llama desde el
        /// primer frame vivo del motor, nunca desde OnEnable/OnDisable, donde Unity no deja
        /// tocar la jerarquía.
        /// </summary>
        protected void RepairHierarchy()
        {
            if (Rig.HierarchyChanged) Rig.RestorePose(true);
        }

        // ============================================================ órdenes

        /// <summary>Se desplaza por su cuenta. La inclinación sale de la velocidad real; esto es opcional.</summary>
        public virtual void SetMoving(bool moving) { }

        /// <summary>
        /// Voltea la raíz visual. Se aplica <b>en el acto</b> (no en el siguiente frame): el
        /// cerebro gira al enemigo justo antes de lanzar el ataque, y el apuntado se calcula con
        /// la matriz ya volteada.
        /// </summary>
        public virtual void SetFacing(int facing)
        {
            Facing = facing < 0 ? -1 : 1;

            var body = Rig.body;
            if (body == null) return;

            var scale = body.localScale;
            scale.x = Mathf.Abs(scale.x) * Facing;
            body.localScale = scale;
        }

        /// <summary>
        /// Carga → suelta (evento) → recupera → fin (evento). <paramref name="aimPoint"/> es el
        /// punto del mundo congelado por el cerebro; null = dispara hacia delante.
        /// </summary>
        public abstract void PlayAttack(Vector2? aimPoint, float speed);

        /// <summary>Golpe recibido. <paramref name="strength"/> 1 = normal; el tambaleo de un jefe pasa más.</summary>
        public abstract void PlayHurt(float strength);

        public void PlayHurt() => PlayHurt(1f);

        /// <summary>
        /// Ha detectado al objetivo (o lo ha perdido): los <see cref="RigSpriteSwap"/> con sprite de
        /// alerta lo enseñan mientras dure. Sólo sprites: no hay gesto.
        /// </summary>
        public void SetAlert(bool alert)
        {
            Alert = alert;
            Rig.SetAlertSprites(alert);
        }

        /// <summary>True mientras quien lo maneja dice que tiene objetivo (ver <see cref="SetAlert"/>).</summary>
        protected bool Alert { get; private set; }

        /// <summary>Etapa visual (fase de un jefe): cambia los sprites por etapa. Sin animación: la tapa el tambaleo.</summary>
        public void SetStage(int stage) => Rig.SetStage(stage);

        /// <summary>Tambaleo fuerte (cambio de fase de un jefe): <see cref="ProceduralRig.staggerStrength"/>.</summary>
        public void PlayStagger() => PlayHurt(Rig.staggerStrength);

        /// <summary>
        /// Gesto con nombre (<see cref="RigGesture"/>): carga durante <paramref name="secondsToRelease"/>,
        /// avisa <see cref="AttackReleased"/> y vuelve al reposo. False si el rig no tiene ese gesto
        /// (quien llama cae entonces a su aviso por tiempo).
        /// </summary>
        public virtual bool PlayGesture(string gestureName, float secondsToRelease) => false;

        /// <summary>Mantiene la pose de carga de un gesto hasta <see cref="ClearHold"/> (el viaje de una embestida).</summary>
        public virtual bool HoldGesture(string gestureName) => false;

        /// <summary>
        /// Acaba de terminar un ataque y quedan <paramref name="seconds"/> de enfriamiento: con la
        /// probabilidad de <see cref="ProceduralRig.transition"/> hace uno de sus gestos de transición
        /// que quepa en ese tiempo. False = no hizo ninguno.
        /// </summary>
        public virtual bool PlayTransition(float seconds) => false;

        /// <summary>Suelta un gesto en curso o mantenido y vuelve al reposo, sin avisar de nada.</summary>
        public virtual void ClearHold() { }

        public abstract void PlayDeath();

        /// <summary>Despertar. Por defecto termina en el acto: un rig sin gesto de despertar no se queda dormido.</summary>
        public virtual void PlayWake(float speed) => RaiseWakeFinished();

        /// <summary>
        /// Volver a dormir (plegarse). <paramref name="instant"/> = pose de dormido ya, sin gesto
        /// (nacer dormido). Por defecto sólo cambia los sprites.
        /// </summary>
        public virtual void PlaySleep(bool instant) => Rig.ShowSprites(RigSpriteState.Asleep);

        // ============================================================ piezas manejadas por un ataque

        /// <summary>Estado de una pieza que un ataque ha tomado prestada (un puño lanzado).</summary>
        protected class DrivenPart
        {
            public float idle;
            public bool returning;
            public float returnTime;
            public float returnDuration;
            public Vector3 fromPosition;
            public Quaternion fromRotation;
            public Vector3 fromScale;
        }

        protected readonly Dictionary<Transform, DrivenPart> Driven = new Dictionary<Transform, DrivenPart>();
        private static readonly List<Transform> Finished = new List<Transform>();

        /// <summary>
        /// Un ataque mueve la pieza <b>en el mundo</b> (su pivote a <paramref name="worldPosition"/>,
        /// girada <paramref name="spin"/> grados sobre su reposo). Mientras la mueva, el motor no la
        /// toca. La trayectoria es del ataque, así que es idéntica en todos los motores; sólo la vuelta
        /// al cuerpo es del motor. Si el ataque deja de llamar (lo cortaron), la pieza vuelve sola
        /// tras <see cref="ProceduralRig.drivenLease"/>.
        /// </summary>
        public void DrivePart(Transform part, Vector2 worldPosition, float spin)
        {
            if (part == null) return;
            Rig.TryGetRest(part, out _, out float restAngle, out _);
            DrivePartAngle(part, worldPosition, restAngle + spin);
        }

        /// <summary>Como <see cref="DrivePart"/>, con el giro local absoluto (grados) en vez de relativo al reposo.</summary>
        public void DrivePartAngle(Transform part, Vector2 worldPosition, float localAngle)
        {
            if (part == null) return;
            if (!Driven.TryGetValue(part, out var state))
            {
                state = new DrivenPart();
                Driven[part] = state;
            }

            state.idle = 0f;
            state.returning = false;

            part.localRotation = Quaternion.Euler(0f, 0f, localAngle);
            part.position = new Vector3(worldPosition.x, worldPosition.y, part.position.z);
        }

        /// <summary>Devuelve la pieza al cuerpo en <paramref name="seconds"/> (lo anima el motor).</summary>
        public void ReleasePart(Transform part, float seconds)
        {
            if (part == null || !Driven.TryGetValue(part, out var state) || state.returning) return;

            state.returning = true;
            state.returnTime = 0f;
            state.returnDuration = Mathf.Max(0.01f, seconds);
            state.fromPosition = part.localPosition;
            state.fromRotation = part.localRotation;
            state.fromScale = part.localScale;
        }

        public bool IsDriven(Transform part) => part != null && Driven.ContainsKey(part);

        /// <summary>Avanza el plazo de las piezas prestadas y retira las que ya volvieron. Una vez por frame.</summary>
        protected void TickDriven(float dt)
        {
            if (Driven.Count == 0) return;

            Finished.Clear();
            foreach (var pair in Driven)
            {
                var state = pair.Value;
                if (!state.returning)
                {
                    state.idle += dt;
                    if (state.idle > Rig.drivenLease) ReleasePart(pair.Key, Rig.drivenReturn);
                    continue;
                }

                state.returnTime += dt;
                if (state.returnTime >= state.returnDuration) Finished.Add(pair.Key);
            }

            foreach (var t in Finished) Driven.Remove(t);
        }

        // ============================================================ avisos

        /// <summary>
        /// Aviso del golpe + fogonazo en la punta del arma (común a todos los motores). El fogonazo es
        /// un placeholder: si el gesto que suelta trae sus propios efectos (<see cref="RigGesture.fx"/>),
        /// no sale — el efecto del gesto ES el fogonazo.
        /// </summary>
        protected void RaiseReleased(RigGesture gesture = null)
        {
            var muzzle = Rig.muzzle;
            bool ownFx = false;
            if (gesture?.fx != null)
                foreach (var fx in gesture.fx)
                    if (fx != null && fx.prefab != null) { ownFx = true; break; }

            if (muzzle != null && Rig.muzzleFlashSize > 0f && !ownFx)
            {
                AbilityFx.Flash(Rig.muzzleFlashSprite, muzzle.position, Vector2.one * Rig.muzzleFlashSize,
                                Rig.muzzleFlashColor, Rig.muzzleFlashTime, 0f, 1.6f,
                                Rig.aimPart != null ? Rig.aimPart.gameObject : gameObject);
            }

            AttackReleased?.Invoke();
        }

        /// <summary>Los sonidos propios de un gesto de este momento ('Start' o 'Release'), en la posición del cuerpo.</summary>
        protected void PlayGestureSounds(RigGesture gesture, GestureSoundMoment moment)
        {
            if (gesture?.sounds == null) return;
            foreach (var s in gesture.sounds)
                if (s != null && s.moment == moment) PlayGestureSound(s);
        }

        /// <summary>Un sonido de gesto, en la posición del cuerpo.</summary>
        protected void PlayGestureSound(RigGestureSound s)
        {
            if (s?.sound != null && AudioManager.Instance != null) AudioManager.Instance.Play(s.sound, transform.position);
        }

        // ============================================================ efectos de gesto (RigFx, siempre del pool)

        /// <summary>Un efecto de gesto vivo: se le sigue la pista para moverlo con su ancla y apagarlo.</summary>
        private struct LiveFx
        {
            public GameObject instance;
            public RigFx data;

            /// <summary>Lo apaga el motor (un bucle, o un prefab sin VfxOneShot). Si no, se apaga solo.</summary>
            public bool owned;

            /// <summary>Bucle: vive hasta que acaba el paso <see cref="endStep"/>. Si no, segundos que le quedan.</summary>
            public bool loop;
            public int endStep;
            public float life;
        }

        private readonly List<LiveFx> _liveFx = new List<LiveFx>();
        private static readonly List<SpriteRenderer> FxRenderers = new List<SpriteRenderer>();
        private Collider2D _fxGround;
        private Rigidbody2D _fxBody;
        private int _fxSortingLayer, _fxSortingOrder, _fxBehindOrder;

        /// <summary>Lo que no cambia en la vida del cuerpo: su collider (pies) y el orden de dibujado de sus efectos.</summary>
        private void CacheFxContext()
        {
            _fxGround = GetComponent<Collider2D>();
            _fxBody = GetComponent<Rigidbody2D>();
            _fxSortingOrder = 1;
            _fxBehindOrder = 1;
            if (Rig.body == null) return;

            // Delante de todas las piezas, en su misma capa (0 quedaría detrás del fondo de la escena).
            Rig.body.GetComponentsInChildren(true, FxRenderers);
            int lowest = int.MaxValue;
            foreach (var r in FxRenderers) lowest = Mathf.Min(lowest, r.sortingOrder);

            // Un efecto DETRÁS del cuerpo necesita un hueco por debajo de la pieza más al fondo, y el
            // 0 no vale (queda detrás del fondo): las piezas suben un puesto, en bloque, una sola vez.
            bool behind = false;
            foreach (var g in Rig.gestures)
                if (g?.fx != null)
                    foreach (var fx in g.fx)
                        if (fx != null && fx.layer == RigFxLayer.Behind) behind = true;
            int lift = behind && lowest < 2 ? 2 - lowest : 0;

            foreach (var r in FxRenderers)
            {
                r.sortingOrder += lift;
                _fxSortingLayer = r.sortingLayerID;
                _fxSortingOrder = Mathf.Max(_fxSortingOrder, r.sortingOrder + 1);
            }

            if (FxRenderers.Count > 0) _fxBehindOrder = Mathf.Max(1, lowest + lift - 1);
            FxRenderers.Clear();
        }

        /// <summary>
        /// Lanza los efectos del paso <paramref name="step"/> cuyo momento cae en (<paramref name="from"/>,
        /// <paramref name="to"/>]. El motor la llama cada frame con el avance del paso: la primera vez
        /// con <c>from</c> negativo (así entra el momento 0) y, al acabar, hasta 1. El paso número
        /// "cuántos pasos tiene" es la vuelta al reposo; un efecto puesto más allá sale en ella.
        /// </summary>
        protected void FireFx(RigGesture gesture, int step, float from, float to)
        {
            if (gesture?.fx == null) return;
            int last = gesture.steps != null ? gesture.steps.Count : 0;
            for (int i = 0; i < gesture.fx.Count; i++)
            {
                var fx = gesture.fx[i];
                if (fx == null || fx.prefab == null || Mathf.Min(fx.step, last) != step || fx.at <= from || fx.at > to) continue;
                SpawnFx(fx, last);
            }
        }

        private void SpawnFx(RigFx fx, int lastStep)
        {
            FxPose(fx, out var position, out var rotation, out float mirror);
            var instance = Core.PrefabPool.Spawn(fx.prefab, position, rotation);
            if (instance == null) return;

            // Tamaño: el del prefab × el del efecto × la escala del cuerpo (un enemigo a mitad de tamaño
            // lleva sus efectos a mitad de tamaño). El pool reutiliza instancias: se parte del prefab.
            float size = fx.scale * (Rig.body != null ? Mathf.Abs(Rig.body.lossyScale.y) : 1f);
            var scale = fx.prefab.transform.localScale * Mathf.Max(0.01f, size);
            scale.x = Mathf.Abs(scale.x) * mirror;
            instance.transform.localScale = scale;

            instance.GetComponentsInChildren(true, FxRenderers);
            foreach (var r in FxRenderers)
            {
                r.sortingLayerID = _fxSortingLayer;
                r.sortingOrder = fx.layer == RigFxLayer.Behind ? _fxBehindOrder : _fxSortingOrder;
            }

            FxRenderers.Clear();

            // Un prefab con VfxOneShot se devuelve solo al pool: el motor sólo lo acompaña (y deja de
            // tocarlo un poco antes, para no mover una instancia que el pool ya haya dado a otro).
            bool selfDespawns = instance.TryGetComponent(out Fx.VfxOneShot oneShot);
            if (selfDespawns) oneShot.Restart();

            float duration = 0.5f;
            if (fx.prefab.TryGetComponent(out Gameplay.SpriteFlipbook flipbook) && flipbook.Duration > 0f)
                duration = flipbook.Duration;

            bool loop = fx.loop && !selfDespawns;
            if (!loop && selfDespawns && !fx.followsPart) return;   // suelto y autónomo: nada que seguir

            _liveFx.Add(new LiveFx
            {
                instance = instance,
                data = fx,
                owned = !selfDespawns,
                loop = loop,
                endStep = Mathf.Min(fx.EndStep, lastStep),
                life = selfDespawns ? duration * 0.9f : duration,
            });
        }

        /// <summary>
        /// Dónde va un efecto ahora mismo. El desplazamiento está en el espacio del ancla, así que el
        /// volteo del cuerpo lo refleja solo; <paramref name="mirror"/> (-1) refleja también el dibujo.
        /// </summary>
        private void FxPose(RigFx fx, out Vector3 position, out Quaternion rotation, out float mirror)
        {
            Transform anchor = null;
            switch (fx.anchor)
            {
                case RigFxAnchor.Part: anchor = fx.part; break;
                case RigFxAnchor.Muzzle: anchor = Rig.muzzle != null ? Rig.muzzle : Rig.aimPart; break;
                case RigFxAnchor.Feet:
                    Vector2 feet = _fxGround != null && _fxGround.enabled
                        ? new Vector2(_fxGround.bounds.center.x, _fxGround.bounds.min.y)
                        : (Vector2)transform.position;
                    position = new Vector3(feet.x + fx.offset.x * Facing, feet.y + fx.offset.y, transform.position.z);
                    if (fx.facing == RigFxFacing.Direction) Towards(FxDirection(fx), fx.angle, out rotation, out mirror);
                    else
                    {
                        mirror = Facing;
                        rotation = Quaternion.Euler(0f, 0f, fx.angle * Facing);
                    }
                    return;
            }

            if (anchor == null) anchor = Rig.body != null ? Rig.body : transform;

            position = anchor.TransformPoint(fx.offset);

            if (fx.facing == RigFxFacing.Direction)
            {
                Towards(FxDirection(fx), fx.angle, out rotation, out mirror);
                return;
            }

            if (fx.facing == RigFxFacing.World)
            {
                mirror = Facing;
                rotation = Quaternion.Euler(0f, 0f, fx.angle * Facing);
                return;
            }

            Vector2 right = anchor.TransformVector(Vector3.right);
            Vector2 up = anchor.TransformVector(Vector3.up);
            float angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;

            // Ancla reflejada (cuerpo mirando a la izquierda): el dibujo se refleja en vez de quedar del revés.
            mirror = right.x * up.y - right.y * up.x < 0f ? -1f : 1f;
            rotation = Quaternion.Euler(0f, 0f, (mirror < 0f ? angle + 180f : angle) + fx.angle * mirror);
        }

        /// <summary>
        /// Gira un efecto dibujado mirando a +X para que mire a <paramref name="direction"/>. Hacia la
        /// izquierda se refleja en vez de quedar cabeza abajo.
        /// </summary>
        private static void Towards(Vector2 direction, float extraAngle, out Quaternion rotation, out float mirror)
        {
            mirror = direction.x < 0f ? -1f : 1f;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(0f, 0f, (mirror < 0f ? angle + 180f : angle) + extraAngle * mirror);
        }

        /// <summary>
        /// Hacia dónde va la acción para ese efecto (<see cref="RigFxFacing.Direction"/>): en la boca,
        /// hacia el objetivo del ataque (<see cref="AimTarget"/>: por ahí sale el tiro, esté como esté
        /// girada el arma) y, sin objetivo, hacia donde apunta el arma — del pivote de la pieza que
        /// apunta a su boca —; en lo demás, hacia donde se mueve el cuerpo, o hacia donde mira si está quieto.
        /// </summary>
        protected virtual Vector2 FxDirection(RigFx fx)
        {
            if (fx.anchor == RigFxAnchor.Muzzle && AimTarget.HasValue)
            {
                Vector2 from = Rig.CastPosition;
                Vector2 shot = AimTarget.Value - from;
                if (shot.sqrMagnitude > 0.0001f) return shot.normalized;
            }

            if (fx.anchor == RigFxAnchor.Muzzle && Rig.muzzle != null && Rig.aimPart != null)
            {
                Vector2 barrel = Rig.muzzle.position - Rig.aimPart.position;
                if (barrel.sqrMagnitude > 0.0001f) return barrel.normalized;
            }

            if (_fxBody != null && _fxBody.linearVelocity.sqrMagnitude > 0.04f) return _fxBody.linearVelocity.normalized;
            return new Vector2(Facing, 0f);
        }

        /// <summary>
        /// Una vez por frame, <b>después</b> de componer el cuerpo: los efectos que siguen a su ancla
        /// se recolocan, y los de un solo uso que apaga el motor se devuelven al pool al acabar.
        /// </summary>
        protected void TickFx(float dt)
        {
            for (int i = _liveFx.Count - 1; i >= 0; i--)
            {
                var live = _liveFx[i];
                if (live.instance == null || !live.instance.activeSelf)
                {
                    _liveFx.RemoveAt(i);
                    continue;
                }

                if (!live.loop)
                {
                    live.life -= dt;
                    if (live.life <= 0f)
                    {
                        if (live.owned) Core.PrefabPool.Despawn(live.instance);
                        _liveFx.RemoveAt(i);
                        continue;
                    }

                    _liveFx[i] = live;
                }

                if (!live.data.followsPart) continue;
                FxPose(live.data, out var position, out var rotation, out float mirror);
                var t = live.instance.transform;
                t.SetPositionAndRotation(position, rotation);
                var scale = t.localScale;
                scale.x = Mathf.Abs(scale.x) * mirror;
                t.localScale = scale;
            }
        }

        /// <summary>Fin de un paso: se apagan los efectos en bucle que duraban hasta él.</summary>
        protected void EndFxStep(int step)
        {
            for (int i = _liveFx.Count - 1; i >= 0; i--)
            {
                if (!_liveFx[i].loop || _liveFx[i].endStep > step) continue;
                Core.PrefabPool.Despawn(_liveFx[i].instance);
                _liveFx.RemoveAt(i);
            }
        }

        /// <summary>
        /// El gesto se corta (otro encima, muerte, vuelta al pool): los bucles se apagan ya; los de un
        /// solo uso acaban donde están.
        /// </summary>
        protected void StopFx()
        {
            for (int i = 0; i < _liveFx.Count; i++)
                if (_liveFx[i].owned) Core.PrefabPool.Despawn(_liveFx[i].instance);
            _liveFx.Clear();
        }

        protected void RaiseFinished() => AttackFinished?.Invoke();
        protected void RaiseWakeFinished() => WakeFinished?.Invoke();
    }
}
