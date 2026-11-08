using ProximityPickup;
using Xunit;

public class GroupPickupLogicTests
{
    private static PickupCandidate Stick(int id, bool prefersEquipped = false)
    {
        return new PickupCandidate(id, "stick", prefersEquipped);
    }

    [Fact]
    public void A_target_with_no_label_uses_the_radius()
    {
        Assert.Equal(PickupSource.Radius, GroupPickupLogic.Plan(PickMode.Group, true, Stick(1), false));
    }

    [Fact]
    public void A_target_with_a_label_takes_the_items_of_the_label()
    {
        Assert.Equal(PickupSource.LabelGroup, GroupPickupLogic.Plan(PickMode.Group, true, Stick(1), true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_pick_mode_One_takes_no_extras(bool hasLabel)
    {
        Assert.Equal(PickupSource.None, GroupPickupLogic.Plan(PickMode.One, true, Stick(1), hasLabel));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_pickup_by_another_character_takes_no_extras(bool hasLabel)
    {
        Assert.Equal(PickupSource.None, GroupPickupLogic.Plan(PickMode.Group, false, Stick(1), hasLabel));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_target_that_goes_into_the_hands_takes_no_extras(bool hasLabel)
    {
        Assert.Equal(PickupSource.None, GroupPickupLogic.Plan(PickMode.Group, true, new PickupCandidate(1, "log", true), hasLabel));
    }

    [Fact]
    public void The_extras_of_a_label_are_its_members_without_the_target_and_without_items_for_the_hands()
    {
        var members = new[] { Stick(1), Stick(2), Stick(3, prefersEquipped: true), Stick(4) };

        Assert.Equal(new[] { 2, 4 }, GroupPickupLogic.LabelExtras(1, members));
    }

    [Fact]
    public void A_label_of_one_item_has_no_extras()
    {
        Assert.Empty(GroupPickupLogic.LabelExtras(1, new[] { Stick(1) }));
    }

    [Fact]
    public void The_title_of_a_card_with_extra_items_shows_the_total()
    {
        Assert.Equal("Stick (11)", GroupPickupLogic.CardTitle("Stick", 3, new[] { 5, 3 }));
    }

    [Fact]
    public void A_card_with_no_extra_items_keeps_the_game_text()
    {
        Assert.Null(GroupPickupLogic.CardTitle("Stick", 3, new int[0]));
    }

    private static RadiusCandidate Near(PickupCandidate candidate, float distance)
    {
        return new RadiusCandidate(candidate, distance);
    }

    [Fact]
    public void The_radius_keeps_items_of_the_same_kind_within_the_radius()
    {
        var found = new[]
        {
            Near(Stick(2), 0.5f),
            Near(Stick(3), 1.9f),
            Near(Stick(4), 2.0f),
        };

        Assert.Equal(new[] { 2, 3, 4 }, GroupPickupLogic.WithinRadius(Stick(1), found, 2f));
    }

    [Fact]
    public void The_radius_drops_other_kinds_far_items_the_target_and_items_for_the_hands()
    {
        var found = new[]
        {
            Near(new PickupCandidate(2, "flint", false), 0.5f),
            Near(Stick(3), 2.5f),
            Near(Stick(1), 0f),
            Near(Stick(4, prefersEquipped: true), 0.5f),
            Near(Stick(5), 1f),
        };

        Assert.Equal(new[] { 5 }, GroupPickupLogic.WithinRadius(Stick(1), found, 2f));
    }

    [Fact]
    public void A_radius_of_0_keeps_nothing()
    {
        var found = new[] { Near(Stick(2), 0f), Near(Stick(3), 0.1f) };

        Assert.Empty(GroupPickupLogic.WithinRadius(Stick(1), found, 0f));
    }
}
