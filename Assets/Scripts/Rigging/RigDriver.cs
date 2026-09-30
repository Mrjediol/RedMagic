using System;
using RedMagic.Abilities;
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

        protected virtual void OnDisable() => Rig.RestorePose();

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

        public abstract void PlayHurt();

        public abstract void PlayDeath();

        /// <summary>Despertar. Por defecto termina en el acto: un rig sin gesto de despertar no se queda dormido.</summary>
        public virtual void PlayWake(float speed) => RaiseWakeFinished();

        /// <summary>
        /// Volver a dormir (plegarse). <paramref name="instant"/> = pose de dormido ya, sin gesto
        /// (nacer dormido). Por defecto sólo cambia los sprites.
        /// </summary>
        public virtual void PlaySleep(bool instant) => Rig.ShowSprites(RigSpriteState.Asleep);

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
        protected void RaiseFinished() => AttackFinished?.Invoke();
        protected void RaiseWakeFinished() => WakeFinished?.Invoke();
    }
}
