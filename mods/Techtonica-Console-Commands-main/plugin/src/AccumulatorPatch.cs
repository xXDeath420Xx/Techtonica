using UnityEngine;
using HarmonyLib;
using System;
using PropStreaming;

namespace ConsoleCommands;

[HarmonyPatch]
public class AccumulatorPatch
{
    public static int AccumulatorsAffected;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AccumulatorInstance), nameof(AccumulatorInstance.SimUpdate))]
    static void Postfix(ref AccumulatorInstance __instance)
    {
        if(!ConsoleCommands.bShouldFillAccumulators) return;
        if(AccumulatorPatch.AccumulatorsAffected < MachineManager.instance.GetMachineList(MachineTypeEnum.Accumulator).GetCurrentArrayCount())
        {
            __instance.storedEnergy = __instance.maxCapacity;
            AccumulatorPatch.AccumulatorsAffected++;
        }
        else if(AccumulatorPatch.AccumulatorsAffected >= MachineManager.instance.GetMachineList(MachineTypeEnum.Accumulator).GetCurrentArrayCount())
        {
            ConsoleCommands.bShouldFillAccumulators = false;
            AccumulatorPatch.AccumulatorsAffected = 0;
        }
    }
}