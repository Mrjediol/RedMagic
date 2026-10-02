using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// 4 · Asigna la paleta de sonidos sintetizados del juego a los huecos que no tienen sonido propio.
    ///
    /// La paleta (clips + reglas "qué hueco recibe qué clip") no vive aquí: la escribe la web
    /// (<c>Web/RedMagicWeb/modules/game-sounds.js</c> → <c>tools/bake-sounds.mjs</c>) en
    /// <see cref="PalettePath"/>. Una regla casa un hueco del registro por expresiones regulares (sin
    /// distinguir mayúsculas) sobre su carpeta, su nombre, el tipo del dueño, el campo/momento y el
    /// nombre del asset; gana la primera que casa. Añadir contenido nuevo (otro jefe, otro proyectil)
    /// no pide regla nueva si su tipo de ataque o su nombre ya dicen qué es.
    ///
    /// Idempotente y conservador: sólo escribe en un hueco vacío, con el clip de prueba genérico o que
    /// suena con el genérico de enemigo. Nunca toca un hueco con clip propio, uno callado a propósito ni
    /// uno quitado desde la pestaña Sounds. Todo sonido que asigna es un sonido nuevo: queda Pending
    /// hasta que el usuario lo califique en la pestaña.
    /// </summary>
    public static class SoundPaletteAssigner
    {
        private const string Menu = "Tools/RedMagic/Audio/";
        public const string PaletteFolder = "Assets/Audio/Sfx/Game";
        public const string PalettePath = PaletteFolder + "/palette.json";

        private sealed class Rule
        {
            public Regex Category, Name, Component, Member, Asset;
            public string Clip;          // null = regla que deja el hueco como está
            public float Volume = 1f;
            public float PitchMin = 1f, PitchMax = 1f;

            public bool Matches(SoundRegistryEntry e, string asset) =>
                Ok(Category, e.Category) && Ok(Name, e.Name) && Ok(Component, e.Slot.ComponentType) &&
                Ok(Member, e.Slot.Member) && Ok(Asset, asset);

            private static bool Ok(Regex regex, string value) => regex == null || regex.IsMatch(value ?? "");
        }

        [MenuItem(Menu + "4 · Asignar paleta de sonidos del juego")]
        private static void AssignMenu() => Run(dryRun: false);

        [MenuItem(Menu + "4b · Paleta: ver qué asignaría (sin tocar nada)")]
        private static void PreviewMenu() => Run(dryRun: true);

        /// <summary>Aplica la paleta (o sólo informa, con <paramref name="dryRun"/>). Devuelve el informe.</summary>
        public static string Run(bool dryRun)
        {
            if (!LoadPalette(out var rules, out var clipPaths, out string error))
            {
                Debug.LogError("[SoundPalette] " + error);
                return error;
            }

            var entries = SoundRegistry.Regenerate(out string scanReport, out bool ok);
            if (!ok)
            {
                Debug.LogError("[SoundPalette] " + scanReport);
                return scanReport;
            }

            var status = SoundRegistry.LoadStatus();
            var test = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundSlotEditor.GenericTestClipPath);
            var log = new StringBuilder();
            var unmatched = new List<string>();
            int assigned = 0, kept = 0, skipped = 0, failed = 0;

            foreach (var e in entries)
            {
                if (e.Slot == null || e.Kind == SoundSlotKind.MusicField) continue;       // música y declarados: fuera
                if (e.ClipSource == ClipSource.Own) { kept++; continue; }                 // ya tiene sonido propio
                if (e.Slot.Silent || (status.TryGetValue(e.Id, out var record) && record.Removed)) { skipped++; continue; }

                string asset = Path.GetFileNameWithoutExtension(e.Slot.AssetPath);
                var rule = rules.FirstOrDefault(r => r.Matches(e, asset));
                if (rule == null) { unmatched.Add($"{e.Category} ▸ {e.Name}"); continue; }
                if (rule.Clip == null) { skipped++; continue; }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPaths[rule.Clip]);
                if (clip == null)
                {
                    failed++;
                    log.AppendLine($"  FALLO {e.Category} ▸ {e.Name}: no se ha importado {clipPaths[rule.Clip]}");
                    continue;
                }

                if (!dryRun)
                {
                    bool wrote = false;
                    bool edited = SoundSlotEditor.Edit(e.Slot, cue =>
                    {
                        if (!IsFree(cue, test)) return;      // alguien le puso un clip desde el escaneo
                        SoundSlotEditor.SetSingleClip(cue, clip);
                        cue.FindPropertyRelative("volume").floatValue = rule.Volume;
                        cue.FindPropertyRelative("pitchMin").floatValue = rule.PitchMin;
                        cue.FindPropertyRelative("pitchMax").floatValue = rule.PitchMax;
                        wrote = true;
                    }, out string editError);

                    if (!edited)
                    {
                        failed++;
                        log.AppendLine($"  FALLO {e.Category} ▸ {e.Name}: {editError}");
                        continue;
                    }
                    if (!wrote) { kept++; continue; }
                }

                assigned++;
                log.AppendLine($"  {e.Category} ▸ {e.Name}  ←  {rule.Clip}");
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                SoundRegistry.Regenerate(out _, out _);
            }

            var report = new StringBuilder();
            report.AppendLine($"[SoundPalette] {(dryRun ? "SIMULACIÓN · asignaría" : "Asignados")}: {assigned} · " +
                              $"con sonido propio (no se tocan): {kept} · callados/quitados/excluidos: {skipped} · " +
                              $"sin regla: {unmatched.Count} · fallos: {failed}");
            if (unmatched.Count > 0) report.AppendLine("Sin regla (siguen como estaban):\n  " + string.Join("\n  ", unmatched));
            report.Append(log);

            if (failed > 0) Debug.LogError(report.ToString());
            else Debug.Log(report.ToString());
            return report.ToString();
        }

        // Libre = sin clips, o sólo con el clip de prueba genérico.
        private static bool IsFree(SerializedProperty cue, AudioClip test)
        {
            var clips = cue.FindPropertyRelative("clips");
            for (int i = 0; i < clips.arraySize; i++)
            {
                var clip = clips.GetArrayElementAtIndex(i).objectReferenceValue;
                if (clip != null && clip != test) return false;
            }
            return true;
        }

        private static bool LoadPalette(out List<Rule> rules, out Dictionary<string, string> clipPaths, out string error)
        {
            rules = new List<Rule>();
            clipPaths = new Dictionary<string, string>();
            error = null;

            if (!File.Exists(PalettePath))
            {
                error = $"No existe {PalettePath}. Genérala: en Web/RedMagicWeb/tools, `npm run bake-sounds`.";
                return false;
            }

            var root = JObject.Parse(File.ReadAllText(PalettePath));
            if (root["sounds"] is JObject sounds)
                foreach (var p in sounds.Properties())
                    clipPaths[p.Name] = $"{PaletteFolder}/{(string)p.Value["family"]}/{p.Name}.wav";

            foreach (var o in (root["rules"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                var rule = new Rule
                {
                    Category = Pattern(o, "category"),
                    Name = Pattern(o, "name"),
                    Component = Pattern(o, "component"),
                    Member = Pattern(o, "member"),
                    Asset = Pattern(o, "asset"),
                    Clip = (string)o["clip"],
                    Volume = (float?)o["volume"] ?? 1f,
                };
                if (o["pitch"] is JArray pitch && pitch.Count == 2)
                {
                    rule.PitchMin = (float)pitch[0];
                    rule.PitchMax = (float)pitch[1];
                }
                if (rule.Clip != null && !clipPaths.ContainsKey(rule.Clip))
                {
                    error = $"{PalettePath}: una regla pide el sonido '{rule.Clip}', que no está en la paleta.";
                    return false;
                }
                rules.Add(rule);
            }

            if (rules.Count == 0) error = $"{PalettePath} no tiene reglas.";
            return rules.Count > 0;
        }

        private static Regex Pattern(JObject rule, string key)
        {
            string text = (string)rule[key];
            return string.IsNullOrEmpty(text) ? null : new Regex(text, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
