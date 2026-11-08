using ProximityPickup;
using Xunit;

public class PickModeTests
{
    [Theory]
    [InlineData("Group", PickMode.Group)]
    [InlineData("One", PickMode.One)]
    [InlineData("one", PickMode.One)]
    [InlineData("Everything", PickMode.Group)]
    [InlineData("", PickMode.Group)]
    [InlineData(null, PickMode.Group)]
    public void Parse_reads_the_config_value_and_falls_back_to_Group(string value, PickMode expected)
    {
        Assert.Equal(expected, PickModes.Parse(value));
    }
}
