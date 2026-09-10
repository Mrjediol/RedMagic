using System.Text;
using RedMagic.Combat;
using RedMagic.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Paso 3-bis: vestir un prefab que ya existe.
    ///
    /// Es el camino para sustituir un placeholder — el círculo rojo de un proyectil, la caja gris
    /// de un enemigo — por arte de verdad <b>sin tocar el juego</b>. Cambia el sprite, engancha el
    /// controller (o el flipbook) y, si el objeto tiene <c>Health</c>, le pone el
    /// <see cref="EnemyAnimation"/> que traduce su estado a animación.
    ///
    /// No borra ni reconfigura nada más: colliders, scripts, rigidbody, tamaños y referencias
    /// quedan exactamente como estaban. Si el resultado no gusta, se revierte el prefab en git y
    /// la lógica no se ha enterado.
    /// </summary>
    public static class PrefabDresser
    {
        public static bool Dress(GameObject prefab, SpriteSheetRecipe recipe, float spriteScale,
                                 StringBuilder log)
        {
            if (prefab == null || recipe == null)
            {
                log.AppendLine("[PrefabDresser] Falta el prefab o la receta.");
                return false;
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Dónde vive el arte hoy. Si el placeholder es una forma en la propia raíz se respeta
            // ahí; si ya hay un hijo con SpriteRenderer, se usa ese.
            var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                var child = new GameObject("Sprite");
                child.transform.SetParent(root.transform, false);
                renderer = child.AddComponent<SpriteRenderer>();
                log.AppendLine("  no había SpriteRenderer: creado el hijo 'Sprite'.");
            }

            var holder = renderer.gameObject;

            var sliced = EnemyFactory.ReloadSliced(recipe);
            if (sliced.ByState.Count == 0)
            {
                log.AppendLine($"[PrefabDresser] '{recipe.characterName}' no está cortada todavía.");
                Object.DestroyImmediate(root);
                return false;
            }

            // Sprite base: primer frame del primer estado.
            string first = recipe.rows[0].state;
            if (sliced.ByState.TryGetValue(first, out var frames) && frames.Count > 0)
            {
                // El color del placeholder se pintaba a mano (el rojo del proyectil). El arte trae
                // el suyo, así que el tinte vuelve a blanco o se vería teñido.
                renderer.sprite = frames[0];
                renderer.color = Color.white;
            }

            if (spriteScale > 0f) holder.transform.localScale = Vector3.one * spriteScale;

            if (recipe.runtime == AnimRuntime.Animator)
            {
                string controllerPath = $"{recipe.ResolvedFolder}/{recipe.characterName}.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

                if (controller == null)
                {
                    log.AppendLine($"  AVISO: falta '{controllerPath}'. Genera los clips primero.");
                }
                else
                {
                    // El Animator va en la RAÍZ, no en el hijo del sprite: los clips animan al hijo
                    // por ruta ("Sprite") y los AnimationEvent del ataque sólo llegan a componentes
                    // de su propio GameObject, que es donde vive EnemyAnimation.
                    var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    var stale = holder != root ? holder.GetComponent<Animator>() : null;
                    if (stale != null) Object.DestroyImmediate(stale, true);

                    log.AppendLine($"  Animator (en la raíz) → {recipe.characterName}.controller");

                    if (holder.name != AnimClipBuilder.RendererPath)
                        log.AppendLine($"  AVISO: los clips animan al hijo '{AnimClipBuilder.RendererPath}' " +
                                       $"y aquí el sprite cuelga de '{holder.name}'. Renómbralo o el " +
                                       $"sprite no cambiará.");
                }
            }
            else
            {
                var machine = holder.GetComponent<SpriteStateMachine>()
                              ?? holder.AddComponent<SpriteStateMachine>();
                AnimClipBuilder.FillStateMachine(machine, recipe, sliced, log);
            }

            // Sólo tiene sentido en algo que pueda recibir daño; un proyectil no lo necesita.
            if (root.GetComponent<Health>() != null && root.GetComponent<EnemyAnimation>() == null)
            {
                root.AddComponent<EnemyAnimation>();
                log.AppendLine("  añadido EnemyAnimation (el objeto tiene Health).");
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            log.AppendLine($"[PrefabDresser] Vestido: {path}");
            return true;
        }
    }
}
