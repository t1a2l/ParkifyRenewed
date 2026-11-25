using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ParkifyRenewed.HarmonyPatches
{
    [HarmonyPatch]
    public static class DistrictManagerPatch
    {
        [HarmonyPatch(typeof(DistrictManager), "CheckParkBuilding")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var isParkGetter = AccessTools.PropertyGetter(typeof(DistrictPark), nameof(DistrictPark.IsPark));
            var replacement = AccessTools.Method(typeof(DistrictManagerPatch), nameof(IsSupportedArea));

            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo method && method == isParkGetter)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }

                yield return instruction;
            }
        }

        private static bool IsSupportedArea(ref DistrictPark park)
        {
            return Mod.IsParkType(park.m_parkType);
        }
    }
}
