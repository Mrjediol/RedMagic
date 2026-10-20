namespace RedMagic.Combat
{
    /// <summary>
    /// Multiplicador de <b>todo</b> el daño que hace un enemigo o un jefe (golpe, proyectil, contacto), puesto desde fuera
    /// en tiempo de juego: el escalado por oleada del modo Supervivencia, las variantes (<c>EnemyVariant</c>). 1 = sin
    /// cambio. No se serializa: el prefab nunca lo lleva puesto, lo pone quien lo crea.
    /// </summary>
    public interface IOutgoingDamageScale
    {
        float OutgoingDamageScale { get; set; }
    }
}
