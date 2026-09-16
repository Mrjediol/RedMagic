using System;
using UnityEngine;

namespace RedMagic.Pipeline
{
    /// <summary>Cómo se anima el personaje una vez cortada la hoja.</summary>
    public enum AnimRuntime
    {
        /// <summary>
        /// <see cref="Animator"/> + AnimatorController generado. Para enemigos y jefes, que se
        /// instancian de uno en uno y no pasan por pool.
        /// </summary>
        Animator,

        /// <summary>
        /// <see cref="RedMagic.Gameplay.SpriteFlipbook"/> vía <see cref="SpriteStateMachine"/>.
        /// <b>Obligatorio para lo que va por pool</b> (proyectiles, FX): <c>PrefabPool</c> no tiene
        /// gancho de reinicio por instancia, así que un Animator se reutilizaría con el estado de
        /// la vez anterior. El flipbook rebobina en <c>OnEnable</c>.
        /// </summary>
        Flipbook,
    }

    /// <summary>Cómo se localizan los frames dentro de la lámina.</summary>
    public enum SliceMode
    {
        /// <summary>
        /// Detecta el contenido solo: bandas por filas, frames por componentes conexas, rótulos
        /// descartados. Es el modo por defecto porque aguanta láminas con texto, márgenes
        /// irregulares y filas de distinto número de frames.
        /// </summary>
        AutoBounds,

        /// <summary>Rejilla uniforme de <see cref="SpriteSheetRecipe.columns"/> × filas.</summary>
        Grid,
    }

    /// <summary>Dónde cae el pivote de cada frame dentro de su celda.</summary>
    public enum AnchorMode
    {
        /// <summary>A los pies, centrado. Lo normal en un personaje: no flota ni se hunde.</summary>
        BottomCenter,

        /// <summary>Centro de la caja de contenido. Para orbes, proyectiles y cosas que giran.</summary>
        Center,
    }

    /// <summary>Una fila de la lámina = un estado de animación.</summary>
    [Serializable]
    public class SheetRow
    {
        [Tooltip("Nombre del estado. Idle / Walk / Attack / Hurt / Death son los que el " +
                 "AnimatorController generado sabe cablear solo; cualquier otro se crea como " +
                 "estado suelto sin transiciones.")]
        public string state = "Idle";

        [Tooltip("Frames esperados en la fila. 0 = los que se detecten. Si se pone un número y " +
                 "no cuadra, el corte avisa en vez de inventarse frames.")]
        [Min(0)] public int frames;

        [Min(0.1f)] public float fps = 10f;

        public bool loop = true;

        [Tooltip("En qué frame sale el golpe (el proyectil, o la caja de melé). El generador clava " +
                 "ahí un AnimationEvent (OnAttackRelease, más OnAttackFinished al final), así que el " +
                 "daño cae en el dibujo exacto en el que el bicho suelta, no cuando lo diga un " +
                 "temporizador. Vale para la fila 'Attack' y para cualquier otra: los gestos de un " +
                 "jefe ('Charge', 'Slam'…) son filas propias que sueltan igual (ver BossAnimator).\n" +
                 "-1 = sin evento (el ataque usa su tiempo de respaldo).")]
        public int releaseFrame = -1;

        [Tooltip("Recuadros de los frames puestos a mano, en píxeles de la lámina (y=0 abajo, como " +
                 "en el Sprite Editor de Unity). Si hay alguno, mandan sobre la detección " +
                 "automática y sobre 'evenSplit'.\n\n" +
                 "Es la salida de emergencia para una fila que ninguna heurística acierta: FX que " +
                 "se solapan de forma irregular, frames de anchos muy distintos. El resto del " +
                 "corte sigue igual — se quita el fondo, se empaqueta en celdas uniformes y el " +
                 "pivote queda en el mismo punto en todos los frames.")]
        public RectInt[] frameRects;

        [Tooltip("Cuánto margen horizontal se admite para dar dos manchas por partes del mismo " +
                 "frame, como múltiplo del margen normal (1 = el de siempre, ~1/12 de la altura " +
                 "del personaje).\n\n" +
                 "Bájalo cuando la detección junte cosas que son de frames distintos: dos poses " +
                 "que casi se tocan (la cola de una llega al hocico de la siguiente), o el " +
                 "proyectil dibujado pegado a la boca — que así se separa y se exporta como " +
                 "prop en vez de estirar la celda del ataque. Súbelo si un mismo dibujo se " +
                 "parte en trozos. Sólo afecta al modo automático.")]
        [Min(0.05f)] public float groupSlack = 1f;

        [Tooltip("Cuántos dibujos sueltos hay en la fila que NO son poses del personaje: el " +
                 "proyectil ya lanzado, dibujado una o dos veces a la derecha del último frame.\n\n" +
                 "Se apartan por la derecha (la lámina siempre los dibuja después de la última " +
                 "pose) y ANTES de agrupar, así que funciona aunque el proyectil se solape en " +
                 "horizontal con el personaje — el caso que 'frames' por sí solo no arregla, " +
                 "porque para cuando se reconcilia la cuenta el orbe ya se ha fundido en la celda " +
                 "del ataque, estirándola y descentrando la pose.\n\n" +
                 "Cada uno se exporta como '<Personaje>_<Fila>_Prop.png' y el primero es el que " +
                 "la ficha del enemigo convierte en proyectil. 0 = ninguno.")]
        [Min(0)] public int propBlobs;

        [Tooltip("Reparte la fila en 'frames' columnas iguales en vez de buscar manchas conexas. " +
                 "Para filas cuyos frames se pisan en horizontal — polvo de un pisotón, un " +
                 "estallido de energía, hojas que salen volando — donde la detección por contenido " +
                 "junta dos dibujos en uno. Necesita que los frames estén dibujados en rejilla.")]
        public bool evenSplit;
    }

    /// <summary>
    /// Un estado extra que no tiene fila propia, hecho con un trozo de otra fila.
    ///
    /// Existe porque una lámina casi nunca dibuja todos los estados que el AnimatorController
    /// necesita: lo normal es que traiga un salto completo y ningún dibujo de caída, o que el
    /// controller tenga un ataque agachado que en la lámina no está. En vez de dejar esos estados
    /// con el arte viejo (lo que se ve como una mezcla de dos personajes), se derivan de la fila
    /// más cercana.
    /// </summary>
    [Serializable]
    public class DerivedClip
    {
        [Tooltip("Nombre del estado que se genera. Debe coincidir con el estado del " +
                 "AnimatorController al que quieres que llegue.")]
        public string state = "Fall";

        [Tooltip("Estado de origen: el 'state' de una de las filas de arriba.")]
        public string fromState = "Jump";

        [Tooltip("Índice del primer frame que se toma de esa fila (0 = el primero).")]
        [Min(0)] public int firstFrame;

        [Tooltip("Cuántos frames se toman. 0 = hasta el final de la fila.")]
        [Min(0)] public int frameCount;

        [Min(0.1f)] public float fps = 10f;

        public bool loop = true;

        [Tooltip("Recorre el tramo elegido al revés (del último frame al primero). Para un cierre " +
                 "que reutiliza los mismos dibujos de una apertura pero en sentido contrario, sin " +
                 "que la lámina tenga que dibujar la secuencia dos veces.")]
        public bool reverse;
    }

    /// <summary>
    /// La configuración de una hoja de sprites, guardada como asset.
    ///
    /// <b>Este asset es el punto de todo el pipeline.</b> La rejilla, el mapeo fila→estado y los
    /// fps se escriben una vez aquí y se quedan: relanzar el corte, regenerar los clips o rehacer
    /// el prefab no exige volver a deducir nada. Una sesión futura solo tiene que abrir el asset,
    /// no releer la lámina.
    /// </summary>
    [CreateAssetMenu(fileName = "NuevaHoja.sheet", menuName = "RedMagic/Pipeline/Sprite Sheet Recipe")]
    public class SpriteSheetRecipe : ScriptableObject
    {
        [Header("Origen")]
        [Tooltip("La lámina tal cual llega. No se toca: el corte escribe hojas nuevas y limpias.")]
        public Texture2D sheet;

        [Tooltip("Nombre del personaje. Da nombre a sprites, clips, controller y carpeta.")]
        public string characterName = "NuevoPersonaje";

        [Tooltip("Vacío = Assets/Art/Characters/<characterName>.")]
        public string outputFolder = "";

        [Header("Corte")]
        public SliceMode sliceMode = SliceMode.AutoBounds;

        [Tooltip("Filas de la lámina, de arriba abajo. Cada una es un estado.")]
        public SheetRow[] rows =
        {
            new SheetRow { state = "Idle",   fps = 8f,  loop = true },
            new SheetRow { state = "Attack", fps = 12f, loop = false },
            new SheetRow { state = "Hurt",   fps = 12f, loop = false },
            new SheetRow { state = "Death",  fps = 8f,  loop = false },
        };

        [Tooltip("Solo en modo Grid: columnas de la rejilla.")]
        [Min(1)] public int columns = 5;

        [Header("Recorte previo (píxeles, como se ve la imagen)")]
        [Tooltip("Franja izquierda que se ignora ANTES de analizar nada. Es lo que quita la " +
                 "columna de rótulos (IDLE, WALK, …) que el artista deja pintada en la lámina: si " +
                 "se deja, cuenta como contenido, se lleva un frame por delante y además su color " +
                 "puede colarse en la detección del fondo.")]
        [Min(0)] public int cropLeft;

        [Min(0)] public int cropRight;
        [Min(0)] public int cropTop;
        [Min(0)] public int cropBottom;

        [Header("Fondo")]
        [Tooltip("Si la lámina no trae alfa real (un JPG, o un PNG con el damero pintado encima), " +
                 "se deduce el fondo de los bordes y se pasa a transparente. Con alfa real, " +
                 "déjalo apagado: es más fiel.")]
        public bool keyBackground = true;

        [Tooltip("Cuánto puede alejarse un pixel del color de fondo y seguir contando como fondo. " +
                 "Súbelo si quedan restos del damero, bájalo si se come el arte.")]
        [Range(0f, 0.5f)] public float backgroundTolerance = 0.12f;

        [Tooltip("Con alfa real: a partir de qué alfa un pixel cuenta como contenido.")]
        [Range(0f, 1f)] public float alphaThreshold = 0.15f;

        [Tooltip("Borde suave al deducir el fondo, en píxeles desde el contorno. 0 = recorte duro, " +
                 "como siempre.\n\n" +
                 "Para arte PINTADO sobre un fondo liso y oscuro (brillos, halos, cristales), donde " +
                 "el recorte duro corta el halo a tijera y deja un cerco del color del fondo. En esa " +
                 "franja, la LUZ que el pixel añade sobre el fondo pasa a ser opacidad (el halo se " +
                 "desvanece solo) y se le quita el fondo mezclado del color. Lo que es más OSCURO " +
                 "que el fondo (la tinta del contorno, la corteza en sombra) se queda opaco, así que " +
                 "la silueta no pierde el perfil. Sobre un fondo claro (damero, blanco) no cambia " +
                 "nada: todo es más oscuro que él. Sólo con 'Key Background'.")]
        [Min(0)] public int softEdge;

        [Tooltip("Rellena los agujeros que el recorte abre DENTRO del personaje, hasta este tamaño " +
                 "(área en píxeles). 0 = no.\n\n" +
                 "En arte pintado sobre un fondo de color, las sombras del interior cogen el tono del " +
                 "fondo y el recorte las perfora: el personaje sale lleno de puntitos transparentes. " +
                 "Sólo se rellena lo que está rodeado de personaje por todos lados y es pequeño; el " +
                 "aire de verdad entre un brazo y el cuerpo es más grande y se respeta. Sólo con " +
                 "'Key Background'.")]
        [Min(0)] public int fillHoles;

        [Header("Celda")]
        [Tooltip("Píxeles de aire alrededor del contenido en cada celda.")]
        [Min(0)] public int margin = 8;

        public AnchorMode anchor = AnchorMode.BottomCenter;

        [Min(1)] public int pixelsPerUnit = 100;

        [Tooltip("Bilinear para arte pintado; Point para pixel art.")]
        public FilterMode filterMode = FilterMode.Bilinear;

        [Header("Animación")]
        public AnimRuntime runtime = AnimRuntime.Animator;

        [Tooltip("Clava OnAttackRelease / OnAttackFinished en el clip 'Attack'. Es lo que hace que " +
                 "el golpe salga en el dibujo exacto, y lo recibe EnemyAnimation. Apágalo para un " +
                 "personaje que no lleve ese componente — el jugador — o Unity avisará en cada " +
                 "reproducción de que nadie escucha el evento.")]
        public bool attackEvents = true;

        [Tooltip("Estados extra montados con trozos de las filas de arriba: la caída sacada del " +
                 "salto, el ataque agachado sacado del ataque… Ver DerivedClip.")]
        public DerivedClip[] derivedClips = new DerivedClip[0];

        [Tooltip("Ruta del SpriteRenderer VISTA DESDE EL GameObject QUE LLEVA EL ANIMATOR. En un " +
                 "enemigo generado el Animator va en la raíz y el sprite en el hijo 'Sprite', que " +
                 "es el valor por defecto. Vacía = el Animator y el SpriteRenderer están en el " +
                 "mismo objeto (así es el prefab del jugador).\n\n" +
                 "Si esto no coincide con el prefab real, los clips animan a un objeto que no " +
                 "existe: el personaje se ve, pero se queda congelado en un frame.")]
        public string rendererPath = "Sprite";

        /// <summary>Carpeta de salida efectiva.</summary>
        public string ResolvedFolder => string.IsNullOrWhiteSpace(outputFolder)
            ? $"Assets/Art/Characters/{characterName}"
            : outputFolder.TrimEnd('/');
    }
}
