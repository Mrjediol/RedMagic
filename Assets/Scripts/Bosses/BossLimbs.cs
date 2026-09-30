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

        /// <summary>Lleva el <b>centro</b> de la pieza a <paramref name="center"/>, girada <paramref name="spin"/> grados.</summary>
        public void DriveCenter(Transform limb, Vector2 center, float spin)
        {
            if (_rig == null || limb == null) return;

            // El giro se aplica primero para medir bien la distancia pivote→centro con la pose nueva.
            _rig.DrivePart(limb, limb.position, spin);
            Vector2 offset = Center(limb) - (Vector2)limb.position;
            _rig.DrivePart(limb, center - offset, spin);
        }

        /// <summary>Devuelve la pieza al cuerpo en <paramref name="seconds"/>.</summary>
        public void Release(Transform limb, float seconds)
        {
            if (_rig != null) _rig.ReleasePart(limb, seconds);
        }
    }
}
