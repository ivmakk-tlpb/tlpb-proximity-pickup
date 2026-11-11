using ProximityPickup;
using Xunit;

public class GroupGatherLogicTests
{
    [Fact]
    public void A_gather_of_one_second_is_quick()
    {
        Assert.True(GroupGatherLogic.IsQuick(true, 1.0f, false, false));
    }

    [Fact]
    public void A_gather_of_more_than_one_second_is_timed()
    {
        Assert.False(GroupGatherLogic.IsQuick(true, 1.01f, false, false));
    }

    [Fact]
    public void A_continuous_gather_is_timed()
    {
        Assert.False(GroupGatherLogic.IsQuick(true, 0.8f, true, false));
    }

    [Fact]
    public void A_resource_whose_drop_goes_into_the_hands_is_not_quick()
    {
        Assert.False(GroupGatherLogic.IsQuick(true, 0.8f, false, true));
    }

    [Fact]
    public void A_resource_that_is_not_ready_is_not_quick()
    {
        Assert.False(GroupGatherLogic.IsQuick(false, 0.8f, false, false));
    }

    [Fact]
    public void The_kind_of_a_resource_is_its_name_with_a_prefix()
    {
        Assert.Equal("resource:Spotted Red Mushroom", GroupGatherLogic.KindKey("Spotted Red Mushroom"));
    }

    [Fact]
    public void A_resource_and_an_item_of_the_same_name_are_different_kinds()
    {
        Assert.NotEqual("stick", GroupGatherLogic.KindKey("stick"));
    }

    [Fact]
    public void The_local_host_player_in_group_mode_runs_a_group_gather()
    {
        Assert.True(GroupGatherLogic.ShouldGroupGather(PickMode.Group, 2f, true, true));
    }

    [Theory]
    [InlineData(PickMode.One, 2f, true, true)]
    [InlineData(PickMode.Group, 0f, true, true)]
    [InlineData(PickMode.Group, 2f, false, true)]
    [InlineData(PickMode.Group, 2f, true, false)]
    public void Each_off_switch_stops_the_group_gather(PickMode mode, float radius, bool byLocalPlayer, bool isHost)
    {
        Assert.False(GroupGatherLogic.ShouldGroupGather(mode, radius, byLocalPlayer, isHost));
    }
}

public class GroupGatherRoomTests
{
    [Fact]
    public void The_extras_stop_at_the_first_one_with_no_room()
    {
        var asked = new List<int>();
        var taken = GroupGatherLogic.TakeUntilNoRoom(new[] { 1, 2, 3, 4 }, id => { asked.Add(id); return id != 3; }).ToList();

        Assert.Equal(new[] { 1, 2 }, taken);
        Assert.Equal(new[] { 1, 2, 3 }, asked);
    }

    [Fact]
    public void The_room_check_runs_just_before_each_extra()
    {
        var log = new List<string>();
        foreach (int id in GroupGatherLogic.TakeUntilNoRoom(new[] { 1, 2 }, id => { log.Add("room " + id); return true; }))
            log.Add("gather " + id);

        Assert.Equal(new[] { "room 1", "gather 1", "room 2", "gather 2" }, log);
    }

    [Fact]
    public void The_room_for_a_clay_deposit_counts_each_drop_and_the_forage_bonus()
    {
        var drops = new[] { ("clay", 1), ("clay", 1), ("clay", 1), ("clay", 1) };

        Assert.Equal(new[] { ("clay", 6) }, GroupGatherLogic.RoomNeeded(drops, 0.5f));
    }

    [Fact]
    public void The_forage_bonus_rounds_up()
    {
        Assert.Equal(new[] { ("cap", 2) }, GroupGatherLogic.RoomNeeded(new[] { ("cap", 1) }, 0.1f));
    }

    [Fact]
    public void With_no_forage_bonus_the_room_is_the_sum_of_each_item()
    {
        var drops = new[] { ("cap", 2), ("spore", 1), ("cap", 1) };

        Assert.Equal(new[] { ("cap", 3), ("spore", 1) }, GroupGatherLogic.RoomNeeded(drops, 0f));
    }

    [Fact]
    public void A_resource_that_is_not_quick_is_never_an_extra()
    {
        string kind = GroupGatherLogic.KindKey("Dandelion");
        var target = new PickupCandidate(1, kind, false);
        var found = new[]
        {
            new RadiusCandidate(new PickupCandidate(2, kind, prefersEquippedSlot: !true), 1f),
            new RadiusCandidate(new PickupCandidate(3, kind, prefersEquippedSlot: !false), 1f),
        };

        Assert.Equal(new[] { 2 }, GroupPickupLogic.WithinRadius(target, found, 2f));
        Assert.Equal(new[] { 2 }, GroupPickupLogic.LabelExtras(1, new[] { found[0].Candidate, found[1].Candidate }));
    }
}
