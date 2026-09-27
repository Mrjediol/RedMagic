using System.Linq;
using System.Text;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RedMagic.Fx.EditorTools
{
    /// <summary>
    /// Efectos de movimiento del jugador como sistemas de partículas (sin texturas nuevas: el círculo
    /// suave por defecto de las partículas de URP). <b>Tools ▸ RedMagic ▸ FX ▸ VFX del jugador · Generar</b>.
    ///
    /// <list type="bullet">
    /// <item><c>VFX_Dash</c> — raíz "Streak": estela de billboards estirados que se emite por distancia
    /// mientras sigue al jugador (el dash); hijo "Puff": soplo al arrancar, empujado hacia atrás.</item>
    /// <item><c>VFX_Jump</c> — "Ring": anillo aplastado de chispas pálidas que se abre hacia los lados y un
    /// poco arriba; hijo "Dust": arco de polvo.</item>
    /// <item><c>VFX_DoubleJump</c> — "Ring": anillo mágico cian saturado que se abre en horizontal;
    /// hijo "Sparks": chispas hacia arriba.</item>
    /// </list>
    /// Todos: Play On Awake y Looping apagados, espacio de mundo, Scaling Mode = Hierarchy (el espejo de
    /// <see cref="VfxOneShot.Spawn"/> los voltea), capa <c>Characters</c> (encima del nivel, debajo de la
    /// UI), y la raíz con Stop Action = Callback → vuelve al pool al apagarse. Pocas partículas y
    /// pequeñas (móvil). Color, tamaño, duración y cantidad se retocan en el propio prefab (módulos
    /// Main / Emission / Color over Lifetime del ParticleSystem).
    ///
    /// <b>Generar</b> sólo crea lo que falta (no pisa lo retocado); <b>Regenerar</b> reescribe los tres
    /// prefabs con los valores de aquí. Ambos cablean <see cref="PlayerVfx"/> en <c>Player.prefab</c> y
    /// borran los antiguos placeholders de cuadrado blanco.
    /// </summary>
    public static class PlayerVfxPack
    {
        public const string PrefabFolder = "Assets/Prefabs/Fx/Player";
        public const string DashPath = PrefabFolder + "/VFX_Dash.prefab";
        public const string JumpPath = PrefabFolder + "/VFX_Jump.prefab";
        public const string DoubleJumpPath = PrefabFolder + "/VFX_DoubleJump.prefab";

        private const string MaterialFolder = "Assets/Art/Fx/Materials";
        private const string AdditivePath = MaterialFolder + "/Fx_ParticleAdditive.mat";
        private const string AlphaPath = MaterialFolder + "/Fx_ParticleAlpha.mat";
        private const string UrpParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
        private static readonly string[] OldPlaceholders =
        {
            "Assets/Prefabs/Fx/VFX_DashWind.prefab",
            "Assets/Prefabs/Fx/VFX_DoubleJump.prefab",
        };

        private const string SortingLayer = "Characters"; // la del jugador (orden 10)
        private const int BehindPlayer = 9;
        private const int InFrontOfPlayer = 11;

        // Paleta mágica del juego: núcleo cian/turquesa que se apaga a un verde azulado oscuro.
        private static readonly Color Cyan = new(0.35f, 1f, 0.95f, 1f);
        private static readonly Color DeepTeal = new(0.05f, 0.5f, 0.55f, 1f);
        private static readonly Color PaleCyan = new(0.85f, 0.97f, 1f, 1f);
        private static readonly Color Dust = new(0.82f, 0.78f, 0.7f, 0.6f);
        private static readonly Color MagicCyan = new(0.1f, 0.95f, 1f, 1f);
        private static readonly Color MagicDeep = new(0.1f, 0.55f, 1f, 1f);

        [MenuItem("Tools/RedMagic/FX/VFX del jugador · Generar")]
        public static void Generate() => Debug.Log(Run(overwrite: false));

        [MenuItem("Tools/RedMagic/FX/VFX del jugador · Regenerar (sobrescribe)")]
        public static void Regenerate() => Debug.Log(Run(overwrite: true));

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run(bool overwrite)
        {
            var log = new StringBuilder("[PlayerVfxPack]\n");
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            var additive = EnsureMaterial(AdditivePath, additive: true, log);
            var alpha = EnsureMaterial(AlphaPath, additive: false, log);

            var dash = Save(DashPath, () => BuildDash(additive), overwrite, log);
            var jump = Save(JumpPath, () => BuildJump(additive, alpha), overwrite, log);
            var doubleJump = Save(DoubleJumpPath, () => BuildDoubleJump(additive), overwrite, log);

            WirePlayer(dash, jump, doubleJump, log);
            RemovePlaceholders(log);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ─── Efectos ──────────────────────────────────────────────────────────────────────────────

        private static GameObject BuildDash(Material additive)
        {
            var root = NewEffect("VFX_Dash");

            // Estela: se emite por distancia recorrida, así que sólo sale mientras sigue al jugador en el
            // dash; las partículas (mundo) se quedan en el camino. Estiradas en su velocidad (hacia atrás).
            var streak = AddSystem(root, additive, BehindPlayer, maxParticles: 48);
            var main = streak.main;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Cyan);
            var emission = streak.emission;
            emission.rateOverDistance = 9f;
            var shape = streak.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.05f, 1.3f, 0.05f);    // una línea vertical a lo alto del cuerpo
            shape.rotation = new Vector3(0f, -90f, 0f);       // la Z de emisión mira a -X (hacia atrás)
            Fade(streak, Cyan, DeepTeal);
            Shrink(streak, 0.3f);
            var renderer = streak.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;
            renderer.velocityScale = 0.05f;

            // Soplo al arrancar: ráfaga corta empujada en contra del dash, que se frena.
            var puff = AddSystem(NewChild(root, "Puff"), additive, InFrontOfPlayer, maxParticles: 12);
            main = puff.main;
            main.duration = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(PaleCyan, Cyan);
            Burst(puff, 8);
            shape = puff.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.18f;
            shape.rotation = new Vector3(0f, -90f, 0f);       // eje del cono → -X
            Fade(puff, Cyan, DeepTeal);
            Shrink(puff, 0.2f);
            Dampen(puff, 0.15f);

            Finish(root);
            return root;
        }

        private static GameObject BuildJump(Material additive, Material alpha)
        {
            var root = NewEffect("VFX_Jump");

            // Anillo en el suelo: elipse aplastada que se abre hacia los lados y sube un poco.
            var ring = AddSystem(root, additive, InFrontOfPlayer, maxParticles: 16);
            var main = ring.main;
            main.duration = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, PaleCyan);
            Burst(ring, 12);
            FlatRing(ring, arc: 360f, flatten: 0.3f, rise: 1.2f);
            Fade(ring, PaleCyan, new Color(0.35f, 0.6f, 0.7f, 1f));
            Shrink(ring, 0.25f);
            Dampen(ring, 0.12f);

            // Arco fino de polvo: pocas motas grandes y tenues, lentas, que crecen al desvanecerse.
            var dust = AddSystem(NewChild(root, "Dust"), alpha, BehindPlayer, maxParticles: 8);
            main = dust.main;
            main.duration = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startColor = Dust;
            Burst(dust, 5);
            FlatRing(dust, arc: 360f, flatten: 0.2f, rise: 0.6f);
            Fade(dust, Dust, Dust);
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.2f));
            Dampen(dust, 0.2f);

            Finish(root);
            return root;
        }

        private static GameObject BuildDoubleJump(Material additive)
        {
            var root = NewEffect("VFX_DoubleJump");

            // Anillo mágico completo, aplastado: se abre en horizontal bajo el jugador y se frena.
            var ring = AddSystem(root, additive, InFrontOfPlayer, maxParticles: 24);
            var main = ring.main;
            main.duration = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.33f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.36f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, MagicCyan);
            Burst(ring, 18);
            FlatRing(ring, arc: 360f, flatten: 0.28f);
            Fade(ring, MagicCyan, MagicDeep);
            Shrink(ring, 0.3f);
            Dampen(ring, 0.1f);

            // Unas chispas hacia arriba, estiradas: es magia, no polvo.
            var sparks = AddSystem(NewChild(root, "Sparks"), additive, InFrontOfPlayer, maxParticles: 8);
            main = sparks.main;
            main.duration = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, MagicCyan);
            Burst(sparks, 6);
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);       // eje del cono → +Y
            Fade(sparks, MagicCyan, MagicDeep);
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.03f;

            Finish(root);
            return root;
        }

        // ─── Piezas ───────────────────────────────────────────────────────────────────────────────

        private static GameObject NewEffect(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<VfxOneShot>(); // lifetime 0 = la de las partículas (red de seguridad)
            return root;
        }

        private static GameObject NewChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        /// <summary>Un sistema de un solo disparo con los ajustes comunes de todos los efectos.</summary>
        private static ParticleSystem AddSystem(GameObject go, Material material, int sortingOrder, int maxParticles)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = 0f;
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.maxParticleSize = 0.2f; // nunca un quad grande tapando la pantalla
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Burst(ParticleSystem ps, short count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        }

        /// <summary>
        /// Anillo inclinado hacia la cámara: se ve como una elipse aplastada (alto/ancho = <paramref name="flatten"/>)
        /// que se abre en horizontal — un anillo en el suelo / bajo los pies visto de lado. <paramref name="rise"/>
        /// añade una deriva hacia arriba (u/s).
        /// </summary>
        private static void FlatRing(ParticleSystem ps, float arc, float flatten, float rise = 0f)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.radiusThickness = 0f;    // desde el borde
            shape.arc = arc;
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            shape.rotation = new Vector3(Mathf.Acos(Mathf.Clamp01(flatten)) * Mathf.Rad2Deg, 0f, 0f);

            if (rise <= 0f) return;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.5f, rise);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        /// <summary>Color de <paramref name="from"/> a <paramref name="to"/> y alfa que se apaga deprisa.</summary>
        private static void Fade(ParticleSystem ps, Color from, Color to)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
        }

        private static void Shrink(ParticleSystem ps, float endSize)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
        }

        private static void Dampen(ParticleSystem ps, float dampen)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0f;
            limit.dampen = dampen;
        }

        /// <summary>
        /// La raíz se apaga la última (Stop Action = Callback → <see cref="VfxOneShot"/> la devuelve al pool):
        /// su duración cubre lo que tarde el sistema más largo del efecto, y ninguna partícula la sobrevive.
        /// </summary>
        private static void Finish(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>();
            var rootSystem = root.GetComponent<ParticleSystem>();

            float longest = 0f;
            foreach (var ps in systems)
                if (ps != rootSystem) longest = Mathf.Max(longest, ps.main.duration + ps.main.startLifetime.constantMax);

            var main = rootSystem.main;
            main.duration = Mathf.Max(main.duration, longest);
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        // ─── Assets ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Material de partículas a partir del de URP (<c>ParticlesUnlit</c>: transparente, con su textura
        /// de partícula por defecto — nada importado). Aditivo para la magia, alfa para el polvo.
        /// </summary>
        private static Material EnsureMaterial(string path, bool additive, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Material>(UrpParticleMaterial);
            var material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (additive)
            {
                material.SetFloat("_Blend", 2f); // URP: 0 Alpha, 1 Premultiply, 2 Additive, 3 Multiply
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.One);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            }
            material.SetFloat("_ZWrite", 0f);

            AssetDatabase.CreateAsset(material, path);
            log.AppendLine($"  Material creado: {path}");
            return material;
        }

        private static GameObject Save(string path, System.Func<GameObject> build, bool overwrite, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite)
            {
                log.AppendLine($"  Ya existe (no se toca): {path}");
                return existing;
            }

            var temp = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            log.AppendLine($"  {(existing != null ? "Regenerado" : "Creado")}: {path}");
            return prefab;
        }

        /// <summary>Pone los tres efectos en <see cref="PlayerVfx"/> si el hueco está vacío o apunta a un placeholder.</summary>
        private static void WirePlayer(GameObject dash, GameObject jump, GameObject doubleJump, StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var vfx = root.GetComponent<PlayerVfx>();
                if (vfx == null)
                {
                    log.AppendLine("  AVISO: Player.prefab no tiene PlayerVfx.");
                    return;
                }

                var so = new SerializedObject(vfx);
                int changed = Assign(so, "dashEffect", dash) + Assign(so, "jumpEffect", jump) +
                              Assign(so, "doubleJumpEffect", doubleJump);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                log.AppendLine($"  Player.prefab ▸ PlayerVfx: {changed} referencia(s) asignada(s).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int Assign(SerializedObject so, string field, GameObject prefab)
        {
            var prop = so.FindProperty(field);
            if (prop == null || prefab == null) return 0;

            var current = prop.objectReferenceValue;
            string currentPath = current != null ? AssetDatabase.GetAssetPath(current) : null;
            bool placeholder = current == null || OldPlaceholders.Contains(currentPath);
            if (!placeholder || current == prefab) return 0;

            prop.objectReferenceValue = prefab;
            return 1;
        }

        /// <summary>Borra los prefabs de cuadrado blanco si ya nadie los usa.</summary>
        private static void RemovePlaceholders(StringBuilder log)
        {
            foreach (var path in OldPlaceholders)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

                string guid = AssetDatabase.AssetPathToGUID(path);
                var users = AssetDatabase.FindAssets("t:Prefab t:Scene t:ScriptableObject", new[] { "Assets" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p != path && AssetDatabase.GetDependencies(p, false).Contains(path))
                    .ToList();

                if (users.Count > 0)
                {
                    log.AppendLine($"  AVISO: {path} sigue en uso por {string.Join(", ", users)}; no se borra.");
                    continue;
                }

                AssetDatabase.DeleteAsset(path);
                log.AppendLine($"  Placeholder borrado: {path} ({guid})");
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
