using System;
using System.Collections.Generic;

namespace RedMagic.Items
{
    /// <summary>
    /// Cuenta viva de puntos por tag de sinergia y <b>única fuente de verdad</b> de qué umbrales
    /// están activos. Se alimenta sólo de los eventos de <see cref="WeaponInventory"/>; ningún otro
    /// sistema debe recontar tags — la UI y los efectos de umbral consultan este objeto.
    ///
    /// Reglas de conteo (documento de diseño, sección 5):
    /// <list type="bullet">
    /// <item>Cada item equipado suma +1 a cada una de sus tags. Cada aparición cuenta: el mismo
    /// asset en dos slots libres suma 2.</item>
    /// <item>El arma suma su tag universal siempre y su elemento innato si tiene uno.</item>
    /// <item>El slot Elemento suma sus 2 tags (1 elemental + 1 universal); Trayectoria y Forma
    /// suman sus 2 tags universales.</item>
    /// <item>Tope 6 (<see cref="BuildTags.SynergyCap"/>): por encima no se gana nada. El bruto se
    /// sigue contando —para que quitar 1 item de 7 deje 6 y no 5— pero los umbrales miran el
    /// valor topado.</item>
    /// </list>
    ///
    /// Umbrales v1: 2 / 4 / 6 para todas las tags (<see cref="BuildTags.Thresholds"/>). Cuando los
    /// universales tengan su propia escala, sólo cambia de dónde lee este tracker esos números.
    ///
    /// <b>No implementa ningún efecto de umbral</b>: sólo cuenta y avisa. La tag
    /// <see cref="BuildTag.Reset"/> se cuenta como cualquier otra; su mecánica especial (reducir N)
    /// vive en los items Reset y consultará <see cref="PointsFor"/> aquí.
    /// </summary>
    public sealed class SynergyTracker : IDisposable
    {
        private readonly WeaponInventory _inventory;
        private readonly Dictionary<BuildTag, int> _points = new Dictionary<BuildTag, int>();

        /// <summary>(tag, umbral 2/4/6) justo al cruzarlo hacia arriba.</summary>
        public event Action<BuildTag, int> ThresholdReached;

        /// <summary>(tag, umbral 2/4/6) justo al caer por debajo (al desequipar).</summary>
        public event Action<BuildTag, int> ThresholdLost;

        /// <summary>(tag, puntos antes, puntos después) en bruto, en cada cambio. Para la UI.</summary>
        public event Action<BuildTag, int, int> PointsChanged;

        /// <summary>
        /// Engancha a la inventory y cuenta en silencio lo que ya hubiera equipado. Quien se
        /// suscriba después lee el estado inicial por la API de consulta y recibe eventos sólo de
        /// los cambios siguientes.
        /// </summary>
        public SynergyTracker(WeaponInventory inventory)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            foreach (var (_, item) in _inventory.EquippedItems())
                AddTags(item, silent: true);
            AddWeaponTags(_inventory.Weapon, silent: true);

            _inventory.ItemEquipped += OnItemEquipped;
            _inventory.ItemUnequipped += OnItemUnequipped;
            _inventory.WeaponChanged += OnWeaponChanged;
        }

        public void Dispose()
        {
            _inventory.ItemEquipped -= OnItemEquipped;
            _inventory.ItemUnequipped -= OnItemUnequipped;
            _inventory.WeaponChanged -= OnWeaponChanged;
        }

        // -- Consulta --------------------------------------------------------------------------------

        /// <summary>Puntos en bruto de una tag. Puede pasar de 6.</summary>
        public int PointsFor(BuildTag tag) => _points.TryGetValue(tag, out int points) ? points : 0;

        /// <summary>Puntos que cuentan para los umbrales: <c>min(bruto, 6)</c>.</summary>
        public int EffectivePointsFor(BuildTag tag) => Math.Min(PointsFor(tag), BuildTags.SynergyCap);

        /// <summary>
        /// true si la tag está en el tope (6+ en bruto). Llevar más items de esa tag no da nada
        /// extra; la UI la marca como "completa".
        /// </summary>
        public bool IsCapped(BuildTag tag) => PointsFor(tag) >= BuildTags.SynergyCap;

        /// <summary>true si el umbral indicado (2, 4 ó 6) está activo ahora mismo.</summary>
        public bool IsThresholdActive(BuildTag tag, int threshold) =>
            EffectivePointsFor(tag) >= threshold;

        /// <summary>
        /// Tier activo: 0 por debajo de 2, 1 con 2-3, 2 con 4-5, 3 con 6+. Es el número de umbrales
        /// de <see cref="BuildTags.Thresholds"/> alcanzados.
        /// </summary>
        public int ActiveTier(BuildTag tag)
        {
            int effective = EffectivePointsFor(tag);
            int tier = 0;
            var thresholds = BuildTags.Thresholds;
            for (int i = 0; i < thresholds.Length; i++)
                if (effective >= thresholds[i]) tier = i + 1;
            return tier;
        }

        /// <summary>Umbrales (2/4/6) activos ahora para la tag, en orden ascendente.</summary>
        public IEnumerable<int> ActiveThresholds(BuildTag tag)
        {
            int effective = EffectivePointsFor(tag);
            foreach (int threshold in BuildTags.Thresholds)
                if (effective >= threshold) yield return threshold;
        }

        /// <summary>Siguiente umbral sin alcanzar, o -1 si ya están todos.</summary>
        public int NextThreshold(BuildTag tag)
        {
            int effective = EffectivePointsFor(tag);
            foreach (int threshold in BuildTags.Thresholds)
                if (effective < threshold) return threshold;
            return -1;
        }

        /// <summary>Puntos que faltan para <see cref="NextThreshold"/>; 0 si no hay siguiente.</summary>
        public int PointsToNextThreshold(BuildTag tag)
        {
            int next = NextThreshold(tag);
            return next < 0 ? 0 : next - EffectivePointsFor(tag);
        }

        /// <summary>
        /// Tags con al menos 1 punto ahora. Para la lista fija de la UI (Hielo, Fuego, Tanque…)
        /// itera <see cref="BuildTags.Elementals"/> / <see cref="BuildTags.Universals"/> y consulta
        /// <see cref="PointsFor"/>.
        /// </summary>
        public IEnumerable<BuildTag> TagsWithPoints()
        {
            foreach (var pair in _points)
                if (pair.Value > 0) yield return pair.Key;
        }

        // -- Reacción a la inventory ----------------------------------------------------------------

        private void OnItemEquipped(InventorySlot slot, ItemDefinition item) => AddTags(item, silent: false);

        private void OnItemUnequipped(InventorySlot slot, ItemDefinition item) => RemoveTags(item, silent: false);

        private void OnWeaponChanged(WeaponDefinition previous, WeaponDefinition next)
        {
            RemoveWeaponTags(previous, silent: false);
            AddWeaponTags(next, silent: false);
        }

        private void AddTags(ItemDefinition item, bool silent)
        {
            if (item == null) return;
            var tags = item.Tags;
            for (int i = 0; i < tags.Count; i++) Adjust(tags[i], +1, silent);
        }

        private void RemoveTags(ItemDefinition item, bool silent)
        {
            if (item == null) return;
            var tags = item.Tags;
            for (int i = 0; i < tags.Count; i++) Adjust(tags[i], -1, silent);
        }

        private void AddWeaponTags(WeaponDefinition weapon, bool silent)
        {
            if (weapon == null) return;
            foreach (var tag in weapon.ContributedTags()) Adjust(tag, +1, silent);
        }

        private void RemoveWeaponTags(WeaponDefinition weapon, bool silent)
        {
            if (weapon == null) return;
            foreach (var tag in weapon.ContributedTags()) Adjust(tag, -1, silent);
        }

        /// <summary>
        /// Único punto donde cambia la cuenta. Mueve el bruto y, si el valor topado cruzó algún
        /// umbral, dispara los eventos: <see cref="ThresholdReached"/> en orden ascendente al
        /// subir, <see cref="ThresholdLost"/> en descendente al bajar, para que un oyente pueda
        /// apilar y desapilar efectos en orden. Con deltas de ±1 se cruza como mucho un umbral por
        /// llamada; los bucles cubren un futuro ajuste en bloque.
        /// </summary>
        private void Adjust(BuildTag tag, int delta, bool silent)
        {
            int oldRaw = PointsFor(tag);
            int newRaw = Math.Max(0, oldRaw + delta);
            if (newRaw == oldRaw) return;

            if (newRaw == 0) _points.Remove(tag);
            else _points[tag] = newRaw;

            if (silent) return;

            PointsChanged?.Invoke(tag, oldRaw, newRaw);

            int oldEffective = Math.Min(oldRaw, BuildTags.SynergyCap);
            int newEffective = Math.Min(newRaw, BuildTags.SynergyCap);
            if (newEffective == oldEffective) return;

            var thresholds = BuildTags.Thresholds;
            if (newEffective > oldEffective)
            {
                for (int i = 0; i < thresholds.Length; i++)
                    if (oldEffective < thresholds[i] && newEffective >= thresholds[i])
                        ThresholdReached?.Invoke(tag, thresholds[i]);
            }
            else
            {
                for (int i = thresholds.Length - 1; i >= 0; i--)
                    if (oldEffective >= thresholds[i] && newEffective < thresholds[i])
                        ThresholdLost?.Invoke(tag, thresholds[i]);
            }
        }
    }
}
