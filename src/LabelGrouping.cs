using System.Collections.Generic;

namespace ProximityPickup
{
    // A rectangle in screen pixels.
    public readonly struct ScreenRect
    {
        public readonly float XMin, YMin, XMax, YMax;

        public ScreenRect(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin; YMin = yMin; XMax = xMax; YMax = yMax;
        }

        // True when the two rectangles, each grown by padding on every side, intersect.
        public bool Overlaps(ScreenRect other, float padding)
        {
            float grow = 2 * padding;
            return XMin < other.XMax + grow && other.XMin < XMax + grow
                && YMin < other.YMax + grow && other.YMin < YMax + grow;
        }
    }

    // A position in the world, in meters.
    public readonly struct WorldPoint
    {
        public readonly float X, Y, Z;

        public WorldPoint(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public bool IsWithin(WorldPoint other, float radius)
        {
            float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz <= radius * radius;
        }
    }

    // The item label of one ground item in this frame.
    public sealed class LabelInput
    {
        public readonly int Id;
        public readonly string Kind;
        public readonly string Name;
        public readonly WorldPoint Position;
        public readonly int Count;
        public readonly float Distance;
        // True for a label on the screen and for a hover card that replaced a label of this item.
        public readonly bool ShowsLabel;

        public LabelInput(int id, string kind, string name, WorldPoint position, int count, float distance, bool showsLabel)
        {
            Id = id; Kind = kind; Name = name; Position = position; Count = count; Distance = distance; ShowsLabel = showsLabel;
        }
    }

    // A label group: the item labels of one item kind that lie close together in the world.
    public sealed class LabelGroup
    {
        public readonly List<int> MemberIds = new List<int>();
        // The member nearest the player. Its label shows the text of the group, and a click on it picks the group.
        public int RepresentativeId;
        public string Kind;
        public string Name;
        // The sum of the stack counts of the members.
        public int Count;
        // True when a member shows a label. Only then does a click take the members of the group.
        public bool ShowsLabel;

        private float nearestDistance = float.MaxValue;

        public string Text => TextOf(Name, Count);

        // "Name (N)", the format of the game for a stack, or the plain name for one stack of 1.
        public static string TextOf(string name, int count)
        {
            return count == 1 ? name : name + " (" + count + ")";
        }

        internal void Add(LabelInput label)
        {
            MemberIds.Add(label.Id);
            Count += label.Count;
            ShowsLabel |= label.ShowsLabel;
            // On a tie the first label stays the representative, so the same input gives the same group.
            if (label.Distance >= nearestDistance) return;
            nearestDistance = label.Distance;
            RepresentativeId = label.Id;
            Name = label.Name;
        }
    }

    public static class LabelGrouping
    {
        // Each label that is not yet in a group starts a group, with the free labels of its kind within the radius.
        // The label with the most neighbors starts first, so an item beside a heap does not take a part of the heap.
        // The lowest id wins a tie, so the groups do not depend on the input order or on the place of the player.
        // The frozen ids (the label group of a hovered item) form one group first, so a hover does not change it.
        public static List<LabelGroup> Group(IReadOnlyList<LabelInput> labels, float radius, ISet<int> frozen = null)
        {
            int n = labels.Count;
            var neighbors = new int[n];
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
                for (int j = i + 1; j < n; j++)
                {
                    if (!AreNear(labels[i], labels[j], radius)) continue;
                    neighbors[i]++;
                    neighbors[j]++;
                }
            }
            System.Array.Sort(order, (a, b) => neighbors[a] != neighbors[b] ? neighbors[b] - neighbors[a] : labels[a].Id.CompareTo(labels[b].Id));

            var taken = new bool[n];
            var groups = new List<LabelGroup>();
            if (frozen != null && frozen.Count > 0)
            {
                LabelGroup kept = null;
                foreach (int i in order)
                {
                    if (!frozen.Contains(labels[i].Id)) continue;
                    if (kept == null)
                    {
                        kept = new LabelGroup { Kind = labels[i].Kind };
                        groups.Add(kept);
                    }
                    taken[i] = true;
                    kept.Add(labels[i]);
                }
            }
            foreach (int i in order)
            {
                if (taken[i]) continue;
                var start = labels[i];
                var group = new LabelGroup { Kind = start.Kind };
                groups.Add(group);
                taken[i] = true;
                group.Add(start);
                foreach (int j in order)
                {
                    if (taken[j] || !AreNear(start, labels[j], radius)) continue;
                    taken[j] = true;
                    group.Add(labels[j]);
                }
            }
            return groups;
        }

        private static bool AreNear(LabelInput a, LabelInput b, float radius)
        {
            return radius > 0f && a.Kind == b.Kind && a.Position.IsWithin(b.Position, radius);
        }
    }
}
