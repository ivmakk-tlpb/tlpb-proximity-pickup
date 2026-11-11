using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HarmonyLib;
using ProximityPickup.Labels;

namespace ProximityPickup.Pickup
{
    // The group dig. After the game's first dig of a hand dig action, the game's own dig runs again for each other spot
    // of the radius, so the drops, the inventory rules, the terrain, and the cost stay the game's. The result of the
    // first dig gets the summed item counts, so the game shows one message.
    //
    // The interop reads zeros from the params of the effect's generic base class, so the params go through native
    // memory at offsets from the game context, as the item counts do (NativeItemCounts).
    [HarmonyPatch]
    internal static class DigPatches
    {
        // DigTerrainEffect.effectParams: the position as 3 floats, then the surface.
        private const int ParamsOffset = 0x30, SurfaceOffset = 0x3C;

        private const float SameSpot = 0.01f;

        // The hand dig action of the last group dig.
        private static uint? lastDug;

        // True from a group dig until the update that ends its action.
        private static bool endPending;

        // True while the postfix runs the digs of the other spots, so it does not run for them again.
        private static bool inner;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DigTerrainEffect), nameof(DigTerrainEffect.ExecuteResult))]
        private static void ExecuteResultPostfix(DigTerrainEffect __instance, ref DigTerrainEffect.DigTerrainEffectResult __result)
        {
            if (inner) return;
            try
            {
                var interaction = __instance.GetInteraction()?.TryCast<TerrainGatherInteraction>();
                var interactor = __instance.GetInteractorEntity();
                var local = LabelPatches.LocalPlayerEntity;
                bool byLocalPlayer = local != null && interactor != null && interactor.id == local.id;
                bool isHost = interactor != null && interactor.isHostEntity;
                float radius = Plugin.PickupRadius.Value;
                Plugin.Debug($"Dig effect: hand dig {interaction != null}, local {byLocalPlayer}, host {isHost}, mode {Plugin.PickMode}, radius {radius}");
                if (!DigSpots.ShouldGroupDig(Plugin.PickMode, radius, interaction != null, byLocalPlayer, isHost)) return;
                if (interaction.id == lastDug || !__result.success) return;

                var clicked = interaction.targetPosition;
                var clickedSurface = interaction.targetSurfaceType;
                // The surfaces that give Dirt, and a farm plot also when it is wet, keep the game's repeating action.
                if (clickedSurface == SurfaceType.DIRT || clickedSurface == SurfaceType.TILLED_SOIL || clickedSurface == SurfaceType.WET_TILLED_SOIL) return;
                if (!ParamsMatch(__instance, clicked, clickedSurface))
                    throw new InvalidOperationException("The native dig params do not match the action");

                // A radius below the grid spacing holds only the clicked spot, which the game already dug, so the
                // game's repeating action stays.
                var spots = DigSpots.Grid(clicked.x, clicked.z, radius);
                if (spots.Count < 2) return;

                lastDug = interaction.id;
                endPending = true;
                // The game's action does not stop for a full bag, so a first dig that does not fit only ends it.
                if (__result.insufficientSpace) return;
                RunOtherSpots(__instance, interactor.world, clicked, clickedSurface, spots, ref __result);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Group dig", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainGatherInteraction), "InteractionUpdateCommon")]
        private static void InteractionUpdatePostfix(TerrainGatherInteraction __instance)
        {
            if (!endPending) return;
            try
            {
                if (__instance.id != lastDug) return;
                endPending = false;
                __instance.End();
                Plugin.Debug($"Group dig: action {__instance.id} ended");
            }
            catch (Exception e)
            {
                endPending = false;
                Plugin.WarnOnce("Group dig end", e);
            }
        }

        // The teardown runs once for each end of an action, also for a cancelled action. The stored id goes with it,
        // so an action that the game ended in this update does not end twice, and a later action with the same id
        // (the ids start again in a new game session) is a new group dig.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainGatherInteraction), "TeardownCommon")]
        private static void TeardownPostfix(TerrainGatherInteraction __instance)
        {
            try
            {
                if (__instance.id != lastDug) return;
                lastDug = null;
                endPending = false;
                Plugin.Debug($"Group dig: action {__instance.id} torn down");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Group dig teardown", e);
            }
        }

        private static void RunOtherSpots(DigTerrainEffect effect, World world, UnityEngine.Vector3 clicked, SurfaceType clickedSurface, List<(float X, float Z)> spots, ref DigTerrainEffect.DigTerrainEffectResult first)
        {
            var clock = Stopwatch.StartNew();
            int digs = 1;
            bool full = false;
            inner = true;
            try
            {
                for (int i = 1; i < spots.Count && !full; i++)
                {
                    var position = world.GetPositionOnTerrain(new UnityEngine.Vector3(spots[i].X, clicked.y, spots[i].Z));
                    var surface = world.GetSurfaceTypeAtTerrainPosition(position);
                    if (surface != clickedSurface || !IsAllowed(world, position, surface)) continue;

                    WriteParams(effect, position, surface);
                    var result = effect.ExecuteResult();
                    if (!result.success) continue;
                    digs++;
                    if (result.insufficientSpace)
                    {
                        effect.OnExecuted(result);
                        full = true;
                    }
                    else if (NativeItemCounts.Capacity(first.itemCounts) == 0) first.itemCounts = result.itemCounts;
                    else NativeItemCounts.AddCounts(first.itemCounts, result.itemCounts);
                }
            }
            finally
            {
                WriteParams(effect, clicked, clickedSurface);
                inner = false;
                Plugin.Debug($"Group dig: {spots.Count} spots, {digs} digs, sum {NativeItemCounts.Sum(first.itemCounts)}, full {full}, {clock.ElapsedMilliseconds} ms");
            }
        }

        // The rule of the game for the start of a hand dig at this position.
        private static bool IsAllowed(World world, UnityEngine.Vector3 position, SurfaceType surface)
        {
            var drops = world.GetGatherItemDropsAtPosition(position);
            bool special = world.specialGatherSurfaceTypes.Contains(surface);
            bool hasDrops = drops != null && drops.Count > 0;
            if (!hasDrops || special) return DigSpots.IsAllowed(hasDrops, special, false, 0f);
            return DigSpots.IsAllowed(true, false, world.IsSolidObjectTakenPosition(position), world.GetTerrainDeepness(position, DigSpots.HandDigRadius));
        }

        // The check that the offset is still right: the params of the first dig are the values of the action.
        private static bool ParamsMatch(DigTerrainEffect effect, UnityEngine.Vector3 position, SurfaceType surface)
        {
            var native = new UnityEngine.Vector3(ReadFloat(effect, 0), ReadFloat(effect, 4), ReadFloat(effect, 8));
            return UnityEngine.Vector3.Distance(native, position) < SameSpot
                && Marshal.ReadInt32(effect.Pointer + SurfaceOffset) == (int)surface;
        }

        private static float ReadFloat(DigTerrainEffect effect, int offset)
        {
            return BitConverter.Int32BitsToSingle(Marshal.ReadInt32(effect.Pointer + ParamsOffset + offset));
        }

        private static void WriteParams(DigTerrainEffect effect, UnityEngine.Vector3 position, SurfaceType surface)
        {
            Marshal.WriteInt32(effect.Pointer + ParamsOffset, BitConverter.SingleToInt32Bits(position.x));
            Marshal.WriteInt32(effect.Pointer + ParamsOffset + 4, BitConverter.SingleToInt32Bits(position.y));
            Marshal.WriteInt32(effect.Pointer + ParamsOffset + 8, BitConverter.SingleToInt32Bits(position.z));
            Marshal.WriteInt32(effect.Pointer + SurfaceOffset, (int)surface);
        }

    }
}
