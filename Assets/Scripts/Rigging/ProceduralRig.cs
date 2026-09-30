using System;
using System.Collections.Generic;
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

        [Tooltip("Giro extra proporcional a la inclinación del cuerpo al moverse (1 = otro tanto; " +
                 "negativo = se queda atrás como un péndulo).")]
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

    /// <summary>
    /// Sprites por estado para un renderer (ojos apagados / encendidos / disparando). Vacío = ese
    /// estado no cambia el sprite.
    /// </summary>
    [Serializable]
    public class RigSpriteSwap
    {
        public SpriteRenderer renderer;
        public Sprite asleep;
        public Sprite awake;
        [Tooltip("Aviso del ataque: se pone al empezar la carga y se quita tras la coz.")]
        public Sprite attacking;

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

    /// <summary>
    /// Un gesto con nombre (el <c>gesture</c> de un ataque de jefe: "Charge", "Slam"…): durante la
    /// carga el cuerpo va hacia <see cref="windup"/>, al soltar salta a <see cref="strike"/> y luego
    /// vuelve al reposo. Todo son datos: un gesto nuevo no necesita código.
    /// </summary>
    [Serializable]
    public class RigGesture
    {
        public string name = "Charge";
        [Tooltip("Pose al final de la carga (llega con easing).")]
        public List<RigPose> windup = new List<RigPose>();
        [Tooltip("Pose del golpe: se alcanza en 'strikeTime' tras soltar.")]
        public List<RigPose> strike = new List<RigPose>();
        [Min(0.01f)] public float strikeTime = 0.08f;
        [Tooltip("Segundos de vuelta al reposo tras el golpe.")]
        [Min(0.01f)] public float recover = 0.45f;
        [Tooltip("Temblor (grados) de la raíz visual en el último 30% de la carga.")]
        public float shake = 1.5f;
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

        /// <summary>Pone a cada <see cref="RigSpriteSwap"/> el sprite del estado (si lo tiene).</summary>
        public void ShowSprites(RigSpriteState state)
        {
            foreach (var swap in spriteSwaps)
            {
                if (swap?.renderer == null) continue;
                var sprite = swap.Get(state);
                if (sprite != null) swap.renderer.sprite = sprite;
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

            for (int i = 0; i < spriteSwaps.Count && i < _restSprites.Count; i++)
            {
                var renderer = spriteSwaps[i]?.renderer;
                if (renderer != null && _restSprites[i] != null) renderer.sprite = _restSprites[i];
            }
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
