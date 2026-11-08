using ProximityPickup;
using Xunit;

public class HoverGeometryTests
{
    private static readonly ScreenRect Label = new ScreenRect(470, 296, 550, 316);
    private static readonly ScreenPoint LabelText = new ScreenPoint(512, 305);
    private static readonly ScreenPoint CardGame = new ScreenPoint(500, 200);
    private static readonly ScreenPoint Title = new ScreenPoint(1, -4);

    [Fact]
    public void The_shift_places_the_title_center_on_the_label_text_center()
    {
        var anchor = HoverGeometry.Anchor(LabelText, CardGame);

        var shift = HoverGeometry.CardShift(anchor, Title);

        Assert.Equal(512f, CardGame.X + Title.X + shift.X);
        Assert.Equal(305f, CardGame.Y + Title.Y + shift.Y);
    }

    [Fact]
    public void The_title_follows_the_game_position_of_the_card()
    {
        var anchor = HoverGeometry.Anchor(LabelText, CardGame);

        var shift = HoverGeometry.CardShift(anchor, Title);

        var moved = new ScreenPoint(530, 180);
        Assert.Equal(542f, moved.X + Title.X + shift.X);
        Assert.Equal(285f, moved.Y + Title.Y + shift.Y);
    }

    [Fact]
    public void The_box_after_the_shift_has_the_rectangle_of_the_label()
    {
        var anchor = HoverGeometry.Anchor(LabelText, CardGame);
        var shift = HoverGeometry.CardShift(anchor, Title);

        var box = HoverGeometry.CardBox(Label, LabelText, CardGame, Title);

        Assert.Equal(Label, new ScreenRect(box.XMin + shift.X, box.YMin + shift.Y, box.XMax + shift.X, box.YMax + shift.Y));
    }

    [Fact]
    public void Touching_labels_in_a_stack_do_not_share_a_strip_that_catches_the_mouse()
    {
        var lower = HoverGeometry.LabelBox(new ScreenRect(100, 50, 200, 70));
        var upper = HoverGeometry.LabelBox(new ScreenRect(100, 70, 200, 90));

        Assert.Equal(70f, lower.YMax - HoverGeometry.EdgeCatchInset);
        Assert.Equal(70f, upper.YMin + HoverGeometry.EdgeCatchInset);
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.6f, 0.6f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(float.NaN, 0f)]
    public void The_progress_fill_width_stays_between_0_and_1(float progress, float expected)
    {
        Assert.Equal(expected, HoverGeometry.FillWidth(progress));
    }

    [Fact]
    public void The_label_box_extends_6_pixels_at_the_sides_and_3_at_the_top_and_the_bottom()
    {
        Assert.Equal(new ScreenRect(94, 47, 206, 73), HoverGeometry.LabelBox(new ScreenRect(100, 50, 200, 70)));
    }
}
