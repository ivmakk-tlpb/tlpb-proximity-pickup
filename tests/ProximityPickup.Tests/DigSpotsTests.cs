using ProximityPickup;
using Xunit;

public class DigSpotsTests
{
    [Fact]
    public void The_clicked_spot_comes_first()
    {
        var spots = DigSpots.Grid(10f, -3f, 2f);
        Assert.Equal((10f, -3f), spots[0]);
    }

    [Fact]
    public void Spots_are_sorted_from_the_clicked_spot_outward()
    {
        var spots = DigSpots.Grid(0f, 0f, 2f);
        for (int i = 1; i < spots.Count; i++)
        {
            float before = spots[i - 1].X * spots[i - 1].X + spots[i - 1].Z * spots[i - 1].Z;
            float now = spots[i].X * spots[i].X + spots[i].Z * spots[i].Z;
            Assert.True(before <= now);
        }
    }

    // The lattice points with x*x + z*z <= 20 are 69, and a radius of 2 m reaches 20.37 grid cells squared.
    [Fact]
    public void A_radius_of_two_meters_fills_the_area_with_about_sixty_four_digs()
    {
        Assert.Equal(69, DigSpots.Grid(0f, 0f, 2f).Count);
    }

    [Fact]
    public void Each_spot_lies_within_the_radius()
    {
        foreach (var spot in DigSpots.Grid(0f, 0f, 2f))
            Assert.True(spot.X * spot.X + spot.Z * spot.Z <= 4.0001f);
    }

    [Theory]
    [InlineData(10f)]
    [InlineData(1000f)]
    public void A_large_radius_has_the_limit_of_spots(float radius)
    {
        Assert.Equal(256, DigSpots.Grid(0f, 0f, radius).Count);
    }

    [Fact]
    public void A_radius_below_the_spacing_holds_only_the_clicked_spot()
    {
        Assert.Single(DigSpots.Grid(1f, 1f, 0.4f));
    }

    [Fact]
    public void No_drops_means_no_dig()
    {
        Assert.False(DigSpots.IsAllowed(false, true, false, 0f));
    }

    [Fact]
    public void A_special_surface_with_drops_needs_no_other_check()
    {
        Assert.True(DigSpots.IsAllowed(true, true, true, 5f));
    }

    [Fact]
    public void A_solid_object_blocks_a_normal_surface()
    {
        Assert.False(DigSpots.IsAllowed(true, false, true, 0f));
    }

    [Theory]
    [InlineData(0.41f, true)]
    [InlineData(0.42f, false)]
    public void The_depth_limit_applies_to_a_normal_surface(float depth, bool allowed)
    {
        Assert.Equal(allowed, DigSpots.IsAllowed(true, false, false, depth));
    }

    [Theory]
    [InlineData(PickMode.Group, 2f, true, true, true, true)]
    [InlineData(PickMode.One, 2f, true, true, true, false)]
    [InlineData(PickMode.Group, 0f, true, true, true, false)]
    [InlineData(PickMode.Group, 2f, false, true, true, false)]
    [InlineData(PickMode.Group, 2f, true, false, true, false)]
    [InlineData(PickMode.Group, 2f, true, true, false, false)]
    public void A_group_dig_needs_each_guard(PickMode mode, float radius, bool handDig, bool localPlayer, bool host, bool expected)
    {
        Assert.Equal(expected, DigSpots.ShouldGroupDig(mode, radius, handDig, localPlayer, host));
    }
}
