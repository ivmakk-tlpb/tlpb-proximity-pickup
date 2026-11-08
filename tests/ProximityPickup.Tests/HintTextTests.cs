using ProximityPickup;
using Xunit;

public class HintTextTests
{
    [Fact]
    public void First_binding_is_the_part_before_the_word_or()
    {
        Assert.Equal("Ctrl", HintText.FirstBinding("Ctrl or Ctrl", "or"));
    }

    [Fact]
    public void A_name_with_one_binding_stays_the_same()
    {
        Assert.Equal("E", HintText.FirstBinding("E", "or"));
    }

    [Fact]
    public void Tags_around_the_word_are_cut_with_it()
    {
        Assert.Equal("<color=#fff>A</color>", HintText.FirstBinding("<color=#fff>A</color> <color=#aaa>or</color> <color=#fff>B</color>", "or"));
    }

    [Fact]
    public void The_letters_or_inside_a_sprite_tag_do_not_cut_the_name()
    {
        Assert.Equal("<sprite name=\"door\">", HintText.FirstBinding("<sprite name=\"door\">", "or"));
        Assert.Equal("<sprite name=\"key or mouse\">", HintText.FirstBinding("<sprite name=\"key or mouse\"> or <sprite name=\"b\">", "or"));
    }

    private static bool Defined(int value) => value == 3 || value == 7;

    [Fact]
    public void No_modifier_when_the_flag_is_zero_whatever_the_value_is()
    {
        Assert.True(HintText.DecodeModifier(0, 12345, Defined, out int? modifier));
        Assert.Null(modifier);
    }

    [Fact]
    public void A_set_flag_with_a_defined_action_gives_the_modifier()
    {
        Assert.True(HintText.DecodeModifier(1, 7, Defined, out int? modifier));
        Assert.Equal(7, modifier);
    }

    [Theory]
    [InlineData(1, 12345)]
    [InlineData(2, 7)]
    [InlineData(255, 3)]
    public void A_bad_read_gives_no_modifier_and_reports_it(int flag, int value)
    {
        Assert.False(HintText.DecodeModifier(flag, value, Defined, out int? modifier));
        Assert.Null(modifier);
    }

    [Fact]
    public void A_line_joins_the_modifier_the_key_the_color_and_the_action()
    {
        Assert.Equal("<b>Ctrl + LMB</b> <color=#A0FF20>Take apart</color>", HintText.Line("Ctrl", "LMB", "A0FF20", "Take apart"));
    }

    [Fact]
    public void A_line_with_no_modifier_shows_only_the_key()
    {
        Assert.Equal("<b>LMB</b> <color=#FFFFFF>Pick up</color>", HintText.Line(null, "LMB", "FFFFFF", "Pick up"));
    }

    [Fact]
    public void A_line_with_no_key_shows_only_the_action()
    {
        Assert.Equal("<color=#FFFFFF>Inspect</color>", HintText.Line(null, "", "FFFFFF", "Inspect"));
    }

    [Fact]
    public void The_word_comes_from_the_game_language()
    {
        Assert.Equal("A", HintText.FirstBinding("A oder B", "oder"));
        Assert.Equal("A or B", HintText.FirstBinding("A or B", "oder"));
    }
}
