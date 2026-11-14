using BepInEx.Configuration;
using HarmonyLib;
using ProximityPickup;
using ProximityPickup.Labels;
using ProximityPickup.Pickup;
using Xunit;

public class PluginTests : GameTestBase
{
    private static void LoadAgainWithFailing(Type patchClass)
    {
        FakeState.Reset();
        Harmony.ThrowFor = patchClass;
        new Plugin().Load();
    }

    [Fact]
    public void The_config_defaults_are_group_pickup_at_two_meters_and_group_dig_at_one_meter_with_no_debug_lines()
    {
        Assert.Equal("Group", Plugin.PickModeEntry.Value);
        Assert.Equal(PickMode.Group, Plugin.PickMode);
        Assert.Equal(2f, Plugin.PickupRadius.Value);
        Assert.Equal(1f, Plugin.DigRadius.Value);
        Assert.False(Plugin.Verbose.Value);
    }

    [Fact]
    public void The_pick_mode_accepts_group_and_one()
    {
        var accepted = (AcceptableValueList<string>)Plugin.PickModeEntry.Description.AcceptableValues;
        Assert.Equal(new[] { "Group", "One" }, accepted.AcceptableValues);
    }

    [Fact]
    public void The_pick_mode_comes_from_the_config_entry()
    {
        Plugin.PickModeEntry.Value = "One";
        Assert.Equal(PickMode.One, Plugin.PickMode);
    }

    [Fact]
    public void Each_patch_class_is_patched()
    {
        Assert.Equal(new[] { typeof(LabelPatches), typeof(CaveLabelPatches), typeof(PickupPatches), typeof(DigPatches), typeof(GatherPatches), typeof(ClickPatches) }, Harmony.Patched);
        Assert.Empty(Harmony.Unpatched);
        Assert.Empty(Plugin.Log.Warnings);
        Assert.Contains("Proximity Pickup loaded.", Plugin.Log.Infos);
    }

    [Fact]
    public void A_patch_class_that_throws_is_unpatched_with_one_warning_and_the_other_stays()
    {
        LoadAgainWithFailing(typeof(LabelPatches));

        Assert.Equal(new[] { typeof(CaveLabelPatches), typeof(PickupPatches), typeof(DigPatches), typeof(GatherPatches), typeof(ClickPatches) }, Harmony.Patched);
        Assert.Equal(new[] { Plugin.PluginGuid + ".LabelPatches" }, Harmony.Unpatched);
        Assert.Equal(new[] { "Label groups is off: Target not found" }, Plugin.Log.Warnings);
    }

    [Fact]
    public void The_second_patch_class_that_throws_leaves_the_first_patched()
    {
        LoadAgainWithFailing(typeof(PickupPatches));

        Assert.Equal(new[] { typeof(LabelPatches), typeof(CaveLabelPatches), typeof(DigPatches), typeof(GatherPatches), typeof(ClickPatches) }, Harmony.Patched);
        Assert.Equal(new[] { Plugin.PluginGuid + ".PickupPatches" }, Harmony.Unpatched);
        Assert.Equal(new[] { "Group pickup is off: Target not found" }, Plugin.Log.Warnings);
    }

    [Fact]
    public void A_failed_group_dig_patch_leaves_the_other_parts_on()
    {
        LoadAgainWithFailing(typeof(DigPatches));

        Assert.Equal(new[] { typeof(LabelPatches), typeof(CaveLabelPatches), typeof(PickupPatches), typeof(GatherPatches), typeof(ClickPatches) }, Harmony.Patched);
        Assert.Equal(new[] { "Group dig is off: Target not found" }, Plugin.Log.Warnings);
    }

    [Fact]
    public void A_failed_group_gather_patch_leaves_the_other_parts_on()
    {
        LoadAgainWithFailing(typeof(GatherPatches));

        Assert.Equal(new[] { typeof(LabelPatches), typeof(CaveLabelPatches), typeof(PickupPatches), typeof(DigPatches), typeof(ClickPatches) }, Harmony.Patched);
        Assert.Equal(new[] { "Group gather is off: Target not found" }, Plugin.Log.Warnings);
    }

    [Fact]
    public void A_failed_middle_click_patch_leaves_the_other_parts_on()
    {
        LoadAgainWithFailing(typeof(ClickPatches));

        Assert.Equal(new[] { typeof(LabelPatches), typeof(CaveLabelPatches), typeof(PickupPatches), typeof(DigPatches), typeof(GatherPatches) }, Harmony.Patched);
        Assert.Equal(new[] { "Middle-click is off: Target not found" }, Plugin.Log.Warnings);
    }

    [Fact]
    public void A_failed_cave_labels_patch_leaves_the_other_parts_on()
    {
        LoadAgainWithFailing(typeof(CaveLabelPatches));

        Assert.Equal(new[] { typeof(LabelPatches), typeof(PickupPatches), typeof(DigPatches), typeof(GatherPatches), typeof(ClickPatches) }, Harmony.Patched);
        Assert.Equal(new[] { Plugin.PluginGuid + ".CaveLabelPatches" }, Harmony.Unpatched);
        Assert.Equal(new[] { "Cave labels is off: Target not found" }, Plugin.Log.Warnings);
    }

    [Fact]
    public void A_repeated_error_gives_one_warning()
    {
        Plugin.WarnOnce("Part", new InvalidOperationException("broken"));
        Plugin.WarnOnce("Part", new InvalidOperationException("broken"));

        var warning = Assert.Single(Plugin.Log.Warnings);
        Assert.StartsWith("Part: InvalidOperationException: broken\n", warning);
    }

    [Fact]
    public void A_distinct_error_gives_its_own_warning()
    {
        Plugin.WarnOnce("Part", new InvalidOperationException("broken"));
        Plugin.WarnOnce("Part", new InvalidOperationException("other"));
        Plugin.WarnOnce("Another part", new InvalidOperationException("broken"));

        Assert.Equal(3, Plugin.Log.Warnings.Count);
    }

    [Fact]
    public void A_debug_line_needs_verbose_on()
    {
        Plugin.Debug("hidden");
        Assert.Empty(Plugin.Log.Debugs);

        Plugin.Verbose.Value = true;
        Plugin.Debug("shown");

        Assert.Equal(new[] { "shown" }, Plugin.Log.Debugs);
    }
}
