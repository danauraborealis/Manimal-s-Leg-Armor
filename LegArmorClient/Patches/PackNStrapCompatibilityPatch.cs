using System;
using System.Reflection;
using BepInEx.Bootstrap;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Manimal.LegArmor.Patches
{
    // PackNStrap 2.1.0/2.1.1 counts corpse-container children and assumes that a
    // five-child layout contains the direct content ArmBand Slot. LegArmor
    // adds a wrapper row, so that count can pass while the slot is absent.
    //
    // This is deliberately scoped to the verified 4.1 plugins and their original
    // zero-argument Slot-returning method. A later PackNStrap UI contract must
    // be checked before this compatibility guard is extended.
    public sealed class PackNStrapCompatibilityPatch : ModulePatch
    {
        private const string PackNStrapGuid = "com.trenchfoot.beltslot";
        private const string SupportedVersions = "2.1.0 or 2.1.1";
        private const string UiMappingsTypeName = "BeltSlot.Helpers.UI_Mappings";
        private const string MappingMethodName = "getComplexLootUI_Mappings";
        private const string InventoryScreenFieldName = "inventoryScreen";
        private const string LootContainerPath =
            "Items Panel/Stash Panel/Complex Loot Panel/Containers Scrollview/Content";
        private const string GearPanelName = "Gear Panel Template(Clone)";
        private const string ArmBandSlotName = "ArmBand Slot";

        private static readonly Type[] NoParameters = Type.EmptyTypes;
        private static MethodBase _targetMethod;
        private static FieldInfo _inventoryScreenField;
        private static bool _enabled;
        private static bool _warningLogged;

        public static void TryEnable()
        {
            if (_enabled) return;

            MethodBase targetMethod;
            string warning;
            Version installedVersion;
            try
            {
                if (!TryResolveTarget(out targetMethod, out installedVersion, out warning))
                {
                    if (warning != null) WarnOnce(warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                WarnOnce($"PackNStrap compatibility discovery failed; corpse-loot compatibility was not enabled: {ex}");
                return;
            }

            _targetMethod = targetMethod;
            try
            {
                new PackNStrapCompatibilityPatch().Enable();
                _enabled = true;
                Plugin.LogSource?.LogInfo($"[LegArmor] PackNStrap {installedVersion} corpse-loot compatibility enabled");
            }
            catch (Exception ex)
            {
                WarnOnce($"PackNStrap {installedVersion} was detected, but its corpse-loot compatibility patch could not be enabled: {ex}");
            }
        }

        protected override MethodBase GetTargetMethod()
        {
            return _targetMethod;
        }

        [PatchPrefix]
        private static bool Prefix(object __instance, ref Slot __result)
        {
            if (TryGetValidComplexLootSlot(__instance, out var slot))
                return true;

            // PackNStrap's SetLootArmbandSlotOnOpen sees null and resets
            // complexStashPanelLoaded, preserving its normal retry/cleanup
            // path instead of allowing its mapping method to throw.
            __result = null;
            return false;
        }

        private static bool TryResolveTarget(out MethodBase targetMethod, out Version installedVersion, out string warning)
        {
            targetMethod = null;
            installedVersion = null;
            warning = null;

            if (!Chainloader.PluginInfos.TryGetValue(PackNStrapGuid, out var pluginInfo) || pluginInfo == null)
                return false;

            installedVersion = pluginInfo.Metadata?.Version;
            if (installedVersion == null || installedVersion.Major != 2 || installedVersion.Minor != 1 ||
                (installedVersion.Build != 0 && installedVersion.Build != 1) || installedVersion.Revision > 0)
            {
                warning = $"PackNStrap {installedVersion?.ToString() ?? "an unknown version"} is present; " +
                          $"corpse-loot compatibility is scoped to PackNStrap {SupportedVersions} and was not enabled";
                return false;
            }

            var pluginInstance = pluginInfo.Instance;
            var pluginAssembly = pluginInstance?.GetType().Assembly;
            if (pluginAssembly == null)
            {
                warning = $"PackNStrap {installedVersion} is present, but its loaded plugin assembly could not be found; " +
                          "corpse-loot compatibility was not enabled";
                return false;
            }

            var mappingsType = pluginAssembly.GetType(UiMappingsTypeName, false);
            if (mappingsType == null)
            {
                warning = $"PackNStrap {installedVersion} does not expose {UiMappingsTypeName}; " +
                          "corpse-loot compatibility was not enabled";
                return false;
            }

            var mappingMethod = mappingsType.GetMethod(
                MappingMethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: NoParameters,
                modifiers: null);
            if (mappingMethod == null || mappingMethod.ReturnType != typeof(Slot))
            {
                warning = $"PackNStrap {installedVersion} has an incompatible {MappingMethodName} signature; " +
                          "corpse-loot compatibility was not enabled";
                return false;
            }

            var inventoryScreenField = mappingsType.GetField(
                InventoryScreenFieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (inventoryScreenField == null || inventoryScreenField.IsInitOnly || inventoryScreenField.IsStatic ||
                !typeof(InventoryScreen).IsAssignableFrom(inventoryScreenField.FieldType))
            {
                warning = $"PackNStrap {installedVersion} has an incompatible {InventoryScreenFieldName} field; " +
                          "corpse-loot compatibility was not enabled";
                return false;
            }

            _inventoryScreenField = inventoryScreenField;
            targetMethod = mappingMethod;
            return true;
        }

        private static bool TryGetValidComplexLootSlot(object mappings, out Slot slot)
        {
            slot = null;
            if (mappings == null || _inventoryScreenField == null) return false;

            var inventoryScreen = _inventoryScreenField.GetValue(mappings) as InventoryScreen;
            if (inventoryScreen == null)
            {
                var commonUi = Singleton<CommonUI>.Instantiated ? Singleton<CommonUI>.Instance : null;
                inventoryScreen = commonUi?.InventoryScreen;
                if (inventoryScreen == null) return false;

                _inventoryScreenField.SetValue(mappings, inventoryScreen);
            }

            var lootContainer = inventoryScreen.transform.Find(LootContainerPath);
            if (lootContainer == null) return false;

            var gearPanel = lootContainer.Find(GearPanelName);
            if (gearPanel == null || gearPanel.Find(ArmBandSlotName) == null) return false;

            var directArmBandSlot = lootContainer.Find(ArmBandSlotName);
            if (directArmBandSlot == null) return false;

            var slotView = directArmBandSlot.GetComponent<SlotView>();
            if (slotView == null || slotView.Slot == null) return false;

            slot = slotView.Slot;
            return true;
        }

        private static void WarnOnce(string message)
        {
            if (_warningLogged) return;
            _warningLogged = true;
            Plugin.LogSource?.LogWarning($"[LegArmor] {message}");
        }
    }
}
