using System.Collections;
using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Banco de pruebas de Play del pipeline de disparo. Ponlo en un GameObject vacío de una
    /// escena y dale a Play: dispara las 4 configuraciones (sin modificadores, sólo trayectoria,
    /// sólo forma, las tres capas) contra un maniquí que crea delante, y deja en consola qué
    /// proyectiles vivos hay, su elemento y si persiguen.
    ///
    /// No es parte del juego: sólo verificación manual. La comprobación de la resolución en sí
    /// (pasos 1-4 + herencia del split) va sin física en <c>ShotPipelineCheck</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShotPipelineHarness : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private TrajectoryModifier autoAim;
        [SerializeField] private ShapeModifier splitFive;
        [SerializeField] private ElementModifier ice;
        [SerializeField] private ElementModifier fire;

        [Tooltip("Capa contra la que impactan los proyectiles (la del maniquí).")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("Segundos entre cada configuración.")]
        [SerializeField] private float stepDelay = 1.5f;

        [SerializeField] private bool runOnStart = true;

        private GameObject _dummy;

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        [ContextMenu("Run all configs")]
        private void RunAllFromMenu() => StartCoroutine(RunAll());

        private IEnumerator RunAll()
        {
            yield return Config("A · sin modificadores", null, null, null);
            yield return Config("B · sólo trayectoria (auto-mira)", autoAim, null, null);
            yield return Config("C · sólo forma (split 5)", null, splitFive, null);
            yield return Config("D · trayectoria + forma + Hielo", autoAim, splitFive, ice);
            yield return Config("D' · elemento cambia a Fuego (último gana)", autoAim, splitFive, fire);
            Debug.Log("[ShotPipelineHarness] terminado.");
        }

        private IEnumerator Config(string label, TrajectoryModifier trajectory, ShapeModifier shape,
                                   ElementModifier element)
        {
            ClearProjectiles();
            SpawnDummy();

            var inventory = new WeaponInventory();
            inventory.SetWeapon(weapon);
            if (trajectory != null) inventory.EquipTrajectory(trajectory);
            if (shape != null) inventory.EquipShape(shape);
            if (element != null) inventory.EquipElement(element);

            var resolved = ShotResolver.Resolve(inventory);
            var ctx = new ShotContext(gameObject, this, null, hitLayers, 1, Vector2.right, "Player");
            ShotResolver.Fire(inventory, ctx);

            yield return new WaitForSeconds(0.05f);
            int afterFire = CountProjectiles(out var elementsFire, out int homingFire);

            yield return new WaitForSeconds(stepDelay);
            int afterImpact = CountProjectiles(out var elementsImpact, out int homingImpact);

            Debug.Log($"[ShotPipelineHarness] {label}\n" +
                      $"  plan: count={resolved.projectileCount} homing={resolved.homingTurnRate:0} " +
                      $"split={resolved.splitCount} element={resolved.element}\n" +
                      $"  tras disparar: {afterFire} proyectil(es), homing={homingFire}, elementos=[{string.Join(",", elementsFire)}]\n" +
                      $"  tras impacto:  {afterImpact} proyectil(es), homing={homingImpact}, elementos=[{string.Join(",", elementsImpact)}]");
        }

        private void SpawnDummy()
        {
            if (_dummy != null) Destroy(_dummy);

            _dummy = new GameObject("Shot Dummy");
            _dummy.transform.position = transform.position + Vector3.right * 4f;
            _dummy.layer = FirstLayerIn(hitLayers);

            var box = _dummy.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;

            _dummy.AddComponent<Health>();
        }

        private static int FirstLayerIn(LayerMask mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask.value & (1 << i)) != 0) return i;
            return 0;
        }

        private static void ClearProjectiles()
        {
            foreach (var projectile in Object.FindObjectsByType<ShotProjectile>(FindObjectsSortMode.None))
                Destroy(projectile.gameObject);
        }

        private static int CountProjectiles(out List<ElementId> elements, out int homing)
        {
            elements = new List<ElementId>();
            homing = 0;
            var all = Object.FindObjectsByType<ShotProjectile>(FindObjectsSortMode.None);
            foreach (var projectile in all)
            {
                elements.Add(projectile.Element);
                if (projectile.IsHoming) homing++;
            }
            return all.Length;
        }
    }
}
