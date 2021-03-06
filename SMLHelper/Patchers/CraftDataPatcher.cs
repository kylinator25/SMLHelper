﻿namespace SMLHelper.V2.Patchers
{
    using Assets;
    using Harmony;
    using System.Collections.Generic;
    using System.Reflection;

    internal class CraftDataPatcher
    {
        #region Internal Fields

        internal static IDictionary<TechType, ITechData> CustomTechData = new SelfCheckingDictionary<TechType, ITechData>("CustomTechData");
        internal static IDictionary<TechType, TechType> CustomHarvestOutputList = new SelfCheckingDictionary<TechType, TechType>("CustomHarvestOutputList");
        internal static IDictionary<TechType, HarvestType> CustomHarvestTypeList = new SelfCheckingDictionary<TechType, HarvestType>("CustomHarvestTypeList");
        internal static IDictionary<TechType, int> CustomFinalCutBonusList = new SelfCheckingDictionary<TechType, int>("CustomFinalCutBonusList", TechTypeExtensions.sTechTypeComparer);
        internal static IDictionary<TechType, Vector2int> CustomItemSizes = new SelfCheckingDictionary<TechType, Vector2int>("CustomItemSizes");
        internal static IDictionary<TechType, EquipmentType> CustomEquipmentTypes = new SelfCheckingDictionary<TechType, EquipmentType>("CustomEquipmentTypes");
        internal static IDictionary<TechType, QuickSlotType> CustomSlotTypes = new SelfCheckingDictionary<TechType, QuickSlotType>("CustomSlotTypes");
        internal static IDictionary<TechType, float> CustomCraftingTimes = new SelfCheckingDictionary<TechType, float>("CustomCraftingTimes");
        internal static IDictionary<TechType, TechType> CustomCookedCreatureList = new SelfCheckingDictionary<TechType, TechType>("CustomCookedCreatureList");
        internal static IDictionary<TechType, CraftData.BackgroundType> CustomBackgroundTypes = new SelfCheckingDictionary<TechType, CraftData.BackgroundType>("CustomBackgroundTypes", TechTypeExtensions.sTechTypeComparer);
        internal static List<TechType> CustomBuildables = new List<TechType>();

        #endregion

        #region Group Handling

        internal static void AddToCustomGroup(TechGroup group, TechCategory category, TechType techType, TechType after)
        {
            Dictionary<TechGroup, Dictionary<TechCategory, List<TechType>>> groups = CraftData.groups;
            Dictionary<TechCategory, List<TechType>> techGroup = groups[group];
            if (techGroup == null)
            {
                // Should never happen, but doesn't hurt to add it.
                Logger.Log("Invalid TechGroup!", LogLevel.Error);
                return;
            }

            List<TechType> techCategory = techGroup[category];
            if (techCategory == null)
            {
                Logger.Log($"Invalid TechCategory Combination! TechCategory: {category} TechGroup: {group}", LogLevel.Error);
                return;
            }

            int index = techCategory.IndexOf(after);

            if (index == -1) // Not found
            {
                techCategory.Add(techType);
                Logger.Log($"Added \"{techType.AsString():G}\" to groups under \"{group:G}->{category:G}\"", LogLevel.Debug);
            }
            else
            {
                techCategory.Insert(index + 1, techType);

                Logger.Log($"Added \"{techType.AsString():G}\" to groups under \"{group:G}->{category:G}\" after \"{after.AsString():G}\"", LogLevel.Debug);
            }
        }

        internal static void RemoveFromCustomGroup(TechGroup group, TechCategory category, TechType techType)
        {
            Dictionary<TechGroup, Dictionary<TechCategory, List<TechType>>> groups = CraftData.groups;
            Dictionary<TechCategory, List<TechType>> techGroup = groups[group];
            if (techGroup == null)
            {
                // Should never happen, but doesn't hurt to add it.
                Logger.Log("Invalid TechGroup!", LogLevel.Error);
                return;
            }

            List<TechType> techCategory = techGroup[category];
            if (techCategory == null)
            {
                Logger.Log($"Invalid TechCategory Combination! TechCategory: {category} TechGroup: {group}", LogLevel.Error);
                return;
            }

            techCategory.Remove(techType);

            Logger.Log($"Removed \"{techType.AsString():G}\" from groups under \"{group:G}->{category:G}\"", LogLevel.Debug);
        }

        internal static void AddToCustomTechData(TechType techType, ITechData techData)
        {
            CustomTechData.Add(techType, techData);
        }

        #endregion

        #region Patching

        internal static void Patch(HarmonyInstance harmony)
        {
            // Direct access to private fields made possible by https://github.com/CabbageCrow/AssemblyPublicizer/
            // See README.md for details.
            PatchUtils.PatchDictionary(CraftData.harvestOutputList, CustomHarvestOutputList);
            PatchUtils.PatchDictionary(CraftData.harvestTypeList, CustomHarvestTypeList);
            PatchUtils.PatchDictionary(CraftData.harvestFinalCutBonusList, CustomFinalCutBonusList);
            PatchUtils.PatchDictionary(CraftData.itemSizes, CustomItemSizes);
            PatchUtils.PatchDictionary(CraftData.equipmentTypes, CustomEquipmentTypes);
            PatchUtils.PatchDictionary(CraftData.slotTypes, CustomSlotTypes);
            PatchUtils.PatchDictionary(CraftData.craftingTimes, CustomCraftingTimes);
            PatchUtils.PatchDictionary(CraftData.cookedCreatureList, CustomCookedCreatureList);
            PatchUtils.PatchDictionary(CraftData.backgroundTypes, CustomBackgroundTypes);
            PatchUtils.PatchList(CraftData.buildables, CustomBuildables);

            MethodInfo preparePrefabIDCache = typeof(CraftData).GetMethod("PreparePrefabIDCache", BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(preparePrefabIDCache, null,
                new HarmonyMethod(typeof(CraftDataPatcher).GetMethod("PreparePrefabIDCachePostfix", BindingFlags.NonPublic | BindingFlags.Static)));

            AddCustomTechDataToOriginalDictionary();

            Logger.Log("CraftDataPatcher is done.", LogLevel.Debug);
        }

        private static void PreparePrefabIDCachePostfix()
        {
            Dictionary<TechType, string> techMapping = CraftData.techMapping;
            Dictionary<string, TechType> entClassTechTable = CraftData.entClassTechTable;

            foreach (ModPrefab prefab in ModPrefab.Prefabs)
            {
                techMapping[prefab.TechType] = prefab.ClassID;
                entClassTechTable[prefab.ClassID] = prefab.TechType;
            }
        }

        private static void AddCustomTechDataToOriginalDictionary()
        {
            short added = 0;
            short replaced = 0;
            foreach (TechType techType in CustomTechData.Keys)
            {
                ITechData smlTechData = CustomTechData[techType];

                var techDataInstance = new CraftData.TechData
                {
                    _techType = techType,
                    _craftAmount = smlTechData.craftAmount
                };

                var ingredientsList = new CraftData.Ingredients();

                if (smlTechData.ingredientCount > 0)
                {
                    for (int i = 0; i < smlTechData.ingredientCount; i++)
                    {
                        IIngredient smlIngredient = smlTechData.GetIngredient(i);

                        var ingredient = new CraftData.Ingredient(smlIngredient.techType, smlIngredient.amount);
                        ingredientsList.Add(smlIngredient.techType, smlIngredient.amount);
                    }
                    techDataInstance._ingredients = ingredientsList;
                }

                if (smlTechData.linkedItemCount > 0)
                {
                    var linkedItems = new List<TechType>();
                    for (int l = 0; l < smlTechData.linkedItemCount; l++)
                    {
                        linkedItems.Add(smlTechData.GetLinkedItem(l));
                    }
                    techDataInstance._linkedItems = linkedItems;
                }

                bool techDataExists = CraftData.techData.ContainsKey(techType);

                if (techDataExists)
                {
                    CraftData.techData.Remove(techType);
                    Logger.Log($"{techType} TechType already existed in the CraftData.techData dictionary. Original value was replaced.", LogLevel.Warn);
                    replaced++;
                }
                else
                {
                    added++;
                }

                CraftData.techData.Add(techType, techDataInstance);
            }

            if (added > 0)
                Logger.Log($"Added {added} new entries to the CraftData.techData dictionary.", LogLevel.Info);

            if (replaced > 0)
                Logger.Log($"Replaced {replaced} existing entries to the CraftData.techData dictionary.", LogLevel.Info);
        }

        #endregion
    }
}
