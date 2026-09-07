using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Ponlo en un contenedor de decoración de fondo (p. ej. el objeto "Enviroment" del hub). Al
    /// arrancar —y con el botón del Inspector— apaga el <see cref="Collider2D"/> de cada hijo que
    /// sea <b>pura decoración</b>, para que barriles, vallas, piedras y demás props importados con
    /// collider no frenen al jugador, a los enemigos ni a los proyectiles.
    ///
    /// <b>No toca</b> un hijo que:
    /// <list type="bullet">
    /// <item>tenga un <see cref="Combat.Health"/> (el maniquí de pruebas, un destructible…),</item>
    /// <item>tenga cualquier script del juego (namespace <c>RedMagic.*</c>) en sí mismo o en un
    /// padre hasta este contenedor (caldero, tumba, cofre, spawn points…),</item>
    /// <item>use un collider en <b>trigger</b> (los triggers no frenan nada, sólo detectan),</item>
    /// <item>esté en la lista <see cref="keep"/>.</item>
    /// </list>
    /// Así el mismo contenedor puede seguir albergando objetos de juego sin romperlos.
    ///
    /// Sólo <b>desactiva</b> los colliders (no los destruye): la escena guarda el estado y es
    /// reversible con "Restaurar". Un collider desactivado sale del broadphase de la física, así
    /// que esto es además una pequeña mejora de rendimiento.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnvironmentDecorColliders : MonoBehaviour
    {
        [Tooltip("Hijos que conservan su collider aunque parezcan decoración.")]
        [SerializeField] private List<Transform> keep = new List<Transform>();

        [Tooltip("Además de apagar el collider, pasar el objeto a la capa 'BackGround'.")]
        [SerializeField] private bool moveToBackgroundLayer;

        private void Awake() => Strip(log: false);

        [ContextMenu("Apagar colliders de decoración ahora")]
        private void StripFromMenu() => Strip(log: true);

        [ContextMenu("Restaurar colliders")]
        private void Restore()
        {
            foreach (var col in GetComponentsInChildren<Collider2D>(true))
                col.enabled = true;
        }

        private void Strip(bool log)
        {
            var colliders = GetComponentsInChildren<Collider2D>(true);
            int disabled = 0;

            foreach (var col in colliders)
            {
                if (!ShouldDisable(col)) continue;

                col.enabled = false;
                disabled++;

                if (moveToBackgroundLayer)
                {
                    int backgroundLayer = LayerMask.NameToLayer("BackGround");
                    if (backgroundLayer >= 0) col.gameObject.layer = backgroundLayer;
                }
            }

            if (log)
                Debug.Log($"[EnvironmentDecorColliders] Colliders de decoración apagados: " +
                          $"{disabled}/{colliders.Length}.", this);
        }

        private bool ShouldDisable(Collider2D col)
        {
            if (col == null || !col.enabled) return false;
            if (col.isTrigger) return false;
            if (col.GetComponentInParent<Combat.Health>() != null) return false;

            // Recorre del collider hacia arriba hasta este contenedor: excepción manual o script del juego.
            for (var t = col.transform; t != null; t = t.parent)
            {
                if (keep.Contains(t)) return false;

                var scripts = t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < scripts.Length; i++)
                {
                    var script = scripts[i];
                    if (script == null || ReferenceEquals(script, this)) continue;

                    var ns = script.GetType().Namespace;
                    if (ns != null && ns.StartsWith("RedMagic")) return false;
                }

                if (t == transform) break;
            }

            return true;
        }
    }
}
