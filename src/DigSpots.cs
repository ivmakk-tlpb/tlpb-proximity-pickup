using System;
using System.Collections.Generic;

namespace ProximityPickup
{
    // The spots of a group dig, and the rules for them. The values of a hand dig are the game's own.
    public static class DigSpots
    {
        public const float HandDigRadius = 0.25f;

        private const float DepthLimit = 0.42f;
        private const int MaxSpots = 256;

        // One grid cell has the area of one dig circle, so a radius holds about (radius / dig radius)^2 spots.
        private static readonly float Spacing = HandDigRadius * (float)Math.Sqrt(Math.PI);

        // A circle of this radius holds more than MaxSpots grid points, so a larger radius changes nothing, and the
        // search stays small for each config value.
        private const float MaxSearchRadius = 4.5f;

        // The clicked spot first, then the other spots of the grid from near to far.
        public static List<(float X, float Z)> Grid(float centerX, float centerZ, float radius)
        {
            float reach = Math.Min(radius, MaxSearchRadius);
            int cells = (int)(reach / Spacing);
            var offsets = new List<(int X, int Z)>();
            for (int x = -cells; x <= cells; x++)
                for (int z = -cells; z <= cells; z++)
                    if ((x * x + z * z) * Spacing * Spacing <= reach * reach) offsets.Add((x, z));
            offsets.Sort((a, b) => (a.X * a.X + a.Z * a.Z).CompareTo(b.X * b.X + b.Z * b.Z));

            var spots = new List<(float X, float Z)>();
            for (int i = 0; i < offsets.Count && i < MaxSpots; i++)
                spots.Add((centerX + offsets[i].X * Spacing, centerZ + offsets[i].Z * Spacing));
            return spots;
        }

        // The rule of the game for the start of a hand dig. A special surface (clay) has no depth limit.
        public static bool IsAllowed(bool hasDrops, bool specialSurface, bool solidObject, float deepness)
        {
            if (!hasDrops) return false;
            return specialSurface || (!solidObject && deepness < DepthLimit);
        }

        public static bool ShouldGroupDig(PickMode mode, float radius, bool handDig, bool byLocalPlayer, bool isHost)
        {
            return mode == PickMode.Group && radius > 0f && handDig && byLocalPlayer && isHost;
        }
    }
}
