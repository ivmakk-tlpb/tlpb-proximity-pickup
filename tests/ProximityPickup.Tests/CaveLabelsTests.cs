using ProximityPickup;
using Xunit;

public class CaveLabelsTests
{
    [Theory]
    [InlineData(0f, 10f)]
    [InlineData(3f, 10f)]
    [InlineData(12f, 12f)]
    public void Underground_the_labelled_things_get_at_least_the_day_range(float game, float expected)
    {
        Assert.Equal(expected, CaveLabels.ThingRange(true, game));
    }

    [Theory]
    [InlineData(3f)]
    [InlineData(7f)]
    [InlineData(10f)]
    public void On_the_surface_the_labelled_things_keep_the_game_range(float game)
    {
        Assert.Equal(game, CaveLabels.ThingRange(false, game));
    }

}
