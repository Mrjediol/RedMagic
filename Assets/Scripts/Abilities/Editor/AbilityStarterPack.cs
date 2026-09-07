using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Abilities.EditorTools
{
    /// <summary>
    /// Crea la tanda inicial de habilidades como assets en <c>Assets/Resources/Abilities/</c>.
    ///
    /// Es <b>idempotente</b>: no toca ningún asset que ya exista, así que se puede volver a
    /// ejecutar después de haber retocado los números a mano y sólo aparecerán las que falten.
    /// Está pensado como punto de partida para probar sensaciones, no como la lista definitiva:
    /// para una habilidad nueva, duplica un asset y cambia los números — sólo hace falta escribir
    /// código para un <i>arquetipo</i> nuevo.
    /// </summary>
    public static class AbilityStarterPack
    {
        private const string Folder = "Assets/Resources/Abilities";

        [MenuItem("Tools/RedMagic/Ability Starter Pack")]
        public static void Generate()
        {
            EnsureFolder();

            int created = 0;

            // ---------------------------------------------------------------- cuerpo a cuerpo

            created += Melee("Filo Veloz", "Un corte corto y seco. Poco daño, casi sin espera entre golpes.",
                new Color(0.85f, 0.88f, 0.95f), damage: 14f, cooldown: 0.22f, f => f
                    .Set("offset", new Vector2(0.8f, 0.1f))
                    .Set("size", new Vector2(1.2f, 1f))
                    .Set("knockbackMultiplier", 0.5f));

            created += Melee("Mandoble Pesado", "Un golpe lento y enorme que manda al enemigo lejos.",
                new Color(0.9f, 0.35f, 0.25f), damage: 55f, cooldown: 1.1f, f => f
                    .Set("windup", 0.25f)
                    .Set("offset", new Vector2(1.1f, 0.05f))
                    .Set("size", new Vector2(2f, 1.6f))
                    .Set("knockbackMultiplier", 2.2f));

            created += Melee("Estocada Larga", "Una punzada estrecha que llega mucho más lejos de lo normal.",
                new Color(0.6f, 0.85f, 0.9f), damage: 30f, cooldown: 0.55f, f => f
                    .Set("offset", new Vector2(1.5f, 0.1f))
                    .Set("size", new Vector2(2.8f, 0.55f))
                    .Set("knockbackMultiplier", 1.2f));

            created += Melee("Torbellino", "Cuatro barridos a los dos lados. Para salir de un cerco.",
                new Color(0.95f, 0.75f, 0.3f), damage: 12f, cooldown: 0.95f, f => f
                    .Set("offset", new Vector2(0.9f, 0.05f))
                    .Set("size", new Vector2(1.7f, 1.4f))
                    .Set("bothSides", true)
                    .Set("hits", 4)
                    .Set("timeBetweenHits", 0.1f)
                    .Set("knockbackMultiplier", 0.8f));

            created += Melee("Gancho Ascendente", "Golpe hacia arriba que levanta al enemigo por los aires.",
                new Color(0.95f, 0.55f, 0.8f), damage: 26f, cooldown: 0.7f, f => f
                    .Set("offset", new Vector2(0.85f, 0.45f))
                    .Set("size", new Vector2(1.3f, 1.7f))
                    .Set("angle", 45f)
                    .Set("launchMultiplier", 2.5f)
                    .Set("knockbackMultiplier", 1.4f));

            // ---------------------------------------------------------------- a distancia

            created += Projectile("Bola de Fuego", "El disparo de toda la vida: recto, rápido y fiable.",
                new Color(1f, 0.55f, 0.15f), damage: 22f, cooldown: 0.6f, f => f
                    .Set("projectile.speed", 13f)
                    .Set("projectile.lifetime", 2.5f)
                    .Set("projectile.size", new Vector2(0.4f, 0.4f)));

            created += Projectile("Escopeta Espectral", "Cinco perdigones en abanico. Devastadora de cerca, inútil de lejos.",
                new Color(0.75f, 0.85f, 1f), damage: 9f, cooldown: 0.85f, f => f
                    .Set("projectilesPerShot", 5)
                    .Set("spreadAngle", 34f)
                    .Set("randomSpread", 3f)
                    .Set("projectile.speed", 16f)
                    .Set("projectile.lifetime", 0.45f)
                    .Set("projectile.size", new Vector2(0.22f, 0.22f))
                    .Set("knockbackMultiplier", 0.6f));

            created += Projectile("Lanza Astral", "Atraviesa hasta tres enemigos en línea. Premia alinearlos.",
                new Color(0.6f, 0.75f, 1f), damage: 26f, cooldown: 0.8f, f => f
                    .Set("projectile.speed", 20f)
                    .Set("projectile.pierce", 3)
                    .Set("projectile.size", new Vector2(1f, 0.18f))
                    .Set("knockbackMultiplier", 0.7f));

            created += Projectile("Orbe Perseguidor", "Lento pero gira para buscar al enemigo más cercano.",
                new Color(0.7f, 0.45f, 0.95f), damage: 18f, cooldown: 0.7f, f => f
                    .Set("projectile.speed", 7f)
                    .Set("projectile.lifetime", 4f)
                    .Set("projectile.homingTurnRate", 200f)
                    .Set("projectile.homingRange", 10f)
                    .Set("projectile.size", new Vector2(0.45f, 0.45f)));

            created += Projectile("Granada Rúnica", "Vuela en parábola y revienta en un radio grande al caer.",
                new Color(0.5f, 0.9f, 0.45f), damage: 12f, cooldown: 1.1f, f => f
                    .Set("projectile.speed", 11f)
                    .Set("projectile.arcGravity", 22f)
                    .Set("projectile.lifetime", 3f)
                    .Set("projectile.impactRadius", 2.2f)
                    .Set("projectile.impactDamage", 38f)
                    .Set("projectile.size", new Vector2(0.35f, 0.35f))
                    .Set("knockbackMultiplier", 1.8f));

            created += Projectile("Ráfaga Arcana", "Tres disparos seguidos con algo de dispersión.",
                new Color(0.95f, 0.9f, 0.5f), damage: 8f, cooldown: 0.75f, f => f
                    .Set("burstCount", 3)
                    .Set("burstInterval", 0.09f)
                    .Set("randomSpread", 4f)
                    .Set("projectile.speed", 18f)
                    .Set("projectile.lifetime", 1.6f)
                    .Set("projectile.size", new Vector2(0.25f, 0.25f))
                    .Set("knockbackMultiplier", 0.3f));

            // ---------------------------------------------------------------- área / terreno

            created += Nova("Onda de Choque", "Explosión a tu alrededor que aparta a todo el mundo.",
                new Color(0.55f, 0.8f, 1f), damage: 28f, cooldown: 1f, f => f
                    .Set("radius", 3.2f)
                    .Set("knockbackMultiplier", 2f));

            created += Nova("Pisotón Sísmico", "Sólo en el suelo: un pisotón que sacude toda la sala.",
                new Color(0.8f, 0.6f, 0.35f), damage: 40f, cooldown: 1.4f, f => f
                    .Set("windup", 0.2f)
                    .Set("radius", 4f)
                    .Set("requiresGround", true)
                    .Set("knockbackMultiplier", 2.5f));

            created += Nova("Aura Ardiente", "Seis pulsos de fuego seguidos mientras te mueves.",
                new Color(1f, 0.4f, 0.2f), damage: 7f, cooldown: 2.5f, f => f
                    .Set("radius", 2.4f)
                    .Set("pulses", 6)
                    .Set("timeBetweenPulses", 0.35f)
                    .Set("knockbackMultiplier", 0f));

            created += Beam("Rayo Arcano", "Un haz instantáneo que golpea a todo lo que tenga delante.",
                new Color(0.8f, 0.6f, 1f), damage: 30f, cooldown: 0.9f, f => f
                    .Set("length", 9f)
                    .Set("width", 0.5f)
                    .Set("knockbackMultiplier", 0.8f));

            created += Beam("Lengua de Fuego", "Cono corto y ancho. No hace falta apuntar fino.",
                new Color(1f, 0.65f, 0.2f), damage: 16f, cooldown: 0.5f, f => f
                    .Set("length", 3.2f)
                    .Set("width", 1.8f)
                    .Set("visualDuration", 0.16f)
                    .Set("knockbackMultiplier", 0.4f));

            created += Zone("Charco Venenoso", "Deja un charco a tus pies que va quemando cinco segundos.",
                new Color(0.5f, 0.85f, 0.35f), damage: 6f, cooldown: 2.5f, f => f
                    .Set("mode", (int)DamageZone.Mode.Continuous)
                    .Set("radius", 1.8f)
                    .Set("duration", 5f)
                    .Set("tickInterval", 0.5f)
                    .Set("knockbackMultiplier", 0f));

            created += Zone("Mina Rúnica", "Trampa que espera y estalla en cuanto alguien se acerca.",
                new Color(0.95f, 0.3f, 0.35f), damage: 45f, cooldown: 1.6f, f => f
                    .Set("placement", (int)ZoneAbility.Placement.InFront)
                    .Set("mode", (int)DamageZone.Mode.ProximityBomb)
                    .Set("radius", 2.2f)
                    .Set("duration", 12f)
                    .Set("armDelay", 0.35f)
                    .Set("knockbackMultiplier", 2f));

            created += Orbit("Orbes Guardianes", "Tres orbes giran a tu alrededor y dañan lo que tocan.",
                new Color(0.6f, 0.9f, 0.95f), damage: 9f, cooldown: 4f, f => f
                    .Set("orbCount", 3)
                    .Set("orbitRadius", 1.7f)
                    .Set("duration", 6f)
                    .Set("tickInterval", 0.35f)
                    .Set("degreesPerSecond", 220f)
                    .Set("knockbackMultiplier", 0.4f));

            created += Turret("Tótem Centinela", "Planta un tótem que dispara solo durante nueve segundos.",
                new Color(0.85f, 0.8f, 0.55f), damage: 12f, cooldown: 6f, f => f
                    .Set("duration", 9f)
                    .Set("range", 7f)
                    .Set("fireInterval", 0.7f)
                    .Set("projectile.speed", 12f)
                    .Set("projectile.lifetime", 1.6f)
                    .Set("projectile.size", new Vector2(0.25f, 0.25f))
                    .Set("knockbackMultiplier", 0.3f));

            // ---------------------------------------------------------------- utilidad

            created += Dash("Embestida Sombría", "Cargas hacia delante atravesando enemigos, invulnerable.",
                new Color(0.45f, 0.35f, 0.6f), damage: 24f, cooldown: 1f, f => f
                    .Set("dashSpeed", 24f)
                    .Set("dashDuration", 0.22f)
                    .Set("hitSize", new Vector2(1.6f, 1.3f))
                    .Set("knockbackMultiplier", 1.2f));

            created += Buff("Sangre Fría", "Te cura, te vuelve intocable un segundo y sube tu daño.",
                new Color(0.95f, 0.25f, 0.35f), damage: 0f, cooldown: 12f, f => f
                    .Set("healAmount", 15f)
                    .Set("invulnerabilitySeconds", 1.2f)
                    .Set("damageMultiplier", 1.5f)
                    .Set("damageBuffSeconds", 5f)
                    .Set("knockbackMultiplier", 0f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AbilityLibrary.Invalidate();

            Debug.Log($"[AbilityStarterPack] {created} habilidades nuevas en {Folder} " +
                      "(las que ya existían se han dejado como estaban).");
        }

        // ------------------------------------------------------------------ atajos por arquetipo

        private static int Melee(string name, string description, Color accent, float damage,
                                 float cooldown, Func<Fields, Fields> configure) =>
            Create<MeleeArcAbility>(name, description, AbilityCategory.Melee, accent, damage, cooldown, configure);

        private static int Projectile(string name, string description, Color accent, float damage,
                                      float cooldown, Func<Fields, Fields> configure) =>
            Create<ProjectileAbility>(name, description, AbilityCategory.Ranged, accent, damage, cooldown, configure);

        private static int Nova(string name, string description, Color accent, float damage,
                                float cooldown, Func<Fields, Fields> configure) =>
            Create<NovaAbility>(name, description, AbilityCategory.Area, accent, damage, cooldown, configure);

        private static int Beam(string name, string description, Color accent, float damage,
                                float cooldown, Func<Fields, Fields> configure) =>
            Create<BeamAbility>(name, description, AbilityCategory.Area, accent, damage, cooldown, configure);

        private static int Zone(string name, string description, Color accent, float damage,
                                float cooldown, Func<Fields, Fields> configure) =>
            Create<ZoneAbility>(name, description, AbilityCategory.Area, accent, damage, cooldown, configure);

        private static int Orbit(string name, string description, Color accent, float damage,
                                 float cooldown, Func<Fields, Fields> configure) =>
            Create<OrbitAbility>(name, description, AbilityCategory.Area, accent, damage, cooldown, configure);

        private static int Turret(string name, string description, Color accent, float damage,
                                  float cooldown, Func<Fields, Fields> configure) =>
            Create<TurretAbility>(name, description, AbilityCategory.Area, accent, damage, cooldown, configure);

        private static int Dash(string name, string description, Color accent, float damage,
                                float cooldown, Func<Fields, Fields> configure) =>
            Create<DashStrikeAbility>(name, description, AbilityCategory.Utility, accent, damage, cooldown, configure);

        private static int Buff(string name, string description, Color accent, float damage,
                                float cooldown, Func<Fields, Fields> configure) =>
            Create<BuffAbility>(name, description, AbilityCategory.Utility, accent, damage, cooldown, configure);

        // ------------------------------------------------------------------ creación

        private static int Create<T>(string displayName, string description, AbilityCategory category,
                                     Color accent, float damage, float cooldown,
                                     Func<Fields, Fields> configure) where T : AbilityDefinition
        {
            string path = $"{Folder}/{FileName(displayName)}.asset";
            if (File.Exists(path)) return 0;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            var fields = new Fields(asset)
                .Set("displayName", displayName)
                .Set("description", description)
                .Set("category", (int)category)
                .Set("accent", accent)
                .Set("damage", damage)
                .Set("cooldown", cooldown);

            configure(fields);
            fields.Apply();

            return 1;
        }

        /// <summary>
        /// Nombre de archivo a partir del nombre visible: sin acentos ni espacios, para que el
        /// asset se pueda buscar y versionar cómodamente. El nombre bonito vive dentro del asset.
        /// </summary>
        private static string FileName(string displayName)
        {
            var normalized = displayName
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace("Á", "A").Replace("É", "E").Replace("Í", "I")
                .Replace("Ó", "O").Replace("Ú", "U").Replace("Ñ", "N");

            return "Ability_" + normalized.Replace(" ", "");
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Abilities");
        }

        /// <summary>
        /// Escritor de campos serializados encadenable. Se usa <see cref="SerializedObject"/> y no
        /// asignación directa porque los campos de las habilidades son privados a propósito: la
        /// clase controla su propia API y sólo el editor puede rellenarlos.
        /// </summary>
        private class Fields
        {
            private readonly SerializedObject _so;

            public Fields(UnityEngine.Object target) => _so = new SerializedObject(target);

            public Fields Set(string path, float value) => Write(path, p => p.floatValue = value);
            public Fields Set(string path, int value) => Write(path, p => p.intValue = value);
            public Fields Set(string path, bool value) => Write(path, p => p.boolValue = value);
            public Fields Set(string path, string value) => Write(path, p => p.stringValue = value);
            public Fields Set(string path, Vector2 value) => Write(path, p => p.vector2Value = value);
            public Fields Set(string path, Color value) => Write(path, p => p.colorValue = value);

            private Fields Write(string path, Action<SerializedProperty> write)
            {
                var property = _so.FindProperty(path);
                if (property == null)
                {
                    Debug.LogWarning($"[AbilityStarterPack] El campo '{path}' no existe en {_so.targetObject.name}.");
                    return this;
                }

                // Los enums se escriben por intValue, igual que los int: SerializedProperty no
                // distingue, y así una sola sobrecarga cubre ambos.
                write(property);
                return this;
            }

            public void Apply() => _so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
