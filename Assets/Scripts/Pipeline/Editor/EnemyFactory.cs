using System.Text;
using RedMagic.Bosses.EditorTools;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Paso 3 del pipeline: <see cref="EnemyRecipe"/> → prefab de enemigo completo.
    ///
    /// Es a los enemigos lo que <c>BossAuthoring</c> es a los jefes: <b>el sitio donde vive todo lo
    /// que comparten</b>. La ficha dice qué tiene este de particular; la fábrica monta el cuerpo,
    /// el collider, <c>Health</c>, <c>Knockback</c>, <c>HitFlash</c>, <c>Corpse</c>,
    /// <c>CurrencyDropper</c> y el trío de enemigo — <see cref="EnemyStats"/>,
    /// <see cref="EnemyBrain"/>, <see cref="EnemyAnimation"/>, <see cref="EnemyAttack"/> — sin que
    /// nadie tenga que acordarse.
    ///
    /// <b>Los valores afinados a mano mandan.</b> La ficha siembra el <see cref="EnemyStats"/> la
    /// primera vez; a partir de ahí regenerar el arte no los pisa, igual que un pack de jefe no
    /// pisa un asset ya existente. Para volver a los de la ficha está el menú de "resetear".
    /// </summary>
    public static class EnemyFactory
    {
        public static GameObject Generate(EnemyRecipe recipe, StringBuilder log, bool resetTuning = false)
        {
            if (recipe == null)
            {
                log.AppendLine("[EnemyFactory] Sin receta.");
                return null;
            }

            SheetSlicer.EnsureFolder(recipe.ResolvedFolder);

            string path = recipe.PrefabPath;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // Se edita sobre una instancia y se vuelve a guardar: así un prefab ya colocado en
            // escenas conserva su GUID y sus referencias.
            var root = existing != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(existing)
                : new GameObject($"Enemy_{recipe.enemyName}");

            log.AppendLine($"[EnemyFactory] {(existing != null ? "Actualizando" : "Creando")} {path}");

            BossAuthoring.TrySetTag(root, recipe.tag);

            var visual = Visual(root, recipe, log);
            var stats = Stats(root, recipe, log, resetTuning);
            ApplyBody(root, recipe, stats, visual, log);
            ApplyShared(root, recipe);
            ApplyAnimation(root, recipe, log);
            ApplyProjectile(root, recipe, stats, log);
            Retire(root, log);

            // Otra vez al final: al sembrar aún no existían el collider ni el Corpse, y lo que
            // EnemyStats les empuja (fase por el terreno, tinte del cadáver) quedaba sólo en
            // runtime. Así el prefab guardado ya dice la verdad.
            stats.Apply();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            log.AppendLine($"[EnemyFactory] Listo: {path}");
            return saved;
        }

        // ============================================================ hijo visual

        /// <summary>
        /// El hijo "Sprite". Está separado de la raíz porque así la escala del arte no toca al
        /// collider, y porque el Animator vive en la raíz (para recibir los AnimationEvent) y anima
        /// a este hijo por ruta.
        /// </summary>
        private static GameObject Visual(GameObject root, EnemyRecipe recipe, StringBuilder log)
        {
            var child = root.transform.Find(AnimClipBuilder.RendererPath);
            var go = child != null ? child.gameObject : new GameObject(AnimClipBuilder.RendererPath);
            if (child == null) go.transform.SetParent(root.transform, false);

            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * recipe.spriteScale;

            var renderer = Get<SpriteRenderer>(go);
            renderer.sortingOrder = recipe.sortingOrder;

            var idle = IdleSprite(recipe);
            if (idle != null) renderer.sprite = idle;
            else log.AppendLine("  AVISO: no hay sprite de reposo; el prefab sale sin arte.");

            return go;
        }

        /// <summary>Primer frame del estado de reposo: es el sprite en el que se basa el collider.</summary>
        private static Sprite IdleSprite(EnemyRecipe recipe)
        {
            if (recipe.art == null) return null;

            string state = AnimClipBuilder.Idle;
            if (recipe.art.rows is { Length: > 0 }) state = recipe.art.rows[0].state;

            string sheet = $"{recipe.art.ResolvedFolder}/{recipe.art.characterName}_{state}.png";
            string name = SheetSlicer.SpriteName(recipe.art.characterName, state, 0);

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheet))
                if (asset is Sprite sprite && sprite.name == name) return sprite;

            return null;
        }

        // ============================================================ valores

        /// <summary>
        /// Siembra el <see cref="EnemyStats"/> desde la ficha la primera vez y lo respeta después.
        /// Es el punto donde se decide que quien afina gana: el arte se puede regenerar mil veces
        /// sin perder los números que alguien ajustó jugando.
        /// </summary>
        private static EnemyStats Stats(GameObject root, EnemyRecipe recipe, StringBuilder log, bool seed)
        {
            // Lo que decide sembrar es si el bloque de valores existía ya, no si existía el prefab:
            // un prefab del sistema anterior tiene prefab pero no EnemyStats, y ahí hay que
            // sembrar o el enemigo saldría con los valores por defecto de la clase.
            bool hadStats = root.GetComponent<EnemyStats>() != null;
            var stats = Get<EnemyStats>(root);
            seed |= !hadStats;

            if (seed)
            {
                // Clonado, no la misma instancia: si el prefab y la ficha compartieran objeto,
                // afinar el enemigo editaría la ficha por la espalda.
                stats.EditorSetTuning(recipe.tuning.Clone());
                EditorUtility.SetDirty(stats);
                log.AppendLine($"  EnemyStats sembrado desde la ficha ({recipe.tuning.archetype}).");
            }
            else
            {
                log.AppendLine("  EnemyStats conservado (los valores afinados a mano mandan).");
            }

            return stats;
        }

        // ============================================================ cuerpo

        private static void ApplyBody(GameObject root, EnemyRecipe recipe, EnemyStats stats,
                                      GameObject visual, StringBuilder log)
        {
            var tuning = stats.Tuning;

            var body = Get<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = tuning.Flies ? 0f : tuning.gravityScale;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var box = Get<BoxCollider2D>(root);
            var size = recipe.colliderSize;

            if (size.sqrMagnitude <= 0.0001f)
            {
                // Deducido del arte: alto casi completo y ancho recortado, porque la copa de un
                // árbol o las alas de un bicho no deberían chocar contra las paredes.
                var renderer = visual.GetComponent<SpriteRenderer>();
                var bounds = renderer != null && renderer.sprite != null
                    ? renderer.sprite.bounds.size * recipe.spriteScale
                    : new Vector3(1f, 1.5f, 0f);

                size = new Vector2(Mathf.Max(0.2f, bounds.x * 0.55f), Mathf.Max(0.2f, bounds.y * 0.9f));
                log.AppendLine($"  collider deducido del sprite: {size}");
            }

            box.size = size;

            // El collider se coloca según DÓNDE ESTÉ EL PIVOTE del arte, no por convención. Con
            // `BottomCenter` (lo normal en un personaje) el origen está a los pies, así que el
            // collider sube desde el suelo y colocar al enemigo es dejarlo sobre el terreno. Con
            // `Center` — lo que usa cualquier bicho que flota, porque no tiene pies sobre los que
            // apoyarse — el origen ya está en mitad del cuerpo y hay que centrarlo: dar por hecho
            // que era BottomCenter dejaba el collider medio cuerpo por encima del dibujo, que se
            // traduce en disparos que atraviesan al bicho y golpes que impactan en el aire.
            bool centeredPivot = recipe.art != null && recipe.art.anchor == AnchorMode.Center;

            box.offset = recipe.colliderOffset.sqrMagnitude > 0.0001f
                ? recipe.colliderOffset
                : centeredPivot ? Vector2.zero : new Vector2(0f, size.y * 0.5f);
        }

        // ============================================================ componentes compartidos

        private static void ApplyShared(GameObject root, EnemyRecipe recipe)
        {
            // Vida y retroceso los rellena EnemyStats.Apply() con los valores centralizados; aquí
            // sólo se garantiza que los componentes existan.
            Get<Health>(root);
            Get<Knockback>(root);
            Get<HitFlash>(root);
            Get<Corpse>(root);

            new BossAuthoring.Fields(Get<CurrencyDropper>(root))
                .Set("tier", (int)recipe.tier)
                .Apply();

            Get<EnemyBrain>(root);
            Get<EnemyAttack>(root);
        }

        // ============================================================ animación

        private static void ApplyAnimation(GameObject root, EnemyRecipe recipe, StringBuilder log)
        {
            Get<EnemyAnimation>(root);
            if (recipe.art == null) return;

            var visual = root.transform.Find(AnimClipBuilder.RendererPath);

            if (recipe.art.runtime == AnimRuntime.Animator)
            {
                string path = $"{recipe.art.ResolvedFolder}/{recipe.art.characterName}.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                if (controller == null)
                {
                    log.AppendLine($"  AVISO: no existe '{path}'. Genera los clips antes del prefab.");
                }
                else
                {
                    // El Animator va en la RAÍZ: los AnimationEvent del ataque sólo llegan a
                    // componentes de su propio GameObject, y ahí es donde está EnemyAnimation.
                    var animator = Get<Animator>(root);
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.updateMode = AnimatorUpdateMode.Normal;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    log.AppendLine("  Animator en la raíz, animando al hijo 'Sprite' por ruta.");
                }

                // Restos de la versión anterior, cuando el Animator colgaba del hijo.
                if (visual != null)
                {
                    var stale = visual.GetComponent<Animator>();
                    if (stale != null) Object.DestroyImmediate(stale, true);

                    var staleFlipbook = visual.GetComponent<SpriteStateMachine>();
                    if (staleFlipbook != null) Object.DestroyImmediate(staleFlipbook, true);
                }

                return;
            }

            if (visual == null) return;

            var machine = Get<SpriteStateMachine>(visual.gameObject);
            AnimClipBuilder.FillStateMachine(machine, recipe.art, ReloadSliced(recipe.art), log);

            var staleAnimator = root.GetComponent<Animator>();
            if (staleAnimator != null) Object.DestroyImmediate(staleAnimator, true);
        }

        // ============================================================ proyectil

        /// <summary>
        /// Convierte el objeto suelto de la fila de ataque (la piedra que el bicho lanza, dibujada
        /// aparte de la mano) en el prefab del proyectil, y lo mete en el bloque de valores.
        /// </summary>
        private static void ApplyProjectile(GameObject root, EnemyRecipe recipe, EnemyStats stats,
                                            StringBuilder log)
        {
            if (!stats.Tuning.IsRanged) return;

            EnsureMuzzle(root, stats, log);

            if (recipe.art == null) return;

            var prop = LoadPropSprite(recipe.art, recipe.projectilePropState);
            if (prop == null)
            {
                log.AppendLine($"  AVISO: arquetipo a distancia pero la fila " +
                               $"'{recipe.projectilePropState}' no exportó ningún objeto suelto. " +
                               $"El proyectil saldrá construido en código.");
                return;
            }

            string folder = $"Assets/Prefab/Fx/Enemies/{recipe.enemyName}";
            var prefab = ProjectilePrefabFactory.BuildFromSprite(
                folder, $"{recipe.enemyName}_Projectile", prop, recipe.projectileScale);

            stats.Tuning.projectile.prefab = prefab;
            stats.Tuning.projectileSprite = prop;   // respaldo si algún día se quita el prefab
            EditorUtility.SetDirty(stats);

            MirrorSpecOntoPrefab(prefab, stats.Tuning, log);

            log.AppendLine($"  proyectil: '{prefab.name}' desde el prop de '{recipe.projectilePropState}'.");
        }

        /// <summary>
        /// Crea el hijo "Muzzle" (ver <see cref="RedMagic.Enemies.EnemyAttack.MuzzleChildName"/>) la
        /// primera vez que este enemigo se genera a distancia, sembrado en la posición del
        /// <c>muzzleOffset</c> numérico para que el disparo no cambie de sitio al añadirlo. Es
        /// idempotente y de mano gana: si ya existe (porque alguien lo arrastró a la boca del
        /// dragón en el Prefab Editor) no se toca nunca más, igual que el resto de valores afinados
        /// a mano en este pipeline.
        /// </summary>
        private static void EnsureMuzzle(GameObject root, EnemyStats stats, StringBuilder log)
        {
            const string name = RedMagic.Enemies.EnemyAttack.MuzzleChildName;
            if (root.transform.Find(name) != null) return;

            var muzzle = new GameObject(name);
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = stats.Tuning.projectile.muzzleOffset;

            log.AppendLine($"  '{name}' creado en {(Vector2)muzzle.transform.localPosition} " +
                           "(arrástralo en el Prefab Editor a donde deba nacer el disparo).");
        }

        /// <summary>
        /// Copia los números del <c>ProjectileSpec</c> de la ficha sobre el componente
        /// <c>Projectile</c> del prefab.
        ///
        /// El motivo: <c>ProjectileFactory.Spawn</c> llama a <c>Configure</c>/<c>ConfigureBehaviour</c>
        /// con los valores del spec en cada disparo, así que <b>los campos serializados del prefab
        /// no se usan</b> para un enemigo del pipeline — el spec siempre gana. Sin este volcado el
        /// prefab mostraba una velocidad cualquiera (140, un resto de una versión anterior) que no
        /// tenía nada que ver con la real, y tocarla en el prefab no hacía nada. Ahora el prefab es
        /// un espejo fiel: <b>la perilla sigue siendo <c>EnemyStats ▸ Tuning ▸ projectile</c></b>,
        /// pero al menos lo que se ve en el prefab es la verdad.
        /// </summary>
        private static void MirrorSpecOntoPrefab(GameObject prefab, RedMagic.Enemies.EnemyTuning tuning,
                                                 StringBuilder log)
        {
            var projectile = prefab.GetComponent<RedMagic.Gameplay.Projectile>();
            if (projectile == null) return;

            var spec = tuning.projectile;
            var so = new SerializedObject(projectile);
            void Set(string field, float value) { var p = so.FindProperty(field); if (p != null) p.floatValue = value; }
            void SetInt(string field, int value) { var p = so.FindProperty(field); if (p != null) p.intValue = value; }

            Set("speed", spec.speed);
            Set("lifetime", spec.lifetime);
            Set("damage", tuning.attackDamage);
            Set("knockbackMultiplier", tuning.attackKnockbackMultiplier);
            Set("homingTurnRate", spec.homingTurnRate);
            Set("homingRange", spec.homingRange);
            Set("arcGravity", spec.arcGravity);
            Set("impactRadius", spec.impactRadius);
            Set("impactDamage", spec.impactDamage);
            SetInt("pierceCount", spec.pierce);

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefab);

            log.AppendLine($"  proyectil espejado desde la ficha: speed={spec.speed}, lifetime={spec.lifetime}.");
        }

        private static Sprite LoadPropSprite(SpriteSheetRecipe art, string state)
        {
            foreach (var name in new[]
                     {
                         SheetSlicer.PropSpriteName(art.characterName, state, 0, 1),
                         SheetSlicer.PropSpriteName(art.characterName, state, 0, 2),
                     })
            {
                string sheet = $"{art.ResolvedFolder}/{name}.png";
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheet))
                    if (asset is Sprite sprite && sprite.name == name) return sprite;
            }

            return null;
        }

        // ============================================================ limpieza

        /// <summary>
        /// Quita los componentes del sistema anterior. No es opcional: <c>EnemyController</c> y
        /// <see cref="EnemyBrain"/> escriben los dos la velocidad del cuerpo cada FixedUpdate, así
        /// que convivir significa que se pisan y el enemigo se queda temblando en el sitio.
        ///
        /// <c>EnemyController</c> sigue existiendo en el proyecto para los enemigos antiguos y los
        /// esbirros de jefe; sólo se retira de los que genera el pipeline nuevo.
        /// </summary>
        private static void Retire(GameObject root, StringBuilder log)
        {
            foreach (var type in new[]
                     {
                         typeof(RedMagic.Gameplay.EnemyController),
                         typeof(RedMagic.Combat.RangedAttack),
                     })
            {
                var component = root.GetComponent(type);
                if (component == null) continue;

                Object.DestroyImmediate(component, true);
                log.AppendLine($"  retirado {type.Name} (lo sustituye EnemyBrain).");
            }
        }

        // ============================================================ utilidades

        /// <summary>
        /// Vuelve a leer los sprites ya cortados sin rehacer el corte, para poder generar el prefab
        /// en una sesión distinta a la que cortó la lámina.
        /// </summary>
        public static SheetSlicer.Result ReloadSliced(SpriteSheetRecipe recipe)
        {
            var result = new SheetSlicer.Result();

            foreach (var row in recipe.rows)
            {
                string sheet = $"{recipe.ResolvedFolder}/{recipe.characterName}_{row.state}.png";
                var assets = AssetDatabase.LoadAllAssetsAtPath(sheet);
                if (assets == null || assets.Length == 0) continue;

                var sprites = new System.Collections.Generic.List<Sprite>();
                for (int i = 0; ; i++)
                {
                    string name = SheetSlicer.SpriteName(recipe.characterName, row.state, i);
                    Sprite found = null;

                    foreach (var asset in assets)
                        if (asset is Sprite sprite && sprite.name == name) { found = sprite; break; }

                    if (found == null) break;
                    sprites.Add(found);
                }

                if (sprites.Count > 0) result.ByState[row.state] = sprites;
            }

            return result;
        }

        /// <summary>Componente existente o recién añadido. Nunca destruye lo que ya hay.</summary>
        private static T Get<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
