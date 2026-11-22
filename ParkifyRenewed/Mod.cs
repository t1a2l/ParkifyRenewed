using System.Linq;
using CitiesHarmony.API;
using ICities;
using ParkifyRenewed.OptionsFramework;
using ParkifyRenewed.Utils;
using UnityEngine;

namespace ParkifyRenewed
{
    public class Mod : LoadingExtensionBase, IUserMod
    {
        public string Name => "Parkify Renewed";

        public string Description => "Use park buildings as Parklife modules and place them in park, campus, industry, and airport areas.";

        private static bool initialized;

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            if (mode == LoadMode.LoadAsset || mode == LoadMode.NewAsset)
            {
                return;
            }

            if (initialized)
            {
                return;
            }

            if (!HarmonyHelper.IsHarmonyInstalled)
            {
                return;
            }

            if (OptionsWrapper<Options>.Options.PatchMarina)
            {
                PatchMarinaProps();
            }

            if (OptionsWrapper<Options>.Options.PatchFishingTours)
            {
                PatchFishingToursProps();
            }

            initialized = true;
        }

        public static bool IsParkType(DistrictPark.ParkType parkType)
        {
            return DistrictPark.IsParkType(parkType) || DistrictPark.IsCampusType(parkType) || DistrictPark.IsAirportType(parkType) || DistrictPark.IsIndustryType(parkType);
        }

        private static void PatchFishingToursProps()
        {
            var buildingInfo = PrefabCollection<BuildingInfo>.FindLoaded("3x2_Fishing tours");
            if (buildingInfo?.m_props == null)
            {
                return;
            }

            var list = buildingInfo.m_props.ToList();
            var propInfo = PrefabCollection<PropInfo>.FindLoaded("Yacht");
            if (propInfo == null)
            {
                return;
            }

            var prop = new BuildingInfo.Prop
            {
                m_angle = 270,
                m_probability = 100,
                m_finalProp = propInfo,
                m_position = new Vector3(0, 0, -14),
                m_prop = propInfo,
                m_radAngle = 4.71239f,
                m_fixedHeight = true,
                m_index = 0,
                m_requiredLength = 0
            };
            list.Add(prop);
            buildingInfo.m_props = [.. list];
        }

        private static void PatchMarinaProps()
        {
            var buildingInfo = PrefabCollection<BuildingInfo>.FindLoaded("4x4_Marina");
            if (buildingInfo?.m_props == null)
            {
                return;
            }

            var propInfo = PrefabCollection<PropInfo>.FindLoaded("houseboat");
            if (propInfo == null)
            {
                return;
            }

            buildingInfo.m_props[15].m_prop = propInfo;
            buildingInfo.m_props[15].m_finalProp = propInfo;
            buildingInfo.m_props[15].m_position = new Vector3(-13, 0, 3.5f);
            buildingInfo.m_props[15].m_radAngle = 1.5708f;
            buildingInfo.m_props[15].m_angle = 90;
        }
    }
}
