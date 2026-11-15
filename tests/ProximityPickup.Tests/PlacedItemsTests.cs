using ProximityPickup;
using ProximityPickup.Labels;
using UnityEngine;
using Xunit;

// A placed item (a mold, a trap, a placed log) keeps its own label and stays out of each group pickup.
public class PlacedItemsTests : GameTestBase
{
    private const string Mold = "clay_ingot_mold", MoldName = "Clay Ingot Mold";

    private readonly LabelScene scene = new LabelScene();

    private static Vector3 At(float x, float y) => new Vector3(x, y, 0f);

    private static Vector3 World(float x) => new Vector3(x, 0f, 0f);

    private Entity PlacedMold(int id, float x, float screenY)
    {
        var mold = LabelScene.NewPlaced(id, Mold, MoldName, World(x));
        scene.AddLabel(mold, At(300f, screenY), MoldName);
        return mold;
    }

    private Entity DroppedMold(int id, float x, float screenY)
    {
        var mold = LabelScene.NewItem(id, Mold, MoldName, World(x));
        scene.AddLabel(mold, At(300f, screenY), MoldName);
        return mold;
    }

    // Labels

    [Fact]
    public void Placed_molds_side_by_side_keep_their_own_labels()
    {
        PlacedMold(10, 1f, 300f);
        PlacedMold(11, 1.3f, 400f);
        PlacedMold(12, 1.6f, 500f);

        scene.Frame();

        foreach (int id in new[] { 10, 11, 12 })
        {
            Assert.Equal(MoldName, scene.Labels[id].text.text);
            Assert.True(scene.Labels[id].transform.position.y > 0f);
            Assert.False(LabelPatches.TryGetLabelMembers(id, out _));
        }
    }

    [Fact]
    public void A_dropped_mold_beside_a_placed_mold_keeps_its_own_label()
    {
        DroppedMold(10, 1f, 300f);
        PlacedMold(11, 1.2f, 400f);

        scene.Frame();

        Assert.Equal(MoldName, scene.Labels[10].text.text);
        Assert.Equal(MoldName, scene.Labels[11].text.text);
        Assert.True(scene.Labels[11].transform.position.y > 0f);
    }

    [Fact]
    public void Dropped_molds_still_merge_into_a_label_group()
    {
        DroppedMold(10, 1f, 300f);
        DroppedMold(11, 1.2f, 400f);
        DroppedMold(12, 1.4f, 500f);

        scene.Frame();

        Assert.Equal(MoldName + " (3)", scene.Labels[10].text.text);
    }

    [Fact]
    public void The_hover_card_of_a_placed_mold_shows_no_take_one_line()
    {
        var panel = LabelScene.NewHintPanel();
        var mold = PlacedMold(10, 1f, 300f);
        PlacedMold(11, 1.2f, 400f);
        scene.Frame();
        var card = scene.OpenCard(mold, At(320f, 340f), MoldName);
        LabelScene.AddRow(card, "Act");
        PatchCall.Run(typeof(LabelPatches), "MouseEnterPostfix", new object[] { mold });

        scene.Frame();

        Assert.False(panel.controls2.gameObject.activeSelf);
    }

    [Fact]
    public void The_hover_card_of_a_placed_mold_with_the_labels_off_keeps_its_game_title()
    {
        var mold = scene.WithCollider(LabelScene.NewPlaced(10, Mold, MoldName, World(1f)));
        scene.WithCollider(LabelScene.NewPlaced(11, Mold, MoldName, World(1.5f)));
        var card = scene.OpenCard(mold, At(320f, 340f), MoldName);

        scene.Frame();

        Assert.Equal(MoldName, card.labelText.text);
    }
    // Pickups

    private static object Plan => StaticState.Get<object>(typeof(ProximityPickup.Pickup.PickupPatches), "current");

    [Fact]
    public void A_click_on_a_placed_mold_makes_no_plan_and_logs_why()
    {
        Plugin.Verbose.Value = true;
        var player = LabelScene.NewEntity(1);
        LabelPatches.LocalPlayerEntity = player;

        PatchCall.Run(typeof(ProximityPickup.Pickup.PickupPatches), "StartInteractionPrefix", InteractionType.PICKUP, player, LabelScene.NewPlaced(10, Mold, MoldName, World(1f)));

        Assert.Null(Plan);
        Assert.Contains("Pickup plan: target 10 is no ground item, no plan", Plugin.Log.Debugs);
    }

    [Fact]
    public void A_radius_pickup_of_a_dropped_mold_skips_the_placed_molds()
    {
        var dropped = scene.WithCollider(LabelScene.NewItem(10, Mold, MoldName, World(1f)));
        scene.WithCollider(LabelScene.NewPlaced(11, Mold, MoldName, World(1.3f)));
        scene.WithCollider(LabelScene.NewPlaced(12, Mold, MoldName, World(1.6f)));
        var near = scene.WithCollider(LabelScene.NewItem(13, Mold, MoldName, World(1.8f)));

        var extras = ProximityPickup.Pickup.PickupPatches.FindWithinRadius(scene.EntityManager, dropped, Mold, World(1f), out _);

        Assert.Equal(new[] { near }, extras);
    }

    [Fact]
    public void A_placed_member_of_a_label_pickup_is_not_picked()
    {
        var player = LabelScene.NewEntity(1);
        var target = LabelScene.NewItem(10, Mold, MoldName, World(1f));
        var effect = new PickupEffect { Interactor = player, Target = target, entityManager = scene.EntityManager };
        var args = new object[] { effect, player, target, new List<Entity> { LabelScene.NewPlaced(11, Mold, MoldName, World(1.2f)) }, 0 };

        int picked = (int)PatchCall.Run(typeof(ProximityPickup.Pickup.PickupPatches), "PickExtras", args);

        Assert.Equal(0, picked);
    }

    // The log and a failed read

    private static string PlacedLine(int id) => $"Placed item {id} ({Mold}): own label, no group pickup";

    [Fact]
    public void Each_placed_item_is_logged_once()
    {
        Plugin.Verbose.Value = true;
        PlacedMold(10, 1f, 300f);
        PlacedMold(11, 1.3f, 400f);

        scene.Frame();
        scene.Frame();

        Assert.Single(Plugin.Log.Debugs, PlacedLine(10));
        Assert.Single(Plugin.Log.Debugs, PlacedLine(11));
    }

    [Fact]
    public void A_placed_item_is_logged_again_after_the_ids_clear()
    {
        Plugin.Verbose.Value = true;
        var mold = LabelScene.NewPlaced(10, Mold, MoldName, World(1f));
        PlacedItems.IsPlaced(mold);
        for (int id = 1000; id <= 1000 + 4096; id++) PlacedItems.IsPlaced(LabelScene.NewPlaced(id, Mold, MoldName, World(1f)));

        PlacedItems.IsPlaced(mold);

        Assert.Equal(2, Plugin.Log.Debugs.Count(line => line == PlacedLine(10)));
    }

    [Fact]
    public void A_failed_read_warns_once_and_the_other_labels_still_merge()
    {
        var broken = PlacedMold(10, 1f, 300f);
        broken.ThrowOn = typeof(IPlacedItem);
        scene.AddLabel(LabelScene.NewItem(20, "stick", "Stick", World(5f)), At(600f, 300f), "Stick");
        scene.AddLabel(LabelScene.NewItem(21, "stick", "Stick", World(5.2f)), At(600f, 400f), "Stick");

        scene.Frame();

        Assert.False(PlacedItems.IsPlaced(broken));
        Assert.Single(Plugin.Log.Warnings, line => line.StartsWith("Placed items: InvalidOperationException"));
        Assert.Equal("Stick (2)", scene.Labels[20].text.text);
    }
}
