using System;
using System.ComponentModel;
using UnityEngine;

namespace RedMagic.Gameplay.Movement
{
    /// <summary>
    /// Flota y se recoloca a <b>media distancia</b> del objetivo: si está demasiado cerca se aparta,
    /// si está demasiado lejos se acerca, y dentro de la franja se queda quieto. No persigue todo el
    /// rato — lo que hace legible a un jefe que se mueve es que entre ataques busca un sitio y se
    /// planta, no que te pise los talones.
    ///
    /// Sólo en horizontal por defecto (el cuerpo mantiene su altura; el flotar se ve en el dibujo).
    /// Con <see cref="followHeight"/> también sube y baja para quedarse a una altura sobre el
    /// objetivo: así sirve tal cual para un enemigo volador.
    /// </summary>
    [Serializable]
    [DisplayName("Flotar a distancia")]
    public class HoverKeepDistance : MovementBehaviour
    {
        [Tooltip("Por debajo de esta distancia horizontal al objetivo, se aparta.")]
        [Min(0f)]
        public float minDistance = 5f;

        [Tooltip("Por encima de esta distancia horizontal al objetivo, se acerca.")]
        [Min(0f)]
        public float maxDistance = 9f;

        [Tooltip("Velocidad máxima, unidades/s.")]
        [Min(0.1f)]
        public float speed = 3.5f;

        [Tooltip("Aceleración, unidades/s². Bajo = arranca y frena con inercia (pesado).")]
        [Min(0.1f)]
        public float acceleration = 10f;

        [Tooltip("Margen para darse por llegado. Evita que tiemble en el sitio.")]
        [Min(0.01f)]
        public float arriveTolerance = 0.35f;

        [Header("Altura (voladores)")]
        [Tooltip("Además de la distancia, se coloca a una altura fija sobre el objetivo.")]
        public bool followHeight;

        [Tooltip("Altura sobre el objetivo cuando 'followHeight' está activo.")]
        public float heightAboveTarget = 2.5f;

        [Min(0f)]
        public float verticalSpeed = 2.5f;

        public override string Summary => $"flota a {minDistance:0.#}–{maxDistance:0.#} u · {speed:0.#} u/s";

        public override Vector2 DesiredVelocity(in MovementFrame frame)
        {
            Vector2 wanted = Vector2.zero;

            if (frame.HasTarget)
            {
                float dx = frame.TargetPosition.x - frame.Position.x;
                float distance = Mathf.Abs(dx);
                float min = Mathf.Min(minDistance, maxDistance);
                float max = Mathf.Max(minDistance, maxDistance);

                // Dentro de la franja no se mueve: se queda donde está.
                float desiredX = frame.Position.x;

                if (distance < min || distance > max)
                {
                    // Apunta un poco hacia dentro de la franja, no a su borde: llegar justo al borde
                    // es volver a salirse en cuanto el objetivo da un paso.
                    float band = max - min;
                    float want = distance < min ? min + band * 0.25f : max - band * 0.25f;
                    int side = dx >= 0f ? 1 : -1;
                    desiredX = frame.TargetPosition.x - side * want;
                }

                desiredX = frame.ClampX(desiredX);

                float delta = desiredX - frame.Position.x;
                if (Mathf.Abs(delta) > arriveTolerance)
                {
                    // Frena al acercarse al sitio (último metro), así no se pasa y vuelve.
                    wanted.x = Mathf.Sign(delta) * speed * Mathf.Clamp01(Mathf.Abs(delta));
                }

                if (followHeight)
                {
                    float dy = frame.TargetPosition.y + heightAboveTarget - frame.Position.y;
                    wanted.y = Mathf.Abs(dy) > arriveTolerance
                        ? Mathf.Clamp(dy * 2f, -verticalSpeed, verticalSpeed)
                        : 0f;
                }
            }

            return Vector2.MoveTowards(frame.Velocity, wanted, acceleration * frame.DeltaTime);
        }
    }
}
