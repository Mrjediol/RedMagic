using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Lo que ve un efecto de item en <see cref="WeaponUser.Casting"/>, justo antes de que salga un
    /// disparo. <see cref="DamageScale"/> es lo único que se puede cambiar: multiplica el daño de
    /// todo lo que salga de ese disparo (proyectiles, splits, explosión de impacto, haz).
    /// </summary>
    public sealed class CastArgs
    {
        public WeaponUser User;

        /// <summary>-1 izquierda, 1 derecha.</summary>
        public int Facing;

        /// <summary>Centro del cuerpo del jugador en el instante del gesto.</summary>
        public Vector2 Origin;

        /// <summary>Multiplicador de daño del disparo. Arranca en 1; los efectos lo multiplican.</summary>
        public float DamageScale = 1f;

        /// <summary>Todos los proyectiles de este disparo salen dorados (Guanteletes de oro, ver <see cref="GoldMark"/>).</summary>
        public bool ForceGilded;

        /// <summary>El aviso de primer impacto de este disparo, si algún efecto lo ha pedido.</summary>
        public ShotImpactHook Impact;

        /// <summary>
        /// Llama a <paramref name="callback"/> con el punto del primer impacto de este disparo (ver
        /// <see cref="ShotImpactHook"/>). Para efectos que ocurren "donde pegue" (explosión del Yelmo).
        /// </summary>
        public void OnFirstImpact(System.Action<Vector2> callback)
        {
            Impact ??= new ShotImpactHook();
            Impact.Add(callback);
        }
    }
}
