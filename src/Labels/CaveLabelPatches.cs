using System;
using HarmonyLib;

namespace ProximityPickup.Labels
{
    // Underground (in a cave), the game labels nothing without lit fuel in the hands. PlayerManager.handleHighlightItems
    // keeps its label radius in a local value and passes it to the world search of the labelled things. A prefix of that
    // search raises its range argument (CaveLabels), only for the first search inside handleHighlightItems, so the other
    // callers of the search keep their range. The game still filters the found things and makes the labels.
    [HarmonyPatch]
    internal static class CaveLabelPatches
    {
        // Set at the start of handleHighlightItems, and cleared at the first search or at the end.
        private static bool pending;

        // The input of the last Verbose line, so the log gets a line only after a change.
        private static (bool Underground, float Game)? last;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerManager), "handleHighlightItems")]
        private static void HighlightPrefix()
        {
            pending = true;
        }

        // Runs also after an exception of the game method, so the flag never reaches a later search of another caller.
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(PlayerManager), "handleHighlightItems")]
        private static void HighlightFinalizer()
        {
            pending = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(World), nameof(World.GetEntitiesAroundPointWithAnyComponents))]
        private static void ThingsPrefix(World __instance, ref float range)
        {
            if (!pending) return;
            pending = false;
            try
            {
                bool underground = __instance.UndergroundVisible;
                float game = range;
                range = CaveLabels.ThingRange(underground, game);
                if (last == (underground, game)) return;
                last = (underground, game);
                Plugin.Debug($"Cave labels: underground {(underground ? "true" : "false")}, range {game} -> {range}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Cave labels", e);
            }
        }
    }
}
