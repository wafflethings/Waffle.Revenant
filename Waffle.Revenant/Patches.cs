using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Waffle.Revenant
{
    public static class Patches
    {
        public static void Patch()
        {
            if (!Harmony.HasAnyPatches("waffle.ultrakill.revenant"))
            {
                Debug.LogWarning("Trying to patch, patch code run.");
                new Harmony("waffle.ultrakill.revenant").PatchAll(typeof(Patches));
            }
        }

        [HarmonyPatch(typeof(EnemyIdentifier), nameof(EnemyIdentifier.DeliverDamage)), HarmonyPostfix]
        public static void RunRevenantOnHurt(EnemyIdentifier __instance)
        {
            if (__instance.TryGetComponent(out Revenant revenant))
            {
                revenant.OnHurt(new System.Diagnostics.StackTrace().ToString());
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Collided)), HarmonyPrefix]
        public static bool MakeProjectileBounce(Projectile __instance, Collider other)
        {
            if (LayerMaskDefaults.Get(LMD.EnvironmentAndBigEnemies).Contains(other.gameObject.layer))
            {
                if (__instance.GetComponent<IndestructableProjectile>() != null)
                {
                    return false;
                }

                if (__instance.TryGetComponent(out BounceProjectile bounce) && bounce.HasBouncesLeft())
                {
                    if (Physics.Raycast(__instance.transform.position, __instance.transform.forward, out RaycastHit raycastHit, 1000, LayerMaskDefaults.Get(LMD.Environment)))
                    {
                        Quaternion oldRot = bounce.KeepNonRotated.transform.rotation;
                        __instance.transform.forward = Vector3.Reflect(__instance.transform.forward, raycastHit.normal);
                        bounce.KeepNonRotated.transform.rotation = oldRot;
                    }
                    return false;
                }
            }

            return true;
        }

        public static bool Contains(this LayerMask mask, int layer)
        {
            return mask == (mask | (1 << layer));
        }
    }
}
