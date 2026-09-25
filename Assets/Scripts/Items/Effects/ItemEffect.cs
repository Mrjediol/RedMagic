using System;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Un comportamiento de item: lo que hace mientras está equipado. Vive <b>dentro</b> del asset del
    /// item (lista <c>effects</c> con <c>[SerializeReference]</c>), así que sus valores se afinan en el
    /// Inspector del propio item y un item puede combinar varios.
    ///
    /// Un tipo de efecto nuevo es una subclase de ésta (con <c>[Serializable]</c> y, para el
    /// desplegable, <c>[DisplayName]</c>); aparece sola en el Inspector. Un item nuevo con efectos
    /// existentes no necesita código.
    ///
    /// <b>La instancia es dato compartido</b> (el mismo item puede estar en dos slots): el estado de
    /// cada equipado — temporizadores, claves — va en el <see cref="ItemEffectContext"/> que recibe
    /// cada llamada, nunca en campos de la subclase. Lo aplica <see cref="ItemEffectRunner"/>.
    /// </summary>
    [Serializable]
    public abstract class ItemEffect
    {
        /// <summary>Al equiparse el item. Aquí van los cambios que duran mientras esté puesto.</summary>
        public virtual void OnEquip(ItemEffectContext context) { }

        /// <summary>Al quitarse. Tiene que deshacer lo de <see cref="OnEquip"/>.</summary>
        public virtual void OnUnequip(ItemEffectContext context) { }

        /// <summary>Cada frame mientras está equipado (tiempo de juego: en pausa no corre).</summary>
        public virtual void Tick(ItemEffectContext context, float deltaTime) { }

        /// <summary>Una línea para la descripción del item en la UI, con sus valores.</summary>
        public abstract string Summary();
    }

    /// <summary>
    /// Estado de UN efecto de UN item equipado en UN slot. Es también la clave con la que el efecto
    /// registra y retira lo que aporta (p. ej. en <c>PlayerStats</c>).
    /// </summary>
    public sealed class ItemEffectContext
    {
        public ItemDefinition Item { get; }

        /// <summary>Acumulador libre para efectos por intervalo.</summary>
        public float Timer;

        /// <summary>
        /// Estado propio de este equipado para efectos con más que un temporizador (contador de
        /// bajas, "siguiente disparo cargado", los delegados con los que se suscribió a eventos).
        /// Ver <see cref="GetState{T}"/>.
        /// </summary>
        public object State;

        /// <summary>El estado de este equipado, creado la primera vez.</summary>
        public T GetState<T>() where T : class, new() => State as T ?? (T)(State = new T());

        public ItemEffectContext(ItemDefinition item) => Item = item;

        /// <summary>La vida del jugador actual (hub o run), o null si no hay jugador activo.</summary>
        public Health PlayerHealth => ItemEffectRunner.PlayerHealth;

        /// <summary>
        /// Llama a <paramref name="action"/> una vez por cada <paramref name="interval"/> segundos de
        /// juego acumulados. Comodín para "cada segundo, …".
        /// </summary>
        public void Every(float interval, float deltaTime, Action action)
        {
            interval = Mathf.Max(0.05f, interval);
            Timer += deltaTime;
            while (Timer >= interval)
            {
                Timer -= interval;
                action();
            }
        }
    }
}
