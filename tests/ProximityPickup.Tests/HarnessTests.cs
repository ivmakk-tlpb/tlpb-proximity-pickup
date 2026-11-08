using HarmonyLib;
using ProximityPickup;
using ProximityPickup.Labels;
using ProximityPickup.Pickup;
using UnityEngine;
using Xunit;

public class HarnessTests : GameTestBase
{
    private static int Twice(int value) => value * 2;

    private static void Increment(ref int value) => value++;

    private static void Fail() => throw new InvalidOperationException("the original error");

    [Fact]
    public void A_private_method_runs_and_gives_its_result()
    {
        Assert.Equal(6, PatchCall.Run(typeof(HarnessTests), "Twice", 3));
    }

    [Fact]
    public void A_ref_argument_comes_back_in_the_argument_array()
    {
        var args = new object[] { 1 };
        PatchCall.Run(typeof(HarnessTests), "Increment", args);
        Assert.Equal(2, args[0]);
    }

    [Fact]
    public void A_missing_method_is_named_in_the_error()
    {
        var e = Assert.ThrowsAny<Exception>(() => PatchCall.Run(typeof(HarnessTests), "NoSuchPatch"));
        Assert.Contains("NoSuchPatch", e.Message);
    }

    [Fact]
    public void An_exception_of_the_method_comes_through_unwrapped()
    {
        var e = Assert.Throws<InvalidOperationException>(() => PatchCall.Run(typeof(HarnessTests), "Fail"));
        Assert.Equal("the original error", e.Message);
    }

    [Fact]
    public void A_class_of_the_base_class_runs_in_the_game_state_collection()
    {
        Assert.Equal(GameState.Name, TestContext.Current.TestCollection.TestCollectionDisplayName);
    }

    [Fact]
    public void The_base_class_loads_the_plugin()
    {
        Assert.NotNull(Plugin.Log);
        Assert.False(Plugin.Verbose.Value);
    }

    [Fact]
    public void A_reset_empties_a_changed_static_collection()
    {
        StaticState.Get<HashSet<int>>(typeof(LabelPatches), "everLabeled").Add(5);
        StaticState.Reset();
        Assert.Empty(StaticState.Get<HashSet<int>>(typeof(LabelPatches), "everLabeled"));
    }

    [Fact]
    public void A_reset_sets_a_static_field_back_to_its_value_at_class_load()
    {
        StaticState.Set(typeof(LabelPatches), "lastProgressStep", 5);
        StaticState.Reset();
        Assert.Equal(-1, StaticState.Get<int>(typeof(LabelPatches), "lastProgressStep"));
    }

    [Fact]
    public void A_reset_closes_an_open_freeze_and_forgets_its_moves()
    {
        var freeze = StaticState.Get<HoverFreeze>(typeof(LabelPatches), "freeze");
        freeze.LabelSeen(1, 10);
        Assert.True(freeze.CardOpen(1, 10, new Dictionary<int, float> { [7] = 3f }));
        Assert.True(freeze.Active);

        StaticState.Reset();

        Assert.False(freeze.Active);
        Assert.False(freeze.TryGetMove(7, out _));
    }

    [Fact]
    public void The_accessor_reads_a_member_of_a_private_static_object()
    {
        var player = LabelScene.NewEntity(1);
        var item = LabelScene.NewItem(2, "stick", "Stick", Vector3.zero);
        LabelPatches.LocalPlayerEntity = player;

        PatchCall.Run(typeof(PickupPatches), "StartInteractionPrefix", InteractionType.PICKUP, player, item);

        var plan = StaticState.Get<object>(typeof(PickupPatches), "current");
        Assert.Equal(PickupSource.Radius, StaticState.Member<PickupSource>(plan, "Source"));
    }

    [Fact]
    public void A_fake_state_reset_turns_the_harmony_throw_switch_off()
    {
        Harmony.ThrowFor = typeof(LabelPatches);
        FakeState.Reset();
        new Harmony("id").PatchAll(typeof(LabelPatches));
        Assert.Contains(typeof(LabelPatches), Harmony.Patched);
    }

    [Fact]
    public void One_scene_frame_gives_the_members_of_a_label()
    {
        var scene = new LabelScene();
        var a = scene.Item(10, "stick", new Vector3(0f, 0f, 0f), new Vector3(100f, 100f, 0f));
        var b = scene.Item(11, "stick", new Vector3(1f, 0f, 0f), new Vector3(100f, 100f, 0f));

        scene.Frame();

        Assert.Empty(Plugin.Log.Warnings);
        Assert.True(LabelPatches.TryGetLabelMembers(10, out var members));
        Assert.Equal(new[] { a, b }, members);
    }
}
