using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Carpeta y nombre legible de cada hueco para la pestaña Sounds, derivados de dónde vive:
    /// Player (Movement / Attack / Health / Pickup), Enemies/&lt;enemigo&gt;, Bosses/&lt;jefe&gt;,
    /// Projectiles, Interactables, UI (+ Menus), Shop, Legendary Passives, Run Flow, Economy,
    /// Build &amp; Status, Music. Textos de editor: no pasan por la localización.
    /// </summary>
    public static class SoundCategories
    {
        private static Dictionary<string, string> s_attackOwners;

        public static void Reset() => s_attackOwners = null;

        public static void Describe(SoundSlot slot, out string category, out string name)
        {
            string asset = Path.GetFileNameWithoutExtension(slot.AssetPath);
            switch (slot.Kind)
            {
                case SoundSlotKind.EmitterTrigger:
                    DescribeTrigger(slot, asset, out category, out name);
                    return;
                case SoundSlotKind.MusicField:
                    category = "Music";
                    name = slot.ComponentType == "BossController" ? $"Boss override · {Strip(asset, "Boss_")}" : "Menu";
                    return;
            }

            string member = slot.Member;
            switch (slot.ComponentType)
            {
                case "WeaponDefinition":
                {
                    string weapon = Strip(asset, "Weapon_");
                    bool shot = member is "fireSound" or "chargeSound";
                    category = shot ? $"Player/Attack/{weapon}" : $"Projectiles/Weapons/{weapon}";
                    name = Pretty(member);
                    return;
                }
                case "SystemSounds":
                    DescribeSystem(member, out category, out name);
                    return;
                case "LegendaryPassiveTuning":
                    category = "Legendary Passives";
                    name = Pretty(member);
                    return;
                case "PassiveDropCinematicSettings":
                    category = "Legendary Passives";
                    name = "Passive drop · Land";
                    return;
                case "ShopManager":
                    category = "Shop";
                    name = Pretty(member);
                    return;
                case "BossDefinition":
                {
                    category = $"Bosses/{Strip(asset, "Boss_")}";
                    int i = member.IndexOf('[');
                    int j = member.IndexOf(']');
                    int phase = i >= 0 && j > i && int.TryParse(member.Substring(i + 1, j - i - 1), out int p) ? p + 1 : 0;
                    name = $"Phase {phase} transition";
                    return;
                }
            }

            // Sonidos propios de un gesto del rig (ProceduralRig.gestures[i].sounds[j].sound): una fila por sonido.
            if (slot.ComponentType == "ProceduralRig" && member.StartsWith("gestures.Array.data[", StringComparison.Ordinal))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(slot.AssetPath);
                string owner = Has(prefab, "BossController") ? $"Bosses/{Strip(asset, "Boss_")}" : $"Enemies/{Strip(asset, "Enemy_")}";
                category = owner;

                string gestureName = "?";
                int open = member.IndexOf('['), close = member.IndexOf(']');
                var rig = prefab != null ? prefab.GetComponent<RedMagic.Rigging.ProceduralRig>() : null;
                if (rig != null && open >= 0 && close > open && int.TryParse(member.Substring(open + 1, close - open - 1), out int index) &&
                    index >= 0 && index < rig.gestures.Count)
                    gestureName = rig.gestures[index].name;

                // El segundo índice (el sonido dentro del gesto): el primero sin número, los demás "· 2", "· 3"…
                int open2 = member.IndexOf('[', close + 1), close2 = open2 >= 0 ? member.IndexOf(']', open2) : -1;
                int nth = open2 >= 0 && close2 > open2 && int.TryParse(member.Substring(open2 + 1, close2 - open2 - 1), out int n) ? n : 0;
                name = nth > 0 ? $"Gesture · {gestureName} · {nth + 1}" : $"Gesture · {gestureName}";
                return;
            }

            // Ataque de la baraja de un enemigo normal (EnemyAttack_<Enemy>_<id>.asset, una carpeta por enemigo).
            if (IsEnemyAttack(slot.AssetPath, out string enemy))
            {
                category = $"Enemies/{enemy}";
                string attack = Strip(asset, $"EnemyAttack_{enemy}_");
                name = member == "sound" ? $"Attack · {attack}" : $"Attack · {attack} · {Pretty(member)}";
                return;
            }

            if (IsBossAttack(slot.AssetPath, out string boss))
            {
                category = $"Bosses/{boss}/Attacks";
                string attack = Strip(asset, "BossAttack_");
                name = member == "sound" ? attack : $"{attack} · {Pretty(member)}";
                return;
            }

            category = $"Other/{slot.ComponentType}";
            name = $"{asset} · {Pretty(member)}";
        }

        private static void DescribeTrigger(SoundSlot slot, string asset, out string category, out string name)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(slot.AssetPath);
            var t = slot.Trigger;
            string extra = slot.Member.Contains("#") ? " " + slot.Member.Substring(slot.Member.IndexOf('#')) : "";

            if (root != null && root.CompareTag("Player"))
            {
                category = t switch
                {
                    SoundTrigger.Footstep or SoundTrigger.OnJump or SoundTrigger.OnAirJump or
                    SoundTrigger.OnDash or SoundTrigger.OnLand => "Player/Movement",
                    SoundTrigger.OnAttack or SoundTrigger.OnRangedAttack => "Player/Attack",
                    SoundTrigger.OnLoot => "Player/Pickup",
                    _ => "Player/Health",
                };
                name = t switch
                {
                    SoundTrigger.OnAttack => "Melee attack",
                    SoundTrigger.OnHit => "Hurt",
                    SoundTrigger.OnLoot => "Pick up item",
                    _ => TriggerName(t),
                } + extra;
                return;
            }

            if (Has(root, "EnemyStats")) category = $"Enemies/{Strip(asset, "Enemy_")}";
            else if (Has(root, "BossController")) category = $"Bosses/{Strip(asset, "Boss_")}";
            else if (Has(root, "Projectile")) category = $"Projectiles/{Strip(asset, "Fx_")}";
            else category = $"Interactables/{asset}";

            bool projectile = category.StartsWith("Projectiles/");
            bool container = Has(root, "HubLootContainer");
            name = t switch
            {
                SoundTrigger.OnSpawn when projectile => "Launch",
                SoundTrigger.OnHit when projectile => "Hit target",
                SoundTrigger.OnDeath when projectile => "Expire",
                SoundTrigger.OnHit => "Hurt",
                SoundTrigger.OnInteract when container => "Open",
                SoundTrigger.OnInteract => "Use",
                SoundTrigger.OnLoot when container => "Take",
                _ => TriggerName(t),
            } + extra;
        }

        private static void DescribeSystem(string member, out string category, out string name)
        {
            // "itemMenu.open" → UI/Menus/Item · Open
            int dot = member.IndexOf('.');
            if (dot > 0 && member.Substring(0, dot).EndsWith("Menu"))
            {
                string menu = member.Substring(0, dot);
                category = "UI/Menus/" + Pretty(menu.Substring(0, menu.Length - 4));
                name = Pretty(member.Substring(dot + 1));
                return;
            }

            switch (member)
            {
                case "uiHover": case "uiFocus": case "uiClick": case "uiBack": case "uiDeny":
                    category = "UI";
                    name = Pretty(member.Substring(2));
                    return;
                case "upgradeBought": case "weaponLevelUp": case "mirrorPassiveUpgraded":
                    category = "UI/Menu actions";
                    break;
                case "shopEnter":
                    category = "Shop";
                    break;
                case "shopBuy": case "shopDeny": case "shopReroll":
                    category = "Shop";
                    name = Pretty(member.Substring(4));
                    return;
                case "goldGained": case "diamondGained": case "soulFragmentGained": case "skullGained":
                    category = "Economy";
                    break;
                case "synergyTierReached": case "slowApplied": case "goldMarkApplied":
                    category = "Build & Status";
                    break;
                case "enemyHurt": case "enemyDeath": case "enemyMove": case "enemyAttack":
                    category = "Enemies/Generic (fallback)";
                    name = Pretty(member.Substring(5));
                    return;
                default:
                    category = "Run Flow";
                    break;
            }
            name = Pretty(member);
        }

        // Assets/ScriptableObjects/EnemyAttacks/<Enemy>/…: la carpeta dice de quién es (la crea EnemyDeckImporter).
        private static bool IsEnemyAttack(string assetPath, out string enemy)
        {
            const string root = "Assets/ScriptableObjects/EnemyAttacks/";
            enemy = null;
            if (!assetPath.StartsWith(root, StringComparison.Ordinal)) return false;

            string rest = assetPath.Substring(root.Length);
            int slash = rest.IndexOf('/');
            if (slash <= 0) return false;

            enemy = rest.Substring(0, slash);
            return true;
        }

        private static bool IsBossAttack(string assetPath, out string boss)
        {
            if (s_attackOwners == null)
            {
                s_attackOwners = new Dictionary<string, string>();
                foreach (var guid in AssetDatabase.FindAssets("t:BossDefinition", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (SoundSlotScanner.IsIgnored(path)) continue;
                    string owner = Strip(Path.GetFileNameWithoutExtension(path), "Boss_");
                    foreach (var dep in AssetDatabase.GetDependencies(path, false))
                        if (dep.Contains("BossAttack_") && !s_attackOwners.ContainsKey(dep)) s_attackOwners[dep] = owner;
                }
            }
            return s_attackOwners.TryGetValue(assetPath, out boss);
        }

        private static string TriggerName(SoundTrigger t) => t switch
        {
            SoundTrigger.OnSpawn => "Spawn",
            SoundTrigger.OnHit => "Hit",
            SoundTrigger.OnDeath => "Death",
            SoundTrigger.OnAttack => "Attack",
            SoundTrigger.OnJump => "Jump",
            SoundTrigger.OnAirJump => "Air jump",
            SoundTrigger.OnDash => "Dash",
            SoundTrigger.Footstep => "Footstep",
            SoundTrigger.OnActivate => "Activation",
            SoundTrigger.OnInteract => "Interact",
            SoundTrigger.OnLoot => "Loot",
            SoundTrigger.OnLand => "Land",
            SoundTrigger.OnRangedAttack => "Ranged attack",
            SoundTrigger.OnHitTerrain => "Hit terrain",
            SoundTrigger.OnMove => "Move",
            SoundTrigger.OnWake => "Wake",
            SoundTrigger.OnHeal => "Heal",
            SoundTrigger.OnLowHealth => "Low health",
            SoundTrigger.OnVulnerable => "Vulnerable window",
            SoundTrigger.OnEvade => "Evade",
            _ => t.ToString(),
        };

        private static bool Has(GameObject root, string typeName)
        {
            if (root == null) return false;
            foreach (var c in root.GetComponents<Component>())
                for (var t = c != null ? c.GetType() : null; t != null; t = t.BaseType)
                    if (t.Name == typeName) return true;
            return false;
        }

        private static string Strip(string s, string prefix) =>
            s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;

        // "terrainImpactSound" → "Terrain impact"
        private static string Pretty(string member)
        {
            string m = member.EndsWith("Sound") && member.Length > 5 ? member.Substring(0, member.Length - 5) : member;
            string nice = ObjectNames.NicifyVariableName(m);
            return nice.Length > 1 ? nice.Substring(0, 1) + nice.Substring(1).ToLowerInvariant() : nice;
        }
    }
}
