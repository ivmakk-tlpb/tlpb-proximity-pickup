using ProximityPickup;
using Xunit;

public class HoverFreezeTests
{
    private static Dictionary<int, float> Moves(params (int id, float move)[] moves)
    {
        var result = new Dictionary<int, float>();
        foreach (var (id, move) in moves) result[id] = move;
        return result;
    }

    private static HoverFreeze Started()
    {
        var freeze = new HoverFreeze();
        freeze.LabelSeen(5, 10);
        freeze.CardOpen(5, 11, Moves((5, 8f), (6, 20f)));
        return freeze;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    public void A_card_whose_label_was_seen_within_2_frames_starts_a_freeze(int frame)
    {
        var freeze = new HoverFreeze();
        freeze.LabelSeen(5, 10);

        Assert.True(freeze.CardOpen(5, frame, Moves((6, 20f))));

        Assert.True(freeze.Active);
        Assert.True(freeze.TryGetMove(6, out float move));
        Assert.Equal(20f, move);
    }

    [Fact]
    public void A_card_with_an_old_label_is_ignored()
    {
        var freeze = new HoverFreeze();
        freeze.LabelSeen(5, 10);

        Assert.False(freeze.CardOpen(5, 13, Moves((6, 20f))));

        Assert.False(freeze.Active);
        Assert.False(freeze.TryGetMove(6, out _));
    }

    [Fact]
    public void A_card_with_no_label_is_ignored()
    {
        var freeze = new HoverFreeze();

        Assert.False(freeze.CardOpen(5, 10, Moves((6, 20f))));

        Assert.False(freeze.Active);
    }

    [Fact]
    public void The_snapshot_stays_during_later_frames()
    {
        var freeze = Started();

        Assert.True(freeze.CardOpen(5, 50, Moves((6, 99f))));

        Assert.True(freeze.TryGetMove(6, out float move));
        Assert.Equal(20f, move);
    }

    [Fact]
    public void A_snapshot_with_no_moves_is_not_taken_again()
    {
        var freeze = new HoverFreeze();
        freeze.LabelSeen(5, 10);
        freeze.CardOpen(5, 11, Moves());

        freeze.CardOpen(5, 12, Moves((6, 20f)));

        Assert.False(freeze.TryGetMove(6, out _));
    }

    [Fact]
    public void A_label_not_in_the_snapshot_has_no_kept_move()
    {
        Assert.False(Started().TryGetMove(77, out _));
    }

    [Fact]
    public void The_freeze_stays_while_the_holding_card_is_open()
    {
        var freeze = Started();

        Assert.False(freeze.EndFrame(new HashSet<int> { 5 }, labelHovered: false));

        Assert.True(freeze.Active);
    }

    [Fact]
    public void The_freeze_stays_while_a_label_is_hovered()
    {
        var freeze = Started();

        Assert.False(freeze.EndFrame(new HashSet<int>(), labelHovered: true));

        Assert.True(freeze.Active);
    }

    [Fact]
    public void The_freeze_ends_when_no_holding_card_is_open_and_no_label_is_hovered()
    {
        var freeze = Started();

        Assert.True(freeze.EndFrame(new HashSet<int>(), labelHovered: false));

        Assert.False(freeze.Active);
        Assert.False(freeze.TryGetMove(6, out _));
    }

    [Fact]
    public void An_open_card_that_does_not_hold_the_freeze_does_not_keep_it()
    {
        var freeze = Started();
        freeze.CardOpen(7, 50, Moves());

        Assert.True(freeze.EndFrame(new HashSet<int> { 7 }, labelHovered: false));
    }

    [Fact]
    public void A_next_hover_takes_a_new_snapshot()
    {
        var freeze = Started();
        freeze.EndFrame(new HashSet<int>(), labelHovered: false);
        freeze.LabelSeen(5, 60);

        freeze.CardOpen(5, 61, Moves((6, 33f)));

        Assert.True(freeze.TryGetMove(6, out float move));
        Assert.Equal(33f, move);
    }
}
