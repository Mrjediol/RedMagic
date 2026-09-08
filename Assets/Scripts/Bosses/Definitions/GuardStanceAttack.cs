using System.Collections;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// El jefe <b>se cubre</b>: durante unos segundos apenas recibe daño y devuelve un pellizco a
    /// quien insista en pegarle.
    ///
    /// Es la ventana de castigo del revés (<see cref="BossAttack.VulnerableSeconds"/>), y las dos
    /// juntas son lo que convierte "atacar" en una decisión. Un jefe que sólo se expone se juega
    /// pulsando el botón sin mirar; uno que además se cubre obliga a mirarlo <b>antes</b> de
    /// pegar. El aviso, el aura y la barra de vida dicen las tres cosas a la vez, así que comerse
    /// el rebote siempre es culpa de las manos, no de la información.
    ///
    /// Mientras se cubre no ataca. Eso es intencionado: la tentación de aprovechar "que está
    /// quieto" es justo la trampa, y castigarla enseña la regla en un intento.
    ///
    /// Con <see cref="counterAttack"/> encendido, además, al bajar la guardia se abre la ventana
    /// de castigo del propio ataque (los campos de la clase base): "aguanta sin pegar y te lo
    /// devuelvo con intereses", que es la lección completa.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Guard Stance Attack", fileName = "BossAttack_Guard")]
    public class GuardStanceAttack : BossAttack
    {
        [Header("Guardia")]
        [Tooltip("Segundos que aguanta cubierto.")]
        [Min(0.2f)]
        [SerializeField] private float guardSeconds = 2.6f;

        [Tooltip("Daño que recibe mientras se cubre, como fracción del normal. No se pone a 0 a " +
                 "propósito: el golpe tiene que entrar para poder devolverse, y ver saltar un " +
                 "número ridículo es parte del mensaje.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float damageTakenWhileGuarding = 0.12f;

        [Tooltip("Daño fijo que devuelve por cada golpe recibido. Fijo y no proporcional porque " +
                 "así castiga al que machaca el botón, que es justo el comportamiento a corregir.")]
        [Min(0f)]
        [SerializeField] private float reflectDamage = 9f;

        [Min(0f)]
        [SerializeField] private float reflectKnockback = 1.2f;

        [Header("Aviso")]
        [Tooltip("Cada cuánto repinta la marca de 'cubierto' mientras dura.")]
        [Min(0.05f)]
        [SerializeField] private float pulseInterval = 0.25f;

        [Tooltip("Al bajar la guardia queda expuesto (los campos 'Castigo' de este mismo asset). " +
                 "Apagado, simplemente vuelve a la normalidad.")]
        [SerializeField] private bool counterAttack = true;

        public override string ShortStats() =>
            $"se cubre {guardSeconds:0.0}s · recibe ×{damageTakenWhileGuarding:0.00} · devuelve {reflectDamage:0}" +
            (counterAttack && VulnerableSeconds > 0f ? $" · luego expuesto {VulnerableSeconds:0.0}s" : "");

        public override void OnTelegraph(BossContext ctx)
        {
            // El aviso se pinta sobre el propio jefe, no en el suelo: lo que va a cambiar es él,
            // no un trozo de arena.
            Warn(ctx, ctx.Origin + new Vector2(0f, 2.6f), Vector2.one * 4f, ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (!ctx.IsValid) yield break;

            float seconds = ctx.Scaled(guardSeconds);

            Impact();
            ctx.Boss.EnterGuard(seconds, damageTakenWhileGuarding, reflectDamage, reflectKnockback);

            // Mientras aguanta se repinta la marca: una guardia que no se ve es una trampa, y una
            // que se ve es una regla.
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!ctx.IsValid) yield break;

                Warn(ctx, ctx.Origin + new Vector2(0f, 2.6f), Vector2.one * 4.4f, pulseInterval * 1.5f);

                yield return new WaitForSeconds(pulseInterval);
                elapsed += pulseInterval;
            }

            // Sin contraataque no hay nada más que hacer: la ventana de castigo de la clase base la
            // abre el controlador al entrar en la recuperación, así que basta con no estorbar.
            if (!counterAttack) yield break;
        }
    }
}
