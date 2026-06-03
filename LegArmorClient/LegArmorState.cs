using EFT.InventoryLogic;

namespace Manimal.LegArmor
{
    // shared holder slot reference for hot-path patches (damage aggregation).
    // populated by EquipmentTabShowPatch on inventory open.
    public static class LegArmorState
    {
        public static Item Holder;
        public static Slot HolderSlot;
        public static string HolderItemId;

        public static void Bind(string holderItemId, Item holder, Slot slot)
        {
            HolderItemId = holderItemId;
            Holder = holder;
            HolderSlot = slot;
        }

        public static void Clear()
        {
            HolderItemId = null;
            Holder = null;
            HolderSlot = null;
        }
    }
}
