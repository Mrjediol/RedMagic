using System;
using System.Collections.Generic;

namespace RedMagic.Items
{
    /// <summary>
    /// Referencia a un hueco concreto del arma: uno de los 3 dedicados, o uno de los 6 libres por
    /// índice. Se pasa en los eventos para que el oyente sepa qué cambió sin consultar de vuelta.
    /// </summary>
    public readonly struct InventorySlot : IEquatable<InventorySlot>
    {
        /// <summary>Familia del hueco. <see cref="ItemSlot.Free"/> = uno de los 6 libres.</summary>
        public readonly ItemSlot Kind;

        /// <summary>Índice 0..5 cuando <see cref="Kind"/> es <see cref="ItemSlot.Free"/>; -1 en los dedicados.</summary>
        public readonly int FreeIndex;

        private InventorySlot(ItemSlot kind, int freeIndex)
        {
            Kind = kind;
            FreeIndex = freeIndex;
        }

        public static InventorySlot Dedicated(ItemSlot kind) => new InventorySlot(kind, -1);
        public static InventorySlot Free(int index) => new InventorySlot(ItemSlot.Free, index);

        public bool IsFree => Kind == ItemSlot.Free;

        public bool Equals(InventorySlot other) => Kind == other.Kind && FreeIndex == other.FreeIndex;
        public override bool Equals(object obj) => obj is InventorySlot other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ FreeIndex;
        public override string ToString() => IsFree ? $"Free[{FreeIndex}]" : Kind.ToString();
    }

    /// <summary>
    /// Los 9 huecos de items de un arma: 3 dedicados (Elemento, Trayectoria, Forma — exactamente
    /// 1 item cada uno, "último equipado gana") + 6 libres (cualquier item de pool, sin
    /// restricción entre ellos). Guarda además el arma equipada, porque el arma también aporta
    /// tags a la identidad de build.
    ///
    /// <b>Sólo gestiona qué item hay en cada hueco y lo avisa por eventos.</b> No cuenta sinergias
    /// ni sabe nada de UI: el synergy tracker y las pantallas se suscriben a
    /// <see cref="ItemEquipped"/> / <see cref="ItemUnequipped"/> y llevan su propia cuenta, sin
    /// acoplarse a esta clase.
    ///
    /// Es una clase C# normal (sin ciclo de vida de Unity) para poder probarla sola. Dónde vive
    /// (singleton de run, jugador de la run…) y el borrado al terminar la partida se deciden en un
    /// paso posterior; aquí está <see cref="Clear"/> para ese uso.
    ///
    /// <para><b>Contrato de eventos al reemplazar un item:</b> primero
    /// <see cref="ItemUnequipped"/> con el item que sale, luego <see cref="ItemEquipped"/> con el
    /// que entra. Así un oyente que haga "restar tags / sumar tags" nunca ve un estado
    /// intermedio incoherente. Equipar el mismo asset que ya estaba en el hueco es un no-op y no
    /// dispara eventos.</para>
    /// </summary>
    public sealed class WeaponInventory
    {
        public const int FreeSlotCount = 6;

        private ElementModifier _element;
        private TrajectoryModifier _trajectory;
        private ShapeModifier _shape;
        private readonly FreePoolItemDefinition[] _free = new FreePoolItemDefinition[FreeSlotCount];

        private WeaponDefinition _weapon;

        /// <summary>(hueco, item que entra). Se dispara después de que el item ya está colocado.</summary>
        public event Action<InventorySlot, ItemDefinition> ItemEquipped;

        /// <summary>(hueco, item que sale). Se dispara después de que el hueco ya quedó vacío.</summary>
        public event Action<InventorySlot, ItemDefinition> ItemUnequipped;

        /// <summary>(arma anterior, arma nueva). Cualquiera de las dos puede ser null.</summary>
        public event Action<WeaponDefinition, WeaponDefinition> WeaponChanged;

        // -- Arma -------------------------------------------------------------------------------

        public WeaponDefinition Weapon => _weapon;

        public void SetWeapon(WeaponDefinition weapon)
        {
            if (ReferenceEquals(_weapon, weapon)) return;

            var previous = _weapon;
            _weapon = weapon;
            WeaponChanged?.Invoke(previous, weapon);
        }

        // -- Lectura --------------------------------------------------------------------------------

        public ElementModifier Element => _element;
        public TrajectoryModifier Trajectory => _trajectory;
        public ShapeModifier Shape => _shape;

        /// <summary>Item del dedicado indicado, o null. Devuelve null para <see cref="ItemSlot.Free"/>.</summary>
        public ItemDefinition GetDedicated(ItemSlot slot) => slot switch
        {
            ItemSlot.DedicatedElement => _element,
            ItemSlot.DedicatedTrajectory => _trajectory,
            ItemSlot.DedicatedShape => _shape,
            _ => null,
        };

        public IReadOnlyList<FreePoolItemDefinition> FreeSlots => _free;

        /// <summary>Item del slot libre <paramref name="index"/>, o null (también si el índice es inválido).</summary>
        public FreePoolItemDefinition GetFree(int index) =>
            (uint)index < FreeSlotCount ? _free[index] : null;

        public int FreeSlotsUsed
        {
            get
            {
                int n = 0;
                for (int i = 0; i < FreeSlotCount; i++)
                    if (_free[i] != null) n++;
                return n;
            }
        }

        public bool FreeSlotsFull => FreeSlotsUsed >= FreeSlotCount;

        /// <summary>Recorre todos los huecos con item. Lo usará el synergy tracker para reconstruir su cuenta.</summary>
        public IEnumerable<(InventorySlot slot, ItemDefinition item)> EquippedItems()
        {
            if (_element != null)
                yield return (InventorySlot.Dedicated(ItemSlot.DedicatedElement), _element);
            if (_trajectory != null)
                yield return (InventorySlot.Dedicated(ItemSlot.DedicatedTrajectory), _trajectory);
            if (_shape != null)
                yield return (InventorySlot.Dedicated(ItemSlot.DedicatedShape), _shape);

            for (int i = 0; i < FreeSlotCount; i++)
                if (_free[i] != null)
                    yield return (InventorySlot.Free(i), _free[i]);
        }

        // -- Equipar / desequipar -----------------------------------------------------------------

        /// <summary>
        /// Equipa <paramref name="item"/> en el hueco que le toca <b>por su tipo</b>: un
        /// <see cref="ElementModifier"/> va al slot Elemento, un <see cref="TrajectoryModifier"/> al
        /// de Trayectoria, etc. Un item de pool libre necesita un índice 0..5 en
        /// <paramref name="freeSlotIndex"/> (o -1 = primer hueco libre disponible).
        ///
        /// El emparejamiento hueco↔tipo es estructural: no hay forma de meter un item de pool en un
        /// slot dedicado ni al revés, así que "sólo acepta el tipo correcto" se cumple solo.
        /// </summary>
        /// <returns>
        /// false si el item es null, si es de otro tipo desconocido, si es de pool y no cabe, o si
        /// <paramref name="freeSlotIndex"/> está fuera de 0..5.
        /// </returns>
        public bool TryEquip(ItemDefinition item, int freeSlotIndex = -1)
        {
            switch (item)
            {
                case null:
                    return false;
                case ElementModifier element:
                    EquipElement(element);
                    return true;
                case TrajectoryModifier trajectory:
                    EquipTrajectory(trajectory);
                    return true;
                case ShapeModifier shape:
                    EquipShape(shape);
                    return true;
                case FreePoolItemDefinition free:
                    if (freeSlotIndex < 0)
                        return TryAddFree(free, out _);
                    if ((uint)freeSlotIndex >= FreeSlotCount)
                        return false;
                    EquipFree(freeSlotIndex, free);
                    return true;
                default:
                    return false;
            }
        }

        public void EquipElement(ElementModifier item) =>
            SetDedicated(InventorySlot.Dedicated(ItemSlot.DedicatedElement), ref _element, item);

        public void EquipTrajectory(TrajectoryModifier item) =>
            SetDedicated(InventorySlot.Dedicated(ItemSlot.DedicatedTrajectory), ref _trajectory, item);

        public void EquipShape(ShapeModifier item) =>
            SetDedicated(InventorySlot.Dedicated(ItemSlot.DedicatedShape), ref _shape, item);

        /// <summary>Vacía un dedicado. Devuelve el item que estaba (o null).</summary>
        public ItemDefinition UnequipDedicated(ItemSlot slot)
        {
            switch (slot)
            {
                case ItemSlot.DedicatedElement:
                {
                    var previous = _element;
                    SetDedicated(InventorySlot.Dedicated(slot), ref _element, null);
                    return previous;
                }
                case ItemSlot.DedicatedTrajectory:
                {
                    var previous = _trajectory;
                    SetDedicated(InventorySlot.Dedicated(slot), ref _trajectory, null);
                    return previous;
                }
                case ItemSlot.DedicatedShape:
                {
                    var previous = _shape;
                    SetDedicated(InventorySlot.Dedicated(slot), ref _shape, null);
                    return previous;
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// Pone <paramref name="item"/> en el slot libre <paramref name="index"/> (null para
        /// vaciarlo). "Último equipado gana": si había otro item, sale primero. Devuelve el item
        /// que estaba (o null).
        /// </summary>
        public FreePoolItemDefinition EquipFree(int index, FreePoolItemDefinition item)
        {
            if ((uint)index >= FreeSlotCount)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"El índice de slot libre debe estar entre 0 y {FreeSlotCount - 1}.");

            var previous = _free[index];
            if (ReferenceEquals(previous, item))
                return previous;

            var slot = InventorySlot.Free(index);
            if (previous != null)
            {
                _free[index] = null;
                ItemUnequipped?.Invoke(slot, previous);
            }

            _free[index] = item;
            if (item != null)
                ItemEquipped?.Invoke(slot, item);

            return previous;
        }

        /// <summary>Mete el item en el primer slot libre vacío. false (y index -1) si están todos llenos.</summary>
        public bool TryAddFree(FreePoolItemDefinition item, out int index)
        {
            index = -1;
            if (item == null)
                return false;

            for (int i = 0; i < FreeSlotCount; i++)
            {
                if (_free[i] == null)
                {
                    index = i;
                    EquipFree(i, item);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Vacía un slot libre. Devuelve el item que estaba (o null).</summary>
        public FreePoolItemDefinition UnequipFree(int index)
        {
            if ((uint)index >= FreeSlotCount)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"El índice de slot libre debe estar entre 0 y {FreeSlotCount - 1}.");

            var previous = _free[index];
            if (previous == null)
                return null;

            _free[index] = null;
            ItemUnequipped?.Invoke(InventorySlot.Free(index), previous);
            return previous;
        }

        /// <summary>
        /// Vacía los 9 huecos, disparando un <see cref="ItemUnequipped"/> por cada uno que
        /// tuviera item. No toca el arma. Pensado para el reset de fin de run (paso posterior).
        /// </summary>
        public void Clear()
        {
            UnequipDedicated(ItemSlot.DedicatedElement);
            UnequipDedicated(ItemSlot.DedicatedTrajectory);
            UnequipDedicated(ItemSlot.DedicatedShape);
            for (int i = 0; i < FreeSlotCount; i++)
                UnequipFree(i);
        }

        // -- Interno --------------------------------------------------------------------------------

        /// <summary>
        /// Núcleo de "último equipado gana" para los dedicados: si el hueco tenía otro item, sale
        /// primero (evento Unequipped) y luego entra el nuevo (evento Equipped). El mismo asset ya
        /// puesto es un no-op silencioso.
        /// </summary>
        private void SetDedicated<T>(InventorySlot slot, ref T field, T item) where T : ItemDefinition
        {
            if (ReferenceEquals(field, item))
                return;

            if (field != null)
            {
                var outgoing = field;
                field = null;
                ItemUnequipped?.Invoke(slot, outgoing);
            }

            field = item;
            if (item != null)
                ItemEquipped?.Invoke(slot, item);
        }
    }
}
