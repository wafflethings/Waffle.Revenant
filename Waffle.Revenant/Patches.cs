using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Waffle.Revenant
{
    public class Patches
    {
        public static void Patch()
        {
            Debug.LogWarning("Trying to patch, patch code run.");
            new Harmony("waffle.ultrakill.revenant").PatchAll(typeof(Patches));
        }

        [HarmonyPatch(typeof(EnemyIdentifier), nameof(EnemyIdentifier.DeliverDamage)), HarmonyPostfix]
        public static void RunRevenantOnHurt(EnemyIdentifier __instance)
        {
            if (__instance.TryGetComponent(out Revenant revenant))
            {
                revenant.OnHurt(new System.Diagnostics.StackTrace().ToString());
            }
        }
    }
}
