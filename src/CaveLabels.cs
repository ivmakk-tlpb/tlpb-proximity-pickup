using System;

namespace ProximityPickup
{
    // The label range underground (in a cave). The game labels nothing there without lit fuel in the hands, and only
    // within 3 m with it. Underground, the labels get the day range of the surface instead, with or without a torch.
    public static class CaveLabels
    {
        // The day radius of the labelled things on the surface in PlayerManager.handleHighlightItems.
        public const float DayRange = 10f;

        public static float ThingRange(bool underground, float gameRange)
        {
            return underground ? Math.Max(gameRange, DayRange) : gameRange;
        }
    }
}
