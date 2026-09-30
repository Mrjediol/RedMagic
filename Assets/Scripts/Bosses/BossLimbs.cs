using System.Collections.Generic;
using RedMagic.Rigging;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>Qué extremidad usa un ataque de extremidades.</summary>
    public enum LimbPick
    {
        /// <summary>Una y otra, por turnos entre ataques.</summary>
        Alternate,
        /// <summary>La que está más cerca del objetivo.</summary>
        NearestToTarget,
        /// <summary>La más lejana (se la ve venir de más lejos).</summary>
        FarthestFromTarget,
        First,
        Second,
    }

    /// <summary>
    /// Las piezas sueltas de un jefe hecho por partes que sus ataques pueden <b>lanzar</b> (los
    /// puños del Coloso). Va junto al <see cref="BossController"/> y al <see cref="RigDriver"/>.
    ///
    /// Los ataques son ScriptableObjects sin estado, así que no pueden guardar Transforms: piden la
    /// pieza aquí, por índice o por criterio (<see cref="LimbPick"/>), y la mueven en el mundo con
    /// <see cref="DriveCenter"/>. El rig deja de animarla mientras tanto y la devuelve al cuerpo
    /// con <see cref="Release"/> — o solo, si el ataque se corta a medias (cambio de fase).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossLimbs : MonoBehaviour
    {
        [Tooltip("Piezas que se pueden lanzar. Orden: 0 = la de la izquierda en la pose del prefab, " +
                 "1 = la de la derecha.")]
        [SerializeField] private List<Transform> limbs = new List<Transform>();

        [Tooltip("Dónde se agarra un objeto con cada pieza (la roca en el puño). Mismo orden que " +
                 "'limbs'. Vacío = el centro visible de la pieza.")]
        [SerializeField] private List<Transform> grips = new List<Transform>();

        [Tooltip("Radio de contacto de cada pieza, en unidades de mundo (lo que hace daño al tocar).")]
        [Min(0.1f)]
        [SerializeField] private float contactRadius = 0.9f;

        private RigDriver _rig;
        private ProceduralRig _data;
        private int _next;

        public float ContactRadius => contactRadius;
        public int Count => limbs.Count;

        /// <summary>True si hay rig y piezas: si no, los ataques de extremidades caen a su versión sin pieza.</summary>
        public bool Usable => _rig != null && _rig.enabled && _data != null && limbs.Count > 0;

        private void Awake()
        {
            _rig = GetComponent<RigDriver>();
            _data = GetComponent<ProceduralRig>();
        }

        private void OnEnable() => _next = 0;

        public Transform Get(int index) =>
            index >= 0 && index < limbs.Count ? limbs[index] : null;

        /// <summary>Elige una pieza según <paramref name="mode"/>.</summary>
        public Transform Pick(LimbPick mode, Vector2 target)
        {
            if (limbs.Count == 0) return null;

            switch (mode)
            {
                case LimbPick.First: return limbs[0];
                case LimbPick.Second: return limbs[Mathf.Min(1, limbs.Count - 1)];
                case LimbPick.Alternate:
                    var limb = limbs[_next % limbs.Count];
                    _next++;
                    return limb;
            }

            Transform best = null;
            float bestDistance = 0f;
            foreach (var l in limbs)
            {
                if (l == null) continue;
                float d = (Center(l) - target).sqrMagnitude;
                bool better = best == null || (mode == LimbPick.NearestToTarget ? d < bestDistance : d > bestDistance);
                if (!better) continue;
                best = l;
                bestDistance = d;
            }

            return best;
        }

        /// <summary>La otra pieza (para ataques a dos manos).</summary>
        public Transform Other(Transform limb)
        {
            foreach (var l in limbs) if (l != null && l != limb) return l;
            return limb;
        }

        /// <summary>Centro visible de la pieza en el mundo, ahora.</summary>
        public Vector2 Center(Transform limb) => _data != null ? _data.PartCenter(limb) : (Vector2)limb.position;

        /// <summary>Dónde estaría el centro de la pieza si estuviera en su sitio en el cuerpo, ahora.</summary>
        public Vector2 HomeCenter(Transform limb)
        {
            if (_data == null) return limb.position;
            Vector2 offset = Center(limb) - (Vector2)limb.position;
            return _data.RestWorldPosition(limb) + offset;
        }

        /// <summary>Lleva el <b>centro</b> de la pieza a <paramref name="center"/> con giro local <paramref name="localAngle"/>.</summary>
        public void DriveCenter(Transform limb, Vector2 center, float localAngle)
        {
            if (_rig == null || limb == null) return;

            // El giro se aplica primero para medir bien la distancia pivote→centro con la pose nueva.
            _rig.DrivePartAngle(limb, limb.position, localAngle);
            Vector2 offset = Center(limb) - (Vector2)limb.position;
            _rig.DrivePartAngle(limb, center - offset, localAngle);
        }

        /// <summary>Giro local actual de la pieza.</summary>
        public float LocalAngle(Transform limb) => limb.localEulerAngles.z;

        /// <summary>Giro local de la pieza en su pose de reposo.</summary>
        public float RestAngle(Transform limb)
        {
            if (_data == null) return 0f;
            _data.TryGetRest(limb, out _, out float angle, out _);
            return angle;
        }

        /// <summary>
        /// Giro local para que la parte útil de la pieza (<see cref="RigPart.pointAngle"/>: los nudillos)
        /// mire hacia <paramref name="worldDirection"/>. Pasa por el espacio del padre con su escala,
        /// así que con el cuerpo volteado sale bien sin más.
        /// </summary>
        public float AngleToward(Transform limb, Vector2 worldDirection)
        {
            if (limb == null || limb.parent == null || worldDirection.sqrMagnitude < 0.0001f) return RestAngle(limb);

            Vector2 local = limb.parent.InverseTransformVector(worldDirection);
            var data = _data != null ? _data.FindPart(limb) : null;
            float point = data != null ? data.pointAngle : 0f;
            return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - point;
        }

        /// <summary>Donde agarra la pieza (ver <c>grips</c>); sin punto de agarre, la propia pieza.</summary>
        public Transform Grip(Transform limb)
        {
            int index = limbs.IndexOf(limb);
            return index >= 0 && index < grips.Count && grips[index] != null ? grips[index] : limb;
        }

        /// <summary>Devuelve la pieza al cuerpo en <paramref name="seconds"/>.</summary>
        public void Release(Transform limb, float seconds)
        {
            if (_rig != null) _rig.ReleasePart(limb, seconds);
        }
    }
}
