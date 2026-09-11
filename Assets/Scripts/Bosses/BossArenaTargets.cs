using System;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Puntos fijos de la arena, colocados a mano en la escena, para los ataques que no apuntan al
    /// jugador sino a sitios concretos (<see cref="PlatformDenialAttack"/>: el fuego que niega las
    /// plataformas).
    ///
    /// Va en el jefe de la escena y no en el asset del ataque: un ScriptableObject no puede
    /// referenciar objetos de escena, y cada arena tiene sus plataformas en otro sitio.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossArenaTargets : MonoBehaviour
    {
        [Tooltip("GameObjects vacíos colocados donde debe caer cada proyectil. Uno por proyectil, en " +
                 "orden de lanzamiento. Con 'snapToSurface' en el ataque basta dejarlos un poco por " +
                 "encima de la plataforma: el fuego se asienta solo sobre ella.")]
        [SerializeField] private Transform[] targets = Array.Empty<Transform>();

        public Transform[] Targets => targets;

        private void OnDrawGizmos()
        {
            if (targets == null) return;

            Gizmos.color = new Color(0.4f, 1f, 0.25f, 0.9f);
            foreach (var target in targets)
                if (target != null)
                    Gizmos.DrawWireCube(target.position, new Vector3(0.6f, 0.6f, 0f));
        }
    }
}
