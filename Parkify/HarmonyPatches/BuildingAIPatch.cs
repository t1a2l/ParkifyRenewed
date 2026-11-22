using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Parkify.HarmonyPatches
{
    [HarmonyPatch]
    public static class BuildingAIPatch
    {
        [HarmonyPatch(typeof(BuildingAI), "CheckBuildPosition")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var placementModeField = AccessTools.Field(typeof(BuildingInfo), nameof(BuildingInfo.m_placementMode));

            var helper = AccessTools.Method(typeof(BuildingAIPatch), nameof(GetAlignmentPlacementMode));

            int replacements = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, placementModeField))
                {
                    // The BuildingInfo reference is already on the stack.
                    // Preserve labels and exception blocks on this instruction.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = helper;
                    replacements++;
                }

                yield return instruction;
            }

            if (replacements != 1)
            {
                Debug.LogWarning($"[Parkify] Expected one placement-mode read in BuildingAI.CheckBuildPosition; found {replacements}.");
            }
        }

        private static BuildingInfo.PlacementMode GetAlignmentPlacementMode(BuildingInfo info)
        {
            if (BuildingToolPatch.PathSnapEnabled && info.m_placementMode == BuildingInfo.PlacementMode.PathsideOrGround 
                && info.m_buildingAI is ParkBuildingAI parkAI && DistrictPark.IsParkType(parkAI.m_parkType))
            {
                return BuildingInfo.PlacementMode.Roadside;
            }
            return info.m_placementMode;
        }
    }
}
