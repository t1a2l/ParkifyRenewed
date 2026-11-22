using System;
using ColossalFramework;
using ColossalFramework.Math;
using HarmonyLib;
using UnityEngine;

namespace ParkifyRenewed.HarmonyPatches
{
    [HarmonyPatch]
    public static class ParkBuildingAIFindRoadAccessPatch
    {
        [HarmonyPatch(typeof(ParkBuildingAI), "FindRoadAccess")]
        [HarmonyPrefix]
        public static bool FindRoadAccessPrefix(ParkBuildingAI __instance, ushort buildingID, ref Building data, Vector3 position, ref ushort segmentID, ref bool __result, bool mostCloser = false, bool untouchable = true)
        {
            DistrictManager instance = Singleton<DistrictManager>.instance;
            byte b = instance.GetPark(data.m_position);
            if (b != 0 && !Mod.IsParkType(instance.m_parks.m_buffer[b].m_parkType))
            {
                return true;
            }
            ushort num = 0;
            if (b != 0)
            {
                num = instance.m_parks.m_buffer[b].m_randomGate;
                if (num == 0)
                {
                    num = instance.m_parks.m_buffer[b].m_mainGate;
                }
            }
            if (num == 0)
            {
                return true;
            }

            bool isCampus = b != 0 && DistrictPark.IsCampusType(instance.m_parks.m_buffer[b].m_parkType);

            if (!__instance.m_info.m_hasPedestrianPaths)
            {
                if (!isCampus)
                {
                    return true;
                }

                // A campus park without internal paths must connect to a
                // nearby campus path rather than a road.
                if (!FindSegment(position, ref segmentID))
                {
                    data.m_flags |= Building.Flags.RoadAccessFailed;
                    segmentID = 0;
                    __result = false;
                }
                else
                {
                    __result = true;
                }

                return false;
            }

            NetManager instance2 = Singleton<NetManager>.instance;
            ushort num2 = data.m_netNode;
            int num3 = 0;
            while (num2 != 0)
            {
                if (instance2.m_nodes.m_buffer[num2].m_lane != 0)
                {
                    segmentID = 0;
                    __result = true;
                    return false;
                }
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = instance2.m_nodes.m_buffer[num2].GetSegment(i);
                    if (segment != 0 && (instance2.m_segments.m_buffer[segment].m_flags & NetSegment.Flags.Untouchable) == 0)
                    {
                        segmentID = segment;
                        __result = true;
                        return false;
                    }
                }
                num2 = instance2.m_nodes.m_buffer[num2].m_nextBuildingNode;
                if (++num3 > 32768)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }

            if (!FindSegment(position, ref segmentID))
            {
                data.m_flags |= Building.Flags.RoadAccessFailed;
                segmentID = 0;
                __result = false;
            }
            else
            {
                __result = true;
            }

            return false;
        }

        [HarmonyPatch(typeof(ParkBuildingAI), "RenderBuildOverlay")]
        [HarmonyPostfix]
        public static void RenderBuildOverlayPostfix(ParkBuildingAI __instance, RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, float angle)
        {
            bool isParkType = Mod.IsParkType(__instance.m_parkType);

            if (isParkType && __instance.m_info.m_hasPedestrianPaths && Singleton<DistrictManager>.instance.GetPark(position) != 0)
            {
                RenderRoadAccessArrow(__instance, cameraInfo, color, position, angle);
            }
        }

        private static bool FindSegment(Vector3 position, ref ushort segmentID)
        {
            NetManager instance2 = Singleton<NetManager>.instance;
            Bounds bounds = new(position, new Vector3(40f, 40f, 40f));
            int num4 = Mathf.Max((int)((bounds.min.x - 64f) / 64f + 135f), 0);
            int num5 = Mathf.Max((int)((bounds.min.z - 64f) / 64f + 135f), 0);
            int num6 = Mathf.Min((int)((bounds.max.x + 64f) / 64f + 135f), 269);
            int num7 = Mathf.Min((int)((bounds.max.z + 64f) / 64f + 135f), 269);
            for (int j = num5; j <= num7; j++)
            {
                for (int k = num4; k <= num6; k++)
                {
                    ushort num8 = instance2.m_segmentGrid[j * 270 + k];
                    int num9 = 0;
                    while (num8 != 0)
                    {
                        NetSegment seg = instance2.m_segments.m_buffer[num8];
                        NetInfo info = seg.Info;

                        // NEW: skip the building's own protected internal paths
                        if ((seg.m_flags & NetSegment.Flags.Untouchable) != 0)
                        {
                            num8 = seg.m_nextGridSegment;
                            if (++num9 >= 36864)
                            {
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                            continue;
                        }

                        if ((info.m_class.m_service == ItemClass.Service.Road || info.m_class.m_service == ItemClass.Service.Beautification) && !info.m_netAI.IsUnderground() && info.m_hasPedestrianLanes)
                        {
                            ushort startNode = instance2.m_segments.m_buffer[num8].m_startNode;
                            ushort endNode = instance2.m_segments.m_buffer[num8].m_endNode;
                            Vector3 position2 = instance2.m_nodes.m_buffer[startNode].m_position;
                            Vector3 position3 = instance2.m_nodes.m_buffer[endNode].m_position;
                            float num10 = Mathf.Max(Mathf.Max(bounds.min.x - 64f - position2.x, bounds.min.z - 64f - position2.z), Mathf.Max(position2.x - bounds.max.x - 64f, position2.z - bounds.max.z - 64f));
                            float num11 = Mathf.Max(Mathf.Max(bounds.min.x - 64f - position3.x, bounds.min.z - 64f - position3.z), Mathf.Max(position3.x - bounds.max.x - 64f, position3.z - bounds.max.z - 64f));
                            if ((num10 < 0f || num11 < 0f) && instance2.m_segments.m_buffer[num8].m_bounds.Intersects(bounds) && instance2.m_segments.m_buffer[num8].GetClosestLanePosition(position, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, VehicleInfo.VehicleCategory.None, VehicleInfo.VehicleType.None, requireConnect: false, out var positionA, out var _, out var _, out var _, out var _, out var _))
                            {
                                float num12 = Vector3.SqrMagnitude(position - positionA);
                                if (num12 < 400f)
                                {
                                    segmentID = num8;
                                    return true;
                                }
                            }
                        }
                        num8 = instance2.m_segments.m_buffer[num8].m_nextGridSegment;
                        if (++num9 >= 36864)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            return false;
        }

        private static void RenderRoadAccessArrow(PlayerBuildingAI __instance, RenderManager.CameraInfo cameraInfo, Color color, Vector3 position, float angle)
        {
            GameAreaProperties properties = Singleton<GameAreaManager>.instance.m_properties;
            if (properties != null)
            {
                Vector3 vector = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 vector2 = new(vector.z, 0f, 0f - vector.x);
                int cellLength = __instance.m_info.m_cellLength;
                Quad3 quad = default;
                quad.a = position - 4f * vector - ((float)cellLength * 4f + 8f) * vector2;
                quad.b = position - 4f * vector - (float)cellLength * 4f * vector2;
                quad.c = position + 4f * vector - (float)cellLength * 4f * vector2;
                quad.d = position + 4f * vector - ((float)cellLength * 4f + 8f) * vector2;
                Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
                Singleton<RenderManager>.instance.OverlayEffect.DrawQuad(cameraInfo, properties.m_directionArrow, color, quad, -10f, 1034f, renderLimits: false, alphaBlend: true);
            }
        }
    }
}
