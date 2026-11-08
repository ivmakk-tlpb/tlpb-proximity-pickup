using ProximityPickup;
using Xunit;

public class LabelGroupingTests
{
    private const float Radius = 2f;

    // A label of an item at (x, z) on flat ground.
    private static LabelInput Label(int id, string kind, float x, float z = 0f, int count = 1, float distance = 1f, string name = null, float y = 0f, bool showsLabel = true)
    {
        return new LabelInput(id, kind, name ?? kind, new WorldPoint(x, y, z), count, distance, showsLabel);
    }

    private static LabelGroup GroupOf(List<LabelGroup> groups, int id)
    {
        return groups.Single(g => g.MemberIds.Contains(id));
    }

    [Fact]
    public void Two_items_of_one_kind_within_the_radius_merge()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0), Label(2, "Stick", 1.5f) }, Radius);

        var group = Assert.Single(groups);
        Assert.Equal(new[] { 1, 2 }, group.MemberIds.OrderBy(i => i));
    }

    [Fact]
    public void Two_items_of_different_kinds_do_not_merge()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0), Label(2, "Flint", 0.5f) }, Radius);

        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { 1 }, GroupOf(groups, 1).MemberIds);
        Assert.Equal(new[] { 2 }, GroupOf(groups, 2).MemberIds);
    }

    [Fact]
    public void Two_items_beyond_the_radius_do_not_merge()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0), Label(2, "Stick", 2.5f) }, Radius);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void An_item_at_exactly_the_radius_merges()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0), Label(2, "Stick", 2f) }, Radius);

        Assert.Single(groups);
    }

    [Fact]
    public void The_distance_counts_the_height()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0), Label(2, "Stick", 0, y: 3f) }, Radius);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void The_middle_of_a_line_of_three_starts_the_group()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0), Label(2, "Stick", 1.5f), Label(3, "Stick", 3f) }, Radius);

        Assert.Equal(new[] { 1, 2, 3 }, Assert.Single(groups).MemberIds.OrderBy(i => i));
    }

    [Fact]
    public void A_line_of_five_splits_into_two_groups_and_the_lowest_id_wins_a_tie()
    {
        var groups = LabelGrouping.Group(new[]
        {
            Label(1, "Stick", 0), Label(2, "Stick", 1.5f), Label(3, "Stick", 3f), Label(4, "Stick", 4.5f), Label(5, "Stick", 6f),
        }, Radius);

        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { 1, 2, 3 }, GroupOf(groups, 1).MemberIds.OrderBy(i => i));
        Assert.Equal(new[] { 4, 5 }, GroupOf(groups, 5).MemberIds.OrderBy(i => i));
    }

    [Fact]
    public void A_lower_id_stick_beside_a_heap_joins_the_heap()
    {
        var groups = LabelGrouping.Group(new[]
        {
            Label(1, "Stick", 2f), Label(2, "Stick", 3f), Label(3, "Stick", 3.5f), Label(4, "Stick", 4f), Label(5, "Stick", 4.5f),
        }, Radius);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Assert.Single(groups).MemberIds.OrderBy(i => i));
    }

    [Fact]
    public void The_groups_do_not_depend_on_the_input_order_or_the_distance_to_the_player()
    {
        LabelInput[] Line(float distance) => new[]
        {
            Label(1, "Stick", 0, distance: distance), Label(2, "Stick", 1.5f, distance: 9f - distance), Label(3, "Stick", 3f, distance: distance),
            Label(4, "Stick", 4.5f, distance: 9f - distance), Label(5, "Stick", 6f, distance: distance),
        };
        string Shape(IEnumerable<LabelInput> labels) => string.Join(";", LabelGrouping.Group(labels.ToList(), Radius)
            .Select(g => string.Join(",", g.MemberIds.OrderBy(i => i))).OrderBy(s => s));

        Assert.Equal("1,2,3;4,5", Shape(Line(1f)));
        Assert.Equal("1,2,3;4,5", Shape(Line(8f).Reverse()));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-2f)]
    public void A_radius_of_0_or_less_merges_nothing(float radius)
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0), Label(2, "Stick", 0) }, radius);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void The_frozen_ids_form_their_group_first()
    {
        var labels = new[] { Label(1, "Stick", 0), Label(2, "Stick", 1f), Label(3, "Stick", 1.5f) };

        var groups = LabelGrouping.Group(labels, Radius, new HashSet<int> { 2, 3 });

        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { 2, 3 }, GroupOf(groups, 2).MemberIds.OrderBy(i => i));
        Assert.Equal(new[] { 1 }, GroupOf(groups, 1).MemberIds);
    }

    [Fact]
    public void A_frozen_single_item_stays_alone()
    {
        var labels = new[] { Label(1, "Stick", 0), Label(2, "Stick", 1f) };

        var groups = LabelGrouping.Group(labels, Radius, new HashSet<int> { 2 });

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void The_representative_of_a_frozen_group_is_the_member_nearest_the_player()
    {
        var labels = new[] { Label(1, "Stick", 0, distance: 5f), Label(2, "Stick", 1f, distance: -1f), Label(3, "Stick", 1.5f, distance: 2f) };

        var groups = LabelGrouping.Group(labels, Radius, new HashSet<int> { 1, 2, 3 });

        Assert.Equal(2, Assert.Single(groups).RepresentativeId);
    }

    [Fact]
    public void A_frozen_id_with_no_label_in_the_frame_makes_no_group()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "Stick", 0) }, Radius, new HashSet<int> { 7 });

        Assert.Equal(new[] { 1 }, Assert.Single(groups).MemberIds);
    }

    [Fact]
    public void The_representative_is_the_member_nearest_the_player()
    {
        var groups = LabelGrouping.Group(new[]
        {
            Label(1, "Stick", 0, distance: 3.0f),
            Label(2, "Stick", 0.5f, distance: 1.5f),
            Label(3, "Stick", 1f, distance: 2.0f),
        }, Radius);

        Assert.Equal(2, Assert.Single(groups).RepresentativeId);
    }

    [Fact]
    public void The_text_shows_the_sum_of_the_stack_counts()
    {
        var groups = LabelGrouping.Group(new[]
        {
            Label(1, "stick", 0, name: "Stick"),
            Label(2, "stick", 0.5f, name: "Stick"),
            Label(3, "stick", 1f, name: "Stick"),
        }, Radius);

        var group = Assert.Single(groups);
        Assert.Equal(3, group.Count);
        Assert.Equal("Stick (3)", group.Text);
    }

    [Fact]
    public void A_stack_of_3_and_8_single_items_show_11()
    {
        var labels = new List<LabelInput> { Label(1, "stick", 0, count: 3, name: "Stick") };
        for (int id = 2; id <= 9; id++) labels.Add(Label(id, "stick", 0.1f * id, name: "Stick"));

        Assert.Equal("Stick (11)", Assert.Single(LabelGrouping.Group(labels, Radius)).Text);
    }

    [Fact]
    public void A_group_shows_a_label_when_one_member_shows_a_label()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "stick", 0, showsLabel: false), Label(2, "stick", 1f) }, Radius);

        Assert.True(Assert.Single(groups).ShowsLabel);
    }

    [Fact]
    public void A_hover_card_that_replaced_no_label_gives_a_group_with_no_label()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "stick", 0, showsLabel: false) }, Radius);

        Assert.False(Assert.Single(groups).ShowsLabel);
    }

    [Fact]
    public void A_frozen_group_with_a_label_shows_a_label()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "stick", 0) }, Radius, new HashSet<int> { 1 });

        Assert.True(Assert.Single(groups).ShowsLabel);
    }

    [Fact]
    public void One_stack_of_1_shows_the_plain_name()
    {
        var groups = LabelGrouping.Group(new[] { Label(1, "stick", 0, name: "Stick") }, Radius);

        Assert.Equal("Stick", Assert.Single(groups).Text);
    }
}
