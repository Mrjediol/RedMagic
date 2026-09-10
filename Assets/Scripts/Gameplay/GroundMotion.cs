using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>Lo que hay bajo los pies de un cuerpo que camina, medido con rayos.</summary>
    public readonly struct GroundContact
    {
        /// <summary>True si hay suelo dentro del alcance del sondeo (puede haber un hueco debajo).</summary>
        public readonly bool Grounded;

        /// <summary>Hueco entre los pies y ese suelo. Casi 0 = apoyado; negativo = penetrando.</summary>
        public readonly float Gap;

        /// <summary>Normal de la superficie. <c>(0,1)</c> en plano.</summary>
        public readonly Vector2 Normal;

        /// <summary>Inclinación de esa superficie en grados. 0 = plano.</summary>
        public readonly float Angle;

        public GroundContact(bool grounded, float gap, Vector2 normal, float angle)
        {
            Grounded = grounded;
            Gap = gap;
            Normal = normal;
            Angle = angle;
        }

        /// <summary>Apoyado de verdad, no sólo con suelo cerca. Es lo que distingue andar de caer.</summary>
        public bool Resting => Grounded && Gap <= GroundMotion.RestingGap;

        /// <summary>Apoyado en una rampa que se puede recorrer andando.</summary>
        public bool OnWalkableSlope => Grounded && Angle > GroundMotion.FlatTolerance
                                                && Angle <= GroundMotion.MaxSlopeAngle;

        /// <summary>Apoyado en algo tan inclinado que cuenta como pared.</summary>
        public bool TooSteep => Grounded && Angle > GroundMotion.MaxSlopeAngle;
    }

    /// <summary>
    /// Andar por rampas y atravesar el terreno, para cualquier cuerpo que no sea el jugador.
    ///
    /// Existe porque los enemigos son <see cref="Rigidbody2D"/> dinámicos a los que la IA les
    /// escribe la velocidad: escribir sólo la <b>X</b> contra una rampa es empujar de frente contra
    /// ella, y el resultado es que el enemigo se queda temblando al pie de la cuesta, o resbala
    /// hacia abajo en cuanto se para. El jugador ya resuelve esto en <see cref="PlayerMovement"/>,
    /// pero allí está entretejido con un controlador cinemático propio (proyección en la pendiente,
    /// subir el pie, deslizar contra paredes) que no se puede reutilizar tal cual. Esto es la parte
    /// que sí necesita un cuerpo dinámico, escrita una vez, para que <see cref="EnemyBrain"/> (el
    /// sistema nuevo) y <see cref="EnemyController"/> (el viejo, que aún mueve a los esbirros de
    /// jefe) caminen igual.
    ///
    /// El ángulo máximo es <b>el mismo que el del jugador</b> a propósito: una rampa que el jugador
    /// sube y el enemigo no, o al revés, se lee como un fallo del nivel.
    /// </summary>
    public static class GroundMotion
    {
        /// <summary>
        /// Rampa más inclinada que se recorre andando. El mismo valor que el <c>maxSlopeAngle</c>
        /// del jugador.
        /// </summary>
        public const float MaxSlopeAngle = 45f;

        /// <summary>Por debajo de esto la superficie se considera plana (ruido de medición).</summary>
        public const float FlatTolerance = 0.5f;

        /// <summary>Hueco máximo bajo los pies que sigue contando como estar apoyado.</summary>
        public const float RestingGap = 0.08f;

        /// <summary>
        /// Desde cuánto por encima de los pies sale el rayo. Generoso a propósito: un cuerpo
        /// dinámico se hunde unos centímetros en el suelo entre pasos de física, y con un origen
        /// pegado a los pies el rayo salía ya por debajo de la superficie y no encontraba nada —
        /// justo el frame en que hacía falta saber que estaba en una rampa.
        /// </summary>
        private const float ProbeRise = 0.35f;

        /// <summary>Cuánto por debajo de los pies se busca suelo. Da margen al bajar una rampa.</summary>
        private const float ProbeDepth = 0.4f;

        /// <summary>Separación de los rayos de los extremos respecto al borde del collider.</summary>
        private const float EdgeInset = 0.06f;

        /// <summary>
        /// Las capas que son <b>terreno</b> en este proyecto: <c>Ground</c> (lo macizo) y
        /// <c>Platform</c> (las plataformas y los puentes inclinados, que el jugador además
        /// atraviesa de abajo arriba).
        ///
        /// Se resuelve por nombre y no con un <c>1 &lt;&lt; 6</c> a mano para que renumerar una capa
        /// no lo rompa en silencio. Es la referencia con la que la auditoría de contenido repara
        /// las máscaras de los enemigos: mirar sólo <c>Ground</c> dejaba a un enemigo plantado al
        /// borde de un puente creyendo que era un precipicio.
        /// </summary>
        public static LayerMask TerrainMask => LayerMask.GetMask("Ground", "Platform");

        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[8];

        /// <summary>
        /// Mide el suelo bajo un collider con tres rayos: cola, centro y morro.
        ///
        /// Se queda con el <b>apoyo más alto</b> (el impacto más cercano a los pies), que es la
        /// superficie que de verdad lo sostiene. Con un solo rayo central, al llegar al pie de una
        /// rampa el cuerpo aún mide plano y empuja de frente contra la cuesta.
        /// </summary>
        public static GroundContact Probe(Collider2D body, LayerMask ground)
        {
            if (body == null) return default;

            var bounds = body.bounds;
            float y = bounds.min.y + ProbeRise;
            float length = ProbeRise + ProbeDepth;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = ground, useTriggers = false };
            var root = body.transform.root;

            bool grounded = false;
            float nearest = float.MaxValue;
            Vector2 normal = Vector2.up;

            for (int i = 0; i < 3; i++)
            {
                float x = i switch
                {
                    0 => bounds.min.x + EdgeInset,
                    1 => bounds.center.x,
                    _ => bounds.max.x - EdgeInset,
                };

                int count = Physics2D.Raycast(new Vector2(x, y), Vector2.down, filter, Hits, length);
                for (int h = 0; h < count; h++)
                {
                    var hit = Hits[h];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(root)) continue;

                    // Un cuerpo que no es estático no es terreno: es otro personaje. Sin esto,
                    // una máscara generosa (las hay heredadas del sistema viejo) convierte al
                    // enemigo de al lado en "suelo" con una normal cualquiera.
                    if (hit.rigidbody != null && hit.rigidbody.bodyType != RigidbodyType2D.Static) continue;

                    if (hit.distance >= nearest) continue;

                    nearest = hit.distance;
                    normal = hit.normal;
                    grounded = true;
                }
            }

            if (!grounded) return default;
            return new GroundContact(true, nearest - ProbeRise, normal, Vector2.Angle(normal, Vector2.up));
        }

        /// <summary>
        /// Convierte una velocidad horizontal en una que <b>sigue la rampa</b>.
        ///
        /// En plano, o contra algo demasiado inclinado, devuelve el movimiento de siempre (la X
        /// pedida y la Y que traiga la física). En una rampa transitable devuelve la tangente de la
        /// superficie a la misma rapidez, así que subir y bajar cuestan lo mismo que andar en
        /// plano — igual que hace la proyección en pendiente del jugador.
        ///
        /// Con suelo cerca pero sin apoyar (bajando una cuesta, medio frame en el aire) sólo se
        /// admite la tangente que <b>baja</b>: pega el cuerpo a la rampa en vez de dejarlo caer a
        /// saltitos, pero nunca lo hace trepar por el aire.
        /// </summary>
        /// <param name="signedSpeed">Velocidad horizontal deseada: positiva a la derecha.</param>
        /// <param name="current">Velocidad actual del cuerpo, para conservar la caída.</param>
        public static Vector2 AlongSlope(in GroundContact ground, float signedSpeed, Vector2 current)
        {
            var flat = new Vector2(signedSpeed, current.y);
            if (!ground.OnWalkableSlope || Mathf.Approximately(signedSpeed, 0f)) return flat;

            var tangent = new Vector2(ground.Normal.y, -ground.Normal.x);
            if (tangent.x * signedSpeed < 0f) tangent = -tangent;

            if (!ground.Resting && tangent.y > 0f) return flat;

            return tangent * Mathf.Abs(signedSpeed);
        }

        /// <summary>
        /// Velocidad para quedarse quieto sin resbalar.
        ///
        /// Parado en una rampa, poner sólo la X a cero no basta: la gravedad sigue tirando y el
        /// cuerpo se desliza cuesta abajo. Apoyado se para del todo; en el aire (o con el suelo aún
        /// lejos) se respeta la caída, porque si no se quedaría flotando.
        /// </summary>
        public static Vector2 Halt(in GroundContact ground, Vector2 current)
            => ground.Resting ? Vector2.zero : new Vector2(0f, current.y);

        /// <summary>
        /// Hace que un objeto atraviese el terreno (o deje de hacerlo), sin capas nuevas ni tocar
        /// la matriz de colisiones del proyecto: es un <c>excludeLayers</c> por collider.
        ///
        /// Es lo que hace que para un volador las plataformas no existan. Sólo se excluye el
        /// terreno: sigue chocando con el jugador, así que el daño por contacto no cambia.
        /// </summary>
        public static void PhaseThroughTerrain(GameObject root, LayerMask terrain, bool on)
        {
            if (root == null) return;

            foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
            {
                int mask = col.excludeLayers.value;
                col.excludeLayers = on ? mask | terrain.value : mask & ~terrain.value;
            }
        }
    }
}
