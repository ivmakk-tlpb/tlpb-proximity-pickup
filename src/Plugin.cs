using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace ProximityPickup
{
    [BepInPlugin(PluginGuid, "Proximity Pickup", "1.0.0")]
    [BepInProcess("TheLastPlague.exe")]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.ivmakk.thelastplagueblight.proximitypickup";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<string> PickModeEntry;
        internal static ConfigEntry<float> PickupRadius;
        internal static ConfigEntry<bool> Verbose;

        private static readonly HashSet<string> warned = new HashSet<string>();

        internal static PickMode PickMode => PickModes.Parse(PickModeEntry.Value);

        public override void Load()
        {
            Log = base.Log;
            PickModeEntry = Config.Bind(
                "General", "PickMode", "Group",
                new ConfigDescription(
                    "Group: a click on a label, or on an item with a label, picks all the items on that label. A click on an item with no label also picks the items of the same kind within PickupRadius. " +
                    "Collecting clay, mud, or sand by hand takes the same kind of ground within PickupRadius in one action. " +
                    "One: each click picks one item and each try collects one spot, as in the game. The merged labels and the label layout stay on in both modes.",
                    new AcceptableValueList<string>("Group", "One")));
            PickupRadius = Config.Bind(
                "General", "PickupRadius", 2f,
                "How close in meters items of the same kind must lie to merge into one label, how far a click on an item with no label reaches, and how far one action of collecting from the ground reaches (2 m gives up to 69 tries, with a limit of 256). " +
                "0 merges no labels, picks only the clicked item, and collects one spot at a time.");
            Verbose = Config.Bind(
                "General", "Verbose", false,
                "Writes extra diagnostic lines to the BepInEx log for troubleshooting. Keep off in normal play.");
            Patch(typeof(Labels.LabelPatches), "Label groups");
            Patch(typeof(Pickup.PickupPatches), "Group pickup");
            Patch(typeof(Pickup.DigPatches), "Group dig");
            Log.LogInfo("Proximity Pickup loaded.");
        }

        // A game update can rename a target. Then this part of the mod is off, and the game works as without it.
        // Each part has its own Harmony instance, so a failed part removes the patches it applied before the
        // failure and stays fully off.
        private static void Patch(Type patches, string name)
        {
            var harmony = new Harmony(PluginGuid + "." + patches.Name);
            try
            {
                harmony.PatchAll(patches);
                Debug(name + ": patched");
            }
            catch (Exception e)
            {
                harmony.UnpatchSelf();
                Log.LogWarning(name + " is off: " + e.Message);
            }
        }

        // One warning for each distinct error, so an error in each frame does not flood the log.
        internal static void WarnOnce(string where, Exception e)
        {
            string text = where + ": " + e.GetType().Name + ": " + e.Message;
            if (warned.Add(text)) Log.LogWarning(text + "\n" + e);
        }

        internal static void Debug(string line)
        {
            if (Verbose.Value) Log.LogDebug(line);
        }
    }
}
