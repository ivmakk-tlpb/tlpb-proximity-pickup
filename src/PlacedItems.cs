using System;
using System.Collections.Generic;

namespace ProximityPickup
{
    // A placed item: an item that the player put into the world in its placed form, such as a mold, a trap, or a
    // placed log. It keeps its own label and stays out of each group pickup, so each one stays usable on its own.
    internal static class PlacedItems
    {
        // The placed items that the log named, by entity id.
        private static readonly HashSet<int> logged = new HashSet<int>();
        private const int MaxLogged = 4096;

        // A failed read (a game update that renamed the component) counts as not placed, so only this check stops.
        internal static bool IsPlaced(Entity entity)
        {
            bool placed;
            try
            {
                placed = ReadPlaced(entity);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Placed items", e);
                return false;
            }
            if (placed && Plugin.Verbose.Value && !logged.Contains(entity.id))
            {
                if (logged.Count >= MaxLogged) logged.Clear();
                logged.Add(entity.id);
                var kind = entity.GetCachedComponent<IPickupable>(false)?.GetItemStack()?.Item?.Identifier;
                Plugin.Debug($"Placed item {entity.id} ({kind}): own label, no group pickup");
            }
            return placed;
        }

        // A separate method, so a missing game type fails here and not in the caller.
        private static bool ReadPlaced(Entity entity)
        {
            return entity.GetCachedComponent<IPlacedItem>(false) != null;
        }
    }
}
