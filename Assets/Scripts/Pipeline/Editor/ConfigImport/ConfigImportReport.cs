using System.Collections.Generic;
using System.Text;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Lo que de verdad hizo un import, para enseñar un resumen honesto en vez de esparcir
    /// <c>Debug.Log</c> sueltos que el usuario tiene que ir juntando él mismo.
    ///
    /// Los tres importadores (enemigo, proyectil, jefe) escriben aquí en vez de loguear
    /// directamente: <see cref="ConfigImportRunner"/> es el único que decide cómo se enseña.
    /// </summary>
    public class ConfigImportReport
    {
        public readonly string SourceFile;

        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();

        /// <summary>Valores que EnemyStats.OnValidate habría ajustado; ver EnemyConfigImporter.</summary>
        public readonly List<string> Clamped = new List<string>();

        /// <summary>Claves de un bloque 'params' de BossAttack que no se pudieron escribir.</summary>
        public readonly List<string> UnresolvedParams = new List<string>();

        /// <summary>
        /// Un ProjectileConfig suelto (no biblioteca) aplicado sobre el asset que estaba
        /// seleccionado en el Project. Separado de <see cref="Updated"/> a propósito: esto no es
        /// "se regeneró un asset por su nombre", es "se sobrescribió un campo de un asset elegido
        /// a mano" — un riesgo distinto que merece su propia línea en el resumen, con el nombre del
        /// asset y a qué campo fue, en vez de perderse dentro de "Actualizados".
        /// </summary>
        public readonly List<string> ProjectileApplied = new List<string>();

        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public ConfigImportReport(string sourceFile) => SourceFile = sourceFile;

        public bool HasErrors => Errors.Count > 0;

        /// <summary>Registra un asset como creado o actualizado, según corresponda.</summary>
        public void Add(bool created, string assetPath) => (created ? Created : Updated).Add(assetPath);

        /// <summary>Registra el asset y campo exactos a los que se aplicó un ProjectileConfig suelto.</summary>
        public void AddProjectileTarget(string assetPath, string targetLabel) =>
            ProjectileApplied.Add($"{assetPath} — {targetLabel}");

        public string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Import Config — {SourceFile}");

            AppendSection(sb, "ERRORES", Errors, "x");
            AppendSection(sb, "Creados", Created, "+");
            AppendSection(sb, "Actualizados", Updated, "~");
            AppendSection(sb, "ProjectileConfig aplicado (asset seleccionado)", ProjectileApplied, "→");
            AppendSection(sb, "Valores ajustados (clamps de EnemyStats/BossDefinition)", Clamped, "!");
            AppendSection(sb, "Claves de 'params' no resueltas", UnresolvedParams, "?");
            AppendSection(sb, "Avisos", Warnings, "·");

            if (Errors.Count == 0 && Created.Count == 0 && Updated.Count == 0 && ProjectileApplied.Count == 0)
                sb.AppendLine("(sin cambios)");

            return sb.ToString();
        }

        private static void AppendSection(StringBuilder sb, string title, List<string> lines, string bullet)
        {
            if (lines.Count == 0) return;

            sb.AppendLine();
            sb.AppendLine($"{title} ({lines.Count}):");
            foreach (var line in lines) sb.AppendLine($"  {bullet} {line}");
        }
    }
}
