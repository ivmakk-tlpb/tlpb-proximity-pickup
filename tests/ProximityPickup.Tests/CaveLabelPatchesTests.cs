using ProximityPickup;
using ProximityPickup.Labels;
using Xunit;

public class CaveLabelPatchesTests : GameTestBase
{
    private readonly World world = new World();

    private void Underground(bool value) => world.Underground = () => value;

    private static void BeginHighlight() => PatchCall.Run(typeof(CaveLabelPatches), "HighlightPrefix");

    private static void EndHighlight() => PatchCall.Run(typeof(CaveLabelPatches), "HighlightFinalizer");

    private float SearchThings(float range)
    {
        var args = new object[] { world, range };
        PatchCall.Run(typeof(CaveLabelPatches), "ThingsPrefix", args);
        return (float)args[1];
    }

    [Fact]
    public void Underground_the_things_search_of_the_highlight_gets_10_meters()
    {
        Underground(true);
        BeginHighlight();

        Assert.Equal(10f, SearchThings(0f));
    }

    [Fact]
    public void On_the_surface_the_things_search_keeps_the_game_range()
    {
        BeginHighlight();

        Assert.Equal(3f, SearchThings(3f));
    }

    [Fact]
    public void A_second_search_in_the_same_highlight_keeps_its_range()
    {
        Underground(true);
        BeginHighlight();
        SearchThings(0f);

        Assert.Equal(5f, SearchThings(5f));
    }

    [Fact]
    public void A_search_outside_the_highlight_keeps_its_range()
    {
        Underground(true);

        Assert.Equal(5f, SearchThings(5f));
    }

    [Fact]
    public void The_end_of_the_highlight_clears_the_flag_also_with_no_search()
    {
        Underground(true);
        BeginHighlight();
        EndHighlight();

        Assert.Equal(5f, SearchThings(5f));
    }

    [Fact]
    public void An_error_in_the_search_prefix_warns_once_and_keeps_the_game_range()
    {
        world.Underground = () => throw new InvalidOperationException("broken");
        BeginHighlight();
        float range = SearchThings(0f);
        BeginHighlight();
        SearchThings(0f);

        Assert.Equal(0f, range);
        Assert.StartsWith("Cave labels: InvalidOperationException: broken", Assert.Single(Plugin.Log.Warnings));
    }

    [Fact]
    public void The_verbose_line_writes_once_for_equal_frames_and_again_after_a_change()
    {
        Plugin.Verbose.Value = true;
        Plugin.Log.Debugs.Clear();
        Underground(true);
        for (int frame = 0; frame < 2; frame++)
        {
            BeginHighlight();
            SearchThings(0f);
            EndHighlight();
        }
        Underground(false);
        BeginHighlight();
        SearchThings(10f);
        EndHighlight();

        Assert.Equal(new[]
        {
            "Cave labels: underground true, range 0 -> 10",
            "Cave labels: underground false, range 10 -> 10",
        }, Plugin.Log.Debugs);
    }
}
