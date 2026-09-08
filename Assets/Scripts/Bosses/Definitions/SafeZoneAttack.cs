using System.Collections;
using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// El ataque que <b>cubre la arena entera menos unos pocos sitios</b>: se marcan N refugios,
    /// pasa un momento, y todo lo que no esté dentro de uno recibe el golpe.
    ///
    /// Es el tercer tipo de esquiva del juego y el más distinto de todos. Los otros dos preguntan
    /// por la altura (<see cref="ShockwaveAttack"/>) o por la distancia
    /// (<see cref="SweepBeamAttack"/>) y se resuelven reaccionando; éste pregunta <b>dónde estás en
    /// el suelo</b> y se resuelve <i>yendo</i> a un sitio, así que obliga a soltar al jefe y correr
    /// — que es justo lo que un jugador acomodado en su rutina de dps no quiere hacer.
    ///
    /// Es justo por construcción: el refugio se pinta en un color propio (<see cref="safeColor"/>,
    /// no el de la fase) y late cada vez más fuerte según se acerca el golpe, así que "no me ha
    /// dado tiempo" siempre es cierto y "no lo he visto" nunca.
    ///
    /// Con <see cref="pulses"/> &gt; 1 y <see cref="moveSpotsEachPulse"/> encendido se convierte en
    /// una carrera de refugio en refugio, que es la versión de fase 2 sin escribir nada nuevo.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Safe Zone Attack", fileName = "BossAttack_SafeZone")]
    public class SafeZoneAttack : BossAttack
    {
        [Header("Refugios")]
        [Tooltip("Cuántos sitios seguros se marcan. Menos refugios = más lejos hay que correr.")]
        [Min(1)]
        [SerializeField] private int safeSpots = 3;

        [Tooltip("Radio del refugio. Tiene que caber el jugador con holgura: si va justo, el " +
                 "ataque deja de leerse como 'ponte ahí' y pasa a ser 'acierta el píxel'.")]
        [Min(0.5f)]
        [SerializeField] private float spotRadius = 2.3f;

        [Tooltip("Separación mínima entre refugios, para que no salgan dos pegados (que serían uno).")]
        [Min(0f)]
        [SerializeField] private float minSeparation = 6f;

        [Tooltip("Margen que se respeta en los bordes de la arena: un refugio pegado a la pared es " +
                 "medio refugio.")]
        [Min(0f)]
        [SerializeField] private float edgeMargin = 2.5f;

        [Tooltip("Coloca SIEMPRE un refugio cerca del jugador. Es lo que hace justo al ataque " +
                 "independientemente de lo grande que sea la arena: sin esto, una mala tirada puede " +
                 "dejar los tres refugios en la otra punta y el golpe pasa a ser daño inevitable.")]
        [SerializeField] private bool guaranteeReachable = true;

        [Tooltip("Radio dentro del que aparece ese refugio garantizado. Debe ser lo que el jugador " +
                 "recorre cómodamente durante la ventana de aviso, no lo máximo que podría correr.")]
        [Min(1f)]
        [SerializeField] private float reachableRadius = 9f;

        [Header("Ritmo")]
        [Tooltip("Segundos entre que se marcan los refugios y cae el golpe. Es el tiempo que hay " +
                 "para llegar corriendo: bájalo sólo si también bajas la distancia.")]
        [Min(0.2f)]
        [SerializeField] private float markerSeconds = 1.6f;

        [Tooltip("Golpes seguidos del ataque.")]
        [Min(1)]
        [SerializeField] private int pulses = 1;

        [Min(0.1f)]
        [SerializeField] private float timeBetweenPulses = 1.3f;

        [Tooltip("Cambia los refugios de sitio en cada golpe. Encendido, el ataque es una carrera; " +
                 "apagado, basta con llegar una vez y quedarse.")]
        [SerializeField] private bool moveSpotsEachPulse = true;

        [Header("Presencia")]
        [Tooltip("Color del refugio. A propósito NO es el color de la fase: lo que hace daño y lo " +
                 "que salva no pueden pintarse igual.")]
        [SerializeField] private Color safeColor = new Color(0.95f, 0.93f, 0.72f, 0.85f);

        [Tooltip("Altura que cubre el golpe sobre el suelo. 0 = toda la arena (saltar no salva).")]
        [Min(0f)]
        [SerializeField] private float blastHeight;

        private const float PulseInterval = 0.12f;

        public override string ShortStats() =>
            $"{Damage:0} dmg · {safeSpots} refugio(s) · {markerSeconds:0.0}s para llegar · {pulses} golpe(s)";

        public override void OnTelegraph(BossContext ctx)
        {
            // El aviso del jefe sólo dice "va a cubrirlo todo". Dónde están los refugios se enseña
            // dentro de Run, ya con el reloj corriendo, para que el aviso no regale la respuesta
            // antes de tiempo.
            var center = new Vector2(ctx.Origin.x, ctx.GroundY + 0.6f);
            Warn(ctx, center, new Vector2(ctx.ArenaHalfWidth * 2f, 1.2f), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            var spots = new float[Mathf.Max(1, safeSpots)];
            PickSpots(ctx, spots);

            for (int pulse = 0; pulse < pulses; pulse++)
            {
                if (!ctx.IsValid) yield break;

                if (pulse > 0 && moveSpotsEachPulse) PickSpots(ctx, spots);

                yield return ShowMarkers(ctx, spots);

                if (!ctx.IsValid) yield break;

                Impact();
                Blast(ctx, spots);

                if (pulse < pulses - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenPulses));
            }
        }

        // ------------------------------------------------------------------ refugios

        /// <summary>
        /// Reparte los refugios por la arena: se sortean con rechazo por separación mínima y, si el
        /// sorteo no encuentra sitio (arena estrecha, muchos refugios), se cae a un reparto regular.
        /// Nunca devuelve menos refugios de los pedidos: quedarse sin sitio donde meterse por una
        /// mala tirada convertiría el ataque en daño inevitable.
        /// </summary>
        private void PickSpots(in BossContext ctx, float[] spots)
        {
            float min = ctx.ArenaMinX + edgeMargin;
            float max = ctx.ArenaMaxX - edgeMargin;
            if (max <= min) { min = ctx.ArenaMinX; max = ctx.ArenaMaxX; }

            for (int i = 0; i < spots.Length; i++)
            {
                // El primero se ancla cerca del jugador: el ataque tiene que ser una carrera que se
                // puede ganar, no una tirada de dados sobre si te tocaba estar en el lado bueno.
                if (i == 0 && guaranteeReachable)
                {
                    float around = ctx.PlayerPosition.x + Random.Range(-reachableRadius, reachableRadius);
                    spots[0] = Mathf.Clamp(around, min, max);
                    continue;
                }

                bool placed = false;

                for (int attempt = 0; attempt < 24 && !placed; attempt++)
                {
                    float candidate = Random.Range(min, max);
                    if (IsFarEnough(candidate, spots, i)) { spots[i] = candidate; placed = true; }
                }

                if (!placed)
                {
                    // Reparto regular: i-ésimo hueco de una rejilla que cubre la arena.
                    float step = spots.Length > 1 ? (max - min) / (spots.Length - 1) : 0f;
                    spots[i] = spots.Length > 1 ? min + step * i : (min + max) * 0.5f;
                }
            }
        }

        private bool IsFarEnough(float candidate, float[] spots, int count)
        {
            for (int i = 0; i < count; i++)
                if (Mathf.Abs(spots[i] - candidate) < minSeparation) return false;

            return true;
        }

        private Vector2 SpotCenter(in BossContext ctx, float x) =>
            new Vector2(x, ctx.GroundY + spotRadius * 0.55f);

        // ------------------------------------------------------------------ aviso y golpe

        /// <summary>
        /// La ventana para llegar. El refugio se repinta cada pocas centésimas subiendo de
        /// intensidad: un latido que va a más se lee como una cuenta atrás, mientras que una marca
        /// fija (o que se apaga, como el resto de avisos) diría justo lo contrario de lo que pasa.
        /// </summary>
        private IEnumerator ShowMarkers(BossContext ctx, float[] spots)
        {
            float window = ctx.Scaled(markerSeconds);
            float elapsed = 0f;

            while (elapsed < window)
            {
                if (!ctx.IsValid) yield break;

                float t = Mathf.Clamp01(elapsed / window);
                float intensity = Mathf.Lerp(0.45f, 1f, t);

                // Lo que va a doler: toda la arena, tenue, para que el refugio destaque encima.
                var danger = ctx.Accent;
                danger.a = 0.16f * intensity;
                Mark(ctx, new Vector2(ctx.Origin.x, ctx.GroundY + BlastHeight(ctx) * 0.5f),
                     new Vector2(ctx.ArenaHalfWidth * 2f, BlastHeight(ctx)), danger, PulseInterval * 1.6f);

                // Y el refugio, en su color, encima.
                var safe = safeColor;
                safe.a = safeColor.a * intensity;

                for (int i = 0; i < spots.Length; i++)
                    Mark(ctx, SpotCenter(ctx, spots[i]), Vector2.one * (spotRadius * 2f), safe,
                         PulseInterval * 1.6f);

                yield return new WaitForSeconds(PulseInterval);
                elapsed += PulseInterval;
            }
        }

        /// <summary>
        /// El golpe: alcanza a todo lo que haya en la arena <b>menos</b> lo que esté dentro de un
        /// refugio. El filtro se hace por posición y no con más colisiones, porque los refugios se
        /// solapan entre sí y con la caja de la arena; comprobar la distancia es exacto y no
        /// depende de en qué orden vengan los colliders.
        /// </summary>
        private void Blast(in BossContext ctx, float[] spots)
        {
            float height = BlastHeight(ctx);
            var center = new Vector2(ctx.Origin.x, ctx.GroundY + height * 0.5f);
            var size = new Vector2(ctx.ArenaHalfWidth * 2f, height);

            var targets = AbilityHit.OverlapBox(ctx.Ability, center, size);

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var target = targets[i];
                if (target == null) continue;
                if (IsSheltered(ctx, spots, target.transform.position)) continue;

                AbilityHit.Damage(target, ctx.Ability, Damage, center, KnockbackMultiplier);
            }

            // El destello del golpe cubre la arena, y cada refugio se marca una vez más al mismo
            // tiempo: quien lo consiguió ve que ese sitio era el bueno.
            var flash = ctx.Accent;
            flash.a = 0.5f;
            Mark(ctx, center, size, flash, 0.35f, 1.05f);

            var safe = safeColor;
            safe.a = 0.9f;
            for (int i = 0; i < spots.Length; i++)
                Mark(ctx, SpotCenter(ctx, spots[i]), Vector2.one * (spotRadius * 2f), safe, 0.4f, 1.3f);
        }

        private bool IsSheltered(in BossContext ctx, float[] spots, Vector2 position)
        {
            for (int i = 0; i < spots.Length; i++)
                if (Vector2.Distance(position, SpotCenter(ctx, spots[i])) <= spotRadius) return true;

            return false;
        }

        private float BlastHeight(in BossContext ctx) =>
            blastHeight > 0f ? blastHeight : ctx.ArenaHeight;
    }
}
