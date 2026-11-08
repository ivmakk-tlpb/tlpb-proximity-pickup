using System;
using System.Collections.Generic;

namespace ProximityPickup
{
    // A ground item that a group pickup can take: its entity id, its item kind, and whether the game puts its stack
    // into the hands on a pickup (InventoryItemStack.PreferEquippedSlot).
    public readonly struct PickupCandidate
    {
        public readonly int Id;
        public readonly string Kind;
        public readonly bool PrefersEquippedSlot;

        public PickupCandidate(int id, string kind, bool prefersEquippedSlot)
        {
            Id = id; Kind = kind; PrefersEquippedSlot = prefersEquippedSlot;
        }
    }

    // Where the extra items of a group pickup come from.
    public enum PickupSource
    {
        None,
        // The target has no label on the screen: the extra items are searched at the end of the pickup, within the
        // pickup radius of the target.
        Radius,
        // The target has a label: the extra items are the other items of that label at the click.
        LabelGroup,
    }

    // A ground item that the radius search found, with its distance to the picked item.
    public readonly struct RadiusCandidate
    {
        public readonly PickupCandidate Candidate;
        public readonly float Distance;

        public RadiusCandidate(PickupCandidate candidate, float distance)
        {
            Candidate = candidate; Distance = distance;
        }
    }

    public static class GroupPickupLogic
    {
        // The extra items of a direct pickup: the items of the same kind as the target within the radius, without
        // the target and without items that go into the hands. A radius of 0 or less keeps nothing.
        public static List<int> WithinRadius(PickupCandidate target, IReadOnlyList<RadiusCandidate> found, float radius)
        {
            var extras = new List<int>();
            if (radius <= 0f) return extras;
            foreach (var near in found)
            {
                var c = near.Candidate;
                if (c.Id == target.Id || c.Kind != target.Kind || c.PrefersEquippedSlot || near.Distance > radius) continue;
                extras.Add(c.Id);
            }
            return extras;
        }

        // The extra items of a pickup from a label: its other members, without items that go into the hands.
        public static List<int> LabelExtras(int targetId, IReadOnlyList<PickupCandidate> members)
        {
            var extras = new List<int>();
            foreach (var member in members)
                if (member.Id != targetId && !member.PrefersEquippedSlot) extras.Add(member.Id);
            return extras;
        }

        // The title of a hover card with no label when a pickup also takes extra items, or null with no extra items.
        public static string CardTitle(string name, int targetCount, IReadOnlyList<int> extraCounts)
        {
            if (extraCounts.Count == 0) return null;
            int total = targetCount;
            foreach (int count in extraCounts) total += count;
            return LabelGroup.TextOf(name, total);
        }

        // The plan of a pickup, fixed at the click. A click on an item with a label or on its label takes the items
        // of that label. A click on an item with no label takes the items of the same kind within the radius.
        public static PickupSource Plan(PickMode mode, bool byLocalPlayer, PickupCandidate target, bool hasLabel)
        {
            // Pickups of other characters, and items that go into the hands, take only the one item.
            if (mode != PickMode.Group || !byLocalPlayer || target.PrefersEquippedSlot) return PickupSource.None;
            return hasLabel ? PickupSource.LabelGroup : PickupSource.Radius;
        }
    }

    // Group: a pickup also takes the items of the same kind within the pickup radius.
    // One: a pickup takes one item, as without the mod.
    public enum PickMode
    {
        Group,
        One,
    }

    public static class PickModes
    {
        // An unknown or empty config value gives the default, Group.
        public static PickMode Parse(string value)
        {
            if (string.Equals(value?.Trim(), "One", StringComparison.OrdinalIgnoreCase)) return PickMode.One;
            return PickMode.Group;
        }
    }
}
