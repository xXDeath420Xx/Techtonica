using System;
using UnityEngine;
using HarmonyLib;

namespace ConsoleCommands;

class InstaMolePatch
{
    [HarmonyPatch(typeof(TerrainManipulator), nameof(TerrainManipulator.OnUpdate))]
    [HarmonyPostfix]
    public static void Postfix(ref TerrainManipulator __instance) 
    {
       if(ConsoleCommands.bShouldInstaMine)
       { 
            __instance.currentManipulatorMode.basePerVoxelActionDuration = 0.0001f;
            __instance._isOverheated = false;
            __instance.currentSlowDownSpeed = 2f;
       }
    }
}