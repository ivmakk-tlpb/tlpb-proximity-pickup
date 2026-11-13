using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace ProximityPickup
{
    [BepInPlugin(PluginGuid, "Proximity Pickup", "1.1.0")]
    [BepInProcess("TheLastPlague.exe")]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.ivmakk.thelastplagueblight.proximitypickup";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<string> PickModeEntry;
        internal static ConfigEntry<float> PickupRadius;
        internal static ConfigEntry<float> DigRadius;
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
                    "A gather of a ready mushroom, herb, or other resource that you gather in a moment also gathers the resources of the same kind on its label, or within PickupRadius with no label. " +
                    "Collecting clay, mud, or sand by hand takes the same kind of ground within DigRadius in one action. " +
                    "One: each click picks one item, gathers one resource, and each try collects one spot, as in the game. The merged labels, the label layout, and the middle-click stay on in both modes.",
                    new AcceptableValueList<string>("Group", "One")));
            PickupRadius = Config.Bind(
                "General", "PickupRadius", 2f,
                "How close in meters items or resources of the same kind must lie to merge into one label, and how far a click on an item or resource with no label reaches. " +
                "0 merges no labels, and picks or gathers only the clicked thing. Collecting from the ground has its own DigRadius.");
            DigRadius = Config.Bind(
                "General", "DigRadius", 1f,
                "How far in meters one action of collecting clay, mud, or sand by hand reaches: 1 m gives up to 21 tries, 2 m up to 69, with a limit of 256. " +
                "0 collects one spot at a time, as in the game.");
            Verbose = Config.Bind(
                "General", "Verbose", false,
                "Writes extra diagnostic lines to the BepInEx log for troubleshooting. Keep off in normal play.");
            Patch(typeof(Labels.LabelPatches), "Label groups");
            Patch(typeof(Pickup.PickupPatches), "Group pickup");
            Patch(typeof(Pickup.DigPatches), "Group dig");
            Patch(typeof(Pickup.GatherPatches), "Group gather");
            Patch(typeof(Pickup.ClickPatches), "Middle-click");
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
