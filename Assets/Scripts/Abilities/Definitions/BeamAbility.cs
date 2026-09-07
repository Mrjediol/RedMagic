using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Golpe instantáneo en línea recta: rayo arcano, lanzallamas corto, barrido de escopeta a
    /// quemarropa. No hay proyectil — se resuelve como una caja larga en la dirección de apuntado,
    /// así que impacta el mismo frame y siempre acierta a lo que tenga delante.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Beam", fileName = "Ability_Beam")]
    public class BeamAbility : AbilityDefinition
    {
        [Header("Haz")]
        [Min(0.5f)]
        [SerializeField] private float length = 8f;

        [Tooltip("Grosor del haz. Un rayo fino apunta; un cono ancho perdona la puntería.")]
        [Min(0.1f)]
        [SerializeField] private float width = 0.6f;

        [Tooltip("Salida del haz respecto al personaje. La X se invierte según hacia dónde mira.")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.6f, 0.1f);

        [Tooltip("Segundos que se ve el haz dibujado.")]
        [Min(0.02f)]
        [SerializeField] private float visualDuration = 0.12f;

        [Tooltip("Se corta al chocar con el escenario. Apagado, atraviesa paredes.")]
        [SerializeField] private bool blockedByGround = true;

        [Tooltip("Capas que cortan el haz cuando lo anterior está activo.")]
        [SerializeField] private LayerMask groundLayers = 1 << 6;   // capa 'Ground' del proyecto

        public override string ShortStats() =>
            $"{Damage:0} dmg · alcance {length:0.0} · {Cooldown:0.00}s";

        public override void Execute(AbilityContext ctx)
        {
            Vector2 origin = ctx.Muzzle(muzzleOffset);
            Vector2 direction = ctx.Aim;

            // El nivel del arma alarga y engorda el haz: es lo que se nota al subir un rayo.
            float length = this.length * ctx.SizeScale;
            float width = this.width * ctx.SizeScale;
            float reach = length;

            // Un rayo que atraviesa el suelo se lee como un fallo: se recorta en la primera pared.
            if (blockedByGround)
            {
                var hit = Physics2D.Raycast(origin, direction, length, groundLayers);
                if (hit.collider != null) reach = hit.distance;
            }

            Vector2 center = origin + direction * (reach * 0.5f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            AbilityHit.DamageBox(ctx, center, new Vector2(reach, width), angle,
                                 Damage, KnockbackMultiplier, origin);

            AbilityFx.Flash(FxSprite, center, new Vector2(reach, width),
                            Accent * new Color(1f, 1f, 1f, 0.8f), visualDuration, angle, 1f, ctx.Caster);
        }
    }
}
