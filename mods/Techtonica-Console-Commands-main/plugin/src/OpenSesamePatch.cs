using System;
using UnityEngine;
using HarmonyLib;

namespace ConsoleCommands;

class OpenSesamePatch
{
    [HarmonyPatch(typeof(ResourceGateInstance), nameof(ResourceGateInstance.CheckForRequiredResources))]
    [HarmonyPrefix]
    public static void setDoorToFree(ref ResourceGateInstance __instance) 
    {
        Debug.Log("Are youuuuu the one im looking foooor? :musical_notes:");
        if (__instance.commonInfo.instanceId != ConsoleCommands.DoorToOpen.commonInfo.instanceId) 
        {
            Debug.Log("I'm noooot the door you're looking fooooor");
            return;
        }
        for (int i = 0; i < __instance.resourcesRequired.Length; i++) 
        {
            __instance.resourcesRequired[i].quantity = 0;
        }
    }
}