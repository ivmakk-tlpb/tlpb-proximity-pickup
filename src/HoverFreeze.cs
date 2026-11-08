using System.Collections.Generic;

namespace ProximityPickup
{
    // Keeps the layout of the labels while a hover card that replaced a label is open. A card holds the freeze
    // when the game showed its label in the last frames, also for a hover on the thing in the world.
    public sealed class HoverFreeze
    {
        private const int LabelSeenFrames = 2;
        private const int MaxSeenLabels = 4096;

        private readonly Dictionary<int, int> labelSeen = new Dictionary<int, int>();
        private readonly Dictionary<int, float> moves = new Dictionary<int, float>();
        private readonly HashSet<int> holders = new HashSet<int>();

        public bool Active { get; private set; }

        public void LabelSeen(int id, int frame)
        {
            if (labelSeen.Count > MaxSeenLabels) labelSeen.Clear();
            labelSeen[id] = frame;
        }

        // An open card of this frame. lastMoves: how far the layout moved each label in the last frame. True: the
        // card holds the freeze.
        public bool CardOpen(int id, int frame, IReadOnlyDictionary<int, float> lastMoves)
        {
            if (holders.Contains(id)) return true;
            if (!labelSeen.TryGetValue(id, out int seen) || frame - seen > LabelSeenFrames) return false;
            holders.Add(id);
            if (!Active)
            {
                Active = true;
                foreach (var move in lastMoves) moves[move.Key] = move.Value;
            }
            return true;
        }

        public bool TryGetMove(int id, out float move)
        {
            return moves.TryGetValue(id, out move);
        }

        // Call after each CardOpen of the frame. True: the freeze ended in this frame.
        public bool EndFrame(ISet<int> openCards, bool labelHovered)
        {
            holders.IntersectWith(openCards);
            if (!Active || holders.Count > 0 || labelHovered) return false;
            Active = false;
            moves.Clear();
            return true;
        }
    }
}
