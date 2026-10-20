using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Survival
{
    /// <summary>
    /// Un sitio donde aparecen enemigos en Supervivencia. Se colocan a mano por el mapa, tantos como se quiera, cada uno
    /// de un tipo: <b>Volador</b> (en el aire), <b>Suelo</b> (sobre el suelo o una plataforma: el enemigo cae desde aquí),
    /// <b>Torreta</b> (donde se planta una torreta) y <b>Jefe</b> (si no hay ninguno, el jefe sale en uno de Suelo).
    /// <see cref="SurvivalManager"/> los encuentra solos en la escena: no hay que arrastrarlos a ninguna lista.
    /// </summary>
    public class SurvivalSpawnPoint : MonoBehaviour
    {
        public SurvivalSpawnKind kind = SurvivalSpawnKind.Ground;

        private static readonly List<SurvivalSpawnPoint> All = new List<SurvivalSpawnPoint>();

        /// <summary>Los puntos activos ahora mismo.</summary>
        public static IReadOnlyList<SurvivalSpawnPoint> Active => All;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static Color ColorOf(SurvivalSpawnKind kind) => kind switch
        {
            SurvivalSpawnKind.Flying => new Color(0.45f, 0.8f, 1f),
            SurvivalSpawnKind.Ground => new Color(0.45f, 1f, 0.45f),
            SurvivalSpawnKind.Turret => new Color(1f, 0.7f, 0.25f),
            _ => new Color(1f, 0.3f, 0.35f),
        };

        private void OnDrawGizmos()
        {
            var c = ColorOf(kind);
            Gizmos.color = c;
            Vector3 p = transform.position;
            switch (kind)
            {
                case SurvivalSpawnKind.Flying:
                    Gizmos.DrawWireSphere(p, 0.6f);
                    Gizmos.DrawLine(p + new Vector3(-0.9f, 0.3f), p + new Vector3(0.9f, 0.3f));
                    break;
                case SurvivalSpawnKind.Turret:
                    Gizmos.DrawWireCube(p, new Vector3(1f, 0.6f, 0f));
                    Gizmos.DrawLine(p, p + Vector3.up * 0.9f);
                    break;
                case SurvivalSpawnKind.Boss:
                    Gizmos.DrawWireSphere(p, 1.4f);
                    Gizmos.DrawLine(p, p + Vector3.down * 2f);
                    break;
                default:
                    Gizmos.DrawWireCube(p, new Vector3(0.8f, 1.2f, 0f));
                    Gizmos.DrawLine(p, p + Vector3.down * 1.5f);
                    break;
            }
#if UNITY_EDITOR
            UnityEditor.Handles.color = c;
            UnityEditor.Handles.Label(p + Vector3.up * 1.1f, kind switch
            {
                SurvivalSpawnKind.Flying => "Volador",
                SurvivalSpawnKind.Ground => "Suelo",
                SurvivalSpawnKind.Turret => "Torreta",
                _ => "Jefe",
            });
#endif
        }
    }
}
