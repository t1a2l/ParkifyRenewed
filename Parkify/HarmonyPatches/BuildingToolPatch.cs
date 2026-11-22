using System;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace Parkify.HarmonyPatches
{
    [HarmonyPatch]
    public static class BuildingToolPatch
    {
        public static bool PathSnapEnabled;

        [HarmonyPatch(typeof(BuildingTool), "SimulationStep")]
        [HarmonyPostfix]
        public static void SimulationStepPostfix()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                PathSnapEnabled = !PathSnapEnabled;
            }
        }

        [HarmonyPatch(typeof(BuildingTool), "SnapToPath")]
        [HarmonyPrefix]
        public static bool Prefix(Vector3 refPos, out Vector3 pos, out Vector3 dir, float maxDistance, ref bool roadOnly, bool concourseOnly, bool pitLaneOnly, ref bool __result)
        {
            pos = refPos;
            dir = Vector3.forward;

            if (!PathSnapEnabled)
            {
                return true; // vanilla behavior
            }

            BuildingTool currentTool = ToolsModifierControl.GetCurrentTool<BuildingTool>();
            if (currentTool == null) return true;

            GetPrefabInfo(currentTool, out BuildingInfo info, out _);

            Debug.Log($"[Parkify] SnapToPath: enabled={PathSnapEnabled}, prefab={currentTool.m_prefab?.name ?? "<null>"}, roadOnly={roadOnly}");

            if (info == null || info.m_buildingAI is not ParkBuildingAI ai) return true;

            bool isParkType = DistrictPark.IsParkType(ai.m_parkType);
            if (isParkType)
            {
                roadOnly = false;
            }

            // custom scan: same as vanilla, but skip Untouchable
            __result = CustomSnapToPath(refPos, out pos, out dir, maxDistance, roadOnly, concourseOnly, pitLaneOnly);
            return false; // skip vanilla
        }

        private static bool CustomSnapToPath(Vector3 refPos, out Vector3 pos, out Vector3 dir, float maxDistance, bool roadOnly, bool concourseOnly = false, bool pitLaneOnly = false)
        {
            bool result = false;
            pos = refPos;
            dir = Vector3.forward;
            float num = refPos.x - maxDistance - 100f;
            float num2 = refPos.z - maxDistance - 100f;
            float num3 = refPos.x + maxDistance + 100f;
            float num4 = refPos.z + maxDistance + 100f;
            int num5 = Mathf.Max((int)(num / 64f + 135f), 0);
            int num6 = Mathf.Max((int)(num2 / 64f + 135f), 0);
            int num7 = Mathf.Min((int)(num3 / 64f + 135f), 269);
            int num8 = Mathf.Min((int)(num4 / 64f + 135f), 269);
            Array16<NetSegment> segments = Singleton<NetManager>.instance.m_segments;
            ushort[] segmentGrid = Singleton<NetManager>.instance.m_segmentGrid;
            for (int i = num6; i <= num8; i++)
            {
                for (int j = num5; j <= num7; j++)
                {
                    ushort num9 = segmentGrid[i * 270 + j];
                    int num10 = 0;
                    while (num9 != 0)
                    {
                        NetSegment.Flags flags = segments.m_buffer[num9].m_flags;

                        if ((flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created)
                        {
                            if ((flags & NetSegment.Flags.Untouchable) != 0)
                            {
                                num9 = segments.m_buffer[num9].m_nextGridSegment;
                                continue;
                            }

                            NetInfo info = segments.m_buffer[num9].Info;
                            if ((pitLaneOnly || info.m_hasPedestrianLanes) && !info.m_netAI.IsUnderground() && (!roadOnly || (info.m_class.m_service == ItemClass.Service.Road && (info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)) && (!concourseOnly || (info.m_class.m_service == ItemClass.Service.PublicTransport && info.m_class.m_level == ItemClass.Level.Level5)) && (!pitLaneOnly || (info.m_class.m_service == ItemClass.Service.Race && info.m_class.m_level == ItemClass.Level.Level2)))
                            {
                                Vector3 min = segments.m_buffer[num9].m_bounds.min;
                                Vector3 max = segments.m_buffer[num9].m_bounds.max;
                                if (min.x < num3 && min.z < num4 && max.x > num && max.z > num2)
                                {
                                    segments.m_buffer[num9].GetClosestPositionAndDirection(refPos, out var pos2, out var dir2);
                                    float num11 = info.m_halfWidth + info.m_netAI.GetSnapDelta();
                                    float num12 = Vector3.Distance(pos2, refPos) - num11;
                                    if (num12 < maxDistance)
                                    {
                                        dir = new Vector3(dir2.z, 0f, 0f - dir2.x).normalized;
                                        if (Vector3.Dot(pos2 - refPos, dir) < 0f)
                                        {
                                            dir = -dir;
                                        }
                                        pos = pos2 - dir * Mathf.CeilToInt(num11 - 0.01f);
                                        maxDistance = num12;
                                        result = true;
                                    }
                                }
                            }
                        }

                        num9 = segments.m_buffer[num9].m_nextGridSegment;
                        if (++num10 >= 32768)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            return result;
        }

        private static void GetPrefabInfo(BuildingTool __instance, out BuildingInfo info, out int relocating)
        {
            info = __instance.m_prefab;
            relocating = __instance.m_relocate;
            if (info is not null)
            {
                relocating = 0;
                return;
            }
            if (relocating != 0)
            {
                BuildingManager instance = Singleton<BuildingManager>.instance;
                if ((instance.m_buildings.m_buffer[relocating].m_flags & (Building.Flags.Created | Building.Flags.Deleted)) == Building.Flags.Created)
                {
                    info = instance.m_buildings.m_buffer[relocating].Info;
                    return;
                }
            }
            info = null;
            relocating = 0;
        }
    }
}
