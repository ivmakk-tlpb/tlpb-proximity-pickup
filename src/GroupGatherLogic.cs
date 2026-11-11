using System;
using System.Collections.Generic;

namespace ProximityPickup
{
    // The rules of the group gather: which resources are quick resources, which resources are of the same kind, and
    // when a gather is a group gather.
    public static class GroupGatherLogic
    {
        // The longest gather time of a quick resource, in seconds (the default gather time of the game).
        private const float QuickDuration = 1.0f;

        private const string KindPrefix = "resource:";

        // A quick resource: the player can gather it now, in 1 s or less, with no continuous gather, and none of
        // its drops goes into the hands.
        public static bool IsQuick(bool canGather, float duration, bool continuous, bool dropsGoIntoHands)
        {
            return canGather && duration <= QuickDuration && !continuous && !dropsGoIntoHands;
        }

        // The kind of a resource is its game name, so the growth variants and the planted resources of one kind
        // are the same kind. The prefix keeps a resource apart from a ground item of the same name.
        public static string KindKey(string name) => KindPrefix + name;

        public static bool ShouldGroupGather(PickMode mode, float radius, bool byLocalPlayer, bool isHost)
        {
            return mode == PickMode.Group && radius > 0f && byLocalPlayer && isHost;
        }

        // The extras in their order, up to the first one with no room. The check of an extra runs just before
        // the caller gathers it, so the check sees the bag after the gathers before it.
        public static IEnumerable<T> TakeUntilNoRoom<T>(IEnumerable<T> extras, Func<T, bool> hasRoom)
        {
            foreach (var extra in extras)
            {
                if (!hasRoom(extra)) yield break;
                yield return extra;
            }
        }

        // The room that the drops of one gather can need: the sum of the amounts of each item, multiplied by the
        // forage bonus and rounded up, because a bonus roll of the game can add to each drop. Items in the order of
        // their first drop.
        public static List<(string Item, int Amount)> RoomNeeded(IEnumerable<(string Item, int Amount)> drops, float forageBonus)
        {
            var sums = new List<(string Item, int Amount)>();
            foreach (var drop in drops)
            {
                int index = sums.FindIndex(s => s.Item == drop.Item);
                if (index < 0) sums.Add(drop);
                else sums[index] = (drop.Item, sums[index].Amount + drop.Amount);
            }
            for (int i = 0; i < sums.Count; i++)
                sums[i] = (sums[i].Item, (int)Math.Ceiling(sums[i].Amount * (1f + forageBonus) - Rounding));
            return sums;
        }

        // A float product such as 4 * 1.5 can come out a little above the whole number.
        private const float Rounding = 0.0001f;
    }
}
