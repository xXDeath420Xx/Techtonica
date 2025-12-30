using UnityEngine;
using HarmonyLib;
using System;
using PropStreaming;

namespace ConsoleCommands;

[HarmonyPatch]
public class ScannerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Scanner), nameof(Scanner.Scan))]
    static void ScanPatch(ref InstanceLookup lookup, ref ScannableData scanData, ref bool isScanning)
    {
        if(scanData.IsAlreadyScanned(lookup)) return;
        if(!isScanning) return;
        PropState PropState;
        PropManager.instance.GetPropState(lookup, out PropState);
        if(ConsoleCommands.bHasScanOverride)
        {
            float scanDuration = scanData.GetScanDuration()*ConsoleCommands.ScanOverrideMultiplier;
            if (PropManager.instance.UpdatePropScanProgress(lookup, (PropState.scanProgress * scanDuration + Time.deltaTime) / scanDuration))
                scanData.CompleteScan(lookup);
        }
        // ConsoleCommands.ScanOverrideMultiplier;
    }
}