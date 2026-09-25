using System;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// "Cuando este disparo impacte por primera vez, avísame con el punto". Lo pide un efecto en
    /// <see cref="WeaponUser.Casting"/> (<see cref="CastArgs.OnFirstImpact"/>) y viaja dentro del
    /// <see cref="ShotContext"/>, así que lo llevan todos los proyectiles de ese disparo —perdigones
    /// e hijos de split incluidos— y el haz. Salta <b>una sola vez</b>, con el primero que llegue:
    /// <list type="bullet">
    /// <item>golpe a un enemigo → centro de su cuerpo;</item>
    /// <item>terreno o fin de vida del proyectil → donde esté el proyectil (la carga no se pierde);</item>
    /// <item>haz → el primer enemigo que toque, o la punta al apagarse.</item>
    /// </list>
    /// </summary>
    public sealed class ShotImpactHook
    {
        private Action<Vector2> _callbacks;

        public bool Fired { get; private set; }

        public void Add(Action<Vector2> callback) => _callbacks += callback;

        public void Fire(Vector2 point)
        {
            if (Fired) return;
            Fired = true;

            var callbacks = _callbacks;
            _callbacks = null;
            callbacks?.Invoke(point);
        }
    }
}
