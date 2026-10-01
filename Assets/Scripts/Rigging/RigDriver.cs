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
    /// instante</b> en que el gesto suelta el golpe (fin de la carga), igual que el
    /// <c>AnimationEvent</c> de un clip de sprites; <see cref="AttackFinished"/> al volver al
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

        /// <summary>1 mira a la derecha, -1 a la izquierda.</summary>
        protected int Facing { get; private set; } = 1;

        /// <summary>True mientras los gestos deben quedarse congelados (pausa).</summary>
        protected static bool Paused => !GameStateManager.CanPlayerAct;

        protected virtual void Awake()
        {
            Rig = GetComponent<ProceduralRig>();
            Rig.CaptureRest();
        }

        protected virtual void OnEnable()
        {
            Facing = 1;
            Rig.RestorePose();
        }

        protected virtual void OnDisable()
        {
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

        /// <summary>Aviso del golpe + fogonazo en la punta del arma (común a todos los motores).</summary>
        protected void RaiseReleased()
        {
            var muzzle = Rig.muzzle;
            if (muzzle != null && Rig.muzzleFlashSize > 0f)
            {
                AbilityFx.Flash(Rig.muzzleFlashSprite, muzzle.position, Vector2.one * Rig.muzzleFlashSize,
                                Rig.muzzleFlashColor, Rig.muzzleFlashTime, 0f, 1.6f,
                                Rig.aimPart != null ? Rig.aimPart.gameObject : gameObject);
            }

            AttackReleased?.Invoke();
        }

        /// <summary>El sonido propio de un gesto (si lo tiene y es de este momento), en la posición del cuerpo.</summary>
        protected void PlayGestureSound(RigGesture gesture, GestureSoundMoment moment)
        {
            if (gesture == null || gesture.soundMoment != moment || gesture.sound == null) return;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(gesture.sound, transform.position);
        }

        protected void RaiseFinished() => AttackFinished?.Invoke();
        protected void RaiseWakeFinished() => WakeFinished?.Invoke();
    }
}
