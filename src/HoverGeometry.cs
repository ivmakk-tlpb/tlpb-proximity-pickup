namespace ProximityPickup
{
    // A point or a distance on the screen, in pixels.
    public readonly struct ScreenPoint
    {
        public readonly float X, Y;

        public ScreenPoint(float x, float y)
        {
            X = x; Y = y;
        }
    }

    // Where a hover card goes so that its title covers the label that it replaced. All centers are the centers of
    // the drawn letters, because the label and the title have different rectangles around their text.
    public static class HoverGeometry
    {
        private const float SidePadding = 6f;
        private const float EdgePadding = 3f;

        // The strip at the top and the bottom of a label box that does not catch the mouse. Labels in a stack touch,
        // so without it each box would reach into its neighbor and a hover there could pick the wrong label.
        public const float EdgeCatchInset = EdgePadding;

        // The place of the title relative to the game position of the card, so the card follows the camera.
        public static ScreenPoint Anchor(ScreenPoint labelText, ScreenPoint cardGamePosition)
        {
            return new ScreenPoint(labelText.X - cardGamePosition.X, labelText.Y - cardGamePosition.Y);
        }

        // How far the card moves from its game position. Title: the title center relative to the card position.
        public static ScreenPoint CardShift(ScreenPoint anchor, ScreenPoint title)
        {
            return new ScreenPoint(anchor.X - title.X, anchor.Y - title.Y);
        }

        // The label rectangle around the title, with the card at its game position.
        public static ScreenRect CardBox(ScreenRect label, ScreenPoint labelText, ScreenPoint cardGamePosition, ScreenPoint title)
        {
            float halfWidth = (label.XMax - label.XMin) / 2f, halfHeight = (label.YMax - label.YMin) / 2f;
            float x = cardGamePosition.X + title.X + (label.XMin + halfWidth - labelText.X);
            float y = cardGamePosition.Y + title.Y + (label.YMin + halfHeight - labelText.Y);
            return new ScreenRect(x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight);
        }

        // The part of the label box that the progress of a hold action fills.
        public static float FillWidth(float progress)
        {
            if (float.IsNaN(progress) || progress < 0f) return 0f;
            return progress > 1f ? 1f : progress;
        }

        // The label box around a label rectangle.
        public static ScreenRect LabelBox(ScreenRect label)
        {
            return new ScreenRect(label.XMin - SidePadding, label.YMin - EdgePadding, label.XMax + SidePadding, label.YMax + EdgePadding);
        }
    }
}
