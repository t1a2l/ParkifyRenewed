using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace ParkifyRenewed.HarmonyPatches
{
    [HarmonyPatch]
    public static class IceCreamStandAIPatch
    {
        [HarmonyPatch(typeof(IceCreamStandAI), "ParkAreaChanged")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // base.ParkAreaChanged(buildingID, ref data, oldPark, newPark);
            yield return new CodeInstruction(OpCodes.Ldarg_0); // this
            yield return new CodeInstruction(OpCodes.Ldarg_1); // buildingID
            yield return new CodeInstruction(OpCodes.Ldarg_2); // ref data
            yield return new CodeInstruction(OpCodes.Ldarg_3); // oldPark
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)4); // newPark

            yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(ParkBuildingAI), nameof(ParkBuildingAI.ParkAreaChanged)));

            // Preserve IceCreamStandAI's original CheckAchievement call.
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
            }
        }
    }
}
