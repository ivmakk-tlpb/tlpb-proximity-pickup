using ProximityPickup;
using ProximityPickup.Labels;
using ProximityPickup.Pickup;
using UnityEngine;
using Xunit;
using Result = DigTerrainEffect.DigTerrainEffectResult;

public class DigPatchesTests : GameTestBase
{
    private const int Clay = 7, Mud = 8;
    private const int SpotsAtTwoMeters = 69;

    private readonly Entity player = LabelScene.NewEntity(1);
    private readonly World world = new World();
    private readonly TerrainGatherInteraction interaction = new TerrainGatherInteraction { id = 40 };
    private readonly DigTerrainEffect effect;
    private readonly Vector3 clicked = new Vector3(10f, 5f, 20f);

    public DigPatchesTests()
    {
        LabelPatches.LocalPlayerEntity = player;
        player.world = world;
        world.specialGatherSurfaceTypes.Add(SurfaceType.CLAY);
        interaction.targetPosition = clicked;
        interaction.targetSurfaceType = SurfaceType.CLAY;
        effect = new DigTerrainEffect { Interactor = player, Interaction = interaction };
        effect.SetParams(clicked, SurfaceType.CLAY);
        effect.OnExecute = Yield(Clay, 1);
    }

    private static System.Func<Vector3, SurfaceType, Result> Yield(int item, int count)
    {
        return (_, _) => new Result { success = true, itemCounts = NativeCounts.Make((item, count)) };
    }

    private static Result Dug(int item = Clay, int count = 1)
    {
        return new Result { success = true, itemCounts = NativeCounts.Make((item, count)) };
    }

    private Result Run(Result? first = null)
    {
        var args = new object[] { effect, first ?? Dug() };
        PatchCall.Run(typeof(DigPatches), "ExecuteResultPostfix", args);
        return (Result)args[1];
    }

    private void AssertVanilla()
    {
        var result = Run();
        Assert.Empty(effect.Executed);
        Assert.Equal(new[] { (Clay, 1) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void A_hand_dig_also_runs_the_game_dig_for_each_other_spot_of_the_radius()
    {
        Run();

        Assert.Equal(SpotsAtTwoMeters - 1, effect.Executed.Count);
        Assert.DoesNotContain(effect.Executed, dig => dig.Position == clicked);
        Assert.All(effect.Executed, dig => Assert.Equal(5f, dig.Position.y));
    }

    [Fact]
    public void Only_spots_with_the_surface_of_the_clicked_spot_are_dug()
    {
        world.specialGatherSurfaceTypes.Add(SurfaceType.MUD);
        world.Surface = position => position.x > 10.1f ? SurfaceType.MUD : SurfaceType.CLAY;

        Run();

        Assert.All(effect.Executed, dig => Assert.Equal(SurfaceType.CLAY, dig.Surface));
        Assert.All(effect.Executed, dig => Assert.True(dig.Position.x <= 10.1f));
        Assert.InRange(effect.Executed.Count, 1, SpotsAtTwoMeters - 2);
    }

    [Fact]
    public void Spots_with_no_drops_are_skipped()
    {
        world.HasDrops = position => position.x <= 10f;

        Run();

        Assert.All(effect.Executed, dig => Assert.True(dig.Position.x <= 10f));
        Assert.InRange(effect.Executed.Count, 1, SpotsAtTwoMeters - 2);
    }

    [Fact]
    public void A_solid_object_skips_a_spot_on_a_normal_surface()
    {
        interaction.targetSurfaceType = SurfaceType.MUD;
        effect.SetParams(clicked, SurfaceType.MUD);
        world.Surface = _ => SurfaceType.MUD;
        world.Solid = position => position.z > 20f;

        Run();

        Assert.All(effect.Executed, dig => Assert.True(dig.Position.z <= 20f));
        Assert.Contains(effect.Executed, dig => dig.Position.z < 20f);
    }

    [Fact]
    public void The_message_of_the_game_gets_the_sum_of_the_digs()
    {
        var result = Run();

        Assert.Equal(new[] { (Clay, SpotsAtTwoMeters) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void Another_item_kind_gets_its_own_entry_in_the_message()
    {
        int count = 0;
        effect.OnExecute = (_, _) => Dug(++count == 1 ? Mud : Clay);

        var result = Run();

        Assert.Equal(new[] { (Clay, SpotsAtTwoMeters - 1), (Mud, 1) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void An_item_kind_with_no_room_in_the_list_is_left_out_of_the_message_with_no_error()
    {
        effect.OnExecute = Yield(Mud, 1);

        var result = Run(new Result { success = true, itemCounts = NativeCounts.Make(1, (Clay, 1)) });

        Assert.Equal(new[] { (Clay, 1) }, NativeCounts.Read(result.itemCounts));
        Assert.Empty(Plugin.Log.Warnings);
    }

    [Fact]
    public void A_clicked_dig_with_an_empty_list_takes_the_list_of_the_first_dig_with_room()
    {
        var result = Run(new Result { success = true, itemCounts = NativeCounts.Make(0) });

        Assert.Equal(new[] { (Clay, SpotsAtTwoMeters - 1) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void A_dig_that_the_game_refuses_adds_nothing()
    {
        int count = 0;
        effect.OnExecute = (_, _) => ++count == 1 ? new Result { success = false } : Dug();

        var result = Run();

        Assert.Equal(new[] { (Clay, SpotsAtTwoMeters - 1) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void The_digs_stop_at_the_first_dig_that_does_not_fit_and_the_game_shows_its_message()
    {
        int count = 0;
        effect.OnExecute = (_, _) => new Result { success = true, insufficientSpace = ++count == 3, itemCounts = NativeCounts.Make((Clay, 1)) };

        var result = Run();

        Assert.Equal(3, effect.Executed.Count);
        Assert.True(Assert.Single(effect.Finished).insufficientSpace);
        Assert.Equal(new[] { (Clay, 3) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void The_effect_gets_the_clicked_spot_back_after_the_digs()
    {
        Run();

        Assert.Equal((clicked, SurfaceType.CLAY), effect.ReadParams());
    }

    [Fact]
    public void A_clicked_dig_that_failed_stays_as_in_the_game()
    {
        var result = Run(new Result { success = false, itemCounts = NativeCounts.Make() });

        Assert.Empty(effect.Executed);
        Assert.False(result.success);
    }

    [Fact]
    public void A_clicked_dig_that_did_not_fit_digs_no_other_spot_and_ends_the_action()
    {
        Run(new Result { success = true, insufficientSpace = true, itemCounts = NativeCounts.Make() });

        PatchCall.Run(typeof(DigPatches), "InteractionUpdatePostfix", interaction);

        Assert.Empty(effect.Executed);
        Assert.Equal(1, interaction.Ends);
    }

    [Fact]
    public void A_native_layout_that_does_not_match_the_action_turns_the_group_dig_off_with_one_warning()
    {
        effect.SetParams(new Vector3(1f, 2f, 3f), SurfaceType.CLAY);

        AssertVanilla();

        Assert.Contains("Group dig", Assert.Single(Plugin.Log.Warnings));
        Assert.Equal((new Vector3(1f, 2f, 3f), SurfaceType.CLAY), effect.ReadParams());
    }

    [Fact]
    public void Another_character_digs_as_in_the_game()
    {
        effect.Interactor = LabelScene.NewEntity(2);
        effect.Interactor.world = world;
        AssertVanilla();
    }

    [Fact]
    public void A_client_digs_as_in_the_game()
    {
        player.isHostEntity = false;
        AssertVanilla();
    }

    [Fact]
    public void The_pick_mode_one_digs_as_in_the_game()
    {
        Plugin.PickModeEntry.Value = "One";
        AssertVanilla();
    }

    [Fact]
    public void A_radius_of_zero_digs_as_in_the_game()
    {
        Plugin.PickupRadius.Value = 0f;
        AssertVanilla();
    }

    [Fact]
    public void A_shovel_dig_stays_as_in_the_game()
    {
        effect.Interaction = new IInteraction();
        AssertVanilla();
    }

    [Theory]
    [InlineData(SurfaceType.DIRT)]
    [InlineData(SurfaceType.TILLED_SOIL)]
    [InlineData(SurfaceType.WET_TILLED_SOIL)]
    public void A_dig_on_dirt_stays_as_in_the_game_and_the_action_goes_on(SurfaceType dirt)
    {
        interaction.targetSurfaceType = dirt;
        effect.SetParams(clicked, dirt);

        AssertVanilla();
        PatchCall.Run(typeof(DigPatches), "InteractionUpdatePostfix", interaction);

        Assert.Equal(0, interaction.Ends);
    }

    [Fact]
    public void A_game_dig_of_the_group_dig_does_not_start_another_group_dig()
    {
        effect.OnExecute = (_, _) =>
        {
            Run();
            return Dug();
        };

        Run();

        Assert.Equal(SpotsAtTwoMeters - 1, effect.Executed.Count);
    }

    [Fact]
    public void An_error_keeps_the_yield_restores_the_clicked_spot_and_gives_one_warning()
    {
        int count = 0;
        effect.OnExecute = (_, _) =>
        {
            if (++count == 3) throw new InvalidOperationException("broken");
            return Dug();
        };

        var result = Run();

        Assert.Equal(new[] { (Clay, 3) }, NativeCounts.Read(result.itemCounts));
        Assert.Equal((clicked, SurfaceType.CLAY), effect.ReadParams());
        Assert.Contains("Group dig", Assert.Single(Plugin.Log.Warnings));
    }

    [Fact]
    public void The_verbose_line_gives_the_spots_digs_and_sum()
    {
        Plugin.Verbose.Value = true;

        Run();

        Assert.Contains(Plugin.Log.Debugs, line => line.Contains($"Group dig: {SpotsAtTwoMeters} spots, {SpotsAtTwoMeters} digs, sum {SpotsAtTwoMeters}"));
    }

    [Fact]
    public void The_verbose_line_gives_each_guard_of_a_dig_that_stays_as_in_the_game()
    {
        Plugin.Verbose.Value = true;
        player.isHostEntity = false;

        Run();

        Assert.Contains(Plugin.Log.Debugs, line => line.Contains("Dig effect: hand dig True, local True, host False"));
    }

    [Fact]
    public void A_later_dig_of_the_same_action_stays_as_in_the_game()
    {
        Run();
        effect.Executed.Clear();

        AssertVanilla();
    }

    [Fact]
    public void A_new_action_is_a_new_group_dig()
    {
        Run();
        effect.Interaction = new TerrainGatherInteraction { id = 41, targetPosition = clicked, targetSurfaceType = SurfaceType.CLAY };
        effect.Executed.Clear();

        Run();

        Assert.Equal(SpotsAtTwoMeters - 1, effect.Executed.Count);
    }

    private static void Update(TerrainGatherInteraction current)
    {
        PatchCall.Run(typeof(DigPatches), "InteractionUpdatePostfix", current);
    }

    [Fact]
    public void The_action_ends_once_on_the_next_update_after_its_group_dig()
    {
        Run();

        Update(interaction);
        Update(interaction);

        Assert.Equal(1, interaction.Ends);
    }

    [Fact]
    public void Another_action_does_not_end()
    {
        Run();
        var other = new TerrainGatherInteraction { id = 41 };

        Update(other);

        Assert.Equal(0, other.Ends);
    }

    [Fact]
    public void An_action_with_no_group_dig_does_not_end()
    {
        Update(interaction);

        Assert.Equal(0, interaction.Ends);
    }

    private static void Teardown(TerrainGatherInteraction current)
    {
        PatchCall.Run(typeof(DigPatches), "TeardownPostfix", current);
    }

    [Fact]
    public void An_action_that_the_game_ended_does_not_end_again()
    {
        Run();
        Teardown(interaction);

        Update(interaction);

        Assert.Equal(0, interaction.Ends);
    }

    [Fact]
    public void A_later_action_with_the_id_of_an_ended_action_is_a_new_group_dig()
    {
        Run();
        Teardown(interaction);
        effect.Executed.Clear();

        Run();

        Assert.Equal(SpotsAtTwoMeters - 1, effect.Executed.Count);
    }

    [Fact]
    public void The_teardown_of_another_action_keeps_the_end_of_the_group_dig()
    {
        Run();

        Teardown(new TerrainGatherInteraction { id = 41 });
        Update(interaction);

        Assert.Equal(1, interaction.Ends);
    }

    [Fact]
    public void An_error_in_the_teardown_gives_one_warning()
    {
        Teardown(null);

        Assert.Contains("Group dig teardown", Assert.Single(Plugin.Log.Warnings));
    }

    [Fact]
    public void A_radius_that_holds_only_the_clicked_spot_keeps_the_repeating_action()
    {
        Plugin.PickupRadius.Value = 0.3f;

        AssertVanilla();
        Update(interaction);

        Assert.Equal(0, interaction.Ends);
    }

    [Fact]
    public void An_error_in_the_end_of_the_action_gives_one_warning()
    {
        Run();

        Update(null);

        Assert.Contains("Group dig end", Assert.Single(Plugin.Log.Warnings));
    }
}
