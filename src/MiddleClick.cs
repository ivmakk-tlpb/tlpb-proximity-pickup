namespace ProximityPickup
{
    // The rule of a middle-click: a short press and release with almost no mouse movement. A longer hold or a drag
    // keeps the game's camera rotation on the middle mouse button.
    public static class MiddleClick
    {
        public const float MaxSeconds = 0.3f;
        public const float MaxPixels = 6f;

        public static bool IsClick(float pressTime, float releaseTime, float movedPixels)
        {
            float held = releaseTime - pressTime;
            // The tolerance keeps a hold of exactly the limit a click, also with the rounding of float times.
            return held >= 0f && held <= MaxSeconds + 0.001f && movedPixels <= MaxPixels;
        }
    }
}
