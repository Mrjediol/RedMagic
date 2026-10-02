using System;
using System.Collections.Generic;
using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Rigging
{
    /// <summary>
    /// Una parte del cuerpo de un personaje hecho de sprites sueltos, con su balanceo propio.
    /// Cada parte lleva su fase: desfasadas, el cuerpo "respira" en vez de moverse en bloque.
    /// </summary>
    [Serializable]
    public class RigPart
    {
        public Transform part;

        [Tooltip("Punto real de articulación, relativo al pivote del sprite (unidades locales). " +
                 "Para sprites de lienzo completo cuyo pivote no cae donde la pieza se une: el " +
                 "giro, el pulso de escala y las vueltas al morir se hacen alrededor de aquí, sin " +
                 "tocar el arte. 0 = el pivote del sprite.")]
        public Vector2 pivot;

        [Tooltip("Centro de la parte visible, relativo al pivote del sprite (unidades locales). Es " +
                 "el punto de contacto cuando un ataque usa la pieza (el puño que golpea).")]
        public Vector2 center;

        [Tooltip("Giro extra proporcional a la inclinación del cuerpo al moverse. Positivo = exagera la " +
                 "inclinación: una pieza que cuelga (punta, puño) se queda atrás como un péndulo. " +
                 "Negativo = la compensa (tiende a quedarse vertical).")]
        public float leanFollow;

        [Tooltip("Hacia dónde apunta la parte \"útil\" de la pieza en su dibujo sin girar, en grados " +
                 "(0 = derecha, -90 = abajo: los nudillos de un puño colgando). Un ataque que lanza la " +
                 "pieza la gira para que esto mire hacia donde va.")]
        public float pointAngle;

        [Header("Balanceo en reposo (seno)")]
        [Tooltip("Amplitud del vaivén, en unidades locales del padre.")]
        public Vector2 swayMove = new Vector2(0f, 0.05f);

        [Tooltip("Amplitud del giro, en grados.")]
        public float swayRotate = 3f;

        [Tooltip("Pulso de escala (0 = ninguno). Una llama o un núcleo que late.")]
        public Vector2 swayScale;

        [Tooltip("Radianes por segundo.")]
        [Min(0f)] public float swaySpeed = 1.5f;

        [Tooltip("Desfase respecto a las demás partes, en radianes.")]
        public float swayPhase;

        [Header("Muerte")]
        [Tooltip("Sale despedida al morir. Apagado = se queda pegada a su padre.")]
        public bool breaksOnDeath = true;
    }

    /// <summary>Estado visual que decide qué sprite lleva cada <see cref="RigSpriteSwap"/>.</summary>
    public enum RigSpriteState
    {
        Asleep,
        Awake,
        Attacking,
    }

    /// <summary>Los estados con nombre de un <see cref="RigNamedSprite"/> (además de dormido / despierto / atacando).</summary>
    public enum RigNamedState
    {
        /// <summary>Ha detectado al objetivo y va a por él (persigue, se retira, ataca, enfría).</summary>
        Alert,

        /// <summary>Mientras dura el golpe recibido.</summary>
        Hurt,

        /// <summary>Muerto (si falta, se usa el de dormido).</summary>
        Dead,

        /// <summary>Mientras dura el gesto <see cref="RigNamedSprite.gesture"/>: "en el ataque 2 uso este brazo".</summary>
        Gesture,
    }

    /// <summary>El sprite de un renderer en un estado con nombre.</summary>
    [Serializable]
    public class RigNamedSprite
    {
        public RigNamedState state = RigNamedState.Alert;

        [Tooltip("Nombre del gesto, cuando el estado es 'Gesture'.")]
        public string gesture = "";

        public Sprite sprite;

        [Tooltip("Sólo 'Gesture': desde qué momento del gesto se ve. La escala son los pasos del gesto: " +
                 "0–1 = el primero, 1–2 = el segundo… y el último tramo (nº de pasos → +1) es la vuelta " +
                 "al reposo. En un gesto clásico de carga + golpe: 0–1 carga, 1–2 golpe, 2–3 vuelta.")]
        [Min(0f)] public float showFrom;

        [Tooltip("Sólo 'Gesture': hasta qué momento del gesto se ve (misma escala que 'showFrom'). " +
                 "Un valor mayor que el final = hasta que acaba.")]
        [Min(0f)] public float showTo = 99f;
    }

    /// <summary>
    /// Sprites por estado para un renderer (ojos apagados / encendidos / disparando). El sprite que se
    /// ve en cada momento lo decide <see cref="ProceduralRig.RefreshSprites"/>: el del estado más
    /// prioritario que lo tenga; un estado vacío deja pasar al siguiente, hasta el sprite del prefab.
    /// </summary>
    [Serializable]
    public class RigSpriteSwap
    {
        public SpriteRenderer renderer;
        public Sprite asleep;
        public Sprite awake;
        [Tooltip("Aviso del ataque: se pone al empezar la carga y se quita tras la coz.")]
        public Sprite attacking;

        [Tooltip("Más estados: detecta al objetivo, herido, muerto, y uno por gesto (ese ataque usa este sprite).")]
        public List<RigNamedSprite> named = new List<RigNamedSprite>();

        /// <summary>
        /// El sprite de un estado con nombre, o null. Para <see cref="RigNamedState.Gesture"/>: el de ese
        /// gesto y sólo si <paramref name="moment"/> (en pasos, ver <see cref="RigNamedSprite.showFrom"/>) cae
        /// en su ventana — "estira la mano, enseña el dedo, lo quita y la recoge".
        /// </summary>
        public Sprite Named(RigNamedState state, string gesture = null, float moment = 0f)
        {
            if (named == null) return null;
            foreach (var n in named)
            {
                if (n == null || n.state != state || n.sprite == null) continue;
                if (state != RigNamedState.Gesture) return n.sprite;
                if (!string.Equals(n.gesture, gesture, StringComparison.OrdinalIgnoreCase)) continue;
                if (moment >= n.showFrom - 0.0001f && moment <= n.showTo + 0.0001f) return n.sprite;
            }

            return null;
        }

        [Tooltip("Sprite por etapa (fase de un jefe): 0 = la primera. Al cambiar de etapa se pone el " +
                 "de esa etapa (el torso agrietado de la fase 2). Vacío = no cambia con la etapa.")]
        public List<Sprite> stages = new List<Sprite>();

        public Sprite Get(RigSpriteState state) => state switch
        {
            RigSpriteState.Asleep => asleep,
            RigSpriteState.Attacking => attacking,
            _ => awake,
        };
    }

    /// <summary>Desplazamiento de una parte dentro de una pose de gesto (se suma al reposo).</summary>
    [Serializable]
    public class RigPose
    {
        [Tooltip("La parte, o la raíz visual (entonces X es 'hacia delante' y el giro 'hacia atrás').")]
        public Transform part;
        public Vector2 move;
        [Tooltip("Grados.")]
        public float rotate;
        [Tooltip("Cambio de escala (0.1 = +10%).")]
        public Vector2 scale;
    }

    /// <summary>En qué momento de un gesto suena su <see cref="RigGesture.sound"/>.</summary>
    public enum GestureSoundMoment
    {
        /// <summary>Al empezar la carga (un cargador que zumba, un rugido de aviso).</summary>
        Start,

        /// <summary>Al soltar el golpe (el disparo, el impacto).</summary>
        Release,
    }

    /// <summary>Cómo se llega a la pose de un paso.</summary>
    public enum RigEase
    {
        /// <summary>Arranca y frena suave (una carga, un movimiento con peso).</summary>
        InOut,

        /// <summary>Sale de golpe y frena (un latigazo, un disparo).</summary>
        Out,

        /// <summary>Arranca despacio y acaba a toda velocidad (coger impulso).</summary>
        In,

        Linear,
    }

    /// <summary>Hacia dónde mira un efecto de gesto.</summary>
    public enum RigFxFacing
    {
        /// <summary>
        /// Hacia donde va la acción: en la boca, hacia donde apunta el arma (por donde sale el
        /// disparo); en el cuerpo o los pies, hacia donde se mueve el enemigo (o hacia donde mira, si
        /// está quieto). Es lo normal: un fogonazo apunta al tiro, una estela sigue al movimiento.
        /// </summary>
        Direction,

        /// <summary>Gira con su ancla (pegado a la pieza, como una pegatina).</summary>
        Anchor,

        /// <summary>Siempre derecho, sin girar (sólo se refleja con el cuerpo).</summary>
        World,
    }

    /// <summary>Delante o detrás del cuerpo.</summary>
    public enum RigFxLayer
    {
        Front,
        Behind,
    }

    /// <summary>
    /// Un paso de un gesto: la pose a la que llega el cuerpo y cuánto tarda. Un gesto es una lista de
    /// pasos, tantos como haga falta ("lanza el arma al aire, la recoge, apunta, dispara").
    /// </summary>
    [Serializable]
    public class RigGestureStep
    {
        [Tooltip("Sólo para reconocerlo en el editor (\"Carga\", \"Lanza\", \"Recoge\"…).")]
        public string name = "";

        [Tooltip("La pose al final del paso, medida desde el reposo. Una pieza que no aparece vuelve a su reposo.")]
        public List<RigPose> poses = new List<RigPose>();

        [Tooltip("Segundos que tarda en llegar.")]
        [Min(0.01f)] public float duration = 0.3f;

        public RigEase ease = RigEase.InOut;

        [Tooltip("Su duración la pone quien lanza el gesto en vez de 'duration': el aviso ('telegraph') " +
                 "del ataque, para que el mismo gesto sirva a ataques con avisos distintos. Con varios " +
                 "pasos así, se reparten ese tiempo. Apagado en todos = el gesto dura lo que diga él, " +
                 "y el ataque sale cuando lleguen sus sueltas.")]
        public bool stretch;

        [Tooltip("Temblor (grados) de la raíz visual en el último 30% del paso.")]
        public float shake;
    }

    /// <summary>
    /// Un instante del gesto en el que SALE el ataque (el disparo, el golpe, la salva). Un gesto puede
    /// tener varios: dispara varias veces. Sin ninguno, suelta al acabar el último paso.
    /// </summary>
    [Serializable]
    public class RigRelease
    {
        [Tooltip("Paso en el que sale (0 = el primero).")]
        [Min(0)] public int step;

        [Tooltip("Momento dentro del paso: 0 = al empezar, 1 = al acabar.")]
        [Range(0f, 1f)] public float at = 1f;

        [Tooltip("De dónde sale: una pieza o un nodo del cuerpo. Vacío = la boca del rig.")]
        public Transform point;

        [Tooltip("Desplazamiento desde ese punto, en sus unidades locales.")]
        public Vector2 offset;
    }

    /// <summary>De dónde cuelga un efecto de gesto.</summary>
    public enum RigFxAnchor
    {
        /// <summary>Una pieza del cuerpo (<see cref="RigFx.part"/>).</summary>
        Part,

        /// <summary>La raíz visual (el cuerpo entero).</summary>
        Body,

        /// <summary>La boca del arma (<see cref="ProceduralRig.muzzle"/>); sin boca, la pieza que apunta.</summary>
        Muzzle,

        /// <summary>El suelo bajo el cuerpo: la base de su collider.</summary>
        Feet,
    }

    /// <summary>
    /// Un efecto enganchado a una fase de un gesto (las partículas de carga en la punta del arma, el
    /// estallido al soltar). Sólo datos: lo lanza el <see cref="RigDriver"/>, siempre desde el pool.
    /// </summary>
    [Serializable]
    public class RigFx
    {
        [Tooltip("Prefab del efecto (lo construye el importador desde la biblioteca web). Vacío = no hace nada.")]
        public GameObject prefab;

        [Tooltip("Paso del gesto en el que sale (0 = el primero). El número de pasos = la vuelta al reposo.")]
        [Min(0)] public int step;

        [Tooltip("Momento dentro del paso: 0 = al empezar, 1 = al acabar.")]
        [Range(0f, 1f)] public float at;

        [Tooltip("De dónde cuelga.")]
        public RigFxAnchor anchor = RigFxAnchor.Part;

        [Tooltip("La pieza, cuando el ancla es 'Part'.")]
        public Transform part;

        [Tooltip("Desplazamiento desde el ancla, en sus unidades locales (se voltea con el cuerpo).")]
        public Vector2 offset;

        [Tooltip("Tamaño relativo al del prefab.")]
        [Min(0.01f)] public float scale = 1f;

        [Tooltip("Sigue al ancla mientras dura (posición y giro). Apagado = se queda donde salió.")]
        public bool followsPart = true;

        [Tooltip("Se repite y se apaga al acabar un paso (una carga). Apagado = se reproduce una vez (un estallido).")]
        public bool loop;

        [Tooltip("Sólo en bucle: el paso al acabar el cual se apaga. -1 = el mismo en el que salió.")]
        public int untilStep = -1;

        [Tooltip("Hacia dónde mira. Por defecto, hacia donde va la acción (el disparo, el movimiento).")]
        public RigFxFacing facing = RigFxFacing.Direction;

        [Tooltip("Giro extra, en grados, sobre esa orientación.")]
        public float angle;

        [Tooltip("Delante o detrás del cuerpo.")]
        public RigFxLayer layer = RigFxLayer.Front;

        // Dato antiguo (fase fija carga / golpe / vuelta): sólo para convertirlo al cargar. -1 = no hay.
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("phase")]
        internal int legacyPhase = -1;

        /// <summary>El paso al acabar el cual se apaga, si es un bucle.</summary>
        public int EndStep => untilStep >= 0 ? Mathf.Max(untilStep, step) : step;
    }

    /// <summary>
    /// Un gesto con nombre (el <c>gesture</c> de un ataque: "Charge", "Slam"…): una <b>línea de
    /// tiempo</b>. El cuerpo recorre sus <see cref="steps"/> en orden (cada uno una pose y un tiempo),
    /// y luego vuelve al reposo en <see cref="recover"/>. Por el camino, en el instante que se quiera:
    /// el ataque sale en cada <see cref="releases"/> (una o varias veces, desde el punto que se diga) y
    /// los <see cref="fx"/> aparecen. Todo son datos: un gesto nuevo no necesita código.
    ///
    /// El gesto clásico es el caso de dos pasos: carga (lo que dure el aviso del ataque,
    /// <see cref="RigGestureStep.stretch"/>) → suelta → golpe.
    /// </summary>
    [Serializable]
    public class RigGesture : ISerializationCallbackReceiver
    {
        public string name = "Charge";

        [Tooltip("Los pasos, en orden. Cada uno: la pose a la que llega y cuánto tarda.")]
        public List<RigGestureStep> steps = new List<RigGestureStep>();

        [Tooltip("Cuándo y desde dónde sale el ataque. Vacío = una vez, al acabar el último paso, desde la boca.")]
        public List<RigRelease> releases = new List<RigRelease>();

        [Tooltip("Segundos de vuelta al reposo tras el último paso.")]
        [Min(0.01f)] public float recover = 0.45f;

        [Header("Efectos")]
        [Tooltip("Efectos que salen en un instante del gesto, colgados de una pieza, la boca, el cuerpo o " +
                 "los pies. Se editan en la web (Enemy Studio ▸ Gestos).")]
        public List<RigFx> fx = new List<RigFx>();

        [Header("Sonido")]
        [Tooltip("Suena al lanzar este gesto, sea cual sea el ataque que lo use. Se asigna en la pestaña " +
                 "Sounds (Enemies/<nombre>/Gestures). Vacío = silencio: el gesto no obliga a sonar.")]
        public SoundCue sound = new SoundCue();

        [Tooltip("Cuándo suena: al empezar el gesto o en su primera suelta.")]
        public GestureSoundMoment soundMoment = GestureSoundMoment.Release;

        // ------------------------------------------------------------ lectura

        private static readonly List<RigRelease> NoReleases = new List<RigRelease>();
        [NonSerialized] private List<RigRelease> _implicitRelease;

        /// <summary>
        /// Las sueltas del gesto. Sin ninguna escrita, una al acabar el último paso desde la boca: un
        /// gesto que lanza un ataque siempre lo suelta. Sin pasos, ninguna (quien lo lanza suelta por tiempo).
        /// </summary>
        public List<RigRelease> EffectiveReleases
        {
            get
            {
                if (releases != null && releases.Count > 0) return releases;
                if (steps == null || steps.Count == 0) return NoReleases;
                if (_implicitRelease == null) _implicitRelease = new List<RigRelease> { new RigRelease() };
                _implicitRelease[0].step = steps.Count - 1;
                _implicitRelease[0].at = 1f;
                return _implicitRelease;
            }
        }

        /// <summary>
        /// Segundos de cada paso para un gesto lanzado con <paramref name="lead"/> segundos de aviso:
        /// los pasos <see cref="RigGestureStep.stretch"/> se reparten lo que falte para que la PRIMERA
        /// suelta caiga justo a <paramref name="lead"/>; los demás duran lo suyo.
        /// </summary>
        public void Durations(float lead, List<float> into)
        {
            into.Clear();
            if (steps == null) return;

            var first = FirstRelease();
            int lastStretch = first != null ? Mathf.Min(first.step, steps.Count - 1) : steps.Count - 1;

            float fixedTime = 0f;
            int stretched = 0;
            for (int i = 0; i <= lastStretch; i++)
            {
                var s = steps[i];
                if (s == null) continue;
                bool releaseStep = first != null && i == first.step;
                if (s.stretch) stretched++;
                else fixedTime += Mathf.Max(0.01f, s.duration) * (releaseStep ? first.at : 1f);
            }

            float share = stretched > 0 ? Mathf.Max(0.01f, (lead - fixedTime) / stretched) : 0f;
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s == null) { into.Add(0.01f); continue; }

                if (!s.stretch || i > lastStretch) { into.Add(Mathf.Max(0.01f, s.duration)); continue; }

                // El paso en el que cae la suelta: lo que se estira es el tramo HASTA ella.
                bool releaseStep = first != null && i == first.step;
                into.Add(releaseStep && first.at > 0.01f ? share / first.at : share);
            }
        }

        /// <summary>Segundos desde que arranca hasta su primera suelta, lanzado con ese aviso.</summary>
        public float SecondsToFirstRelease(float lead)
        {
            var first = FirstRelease();
            if (first == null) return Mathf.Max(0.01f, lead);

            Durations(lead, Scratch);
            float time = 0f;
            for (int i = 0; i < Scratch.Count && i <= first.step; i++)
                time += Scratch[i] * (i == first.step ? first.at : 1f);
            return Mathf.Max(0.01f, time);
        }

        private static readonly List<float> Scratch = new List<float>();

        private RigRelease FirstRelease()
        {
            RigRelease first = null;
            foreach (var r in EffectiveReleases)
                if (r != null && (first == null || r.step < first.step || (r.step == first.step && r.at < first.at))) first = r;
            return first;
        }

        // ------------------------------------------------------------ el gesto clásico

        /// <summary>
        /// El gesto clásico de carga + golpe como línea de tiempo: la carga dura el aviso del ataque
        /// (<see cref="RigGestureStep.stretch"/>), suelta al acabarla y el golpe llega en
        /// <paramref name="strikeTime"/>. Sin pose de golpe, un solo paso.
        /// </summary>
        public static RigGesture Classic(string name, List<RigPose> windup, List<RigPose> strike,
                                         float strikeTime, float recover, float shake)
        {
            var gesture = new RigGesture { name = name, recover = recover };
            gesture.SetClassic(windup, strike, strikeTime, shake);
            return gesture;
        }

        private void SetClassic(List<RigPose> windup, List<RigPose> strike, float strikeTime, float shake)
        {
            steps = new List<RigGestureStep>
            {
                new RigGestureStep { name = "Carga", poses = windup ?? new List<RigPose>(), stretch = true, ease = RigEase.InOut, shake = shake },
            };
            if (strike != null && strike.Count > 0)
                steps.Add(new RigGestureStep { name = "Golpe", poses = strike, duration = Mathf.Max(0.01f, strikeTime), ease = RigEase.Out });
            releases = new List<RigRelease> { new RigRelease { step = 0, at = 1f } };
        }

        // ------------------------------------------------------------ datos de antes de los pasos
        //
        // Un prefab guardado antes de la línea de tiempo trae 'windup' / 'strike' / 'strikeTime' /
        // 'shake' y efectos por fase. Se convierten al cargar (y quedan convertidos en cuanto el prefab
        // se guarda: Tools ▸ Web ▸ Rig ▸ Convertir gestos a pasos). 'strikeTime' > 0 = hay dato antiguo.

        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("windup")]
        private List<RigPose> legacyWindup;
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("strike")]
        private List<RigPose> legacyStrike;
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("strikeTime")]
        private float legacyStrikeTime = -1f;
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("shake")]
        private float legacyShake;

        /// <summary>True si este gesto se acaba de convertir desde el formato de fases y NO tenía golpe (un solo paso).</summary>
        [NonSerialized] public bool convertedSingleStep;

        /// <summary>True si lo que hay en disco es todavía el formato antiguo.</summary>
        public bool HasLegacyData => legacyStrikeTime > 0f;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (legacyStrikeTime <= 0f || (steps != null && steps.Count > 0)) { legacyStrikeTime = -1f; return; }

            bool hasStrike = legacyStrike != null && legacyStrike.Count > 0;
            SetClassic(legacyWindup, hasStrike ? legacyStrike : null, legacyStrikeTime, legacyShake);
            convertedSingleStep = !hasStrike;

            // Efectos: Windup → paso 0; Strike → el paso del golpe (sin golpe salían todos al soltar);
            // Recover → la vuelta. Y siguen girando con su ancla, como estaban puestos.
            if (fx != null)
                foreach (var f in fx)
                {
                    if (f == null || f.legacyPhase < 0) continue;
                    if (f.legacyPhase == 0) f.step = 0;
                    else if (f.legacyPhase == 1) { f.step = hasStrike ? 1 : 0; if (!hasStrike) f.at = 1f; }
                    else f.step = steps.Count;
                    f.facing = RigFxFacing.Anchor;
                    f.legacyPhase = -1;
                }

            legacyWindup = null;
            legacyStrike = null;
            legacyStrikeTime = -1f;
            legacyShake = 0f;
        }
    }

    /// <summary>
    /// Gestos que el cuerpo hace solo, de vez en cuando, mientras no tiene nada que hacer (mirar a los
    /// lados, sacudirse, ajustarse el arma). Sólo dan vida: no avisan de nada ni lanzan ningún ataque.
    /// </summary>
    [Serializable]
    public class RigIdleGestures
    {
        [Tooltip("Nombres de gestos de la lista de gestos. Cada vez se elige uno al azar. Vacío = ninguno.")]
        public List<string> gestures = new List<string>();

        [Tooltip("Cada cuántos segundos, más o menos, lo intenta.")]
        [Min(0.2f)] public float interval = 4f;

        [Tooltip("Probabilidad de hacerlo en cada intento.")]
        [Range(0f, 1f)] public float chance = 0.6f;

        [Tooltip("Segundos que tarda en llegar a la pose de carga del gesto (en un ataque es el aviso).")]
        [Min(0.05f)] public float windup = 0.5f;
    }

    /// <summary>
    /// Los <b>datos</b> de un cuerpo animado por partes: qué partes hay, cómo se balancean, y los
    /// tiempos y amplitudes de cada gesto (apuntar, retroceso, golpe, muerte…).
    ///
    /// No anima nada por sí mismo. Lo anima un <see cref="RigDriver"/> al lado — hay uno por
    /// técnica (<c>RigDriverCode</c> con senos y corrutinas, <c>RigDriverTween</c> con DOTween) —
    /// y todos leen de aquí. Así dos prefabs con distinto motor tienen exactamente los mismos
    /// tiempos: la comparación sólo cambia la implementación, nunca el juego.
    ///
    /// Tampoco sabe nada de enemigos ni de jefes: <c>EnemyAnimation</c> (o el animador de un jefe)
    /// le habla al driver, y el driver avisa con eventos del instante en que sale el golpe.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProceduralRig : MonoBehaviour
    {
        [Header("Cuerpo")]
        [Tooltip("Raíz visual (normalmente 'Visual'). Es la que se voltea al mirar a un lado, se " +
                 "inclina al moverse y flota.")]
        public Transform body;

        public List<RigPart> parts = new List<RigPart>();

        [Header("Flotar (toda la raíz visual)")]
        public Vector2 hoverBob = new Vector2(0f, 0.08f);
        [Min(0f)] public float hoverSpeed = 2.2f;

        [Header("Inclinación al moverse")]
        [Tooltip("Grados de inclinación a velocidad plena, hacia donde avanza.")]
        public float leanDegrees = 12f;

        [Tooltip("Velocidad (u/s) a la que la inclinación es completa.")]
        [Min(0.01f)] public float leanFullSpeed = 2.5f;

        [Tooltip("Rapidez con que la inclinación sigue a la velocidad (1/s).")]
        [Min(0.01f)] public float leanResponse = 8f;

        [Header("Inercia (el cuerpo va detrás de la raíz)")]
        [Tooltip("Rigidez del muelle que une la raíz visual a la raíz. 0 = pegada (sin inercia). Con " +
                 "inercia, al arrancar el cuerpo se queda un poco atrás y al parar se pasa un poco: " +
                 "se lee el peso.")]
        [Min(0f)] public float followStiffness;

        [Tooltip("Amortiguación del muelle. Baja = rebota más al parar.")]
        [Min(0f)] public float followDamping = 9f;

        [Tooltip("Separación máxima entre el cuerpo y la raíz (unidades de mundo).")]
        [Min(0f)] public float followMaxOffset = 0.6f;

        [Tooltip("Gesto (de la lista de gestos) que se mantiene mientras se desplaza por su cuenta: " +
                 "piezas que se quedan atrás, cuerpo echado hacia delante. Vacío = ninguno.")]
        public string moveGesture;

        [Tooltip("Gestos que hace solo en reposo: sin objetivo, sin moverse, sin atacar y despierto.")]
        public RigIdleGestures idle = new RigIdleGestures();

        [Header("Apuntar (brazo-arma)")]
        [Tooltip("La parte que gira hacia el objetivo. Vacío = no apunta.")]
        public Transform aimPart;

        [Tooltip("Ángulo (grados, espacio local) al que apunta el cañón en el dibujo sin girar. " +
                 "El giro de apuntado lo descuenta.")]
        public float aimArtAngle;

        [Tooltip("Giro máximo desde el reposo, en grados, hacia arriba o abajo.")]
        [Range(0f, 180f)] public float aimMaxAngle = 75f;

        [Header("Ataque (tiempos a velocidad 1)")]
        [Tooltip("Segundos de preparación: apunta y se carga. El golpe sale al terminar.")]
        [Min(0f)] public float windup = 0.4f;

        [Tooltip("Segundos tras el golpe hasta volver al reposo. Al acabar se avisa del fin.")]
        [Min(0f)] public float recover = 0.45f;

        [Tooltip("Anticipación: el brazo se echa hacia atrás estos grados antes de disparar.")]
        public float windupPullAngle = 10f;

        [Tooltip("Cuerpo echado hacia atrás durante la carga, en grados.")]
        public float windupBodyTilt = 6f;

        [Tooltip("Segundos que el arma se queda apuntando tras soltar, antes de la coz: el disparo " +
                 "se ve salir del cañón en vez de aparecer con el arma ya levantada.")]
        [Min(0f)] public float recoilDelay = 0.06f;

        [Tooltip("Segundos que tarda la coz en llegar a su máximo (sale con easing, no de golpe).")]
        [Min(0.01f)] public float recoilKick = 0.09f;

        [Tooltip("Coz del arma: grados que sube el cañón al disparar.")]
        public float recoilAngle = 15f;

        [Tooltip("Coz del arma: cuánto retrocede la parte a lo largo del cañón (unidades locales).")]
        public float recoilDistance = 0.3f;

        [Tooltip("Empujón del cuerpo entero hacia atrás al disparar (unidades locales).")]
        public float bodyRecoil = 0.15f;

        [Header("Fogonazo (placeholder, pooled)")]
        [Tooltip("Punta del arma. Vacío = sin fogonazo. Suele ser el 'Muzzle' del brazo-arma.")]
        public Transform muzzle;
        [Tooltip("Sprite del fogonazo. Vacío = el cuadrado por defecto de AbilityFx.")]
        public Sprite muzzleFlashSprite;
        public Color muzzleFlashColor = new Color(0.55f, 1f, 1f, 0.9f);
        [Min(0f)] public float muzzleFlashSize = 0.55f;
        [Min(0.01f)] public float muzzleFlashTime = 0.1f;

        [Header("Sprites por estado")]
        public List<RigSpriteSwap> spriteSwaps = new List<RigSpriteSwap>();

        [Header("Plegar / desplegar (dormir / despertar)")]
        [Tooltip("Parte que se estira en Y desde su pivote (el cuello). Vacío = no se pliega.")]
        public Transform stretchPart;

        [Tooltip("Partes que viajan en la punta de 'stretchPart' (la cabeza). Deben colgar del " +
                 "mismo padre que ella.")]
        public List<Transform> stretchRiders = new List<Transform>();

        [Tooltip("Escala Y (relativa al prefab) del cuello plegado.")]
        [Min(0f)] public float foldedStretch = 0.05f;

        [Tooltip("Escala Y de los pasajeros plegados (cabeza aplastada sobre la base).")]
        [Min(0.01f)] public float foldedRiderScaleY = 0.6f;

        [Tooltip("Desplazamiento extra de los pasajeros plegados (unidades locales).")]
        public Vector2 foldedRiderOffset;

        [Tooltip("Segundos de despertar a velocidad 1. Al acabar se avisa WakeFinished.")]
        [Min(0.01f)] public float wakeDuration = 0.8f;

        [Tooltip("Segundos de volver a plegarse.")]
        [Min(0.01f)] public float sleepDuration = 0.55f;

        [Tooltip("Rebote al desplegarse (curva OutBack; 0 = sin rebote, 1.7 = estándar).")]
        [Min(0f)] public float wakeOvershoot = 2.2f;

        [Tooltip("Parpadeos de los ojos al encenderse (alterna dormido/despierto). 0 = se abren " +
                 "una sola vez, limpio.")]
        [Min(0)] public int wakeFlickers;

        [Tooltip("Tramo final del despertar en el que se abren los ojos (o parpadean): 0.4 = " +
                 "se abren al 60% del despliegue.")]
        [Range(0f, 1f)] public float wakeFlickerPortion = 0.4f;

        [Header("Gestos con nombre (jefes)")]
        public List<RigGesture> gestures = new List<RigGesture>();

        [Header("Piezas manejadas por un ataque (puño lanzado…)")]
        [Tooltip("Si el ataque deja de mover la pieza durante estos segundos (lo cortó un cambio de " +
                 "fase, por ejemplo), vuelve sola al cuerpo.")]
        [Min(0.02f)] public float drivenLease = 0.25f;
        [Tooltip("Segundos de la vuelta automática.")]
        [Min(0.02f)] public float drivenReturn = 0.35f;

        [Header("Golpe recibido")]
        [Tooltip("Multiplicador del golpe en un tambaleo (cambio de fase de un jefe).")]
        [Min(1f)] public float staggerStrength = 2.5f;
        [Min(0.01f)] public float hurtDuration = 0.28f;
        [Tooltip("Grados que se tambalea.")]
        public float hurtTilt = 14f;
        [Tooltip("Aplastamiento del impacto (0.1 = 10%).")]
        public float hurtSquash = 0.12f;

        [Header("Muerte (se desmonta)")]
        [Tooltip("Velocidad con que cada parte sale despedida desde el centro (u/s).")]
        public float breakImpulse = 3.5f;
        [Tooltip("Velocidad extra hacia arriba (u/s).")]
        public float breakUpward = 4f;
        [Tooltip("Giro máximo de cada pieza (grados/s).")]
        public float breakSpin = 540f;
        [Tooltip("Gravedad de las piezas (u/s²).")]
        public float breakGravity = 20f;
        [Tooltip("Rebote contra el suelo (0 = se queda, 1 = rebota entero).")]
        [Range(0f, 1f)] public float breakBounce = 0.35f;
        [Tooltip("Capas contra las que rebotan las piezas.")]
        public LayerMask breakGround = (1 << 6) | (1 << 8);

        // ============================================================ pose de reposo

        private struct Rest
        {
            public Transform transform;
            public Transform parent;
            public int sibling;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        private readonly List<Rest> _rest = new List<Rest>();
        private bool _captured;

        /// <summary>
        /// Guarda la pose del prefab (padre, orden, posición, giro, escala) de la raíz visual y de
        /// todas las partes. Una vez: es la pose de diseño y todo se mide respecto a ella.
        /// </summary>
        public void CaptureRest()
        {
            if (_captured) return;
            _captured = true;

            Add(body);
            Add(aimPart);
            Add(stretchPart);
            foreach (var p in parts) Add(p != null ? p.part : null);
            foreach (var r in stretchRiders) Add(r);

            _restSprites.Clear();
            foreach (var swap in spriteSwaps)
                _restSprites.Add(swap?.renderer != null ? swap.renderer.sprite : null);
        }

        private readonly List<Sprite> _restSprites = new List<Sprite>();

        /// <summary>Etapa actual (fase del jefe, 0 = la primera). Vuelve a 0 con <see cref="RestorePose"/>.</summary>
        public int Stage { get; private set; }

        /// <summary>Cambia de etapa: cada <see cref="RigSpriteSwap"/> con <c>stages</c> pone el sprite de esa etapa.</summary>
        public void SetStage(int stage)
        {
            Stage = Mathf.Max(0, stage);
            RefreshSprites();
        }

        // ---- estado visual: de qué depende el sprite de cada RigSpriteSwap ahora mismo.
        private RigSpriteState _spriteBase = RigSpriteState.Awake;   // dormido / despierto
        private bool _spriteAttacking;       // un ataque en curso (el propio, o un gesto lanzado como ataque)
        private string _spriteGesture;       // el gesto en curso, o null
        private float _spriteMoment;         // por dónde va ese gesto: 0–1 carga, 1–2 golpe, 2–3 vuelta
        private int _spriteWindows;          // qué sprites de gesto están en su ventana ahora (para no refrescar cada frame)
        private bool _spriteAlert, _spriteHurt, _spriteDead;

        /// <summary>
        /// Pone a cada <see cref="RigSpriteSwap"/> su sprite. Gana el primer estado activo que tenga
        /// sprite, en este orden: muerto → herido → el gesto en curso → atacando → alerta → dormido /
        /// despierto → la etapa (fase del jefe) → el sprite del prefab. Así un estado sin sprite nunca
        /// deja "pegado" el del estado anterior.
        /// </summary>
        public void RefreshSprites()
        {
            CaptureRest();
            for (int i = 0; i < spriteSwaps.Count; i++)
            {
                var swap = spriteSwaps[i];
                if (swap?.renderer == null) continue;

                Sprite sprite = null;
                if (_spriteDead) sprite = swap.Named(RigNamedState.Dead) ?? swap.asleep;
                if (sprite == null && !_spriteDead && _spriteHurt) sprite = swap.Named(RigNamedState.Hurt);
                if (sprite == null && !_spriteDead && _spriteGesture != null) sprite = swap.Named(RigNamedState.Gesture, _spriteGesture, _spriteMoment);
                if (sprite == null && !_spriteDead && _spriteAttacking) sprite = swap.attacking;
                if (sprite == null && !_spriteDead && _spriteAlert && _spriteBase == RigSpriteState.Awake) sprite = swap.Named(RigNamedState.Alert);
                if (sprite == null && !_spriteDead) sprite = _spriteBase == RigSpriteState.Asleep ? swap.asleep : swap.awake;
                if (sprite == null && swap.stages != null && swap.stages.Count > 0) sprite = swap.stages[Mathf.Min(Stage, swap.stages.Count - 1)];
                if (sprite == null && i < _restSprites.Count) sprite = _restSprites[i];
                if (sprite != null) swap.renderer.sprite = sprite;
            }
        }

        /// <summary>Un gesto empieza: sus sprites propios y, si es un ataque, el de "atacando" donde no los haya.</summary>
        public void ShowGestureSprites(string gesture, bool isAttack)
        {
            _spriteGesture = gesture;
            _spriteAttacking = isAttack;
            _spriteMoment = 0f;
            _spriteWindows = ActiveGestureWindows();
            RefreshSprites();
        }

        /// <summary>
        /// Por dónde va el gesto en curso, en pasos (0–1 el primero… y el último tramo, la vuelta). Lo
        /// dice el motor cada frame; los sprites sólo se recalculan cuando alguno entra o sale de su ventana.
        /// </summary>
        public void SetGestureMoment(float moment)
        {
            if (_spriteGesture == null) return;
            _spriteMoment = Mathf.Max(0f, moment);
            int windows = ActiveGestureWindows();
            if (windows == _spriteWindows) return;
            _spriteWindows = windows;
            RefreshSprites();
        }

        /// <summary>Un bit por sprite de gesto (del gesto en curso) que está dentro de su ventana.</summary>
        private int ActiveGestureWindows()
        {
            int mask = 0, bit = 0;
            foreach (var swap in spriteSwaps)
            {
                if (swap?.named == null) continue;
                foreach (var n in swap.named)
                {
                    if (n == null || n.state != RigNamedState.Gesture ||
                        !string.Equals(n.gesture, _spriteGesture, StringComparison.OrdinalIgnoreCase)) continue;
                    if (_spriteMoment >= n.showFrom - 0.0001f && _spriteMoment <= n.showTo + 0.0001f) mask |= 1 << (bit & 31);
                    bit++;
                }
            }

            return mask;
        }

        /// <summary>El gesto (o el ataque propio) ha terminado.</summary>
        public void EndGestureSprites()
        {
            if (_spriteGesture == null && !_spriteAttacking) return;
            _spriteGesture = null;
            _spriteAttacking = false;
            RefreshSprites();
        }

        /// <summary>Ha detectado al objetivo / lo ha perdido.</summary>
        public void SetAlertSprites(bool alert)
        {
            if (_spriteAlert == alert) return;
            _spriteAlert = alert;
            RefreshSprites();
        }

        /// <summary>Mientras dura un golpe recibido.</summary>
        public void SetHurtSprites(bool hurt)
        {
            if (_spriteHurt == hurt) return;
            _spriteHurt = hurt;
            RefreshSprites();
        }

        /// <summary>Muerto: su sprite de muerto, o el de dormido (los ojos se apagan).</summary>
        public void ShowDeadSprites()
        {
            _spriteDead = true;
            RefreshSprites();
        }

        /// <summary>Vuelta al estado de recién creado (reutilización por pool): despierto, sin nada en curso.</summary>
        public void ResetSprites()
        {
            _spriteBase = RigSpriteState.Awake;
            _spriteAttacking = _spriteAlert = _spriteHurt = _spriteDead = false;
            _spriteGesture = null;
            RefreshSprites();
        }

        /// <summary>
        /// Dormido / despierto fijan el estado de fondo (y dan por terminado cualquier ataque);
        /// <see cref="RigSpriteState.Attacking"/> marca el aviso del ataque propio.
        /// </summary>
        public void ShowSprites(RigSpriteState state)
        {
            if (state == RigSpriteState.Attacking) _spriteAttacking = true;
            else
            {
                _spriteBase = state;
                _spriteAttacking = false;
                _spriteGesture = null;
            }

            RefreshSprites();
        }

        // ------------------------------------------------------------ de dónde sale el ataque

        [NonSerialized] private Transform _castPoint;
        [NonSerialized] private Vector2 _castOffset;

        /// <summary>El motor lo fija en cada suelta del gesto (<see cref="RigRelease.point"/>).</summary>
        public void SetCastPoint(Transform point, Vector2 offset)
        {
            _castPoint = point;
            _castOffset = offset;
        }

        public void ClearCastPoint() => SetCastPoint(null, Vector2.zero);

        /// <summary>True si hay de dónde sacar un ataque: una suelta con su punto, una boca o una pieza que apunta.</summary>
        public bool HasCastPoint => _castPoint != null || muzzle != null || aimPart != null;

        /// <summary>
        /// Un gesto convertido desde el formato de fases que no tenía golpe pasa de "carga 0–1, vuelta
        /// 2–3" a "paso 0–1, vuelta 1–2": las ventanas de sus sprites de gesto bajan un tramo.
        /// </summary>
        public void FixConvertedWindows()
        {
            foreach (var g in gestures)
            {
                if (g == null || !g.convertedSingleStep) continue;
                g.convertedSingleStep = false;
                foreach (var swap in spriteSwaps)
                {
                    if (swap?.named == null) continue;
                    foreach (var n in swap.named)
                    {
                        if (n == null || n.state != RigNamedState.Gesture ||
                            !string.Equals(n.gesture, g.name, StringComparison.OrdinalIgnoreCase)) continue;
                        if (n.showFrom >= 2f) n.showFrom -= 1f; else if (n.showFrom > 1f) n.showFrom = 1f;
                        if (n.showTo >= 2f) n.showTo -= 1f; else if (n.showTo > 1f) n.showTo = 1f;
                    }
                }
            }
        }

        /// <summary>
        /// De dónde sale el ataque ahora mismo: el punto de la suelta en curso; si no lo dice, la boca;
        /// sin boca, la pieza que apunta; sin ella, el cuerpo.
        /// </summary>
        public Vector2 CastPosition
        {
            get
            {
                if (_castPoint != null) return _castPoint.TransformPoint(_castOffset);
                var t = muzzle != null ? muzzle : aimPart != null ? aimPart : body != null ? body : transform;
                return t.TransformPoint(_castOffset);
            }
        }

        /// <summary>El gesto con ese nombre (sin distinguir mayúsculas), o null.</summary>
        public RigGesture FindGesture(string gestureName)
        {
            if (string.IsNullOrWhiteSpace(gestureName)) return null;
            foreach (var g in gestures)
                if (g != null && string.Equals(g.name, gestureName, StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        /// <summary>Los datos de la parte <paramref name="t"/>, o null.</summary>
        public RigPart FindPart(Transform t)
        {
            foreach (var p in parts) if (p != null && p.part == t) return p;
            return null;
        }

        private static readonly Dictionary<Sprite, Vector2> DrawnSizes = new Dictionary<Sprite, Vector2>();

        /// <summary>
        /// Tamaño en el mundo de lo que <b>se ve</b> de la parte: los límites de la malla ajustada del
        /// sprite (no del lienzo, que puede ser diez veces mayor) por su escala actual. Es la medida
        /// para dimensionar lo que la acompaña: el aviso de un puño es del tamaño de un puño.
        /// </summary>
        public static Vector2 PartSize(Transform t)
        {
            var renderer = t != null ? t.GetComponent<SpriteRenderer>() : null;
            var sprite = renderer != null ? renderer.sprite : null;
            if (sprite == null) return Vector2.one;

            if (!DrawnSizes.TryGetValue(sprite, out var size))
            {
                var vertices = sprite.vertices;
                Vector2 min = vertices[0], max = vertices[0];
                foreach (var v in vertices)
                {
                    min = Vector2.Min(min, v);
                    max = Vector2.Max(max, v);
                }

                size = max - min;
                DrawnSizes[sprite] = size;
            }

            var scale = t.lossyScale;
            return new Vector2(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y));
        }

        /// <summary>Centro visible de una parte en el mundo (ver <see cref="RigPart.center"/>).</summary>
        public Vector2 PartCenter(Transform t)
        {
            var data = FindPart(t);
            return t.TransformPoint(data != null ? (Vector3)data.center : Vector3.zero);
        }

        /// <summary>
        /// Dónde está en el mundo el pivote de la parte en su pose de reposo <b>ahora</b> (con el
        /// cuerpo donde esté y mirando a donde mire). Es el "hogar" al que vuelve una pieza lanzada.
        /// </summary>
        public Vector2 RestWorldPosition(Transform t)
        {
            if (t == null || t.parent == null) return t != null ? (Vector2)t.position : Vector2.zero;
            TryGetRest(t, out var local, out _, out _);
            return t.parent.TransformPoint(local);
        }

        /// <summary>True si este rig sabe plegarse (dormir / despertar).</summary>
        public bool CanFold => stretchPart != null;

        /// <summary>
        /// Estiramiento Y del cuello para un grado de despliegue (0 plegado, 1 prefab; &gt;1 en el
        /// rebote).
        /// </summary>
        public float StretchAt(float unfold) => Mathf.LerpUnclamped(foldedStretch, 1f, unfold);

        /// <summary>
        /// Cuánto sube un pasajero sobre su reposo con el cuello a <paramref name="stretch"/>: la
        /// distancia pivote-del-cuello → pasajero escala con el cuello, así la cabeza va siempre en
        /// la punta.
        /// </summary>
        public Vector3 RiderOffset(Vector3 riderRest, float stretch, float unfold)
        {
            TryGetRest(stretchPart, out var neckRest, out _, out _);
            float tip = riderRest.y - neckRest.y;
            return new Vector3(0f, tip * (stretch - 1f), 0f) +
                   (Vector3)(foldedRiderOffset * (1f - Mathf.Clamp01(unfold)));
        }

        /// <summary>Escala Y de un pasajero para un grado de despliegue.</summary>
        public float RiderScaleY(float unfold) => Mathf.LerpUnclamped(foldedRiderScaleY, 1f, Mathf.Clamp01(unfold));

        /// <summary>
        /// Curva OutBack, la misma fórmula que DOTween (<c>Ease.OutBack</c> con
        /// <c>overshoot</c>): los dos motores despliegan idéntico.
        /// </summary>
        public static float OutBack(float t, float overshoot)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        }

        /// <summary>Curva InBack (la inversa), igual que <c>Ease.InBack</c>.</summary>
        public static float InBack(float t, float overshoot)
        {
            t = Mathf.Clamp01(t);
            return t * t * ((overshoot + 1f) * t - overshoot);
        }

        private void Add(Transform t)
        {
            if (t == null) return;
            foreach (var r in _rest) if (r.transform == t) return;

            _rest.Add(new Rest
            {
                transform = t,
                parent = t.parent,
                sibling = t.GetSiblingIndex(),
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale,
            });
        }

        /// <summary>
        /// True si alguna pieza cuelga de otro padre (la muerte las desmonta). Hay que devolverlas
        /// fuera de OnEnable/OnDisable: Unity prohíbe cambiar la jerarquía mientras se activa o
        /// desactiva el objeto.
        /// </summary>
        public bool HierarchyChanged
        {
            get
            {
                foreach (var r in _rest)
                    if (r.transform != null && r.transform.parent != r.parent) return true;
                return false;
            }
        }

        /// <summary>
        /// Deja todo como en el prefab. Con <paramref name="hierarchy"/> devuelve además cada pieza
        /// a su padre y orden (sólo a las que se movieron). Es lo que hace reutilizable a un
        /// enemigo por pool.
        /// </summary>
        public void RestorePose(bool hierarchy = false)
        {
            CaptureRest();

            // Primero padres (en orden de captura el cuerpo va antes que sus piezas), luego pose.
            if (hierarchy)
            {
                foreach (var r in _rest)
                {
                    if (r.transform == null || r.transform.parent == r.parent) continue;
                    r.transform.SetParent(r.parent, false);
                    if (r.parent != null) r.transform.SetSiblingIndex(Mathf.Min(r.sibling, r.parent.childCount - 1));
                }
            }

            foreach (var r in _rest)
            {
                if (r.transform == null) continue;
                r.transform.localPosition = r.position;
                r.transform.localRotation = r.rotation;
                r.transform.localScale = r.scale;
            }

            Stage = 0;
            ResetSprites();
        }

        /// <summary>Pose de diseño de una parte capturada. False si no está en el rig.</summary>
        public bool TryGetRest(Transform t, out Vector3 position, out float angle, out Vector3 scale)
        {
            CaptureRest();
            foreach (var r in _rest)
            {
                if (r.transform != t) continue;
                position = r.position;
                angle = r.rotation.eulerAngles.z;
                scale = r.scale;
                return true;
            }

            position = Vector3.zero;
            angle = 0f;
            scale = Vector3.one;
            return false;
        }

        // ============================================================ utilidades compartidas

        /// <summary>
        /// Cuántos grados hay que girar <see cref="aimPart"/> <b>desde su reposo</b> para que su
        /// cañón apunte a <paramref name="worldPoint"/>, limitado a <see cref="aimMaxAngle"/>.
        /// Tiene en cuenta el volteo de la raíz visual: la dirección se pasa al espacio del padre
        /// con su escala, así que mirando a la izquierda sale reflejada sola.
        /// </summary>
        public float AimOffsetTowards(Vector2 worldPoint, Vector3 restPosition, float restAngle)
        {
            if (aimPart == null || aimPart.parent == null) return 0f;

            Vector2 from = aimPart.parent.TransformPoint(restPosition);
            Vector2 local = aimPart.parent.InverseTransformVector(worldPoint - from);
            if (local.sqrMagnitude < 0.0001f) return 0f;

            float wanted = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - aimArtAngle;

            // Un objetivo a la espalda (posible un frame antes de voltear) no retuerce el brazo.
            return Mathf.Clamp(Mathf.DeltaAngle(restAngle, wanted), -aimMaxAngle, aimMaxAngle);
        }

        /// <summary>
        /// Corrección de posición para que una pieza gire/escale alrededor de su
        /// <see cref="RigPart.pivot"/> y no del pivote del sprite: lo que hay que sumar a la
        /// posición de reposo con el giro y la escala nuevos.
        /// </summary>
        public static Vector3 PivotCorrection(Vector2 pivot, float restAngle, Vector3 restScale,
                                              float angle, Vector3 scale)
        {
            if (pivot.sqrMagnitude < 0.000001f) return Vector3.zero;

            Vector3 before = Quaternion.Euler(0f, 0f, restAngle) * Vector3.Scale(restScale, pivot);
            Vector3 after = Quaternion.Euler(0f, 0f, angle) * Vector3.Scale(scale, pivot);
            return before - after;
        }

        /// <summary>Dirección local del cañón en reposo (según el dibujo).</summary>
        public Vector2 AimArtDirection =>
            new Vector2(Mathf.Cos(aimArtAngle * Mathf.Deg2Rad), Mathf.Sin(aimArtAngle * Mathf.Deg2Rad));

        /// <summary>Duración total de un ataque a la velocidad dada.</summary>
        public float AttackDuration(float speed) => (windup + recover) / Mathf.Max(0.01f, speed);

        private void OnDrawGizmosSelected()
        {
            if (aimPart == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.9f);
            Vector3 dir = aimPart.TransformDirection((Vector3)AimArtDirection);
            Gizmos.DrawLine(aimPart.position, aimPart.position + dir * 1.2f);
        }
    }
}
