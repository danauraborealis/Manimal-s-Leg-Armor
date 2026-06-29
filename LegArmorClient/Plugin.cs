using BepInEx;
using BepInEx.Logging;
using Manimal.LegArmor.Patches;

namespace Manimal.LegArmor
{
    // ModInfo is generated from Directory.Build.props - bump the version there.
    [BepInPlugin(ModInfo.Guid, ModInfo.ForgeName, ModInfo.Version)]
    [BepInDependency("com.wtt.commonlib")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;
        public static Plugin Instance;

        // tpls in ServerModFiles/db/CustomItems/LegArmors.json.
        public const string LegArmorClass2ThighsTpl = "5e9c4f1d8a2b4c3d7f0e2a01";
        public const string LegArmorClass3ThighsTpl = "5e9c4f1d8a2b4c3d7f0e2a02";
        public const string LegArmorClass2FullTpl   = "5e9c4f1d8a2b4c3d7f0e2a03";
        public const string LegArmorClass3FullTpl   = "5e9c4f1d8a2b4c3d7f0e2a04";

        // mirrors LegArmorHolderService.HolderTpl.
        public const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        private void Awake()
        {
            LogSource = Logger;
            Instance = this;

            // each patch is its own ModulePatch so one broken one wont block the rest.
            new InventoryArmorAggregatePatch().Enable();
            new EquipmentTabShowPatch().Enable();
            new LegArmorContainersPanelPatch().Enable();
            new HideHolderGridPatch().Enable();
            new EquipItemWindowSlotIdPatch().Enable();
            new ArmorVestRejectLegArmorPatch().Enable();
            new ArmorVestHideLegArmorsPatch().Enable();
            new LegArmorIsSearchedPatch().Enable();
            new LegArmorExaminedPatch().Enable();
            new LegArmorSlotGatePatch().Enable();
            new LegArmorCanModifyItemPatch().Enable();
            new LegArmorCanPlaceInPatch().Enable();
            new LegArmorIsItemKnownPatch().Enable();
            new LegArmorFindSlotPatch().Enable();

            new PlayerBodyMountLegArmorPatch().Enable();

            LogSource.LogInfo($"LegArmor loaded v{ModInfo.Version}");
        }
    }
}
