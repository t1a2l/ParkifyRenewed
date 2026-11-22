using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace Parkify.HarmonyPatches
{
    [HarmonyPatch]
    public static class PedestrianPathAIPatch
    {
        private delegate void ChangeLaneConnection(PedestrianPathAI ai, ushort nodeID, ref NetNode data, uint laneID, byte offset);

        private delegate void DeleteLaneConnection(PedestrianPathAI ai, ushort nodeID, ref NetNode data);

        private static readonly ChangeLaneConnection AddConnection = AccessTools.MethodDelegate<ChangeLaneConnection>(AccessTools.Method(typeof(PedestrianPathAI), "AddLaneConnection"));

        private static readonly DeleteLaneConnection RemoveConnection = AccessTools.MethodDelegate<DeleteLaneConnection>(AccessTools.Method(typeof(PedestrianPathAI), "RemoveLaneConnection"));

        [HarmonyPatch(typeof(PedestrianPathAI), "UpdateLaneConnection")]
        [HarmonyPrefix]
        public static bool UpdateLaneConnectionPrefix(PedestrianPathAI __instance, ushort nodeID, ref NetNode data)
        {
            if ((data.m_flags & NetNode.Flags.Untouchable) == 0)
            {
                return true;
            }

            if (!IsEligibleParkNode(nodeID))
            {
                return true;
            }

            uint bestLane = 0;
            byte bestOffset = 0;
            float bestDistanceSqr = 1E+10f;

            if ((data.m_flags & NetNode.Flags.ForbidLaneConnection) == 0)
            {
                bool isEnd = (data.m_flags & NetNode.Flags.End) != 0;

                if (__instance.m_connectService1 != ItemClass.Service.None)
                {
                    float distance1 = isEnd ? __instance.m_maxConnectDistanceEnd1 : __instance.m_maxConnectDistance1;
                    ConsiderConnection(data.m_position, __instance.m_connectService1, ItemClass.SubService.None, ItemClass.Level.Level1, false, __instance.m_canConnectTunnel, distance1, ref bestLane, ref bestOffset, ref bestDistanceSqr);
                }

                if (__instance.m_connectService2 != ItemClass.Service.None)
                {
                    float distance2 = isEnd ? __instance.m_maxConnectDistanceEnd2 : __instance.m_maxConnectDistance2;
                    ConsiderConnection(data.m_position, __instance.m_connectService2, ItemClass.SubService.None, ItemClass.Level.Level1, false, __instance.m_canConnectTunnel, distance2, ref bestLane, ref bestOffset, ref bestDistanceSqr);
                }

                float distance3 = isEnd ? __instance.m_maxConnectDistanceEnd3 : __instance.m_maxConnectDistance3;

                if (__instance.m_connectService3 != ItemClass.Service.None)
                {
                    ConsiderConnection(data.m_position, __instance.m_connectService3, __instance.m_connectSubService3, __instance.m_connectLevel3, true, __instance.m_canConnectTunnel, distance3, ref bestLane, ref bestOffset, ref bestDistanceSqr);
                }

                // Additional external park-path support.
                // Reuse service 3's normal/end connection range.
                ConsiderConnection(data.m_position, ItemClass.Service.Beautification, ItemClass.SubService.BeautificationParks, ItemClass.Level.Level1, true, __instance.m_canConnectTunnel, distance3, ref bestLane, ref bestOffset, ref bestDistanceSqr);

                ConsiderConnection(data.m_position, ItemClass.Service.Beautification, ItemClass.SubService.BeautificationParks, ItemClass.Level.Level3, true, __instance.m_canConnectTunnel, distance3, ref bestLane, ref bestOffset, ref bestDistanceSqr);
            }

            if (bestLane != data.m_lane)
            {
                if (data.m_lane != 0)
                {
                    RemoveConnection(__instance, nodeID, ref data);
                }

                if (bestLane != 0)
                {
                    AddConnection(__instance, nodeID, ref data, bestLane, bestOffset);
                }
            }

            return false;
        }

        private static bool IsEligibleParkNode(ushort nodeID)
        {
            Building[] buildings = Singleton<BuildingManager>.instance.m_buildings.m_buffer;

            NetNode[] nodes = Singleton<NetManager>.instance.m_nodes.m_buffer;

            for (int i = 1; i < buildings.Length; i++)
            {
                Building.Flags flags = buildings[i].m_flags;

                if ((flags & (Building.Flags.Created | Building.Flags.Deleted)) != Building.Flags.Created)
                {
                    continue;
                }

                ushort current = buildings[i].m_netNode;

                if (current == 0)
                {
                    continue;
                }

                BuildingInfo info = buildings[i].Info;

                if (info == null || info.m_buildingAI is not ParkBuildingAI parkAI || !DistrictPark.IsParkType(parkAI.m_parkType))
                {
                    continue;
                }

                int iterations = 0;

                while (current != 0)
                {
                    if (current == nodeID)
                    {
                        return true;
                    }

                    current = nodes[current].m_nextBuildingNode;

                    if (++iterations >= nodes.Length)
                    {
                        Debug.LogError("[Parkify] Invalid building-node list.");
                        break;
                    }
                }
            }

            return false;
        }

        private static void ConsiderConnection(Vector3 position, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level, bool matchSubServiceAndLevel, bool allowUnderground, float maxDistance, ref uint bestLane, ref byte bestOffset, ref float bestDistanceSqr)
        {
            if (!FindExternalPedestrianLane(position, service, subService, level, matchSubServiceAndLevel, allowUnderground, maxDistance, out PathUnit.Position pathPosition, out float distanceSqr))
            {
                return;
            }

            if (distanceSqr >= bestDistanceSqr)
            { 
                return; 
            }

            uint laneID = PathManager.GetLaneID(pathPosition);

            if (laneID == 0)
            { 
                return; 
            }

            bestDistanceSqr = distanceSqr;
            bestLane = laneID;
            bestOffset = pathPosition.m_offset;
        }

        private static bool FindExternalPedestrianLane(Vector3 position, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level, bool matchSubServiceAndLevel, bool allowUnderground, float maxDistance, out PathUnit.Position result, out float distanceSqr)
        {
            result = default;
            distanceSqr = 1E+10f;

            if (maxDistance <= 0f)
            { 
                return false; 
            }

            Bounds bounds = new(position, Vector3.one * (maxDistance * 2f));

            int minX = Mathf.Max((int)((bounds.min.x - 64f) / 64f + 135f), 0);

            int minZ = Mathf.Max((int)((bounds.min.z - 64f) / 64f + 135f), 0);

            int maxX = Mathf.Min((int)((bounds.max.x + 64f) / 64f + 135f), 269);

            int maxZ = Mathf.Min((int)((bounds.max.z + 64f) / 64f + 135f), 269);

            NetManager manager = Singleton<NetManager>.instance;
            float bestDistance = maxDistance * maxDistance;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    ushort segmentID = manager.m_segmentGrid[z * 270 + x];

                    int iterations = 0;

                    while (segmentID != 0)
                    {
                        // Always advance at the bottom of this loop.
                        ushort nextSegment = manager.m_segments.m_buffer[segmentID].m_nextGridSegment;

                        NetSegment.Flags flags = manager.m_segments.m_buffer[segmentID].m_flags;

                        NetInfo info = manager.m_segments.m_buffer[segmentID].Info;

                        bool validFlags = (flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted | NetSegment.Flags.Untouchable | NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) == NetSegment.Flags.Created;

                        bool classMatches = false;

                        if (info != null)
                        {
                            classMatches = info.m_class.m_service == service && (!matchSubServiceAndLevel || (info.m_class.m_subService == subService && info.m_class.m_level == level));

                            // Preserve the original Road search exception.
                            if (service == ItemClass.Service.Road && info.m_class.m_service == ItemClass.Service.Race && info.m_class.m_level == ItemClass.Level.Level3)
                            {
                                classMatches = true;
                            }
                        }

                        if (validFlags && classMatches && (manager.m_segments.m_buffer[segmentID].m_flags2 & NetSegment.Flags2.EventClosed) == 0 && (allowUnderground || !info.m_netAI.IsUnderground()) && manager.m_segments.m_buffer[segmentID].m_bounds.Intersects(bounds))
                        {
                            // This is a pedestrian-only search:
                            // the original pedestrian-street adjustments
                            // do not change these lane/vehicle arguments.
                            if (manager.m_segments.m_buffer[segmentID].GetClosestLanePosition(position, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, VehicleInfo.VehicleCategory.None, VehicleInfo.VehicleType.None, true, out var lanePosition, out var laneIndex, out var laneOffset, out var _, out var _, out var _))
                            {
                                float distance = Vector3.Distance(position, lanePosition);

                                // Same excludeLaneWidth behavior.
                                distance = Mathf.Max(0f, distance - info.m_lanes[laneIndex].m_width * 0.5f);

                                float candidateDistanceSqr = distance * distance;

                                if (candidateDistanceSqr < bestDistance)
                                {
                                    bestDistance = candidateDistanceSqr;
                                    distanceSqr = candidateDistanceSqr;

                                    result.m_segment = segmentID;
                                    result.m_lane = (byte)laneIndex;
                                    result.m_offset = (byte)Mathf.Clamp(Mathf.RoundToInt(laneOffset * 255f), 0, 255);
                                }
                            }
                        }

                        segmentID = nextSegment;

                        if (++iterations >= 36864)
                        {
                            Debug.LogError("[Parkify] Invalid segment-grid list.");
                            break;
                        }
                    }
                }
            }

            return result.m_segment != 0;
        }
    }
}