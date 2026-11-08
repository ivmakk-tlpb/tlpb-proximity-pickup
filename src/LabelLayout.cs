using System;
using System.Collections.Generic;

namespace ProximityPickup
{
    // A label for the layout: an id and its screen rectangle.
    public readonly struct LayoutBox
    {
        public readonly int Id;
        public readonly ScreenRect Rect;

        public LayoutBox(int id, ScreenRect rect)
        {
            Id = id; Rect = rect;
        }
    }

    public static class LabelLayout
    {
        // Places the movable labels so that no two labels overlap. The obstacles never move. A movable label
        // keeps its x and takes the free y nearest its own y. The labels are placed in order of their y.
        // A free y inside the screen (screenYMin to screenYMax) wins over one outside it.
        public static Dictionary<int, ScreenRect> Place(IReadOnlyList<LayoutBox> movable, IReadOnlyList<LayoutBox> obstacles,
            float screenYMin = float.MinValue, float screenYMax = float.MaxValue)
        {
            var placed = new List<ScreenRect>(movable.Count + obstacles.Count);
            for (int i = 0; i < obstacles.Count; i++) placed.Add(obstacles[i].Rect);

            var order = new List<LayoutBox>(movable);
            order.Sort((a, b) =>
            {
                int byY = a.Rect.YMin.CompareTo(b.Rect.YMin);
                return byY != 0 ? byY : a.Id.CompareTo(b.Id);
            });

            var result = new Dictionary<int, ScreenRect>(movable.Count);
            foreach (var box in order)
            {
                var rect = FreeSpot(box.Rect, placed, screenYMin, screenYMax);
                placed.Add(rect);
                result[box.Id] = rect;
            }
            return result;
        }

        // The rectangle moved on y the shortest distance that gives it free space. The candidates are the
        // own y and the y right above or right below each placed rectangle. A candidate on the screen wins over
        // one off the screen, so a label near a screen edge moves away from the edge when it can.
        private static ScreenRect FreeSpot(ScreenRect rect, List<ScreenRect> placed, float screenYMin, float screenYMax)
        {
            if (IsFree(rect, placed)) return rect;

            float height = rect.YMax - rect.YMin;
            ScreenRect best = rect;
            float bestMove = float.MaxValue;
            bool bestOnScreen = false;
            foreach (var other in placed)
            {
                if (!(rect.XMin < other.XMax && other.XMin < rect.XMax)) continue;
                for (int side = 0; side < 2; side++)
                {
                    float y = side == 0 ? other.YMax : other.YMin - height;
                    float move = Math.Abs(y - rect.YMin);
                    bool onScreen = y >= screenYMin && y + height <= screenYMax;
                    if (bestOnScreen && !onScreen) continue;
                    if (move >= bestMove && onScreen == bestOnScreen) continue;
                    var candidate = new ScreenRect(rect.XMin, y, rect.XMax, y + height);
                    if (!IsFree(candidate, placed)) continue;
                    best = candidate;
                    bestMove = move;
                    bestOnScreen = onScreen;
                }
            }
            return best;
        }

        private static bool IsFree(ScreenRect rect, List<ScreenRect> placed)
        {
            foreach (var other in placed)
                if (rect.Overlaps(other, 0f)) return false;
            return true;
        }
    }
}
