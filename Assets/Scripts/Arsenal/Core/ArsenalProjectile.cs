using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Proyectil del arsenal que la <b>captura</b> de la Singularidad puede atrapar (regla genérica de la web:
    /// <c>capturable: true</c>, <c>state: 'fly'</c>, respetar <c>captured</c> y, si se quiere Multiplicar, <see cref="CloneForCapture"/>).
    /// Mientras está capturado no se mueve solo: la grieta lo coloca en órbita (<see cref="pos"/>, <see cref="depth"/>) y al
    /// colapsar lo suelta con <see cref="Eject"/>.
    /// </summary>
    public abstract class ArsenalProjectile : ArsenalEntity
    {
        public Vector2 pos, vel;
        /// <summary>Se puede capturar ahora (en vuelo y no es metralla).</summary>
        public virtual bool Capturable => false;
        public bool captured;
        /// <summary>Lo ha soltado una grieta (vida extra para que llegue).</summary>
        public bool ejected;
        /// <summary>Profundidad en la órbita (&lt; 0 = mitad trasera, se dibuja detrás).</summary>
        public float depth;
        /// <summary>Multiplicador de daño (Arsenal de la Singularidad lo sube al soltar).</summary>
        public float dmgMul = 1f;
        /// <summary>Bala enemiga (sólo la captura el Escudo de vacío).</summary>
        public bool hostile;
        /// <summary>Ángulo con el que se dibuja (en vuelo, el de la velocidad; en órbita, tangente a la órbita).</summary>
        public float ang;
        /// <summary>La última grieta que lo capturó (no vuelve a capturarlo la misma).</summary>
        public object lastCapturer;

        /// <summary>La grieta lo coloca en su órbita este frame.</summary>
        public virtual void Orbit(Vector2 p, float angle, float orbitDepth)
        {
            pos = p; ang = angle; depth = orbitDepth;
        }

        /// <summary>Una copia en vuelo (Singularidad · Multiplicar); null = no se puede multiplicar.</summary>
        public virtual ArsenalProjectile CloneForCapture() => null;

        /// <summary>La grieta lo suelta a <paramref name="velocity"/> con ×<paramref name="mul"/> de daño.</summary>
        public virtual void Eject(Vector2 velocity, float mul)
        {
            vel = velocity;
            ang = Mathf.Atan2(velocity.y, velocity.x);
            dmgMul *= mul;
            captured = false;
            ejected = true;
            hostile = false;
        }
    }
}
