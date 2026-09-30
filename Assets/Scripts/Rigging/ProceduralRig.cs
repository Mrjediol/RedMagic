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

        [Tooltip("Coz del arma: grados que sube el cañón al disparar.")]
        public float recoilAngle = 22f;

        [Tooltip("Coz del arma: cuánto retrocede la parte a lo largo del cañón (unidades locales).")]
        public float recoilDistance = 0.3f;

        [Tooltip("Empujón del cuerpo entero hacia atrás al disparar (unidades locales).")]
        public float bodyRecoil = 0.15f;

        [Header("Golpe recibido")]
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
            foreach (var p in parts) Add(p != null ? p.part : null);
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
        /// Deja todo como en el prefab, incluida la jerarquía (la muerte desmonta las piezas). Es
        /// lo que hace reutilizable a un enemigo por pool: se llama al activarse y al apagarse.
        /// </summary>
        public void RestorePose()
        {
            CaptureRest();

            // Primero padres (en orden de captura el cuerpo va antes que sus piezas), luego pose.
            foreach (var r in _rest)
            {
                if (r.transform == null) continue;
                if (r.transform.parent != r.parent) r.transform.SetParent(r.parent, false);
                r.transform.SetSiblingIndex(Mathf.Min(r.sibling, r.parent != null ? r.parent.childCount - 1 : 0));
            }

            foreach (var r in _rest)
            {
                if (r.transform == null) continue;
                r.transform.localPosition = r.position;
                r.transform.localRotation = r.rotation;
                r.transform.localScale = r.scale;
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
