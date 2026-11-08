using ProximityPickup;
using Xunit;

public class LabelLayoutTests
{
    // A label 60 x 20 px with its lower left corner at (x, y).
    private static LayoutBox Box(int id, float x, float y)
    {
        return new LayoutBox(id, new ScreenRect(x, y, x + 60, y + 20));
    }

    private static void AssertNoOverlap(IReadOnlyList<ScreenRect> rects)
    {
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
                Assert.False(rects[i].Overlaps(rects[j], 0f), $"rect {i} overlaps rect {j}");
    }

    [Fact]
    public void Labels_that_do_not_overlap_keep_their_position()
    {
        var placed = LabelLayout.Place(new[] { Box(1, 0, 0), Box(2, 100, 0), Box(3, 0, 50) }, Array.Empty<LayoutBox>());

        Assert.Equal(new ScreenRect(0, 0, 60, 20), placed[1]);
        Assert.Equal(new ScreenRect(100, 0, 160, 20), placed[2]);
        Assert.Equal(new ScreenRect(0, 50, 60, 70), placed[3]);
    }

    [Fact]
    public void Two_overlapping_labels_end_apart_and_move_only_on_y()
    {
        var placed = LabelLayout.Place(new[] { Box(1, 0, 0), Box(2, 10, 5) }, Array.Empty<LayoutBox>());

        AssertNoOverlap(new[] { placed[1], placed[2] });
        Assert.Equal(0f, placed[1].XMin);
        Assert.Equal(10f, placed[2].XMin);
    }

    [Fact]
    public void A_label_takes_the_shorter_move_up()
    {
        // Label 1 covers y 0-20. Label 2 at y 15 moves up 5 px to y 20, not down 35 px to y -20.
        var placed = LabelLayout.Place(new[] { Box(1, 0, 0), Box(2, 0, 15) }, Array.Empty<LayoutBox>());

        Assert.Equal(new ScreenRect(0, 0, 60, 20), placed[1]);
        Assert.Equal(new ScreenRect(0, 20, 60, 40), placed[2]);
    }

    [Fact]
    public void A_label_takes_the_shorter_move_down()
    {
        // The obstacle covers y 10-30. The label at y 0 moves down 10 px to y -10, not up 30 px to y 30.
        var placed = LabelLayout.Place(new[] { Box(1, 0, 0) }, new[] { Box(9, 0, 10) });

        Assert.Equal(new ScreenRect(0, -10, 60, 10), placed[1]);
    }

    [Fact]
    public void A_stack_of_5_labels_at_one_point_ends_with_no_overlap()
    {
        var boxes = Enumerable.Range(1, 5).Select(id => Box(id, 0, 0)).ToArray();

        var placed = LabelLayout.Place(boxes, Array.Empty<LayoutBox>());

        AssertNoOverlap(placed.Values.ToList());
        Assert.All(placed.Values, r => Assert.Equal(0f, r.XMin));
    }

    [Fact]
    public void An_item_label_moves_off_a_fixed_obstacle_and_the_obstacle_stays()
    {
        var obstacle = Box(9, 20, 5);

        var placed = LabelLayout.Place(new[] { Box(1, 0, 0) }, new[] { obstacle });

        Assert.False(placed[1].Overlaps(obstacle.Rect, 0f));
        Assert.False(placed.ContainsKey(9));
    }

    [Fact]
    public void The_same_input_gives_the_same_output_in_any_order()
    {
        var boxes = new[] { Box(1, 0, 0), Box(2, 5, 0), Box(3, 10, 3), Box(4, 0, 8) };
        var obstacles = new[] { Box(9, 30, 10) };

        var first = LabelLayout.Place(boxes, obstacles);
        var again = LabelLayout.Place(boxes, obstacles);
        var shuffled = LabelLayout.Place(boxes.Reverse().ToArray(), obstacles);

        Assert.Equal(first, again);
        Assert.Equal(first, shuffled);
    }

    [Fact]
    public void A_label_at_the_top_edge_moves_down_and_stays_on_the_screen()
    {
        // The screen is 0-100 px high. Both labels cover y 80-100, so the move up to y 100 leaves the screen.
        var placed = LabelLayout.Place(new[] { Box(1, 0, 80), Box(2, 0, 80) }, Array.Empty<LayoutBox>(), 0f, 100f);

        Assert.Equal(new ScreenRect(0, 80, 60, 100), placed[1]);
        Assert.Equal(new ScreenRect(0, 60, 60, 80), placed[2]);
    }

    [Fact]
    public void A_label_at_the_bottom_edge_moves_up_and_stays_on_the_screen()
    {
        // The obstacle covers y 0-20 at the bottom edge. The label at y 5 moves up to y 20, not down below 0.
        var placed = LabelLayout.Place(new[] { Box(1, 0, 5) }, new[] { Box(9, 0, 0) }, 0f, 100f);

        Assert.Equal(new ScreenRect(0, 20, 60, 40), placed[1]);
    }
}
