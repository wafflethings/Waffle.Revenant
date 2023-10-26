using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Waffle.Revenant.Mod
{
    [BepInPlugin("waffle.ultrakill.revenant", "Revenant Mod", "1.0.0")]
    public class RevenantMod : BaseUnityPlugin
    {
        public static Harmony Harmony = new Harmony("waffle.ultrakill.revenant");
        public static bool DoneSpawnMenu = false;
        public static bool DoneTerminal = false;

        public static AssetBundle Assets;
        public static SpawnableObject RevenantSpawnable;
        public static GameObject RevenantAssets;

        public void Start()
        {
            Assembly.LoadFrom(Path.Combine(ModPath(), "Waffle.Revenant.dll"));
            Assets = AssetBundle.LoadFromFile(Path.Combine(ModPath(), "revenant_assets.bundle"));
            RevenantSpawnable = Assets.LoadAsset<SpawnableObject>("Revenant Spawnable.asset");
            RevenantAssets = Assets.LoadAsset<GameObject>("Revenant Assets.prefab");
            Harmony.PatchAll(typeof(RevenantMod));

            Debug.Log("Loaded Revenant mod successfully!! :3");
        }

        [HarmonyPatch(typeof(NewMovement), nameof(NewMovement.Start)), HarmonyPrefix]
        public static void CreateAssets()
        {
            Instantiate(RevenantAssets);
        }

        [HarmonyPatch(typeof(SpawnMenu), nameof(SpawnMenu.Awake)), HarmonyPrefix]
        public static void AddRevenantToArm(SpawnMenu __instance)
        {
            if (DoneSpawnMenu)
            {
                return;
            }

            __instance.objects.enemies = __instance.objects.enemies.Concat(new SpawnableObject[] { RevenantSpawnable }).ToArray();
            DoneSpawnMenu = true;
        }

        [HarmonyPatch(typeof(EnemyInfoPage), nameof(EnemyInfoPage.Start)), HarmonyPrefix]
        public static void AddRevenantToTerminal(EnemyInfoPage __instance)
        {
            if (DoneTerminal)
            {
                return;
            }

            __instance.objects.enemies = __instance.objects.enemies.Concat(new SpawnableObject[] { RevenantSpawnable }).ToArray();
            DoneTerminal = true;
        }

        public static string ModPath()
        {
            return Assembly.GetExecutingAssembly().Location.Substring(0, Assembly.GetExecutingAssembly().Location.LastIndexOf(Path.DirectorySeparatorChar));
        }
    }
}
