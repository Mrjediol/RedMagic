using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RedMagic.Fx;
using RedMagic.Gameplay;
using RedMagic.Pipeline;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Construye (o recupera) el prefab <b>pooled</b> de un proyectil o de un VFX autorizado en la
    /// web, a partir del mismo <c>manifest.json</c> + carpetas de PNG por animación que
    /// <see cref="EnemyImporter.BuildEnemyFromFolderPath"/> ya consume para los enemigos.
    ///
    /// <b>Es idempotente y perezoso a propósito.</b> <see cref="BuildOrGetProjectilePrefab"/> /
    /// <see cref="BuildOrGetFxPrefab"/> devuelven el prefab que ya exista para ese id de biblioteca
    /// y sólo construyen cuando no hay ninguno, que es lo que permite que importar un enemigo
    /// materialice su proyectil al vuelo sin haberlo importado antes por separado
    /// (<see cref="ProjectileConfigImporter"/> → <c>libraryId</c>).
    ///
    /// <b>Sin Animator, a diferencia del importador de enemigos.</b> El encargo decía "igual que
    /// EnemyImporter (clips + controller)", pero eso vale para un enemigo y NO para algo que pasa
    /// por <c>Core.PrefabPool</c>: el pool no tiene gancho de reinicio por instancia, así que un
    /// Animator reutilizado vuelve del pool a mitad de su animación anterior. La regla del proyecto
    /// es explícita — <i>Animator para enemigos/jefes; flipbook para todo lo que se poolea</i> (ver
    /// SPRITE_PIPELINE.md y el comentario de <see cref="SpriteStateMachine"/>) — así que lo que se
    /// reutiliza de EnemyImporter es la LECTURA (manifest + <see cref="EnemyImporter.LoadAndConfigureSprites"/>,
    /// sus mismos pivotes y PPU) y lo que cambia es el motor de animación:
    /// <see cref="SpriteStateMachine"/> con varios estados, <see cref="SpriteFlipbook"/> con uno solo.
    /// Ambos rebobinan en <c>OnEnable</c>, que es el único reinicio que el pool ofrece.
    ///
    /// <b>Qué NO hace.</b> Nada de números. Velocidad, vida, perforación, autoguiado… siguen
    /// viniendo de <see cref="ProjectileConfigImporter"/> exactamente como hoy; aquí sólo se
    /// produce el cascarón visual pooled al que <c>ProjectileSpec.prefab</c> acabará apuntando.
    /// Mezclar las dos cosas rompería la convención documentada de que el tuning de un proyectil
    /// vive en <c>EnemyStats ▸ Tuning ▸ projectile</c> y el prefab sólo aporta el aspecto.
    ///
    /// <b>fx-config.json, si viene.</b> La pestaña web "Proyectil/VFX" exporta, junto al
    /// <c>manifest.json</c> y las carpetas de PNG, un <c>fx-config.json</c>
    /// (<c>docs/schemas/fx-config.schema.json</c>). Es <b>opcional</b>: una carpeta sin él se
    /// construye exactamente igual que antes de que existiera. Cuando está, aporta una sola cosa al
    /// prefab — el <see cref="ProjectileMovementPlaceholder"/> con el modo de movimiento que el
    /// config pidió, más los valores de vista previa suelta. Sigue sin aportar tuning: ver el
    /// párrafo anterior y el <c>$comment</c> de <c>projectile</c> en ese esquema.
    /// </summary>
    public static class FxPrefabBuilder
    {
        /// <summary>Kinds que este builder sabe construir. Mismo vocabulario que entry-kinds.js.</summary>
        public const string KindProjectile = "projectile";
        public const string KindFx = "fx";

        /// <summary>
        /// Casa de las FUENTES (manifest + PNG) que llegan de la web, una carpeta por entrada.
        /// Separada de la de los prefabs porque es material de entrada re-importable, y porque los
        /// clips/sprites que el prefab referencia viven aquí: borrar esta carpeta después de
        /// importar dejaría el prefab sin sprites, el mismo motivo por el que
        /// <see cref="CombinedBundleImporter"/> nunca borra la suya.
        /// </summary>
        public const string SourceRoot = "Assets/Art/WebLibrary";

        /// <summary>Destino de los prefabs, junto al resto de FX del proyecto (Assets/Prefabs/Fx/…).</summary>
        private const string ProjectilePrefabRoot = "Assets/Prefabs/Fx/Projectiles";
        private const string FxPrefabRoot = "Assets/Prefabs/Fx/Vfx";

        // ============================================================ API

        /// <summary>
        /// El prefab pooled del proyectil de esa entrada de biblioteca: el que ya existiera, o uno
        /// recién construido desde su manifest. Devuelve null (y deja el motivo en
        /// <paramref name="log"/>) si no hay fuente en disco para ese id.
        /// </summary>
        public static GameObject BuildOrGetProjectilePrefab(string libraryId, StringBuilder log = null)
            => BuildOrGet(libraryId, KindProjectile, log);

        /// <summary>El prefab pooled del VFX de esa entrada. Ver <see cref="BuildOrGetProjectilePrefab"/>.</summary>
        public static GameObject BuildOrGetFxPrefab(string libraryId, StringBuilder log = null)
            => BuildOrGet(libraryId, KindFx, log);

        private static GameObject BuildOrGet(string libraryId, string expectedKind, StringBuilder log)
        {
            if (string.IsNullOrWhiteSpace(libraryId))
            {
                Append(log, "[FxPrefabBuilder] libraryId vacío.");
                return null;
            }

            // 1) ¿Ya está construido? Se busca por el marcador (el id), no por la ruta — ver
            //    WebLibrarySource sobre por qué buscar por nombre produciría duplicados.
            var existing = FindExistingPrefab(libraryId);
            if (existing != null)
            {
                Append(log, $"[FxPrefabBuilder] '{libraryId}' ya construido: {AssetDatabase.GetAssetPath(existing)}");
                return existing;
            }

            // 2) ¿Hay fuente en disco?
            string sourceFolder = FindSourceFolder(libraryId);
            if (sourceFolder == null)
            {
                Append(log, $"[FxPrefabBuilder] no hay ninguna fuente para libraryId '{libraryId}'. " +
                            $"Exporta esa entrada desde la web e impórtala (Tools > Web > Import Config... > " +
                            $"Importar proyectil/VFX...), o inclúyela en el bundle combinado del enemigo.");
                return null;
            }

            return BuildFromFolder(sourceFolder, expectedKind, log);
        }

        /// <summary>
        /// Construye el prefab desde una carpeta concreta (manifest.json + una carpeta de PNG por
        /// animación). Es el punto de entrada del importador manual, que ya sabe dónde dejó la
        /// fuente y no necesita buscarla por id.
        /// </summary>
        public static GameObject BuildFromFolder(string sourceFolder, string expectedKind, StringBuilder log = null)
        {
            string manifestPath = $"{sourceFolder}/manifest.json";
            string absManifest = ToAbsolute(manifestPath);
            if (!File.Exists(absManifest))
            {
                Append(log, $"[FxPrefabBuilder] no hay manifest.json en '{sourceFolder}'.");
                return null;
            }

            var manifest = EnemyImporter.ParseManifest(File.ReadAllText(absManifest));
            if (manifest == null || manifest.animations == null || manifest.animations.Count == 0)
            {
                Append(log, $"[FxPrefabBuilder] el manifest de '{sourceFolder}' no trae animaciones.");
                return null;
            }

            // 'kind' es additivo: un manifest exportado antes de que existiera no lo trae. Se
            // asume el kind que el llamante esperaba en vez de rechazarlo, igual que el schema
            // trata la ausencia de 'kind' como "enemy" — fallar aquí rompería bundles válidos.
            string kind = string.IsNullOrWhiteSpace(manifest.kind) ? expectedKind : manifest.kind;
            if (kind != KindProjectile && kind != KindFx)
            {
                Append(log, $"[FxPrefabBuilder] '{sourceFolder}' es de tipo '{kind}', que este builder no " +
                            "construye (sólo 'projectile' y 'fx'). Un enemigo va por EnemyImporter.");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(expectedKind) && kind != expectedKind)
                Append(log, $"[FxPrefabBuilder] AVISO: se pidió un '{expectedKind}' pero el manifest dice " +
                            $"'{kind}'. Se construye como '{kind}'.");

            string name = SafeName(manifest.enemyName);

            // El pivote va centrado para las dos clases: un proyectil rota sobre su centro al
            // orientarse en vuelo (Projectile escribe transform.right) y un VFX se coloca sobre un
            // punto, no sobre unos pies. BottomCenter dejaría el centro de giro bajo el cuerpo.
            var states = new List<SpriteStateMachine.State>();
            foreach (var anim in manifest.animations)
            {
                var sprites = EnemyImporter.LoadAndConfigureSprites($"{sourceFolder}/{anim.name}", AnchorMode.Center);
                if (sprites.Count == 0)
                {
                    Append(log, $"[FxPrefabBuilder] la animación '{anim.name}' de '{name}' no tiene PNG; se omite.");
                    continue;
                }

                states.Add(new SpriteStateMachine.State
                {
                    name = anim.name,
                    frames = sprites.ToArray(),
                    fps = anim.fps > 0 ? anim.fps : 10f,
                    loop = AnimStates.Loops(anim.name, anim.loop),   // "Impact" nunca repite
                });
            }

            if (states.Count == 0)
            {
                Append(log, $"[FxPrefabBuilder] '{name}': ninguna animación tenía frames.");
                return null;
            }

            return kind == KindProjectile
                ? BuildProjectile(name, states, sourceFolder, manifest, log)
                : BuildFx(name, states, sourceFolder, manifest, log);
        }

        // ============================================================ fx-config.json (opcional)

        /// <summary>
        /// Lo único que este builder lee del <c>fx-config.json</c>: el modo de movimiento y los dos
        /// valores con los que el proyectil vuela solo si nadie lo dispara. Deliberadamente NO lleva
        /// el resto del <c>ProjectileSpec</c> — ese bloque es el tuning de partida y lo escribe
        /// <see cref="ProjectileConfigImporter"/> sobre quien dispara, no sobre el prefab.
        /// </summary>
        private readonly struct MovementPreview
        {
            public readonly string Mode;
            public readonly float Speed;
            public readonly float Lifetime;

            public MovementPreview(string mode, float speed, float lifetime)
            {
                Mode = mode;
                Speed = speed;
                Lifetime = lifetime;
            }

            /// <summary>Lo que se estampa cuando la carpeta no trae ningún fx-config.json.</summary>
            public static MovementPreview Default => new MovementPreview("Straight", 12f, 2.5f);
        }

        /// <summary>
        /// Lee el <c>fx-config.json</c> de la carpeta si existe. Nunca lanza: un JSON roto o de otra
        /// cosa deja los valores por defecto y un aviso, porque el arte ya se importó bien y perder
        /// el prefab entero por un campo de vista previa sería desproporcionado.
        /// </summary>
        private static MovementPreview ReadMovementPreview(string sourceFolder, StringBuilder log)
        {
            string path = ToAbsolute($"{sourceFolder}/fx-config.json");
            if (!File.Exists(path)) return MovementPreview.Default;

            try
            {
                var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                string mode = root.Value<string>("movement");

                // ToObject<float>() y no Value<float>(): este archivo no importa el namespace de
                // Newtonsoft (usa nombres completos), y Value<T>() sin clave es el método de
                // extensión de Newtonsoft.Json.Linq, que sólo existe con el 'using' puesto.
                var spec = root["projectile"] as Newtonsoft.Json.Linq.JObject;
                float speed = spec?["speed"] != null ? spec["speed"].ToObject<float>() : MovementPreview.Default.Speed;
                float lifetime = spec?["lifetime"] != null ? spec["lifetime"].ToObject<float>() : MovementPreview.Default.Lifetime;

                return new MovementPreview(
                    string.IsNullOrWhiteSpace(mode) ? MovementPreview.Default.Mode : mode,
                    speed, lifetime);
            }
            catch (System.Exception e)
            {
                Append(log, $"[FxPrefabBuilder] no se pudo leer '{sourceFolder}/fx-config.json' " +
                            $"({e.Message}); se usan los valores por defecto del placeholder.");
                return MovementPreview.Default;
            }
        }

        /// <summary>
        /// Estampa el <see cref="ProjectileMovementPlaceholder"/>. El modo se escribe por NOMBRE
        /// (<see cref="System.Enum.TryParse{TEnum}(string, bool, out TEnum)"/>), nunca por ordinal,
        /// para que reordenar el enum no repunte configs ya exportados; un nombre que este enum no
        /// conoce se avisa y cae en <c>Straight</c> en vez de romper el import.
        /// </summary>
        private static void ApplyMovementPlaceholder(GameObject root, MovementPreview preview, StringBuilder log)
        {
            var placeholder = root.AddComponent<ProjectileMovementPlaceholder>();

            if (!System.Enum.TryParse<ProjectileMovementPlaceholder.MovementMode>(preview.Mode, false, out var mode))
            {
                Append(log, $"[FxPrefabBuilder] modo de movimiento '{preview.Mode}' desconocido; " +
                            "se usa 'Straight'.");
                mode = ProjectileMovementPlaceholder.MovementMode.Straight;
            }

            var so = new SerializedObject(placeholder);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.FindProperty("speed").floatValue = Mathf.Max(0.01f, preview.Speed);
            so.FindProperty("lifetime").floatValue = Mathf.Max(0.05f, preview.Lifetime);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ============================================================ construcción

        private static GameObject BuildProjectile(string name, List<SpriteStateMachine.State> states,
                                                  string sourceFolder, EnemyImporter.Manifest manifest,
                                                  StringBuilder log)
        {
            string folder = $"{ProjectilePrefabRoot}/{name}";
            string path = $"{folder}/Fx_{name}.prefab";
            SheetSlicer.EnsureFolder(folder);

            var root = new GameObject($"Fx_{name}");
            try
            {
                // 'Move' es el estado en vuelo y el que debe verse al salir del pool; si la hoja no
                // lo trae (nombre propio), manda el primero declarado.
                var primary = PickPrimary(states, "Move");
                var renderer = SetUpRenderer(root, primary);
                ApplyAnimation(root, states, primary);

                var body = root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;              // ProjectileSpec.arcGravity lo ajusta por disparo
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                var collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                var size = primary.frames[0].bounds.size;
                collider.radius = Mathf.Max(0.01f, Mathf.Max(size.x, size.y) * 0.5f);

                // destroyWhenDone se queda en su default (false): ProjectileFactory lo poolea por
                // PrefabPool, nunca Instantiate/Destroy — misma decisión que ProjectilePrefabFactory.
                var projectile = root.AddComponent<Projectile>();

                // El arte que llega de la biblioteca web no sigue la convención "autorizado
                // mirando +X" que exige rotar el transform entero (ShotProjectile/Projectile de
                // toda la vida) — es un sprite suelto tipo seta/roca/orbe, dibujado en su
                // orientación natural. Rotar ESE arte lo pondría boca abajo al volar hacia la
                // izquierda. faceTravelDirection=false hace que sólo se refleje en horizontal según
                // la dirección (igual que los proyectiles redondos de BulletHellAttack, que
                // tampoco rotan) — el "arriba" del sprite se queda siempre arriba.
                var projectileSo = new SerializedObject(projectile);
                projectileSo.FindProperty("faceTravelDirection").boolValue = false;
                projectileSo.ApplyModifiedPropertiesWithoutUndo();

                // El suplente de movimiento: lleva el modo que pidió el fx-config (o 'Straight' si
                // no hay config) y hace volar el prefab por su cuenta cuando nadie lo dispara, que
                // es la comprobación de "esto se construyó bien" en Play. Ver su comentario de
                // clase: se aparta en cuanto ProjectileFactory lanza de verdad.
                ApplyMovementPlaceholder(root, ReadMovementPreview(sourceFolder, log), log);

                MarkSource(root, manifest, KindProjectile, sourceFolder);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Append(log, $"[FxPrefabBuilder] proyectil '{name}' construido: {path} " +
                            $"({states.Count} estado(s): {string.Join("/", states.Select(s => s.name))}).");
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject BuildFx(string name, List<SpriteStateMachine.State> states,
                                          string sourceFolder, EnemyImporter.Manifest manifest,
                                          StringBuilder log)
        {
            string folder = $"{FxPrefabRoot}/{name}";
            string path = $"{folder}/Fx_{name}.prefab";
            SheetSlicer.EnsureFolder(folder);

            var root = new GameObject($"Fx_{name}");
            try
            {
                var primary = PickPrimary(states, "Spawn");

                // Un VFX de un solo uso NUNCA repite, diga lo que diga el manifiesto (el exportador
                // marca loop por defecto): con bucle, el margen final de VfxOneShot enseñaba otra vez
                // el primer dibujo. Sólo se reproduce el estado principal (VfxOneShot lo fuerza a una
                // pasada), así que la duración es la suya.
                primary.loop = false;
                SetUpRenderer(root, primary);
                ApplyAnimation(root, states, primary);

                // VfxOneShot sólo sabe medir clips de Animator; con un flipbook hay que darle la
                // duración a mano (SPRITE_PIPELINE.md lo documenta como obligatorio).
                var oneShot = root.AddComponent<VfxOneShot>();
                float lifetime = primary.Duration;
                WritePrivateFloat(oneShot, "lifetime", lifetime);

                MarkSource(root, manifest, KindFx, sourceFolder);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Append(log, $"[FxPrefabBuilder] VFX '{name}' construido: {path} " +
                            $"({states.Count} estado(s): {string.Join("/", states.Select(s => s.name))}, " +
                            $"lifetime {lifetime:0.##}s).");
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>El estado preferido si existe, si no el primero declarado.</summary>
        private static SpriteStateMachine.State PickPrimary(List<SpriteStateMachine.State> states, string preferred)
            => states.FirstOrDefault(s => s.name == preferred) ?? states[0];

        private static SpriteRenderer SetUpRenderer(GameObject root, SpriteStateMachine.State primary)
        {
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = primary.frames[0];
            renderer.color = Color.white;
            renderer.sortingOrder = 6;   // mismo orden que ProjectilePrefabFactory ya usa
            return renderer;
        }

        /// <summary>
        /// Un solo estado → <see cref="SpriteFlipbook"/>; varios → <see cref="SpriteStateMachine"/>.
        /// Nunca un Animator: ver el comentario de clase.
        /// </summary>
        private static void ApplyAnimation(GameObject root, List<SpriteStateMachine.State> states,
                                           SpriteStateMachine.State primary)
        {
            if (states.Count == 1)
            {
                var flipbook = root.AddComponent<SpriteFlipbook>();
                var so = new SerializedObject(flipbook);
                WriteSpriteArray(so.FindProperty("frames"), primary.frames);
                so.FindProperty("framesPerSecond").floatValue = primary.fps;
                // pingPong/randomStart son para un idle decorativo; un proyectil o un one-shot
                // tienen que leerse en su orden y desde el principio.
                so.FindProperty("pingPong").boolValue = false;
                so.FindProperty("randomStart").boolValue = false;
                so.FindProperty("oneShot").boolValue = !primary.loop;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            var machine = root.AddComponent<SpriteStateMachine>();
            var mso = new SerializedObject(machine);
            var statesProp = mso.FindProperty("states");
            statesProp.arraySize = states.Count;
            for (int i = 0; i < states.Count; i++)
            {
                var element = statesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = states[i].name;
                element.FindPropertyRelative("fps").floatValue = states[i].fps;
                element.FindPropertyRelative("loop").boolValue = states[i].loop;
                WriteSpriteArray(element.FindPropertyRelative("frames"), states[i].frames);
            }
            mso.FindProperty("defaultState").stringValue = primary.name;
            mso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteSpriteArray(SerializedProperty prop, Sprite[] sprites)
        {
            prop.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static void WritePrivateFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop != null) { prop.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void MarkSource(GameObject root, EnemyImporter.Manifest manifest, string kind, string sourceFolder)
        {
            var marker = root.AddComponent<WebLibrarySource>();
            marker.EditorSet(manifest.libraryId ?? string.Empty, kind, sourceFolder);
        }

        // ============================================================ búsqueda

        /// <summary>
        /// El prefab ya construido para ese id, buscado por el marcador <see cref="WebLibrarySource"/>
        /// y no por la ruta — así sobrevive a que alguien renombre o mueva el prefab a mano.
        /// </summary>
        public static GameObject FindExistingPrefab(string libraryId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var marker = go != null ? go.GetComponent<WebLibrarySource>() : null;
                if (marker != null && marker.LibraryId == libraryId) return go;
            }
            return null;
        }

        /// <summary>
        /// La carpeta de Assets/ cuyo manifest.json declara ese libraryId. Se mira primero la ruta
        /// canónica (barata) y sólo si falla se recorren los manifests que haya bajo Assets/, para
        /// que una carpeta descomprimida a mano en otro sitio también valga.
        /// </summary>
        public static string FindSourceFolder(string libraryId)
        {
            foreach (var kind in new[] { KindProjectile, KindFx })
            {
                string canonical = $"{SourceRoot}/{kind}/{libraryId}";
                if (File.Exists(ToAbsolute($"{canonical}/manifest.json"))) return canonical;
            }

            string assetsRoot = Path.GetFullPath("Assets");
            foreach (var abs in Directory.EnumerateFiles(assetsRoot, "manifest.json", SearchOption.AllDirectories))
            {
                string id;
                try { id = EnemyImporter.ParseManifest(File.ReadAllText(abs))?.libraryId; }
                catch { continue; }   // un manifest.json de otra cosa: no es un error, no es el nuestro

                if (!string.IsNullOrEmpty(id) && id == libraryId)
                    return ToAssetsRelative(Path.GetDirectoryName(abs));
            }

            return null;
        }

        /// <summary>Carpeta canónica donde el importador manual deja la fuente de una entrada.</summary>
        public static string SourceFolderFor(string kind, string libraryId) => $"{SourceRoot}/{kind}/{libraryId}";

        // ============================================================ utilidades

        private static string SafeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unnamed";
            var sb = new StringBuilder();
            foreach (char c in raw.Trim())
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return sb.ToString();
        }

        private static string ToAbsolute(string assetsRelative)
            => Path.GetFullPath(assetsRelative);

        private static string ToAssetsRelative(string absolute)
        {
            string full = Path.GetFullPath(absolute).Replace('\\', '/');
            int idx = full.IndexOf("/Assets/", System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return full.Substring(idx + 1);
            return full.EndsWith("/Assets") ? "Assets" : full;
        }

        private static void Append(StringBuilder log, string message)
        {
            if (log != null) log.AppendLine(message);
            else Debug.Log(message);
        }
    }
}
