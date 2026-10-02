using System;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Quien <b>lanza</b> un <see cref="BossAttack"/>: lo que el ataque necesita de su dueño, sea un
    /// jefe (<see cref="BossController"/>) o un enemigo normal (<c>EnemyAttackHost</c>). Existe para
    /// que los ataques del catálogo sirvan a los dos sin saber cuál tienen delante: un ataque nuevo es
    /// una clase, y la puede usar cualquiera.
    ///
    /// Lo que sólo tiene un jefe (fases, guardia, esbirros, anclas) no está aquí: los ataques que lo
    /// usan se marcan con <see cref="BossOnlyAttribute"/> y leen <see cref="BossContext.Boss"/>.
    /// </summary>
    public interface IAttackHost
    {
        GameObject gameObject { get; }
        Transform transform { get; }

        /// <summary>Punto de apoyo: la X de la raíz a la altura de los pies. Origen de los ataques.</summary>
        Vector2 FeetPosition { get; }

        /// <summary>Altura de los pies respecto a la raíz (≤ 0 con la raíz en el centro del cuerpo).</summary>
        float FeetHeight { get; }

        /// <summary>
        /// De dónde sale un conjuro: la punta del arma del cuerpo (la boca de un rig), si la tiene.
        /// Por defecto, los pies — el origen de siempre de los ataques.
        /// </summary>
        Vector2 CastPoint => FeetPosition;

        /// <summary>El cuerpo que hace los gestos (rig o Animator). Null si no anima.</summary>
        IAttackBody Body { get; }

        /// <summary>Mientras está a true no se gira hacia el objetivo (una embestida que le pasa por encima).</summary>
        bool FacingLocked { get; set; }

        /// <summary>Desplaza el cuerpo: <paramref name="feetPosition"/> es dónde poner los pies, no la raíz.</summary>
        void MoveBody(Vector2 feetPosition);

        // Visuales compartidos por todos los ataques del dueño. Vacío = el placeholder en código.
        GameObject WarnPrefab { get; }
        GameObject WarnCirclePrefab { get; }
        GameObject WarnArrowPrefab { get; }
        GameObject ShockwavePrefab { get; }
        GameObject SweepBeamPrefab { get; }
        GameObject HazardPrefab { get; }
        GameObject PlatformPrefab { get; }
        GameObject AnchorPrefab { get; }
    }

    /// <summary>
    /// El cuerpo de quien ataca, visto por el ataque: reproduce un <b>gesto</b> con nombre y avisa de
    /// cuándo suelta. Lo cumplen <see cref="BossAnimator"/> y <c>EnemyAnimation</c> (rig por partes o
    /// Animator de sprite sheet, da igual).
    /// </summary>
    public interface IAttackBody
    {
        /// <summary>Lanza el gesto para soltar en <paramref name="secondsToRelease"/>. False si no existe.</summary>
        bool PlayGesture(string state, float secondsToRelease);

        /// <summary>
        /// Hacia dónde va el ataque que se va a lanzar (el punto al que apunta). El cuerpo lo usa para
        /// orientar sus efectos de disparo hacia el tiro. Se llama antes de <see cref="PlayGesture"/>.
        /// </summary>
        void AimAt(Vector2 point) { }

        /// <summary>True desde que el gesto en curso ha soltado su golpe (su primera suelta).</summary>
        bool Released { get; }

        /// <summary>
        /// Un gesto puede soltar varias veces (dispara tres veces, lanza y remata): cuántas sueltas
        /// lleva el gesto en curso y cuántas tiene en total. Un cuerpo de un solo golpe: 0/1 → 1/1.
        /// </summary>
        int ReleaseCount => Released ? 1 : 0;
        int ReleaseTotal => 1;

        /// <summary>Segundos que tardará en soltar el último gesto lanzado.</summary>
        float SecondsToRelease { get; }

        /// <summary>Mantiene una pose (el viaje de una embestida) hasta <see cref="ReturnToRest"/>.</summary>
        bool Hold(string state);

        /// <summary>Si hay una pose mantenida, la suelta; si no, nada.</summary>
        void ClearHold();

        /// <summary>Suelta lo que hubiera (gesto o pose) y vuelve al reposo.</summary>
        void ReturnToRest();
    }

    /// <summary>
    /// El ataque necesita algo que sólo tiene un jefe (fases, guardia, esbirros, blancos de arena):
    /// el catálogo lo exporta con <c>hosts: "boss"</c> y la web no lo ofrece a enemigos normales.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class BossOnlyAttribute : Attribute
    {
    }
}
