using ProximityPickup;
using Xunit;

public class MiddleClickTests
{
    [Theory]
    [InlineData(10.0f, 10.1f, 0f)]
    [InlineData(10.0f, 10.3f, 6f)]
    [InlineData(10.0f, 10.0f, 0f)]
    public void A_short_release_with_little_movement_is_a_click(float press, float release, float moved)
    {
        Assert.True(MiddleClick.IsClick(press, release, moved));
    }

    [Theory]
    [InlineData(10.0f, 10.31f, 0f)]
    [InlineData(10.0f, 12.0f, 0f)]
    public void A_longer_hold_is_not_a_click(float press, float release, float moved)
    {
        Assert.False(MiddleClick.IsClick(press, release, moved));
    }

    [Theory]
    [InlineData(6.1f)]
    [InlineData(40f)]
    public void A_drag_is_not_a_click(float moved)
    {
        Assert.False(MiddleClick.IsClick(10.0f, 10.1f, moved));
    }

    [Fact]
    public void A_release_before_the_press_is_not_a_click()
    {
        Assert.False(MiddleClick.IsClick(10.0f, 9.9f, 0f));
    }
}
