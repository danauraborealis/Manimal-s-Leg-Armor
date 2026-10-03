using EFT.InventoryLogic;
using WTTClientCommonLib.Attributes;

namespace Manimal.LegArmor
{
    // client-side type for items with our LegArmor parent. EFT's item
    // factory refuses to load if any taxonomy node has no registered C#
    // type. extends Armor (vanilla soft armor) with no new
    // behavior - the subclass only exists so [CustomParent] has something
    // to point at.
    [CustomParent("5e9c4f1d8a2b4c3d7f0e1c00", typeof(LegArmorItem), typeof(ArmorTemplate))]
    public class LegArmorItem : Armor
    {
        public LegArmorItem(string id, ArmorTemplate template) : base(id, template) { }
    }
}
