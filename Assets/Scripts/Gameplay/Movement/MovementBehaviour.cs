using System;
using UnityEngine;

namespace RedMagic.Gameplay.Movement
{
    /// <summary>
    /// Lo que un <see cref="MovementBehaviour"/> sabe del mundo en un paso de física. Lo rellena
    /// <see cref="Mover"/>; el comportamiento no toca ningún componente, sólo decide una velocidad.
    /// </summary>
    public readonly struct MovementFrame
    {
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly bool HasTarget;
        public readonly Vector2 TargetPosition;
        public readonly bool Bounded;
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float DeltaTime;

        public MovementFrame(Vector2 position, Vector2 velocity, bool hasTarget, Vector2 targetPosition,
                             bool bounded, float minX, float maxX, float deltaTime)
        {
            Position = position;
            Velocity = velocity;
            HasTarget = hasTarget;
            TargetPosition = targetPosition;
            Bounded = bounded;
            MinX = minX;
            MaxX = maxX;
            DeltaTime = deltaTime;
        }

        /// <summary>X recortada a los límites (si los hay).</summary>
        public float ClampX(float x) => Bounded ? Mathf.Clamp(x, MinX, MaxX) : x;
    }

    /// <summary>
    /// Un <b>tipo de movimiento</b>, como dato: una clase C# normal (no un componente) que se elige
    /// de un desplegable en el <see cref="Mover"/> y se afina ahí mismo. Es genérico a propósito:
    /// no sabe si lo lleva un jefe, un enemigo o un aliado — sólo recibe dónde está, dónde está su
    /// objetivo y hasta dónde puede ir, y devuelve una velocidad.
    ///
    /// Un tipo de movimiento nuevo = una subclase nueva. Sale sola en el desplegable
    /// (<see cref="Core.SubclassPickerAttribute"/>) y en el catálogo que se exporta a la web.
    /// </summary>
    [Serializable]
    public abstract class MovementBehaviour
    {
        /// <summary>Velocidad deseada para este paso. Debe ser pura: sin estado entre llamadas salvo lo que la propia subclase guarde.</summary>
        public abstract Vector2 DesiredVelocity(in MovementFrame frame);

        /// <summary>Se llama al reanudar el movimiento (tras una pausa). Para reiniciar estado interno.</summary>
        public virtual void OnResume()
        {
        }

        /// <summary>Resumen de una línea para el Inspector.</summary>
        public virtual string Summary => GetType().Name;
    }
}
