using System.Collections;
using ProximityPickup;
using ProximityPickup.Labels;
using UnityEngine;
using Xunit;

public class LabelFlowTests : GameTestBase
{
    private const float HiddenY = -10000f;

    private readonly LabelScene scene = new LabelScene();

    private static Vector3 At(float x, float y) => new Vector3(x, y, 0f);

    private static Vector3 World(float x) => new Vector3(x, 0f, 0f);

    private static HashSet<int> Frozen => StaticState.Get<HashSet<int>>(typeof(LabelPatches), "frozen");

    private static HoverFreeze Freeze => StaticState.Get<HoverFreeze>(typeof(LabelPatches), "freeze");

    private static object CardStates => StaticState.Get<object>(typeof(LabelPatches), "cardStates");

    private static void Enter(Entity entity) => PatchCall.Run(typeof(LabelPatches), "MouseEnterPostfix", new object[] { entity });

    private static void Exit(Entity entity) => PatchCall.Run(typeof(LabelPatches), "MouseExitPostfix", new object[] { entity });

    private Vector3 PositionOf(int id) => scene.Labels[id].transform.position;

    private static object DataOf(Entity entity) => PatchCall.Run(typeof(LabelPatches), "DataOf", entity);

    private static object GeometryOf(Component component, TMPro.TextMeshProUGUI text)
    {
        return PatchCall.Run(typeof(LabelPatches), "GeometryOf", component, text);
    }

    private static Vector2 TextSize(object geometry, string text)
    {
        return (Vector2)PatchCall.Run(typeof(LabelPatches), "TextSize", geometry, text);
    }

    // Fills a private dictionary up to its size limit with placeholder entries.
    private static IDictionary Fill(string field, int count, Func<int, object> key)
    {
        var map = StaticState.Get<IDictionary>(typeof(LabelPatches), field);
        var valueType = map.GetType().GetGenericArguments()[1];
        object value = valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
        for (int i = 0; i < count; i++) map[key(i)] = value;
        return map;
    }

    // Run, groups

    [Fact]
    public void Three_close_sticks_show_one_label_with_the_sum()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "stick", World(1.2f), At(100f, 130f), quantity: 2);

        scene.Frame();

        Assert.Equal("Stick (3)", scene.Labels[10].text.text);
        Assert.Equal(At(100f, 100f), PositionOf(10));
    }

    [Fact]
    public void The_hidden_members_go_below_the_screen_and_keep_their_x()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "stick", World(1.2f), At(150f, 130f));
        scene.Item(12, "stick", World(1.4f), At(170f, 150f));

        scene.Frame();

        Assert.Equal(new Vector3(150f, HiddenY, 0f), PositionOf(11));
        Assert.Equal(new Vector3(170f, HiddenY, 0f), PositionOf(12));
        Assert.Equal("Stick (3)", scene.Labels[10].text.text);
    }

    [Fact]
    public void The_nearest_member_shows_the_label()
    {
        scene.Item(10, "stick", World(1.5f), At(100f, 100f));
        scene.Item(11, "stick", World(1f), At(150f, 130f));

        scene.Frame();

        Assert.Equal("Stick (2)", scene.Labels[11].text.text);
        Assert.Equal(HiddenY, PositionOf(10).y);
    }

    [Fact]
    public void Labels_of_another_kind_stay()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "rock", World(1f), At(300f, 100f));

        scene.Frame();

        Assert.Equal("Stick", scene.Labels[10].text.text);
        Assert.Equal("Rock", scene.Labels[11].text.text);
        Assert.Equal(At(300f, 100f), PositionOf(11));
    }

    [Fact]
    public void Items_past_the_radius_stay_apart()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "stick", World(4f), At(300f, 100f));

        scene.Frame();

        Assert.Equal("Stick", scene.Labels[10].text.text);
        Assert.Equal(At(300f, 100f), PositionOf(11));
    }

    [Fact]
    public void An_inactive_label_takes_no_part()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "stick", World(1.2f), At(100f, 130f));
        scene.Labels[11].gameObject.SetActive(false);

        scene.Frame();

        Assert.Equal("Stick", scene.Labels[10].text.text);
        Assert.False(LabelPatches.TryGetLabelMembers(11, out _));
    }

    [Fact]
    public void A_null_label_and_a_null_entity_are_skipped()
    {
        scene.Hud.entityLabelMap.Add(LabelScene.NewItem(20, "stick", "Stick", World(1f)), null);
        scene.Hud.entityLabelMap.Add(null, scene.AddZone(At(10f, 10f), "Zone"));
        scene.Item(10, "stick", World(1f), At(100f, 100f));

        scene.Frame();

        Assert.Empty(Plugin.Log.Warnings);
        Assert.Equal("Stick", scene.Labels[10].text.text);
    }

    [Fact]
    public void Null_maps_are_skipped_and_the_frame_ends_with_no_warning()
    {
        scene.Hud.entityLabelMap = null;
        scene.Hud.terrainLayerZoneLabelMap = null;
        scene.Hud.entityHudMap = null;

        scene.Frame();

        Assert.Empty(Plugin.Log.Warnings);
        Assert.Same(scene.Player, LabelPatches.LocalPlayerEntity);
    }

    [Fact]
    public void A_frame_with_no_player_has_no_local_player()
    {
        scene.Hud.player = null;
        scene.Item(10, "stick", World(1f), At(100f, 100f));

        scene.Frame();

        Assert.Empty(Plugin.Log.Warnings);
        Assert.Null(LabelPatches.LocalPlayerEntity);
    }

    [Fact]
    public void A_label_that_is_not_an_item_takes_part_in_the_layout()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.AddLabel(LabelScene.NewEntity(20), At(100f, 100f), "Door");

        scene.Frame();

        Assert.Equal(At(100f, 120f), PositionOf(20));
        Assert.False(LabelPatches.TryGetLabelMembers(20, out _));
    }

    [Fact]
    public void A_terrain_zone_label_takes_part_in_the_layout()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        var zone = scene.AddZone(At(100f, 100f), "Forest");

        scene.Frame();

        Assert.Equal(At(100f, 100f), zone.transform.position);
        Assert.Equal(At(100f, 120f), PositionOf(10));
    }

    [Fact]
    public void An_inactive_terrain_zone_label_takes_no_part()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.AddZone(At(100f, 100f), "Forest").gameObject.SetActive(false);

        scene.Frame();

        Assert.Equal(At(100f, 100f), PositionOf(10));
    }

    [Fact]
    public void The_label_of_a_group_gives_its_members()
    {
        var a = scene.Item(10, "stick", World(1f), At(100f, 100f));
        var b = scene.Item(11, "stick", World(1.2f), At(100f, 130f));

        scene.Frame();

        Assert.True(LabelPatches.TryGetLabelMembers(11, out var members));
        Assert.Equal(new[] { a, b }, members);
    }

    [Fact]
    public void An_item_with_no_label_gives_no_members()
    {
        scene.Frame();
        Assert.False(LabelPatches.TryGetLabelMembers(10, out var members));
        Assert.Null(members);
    }

    [Fact]
    public void A_radius_of_zero_merges_no_labels()
    {
        Plugin.PickupRadius.Value = 0f;
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "stick", World(1f), At(300f, 100f));

        scene.Frame();

        Assert.Equal("Stick", scene.Labels[10].text.text);
        Assert.Equal(At(300f, 100f), PositionOf(11));
    }

    // Layout and writes

    [Fact]
    public void Overlapping_labels_move_apart_on_the_vertical_axis_only()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "rock", World(1f), At(100f, 100f));

        scene.Frame();

        Assert.Equal(At(100f, 100f), PositionOf(10));
        Assert.Equal(At(100f, 120f), PositionOf(11));
    }

    [Fact]
    public void A_transform_at_the_written_position_gives_the_game_position()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "rock", World(1f), At(100f, 100f));
        scene.Frame();

        scene.Frame(gamePlaces: false);

        Assert.Equal(At(100f, 120f), PositionOf(11));
        // Measured from the game position, the label still has to move. From the written position it would not.
        Assert.Equal(20f, StaticState.Get<Dictionary<int, float>>(typeof(LabelPatches), "lastMove")[11]);
    }

    [Fact]
    public void A_transform_that_the_game_moved_uses_the_new_game_position()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "rock", World(1f), At(100f, 100f));
        scene.Frame();
        scene.MoveInGame(scene.Labels[11], At(100f, 300f));

        scene.Frame();

        Assert.Equal(At(100f, 300f), PositionOf(11));
    }

    [Fact]
    public void A_label_with_no_move_goes_back_to_the_game_position()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "rock", World(1f), At(100f, 100f));
        scene.Frame();
        scene.Labels[10].gameObject.SetActive(false);

        scene.Frame(gamePlaces: false);

        Assert.Equal(At(100f, 100f), PositionOf(11));
    }

    [Fact]
    public void A_label_that_never_moved_gets_no_write()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Frame();
        int before = scene.Labels[10].transform.PositionWrites;

        scene.Frame(gamePlaces: false);

        Assert.Equal(before, scene.Labels[10].transform.PositionWrites);
    }

    [Fact]
    public void A_label_at_the_top_of_the_screen_moves_down()
    {
        Screen.height = 130;
        scene.Item(10, "stick", World(1f), At(100f, 110f));
        scene.Item(11, "rock", World(1f), At(100f, 110f));

        scene.Frame();

        Assert.Equal(At(100f, 90f), PositionOf(11));
    }

    // Hover

    [Fact]
    public void A_mouse_enter_sets_the_hovered_entity_and_freezes_the_group()
    {
        var a = scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "stick", World(1.2f), At(100f, 130f));
        scene.Frame();

        Enter(a);

        Assert.Same(a, LabelPatches.Hovered);
        Assert.Equal(new[] { 10, 11 }, Frozen.OrderBy(id => id));
    }

    [Fact]
    public void A_mouse_enter_on_an_entity_with_no_group_freezes_only_it()
    {
        var door = LabelScene.NewEntity(20);

        Enter(door);

        Assert.Equal(new[] { 20 }, Frozen);
    }

    [Fact]
    public void A_mouse_enter_with_no_entity_clears_the_hover_and_the_freeze()
    {
        Enter(LabelScene.NewEntity(20));

        Enter(null);

        Assert.Null(LabelPatches.Hovered);
        Assert.Empty(Frozen);
    }

    [Fact]
    public void A_frozen_group_stays_when_the_items_move_apart()
    {
        var a = scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "stick", World(1.2f), At(100f, 130f));
        scene.Frame();
        Enter(a);
        Plugin.PickupRadius.Value = 0f;

        scene.Frame();

        Assert.Equal("Stick (2)", scene.Labels[10].text.text);
    }

    [Fact]
    public void A_mouse_enter_logs_the_hover_with_verbose_on()
    {
        Plugin.Verbose.Value = true;
        var a = LabelScene.NewItem(20, "stick", "Stick", World(1f));

        Enter(a);

        Assert.Contains(Plugin.Log.Debugs, line => line == "Hover: entity 20, kind stick, label group 1");
    }

    [Fact]
    public void An_exception_in_a_mouse_enter_goes_to_WarnOnce()
    {
        Plugin.Verbose = null;

        Enter(LabelScene.NewEntity(20));

        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Label hover: "));
    }

    [Fact]
    public void A_mouse_exit_ends_the_hover_after_the_frame_with_no_hover_card()
    {
        var a = scene.Item(10, "stick", World(1f), At(100f, 100f));
        Enter(a);

        Exit(a);
        Assert.Same(a, LabelPatches.Hovered);

        scene.Frame();
        Assert.Null(LabelPatches.Hovered);
        Assert.Empty(Frozen);
    }

    [Fact]
    public void A_mouse_exit_ends_the_hover_only_after_the_hover_card_closes()
    {
        var a = scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Frame();
        Enter(a);
        var card = scene.OpenCard(a, At(120f, 140f), "Stick");
        Exit(a);

        scene.Frame();
        Assert.Same(a, LabelPatches.Hovered);

        card.gameObject.SetActive(false);
        scene.Frame();
        Assert.Null(LabelPatches.Hovered);
        Assert.Empty(Frozen);
    }

    [Fact]
    public void A_mouse_enter_cancels_an_earlier_exit()
    {
        var a = scene.Item(10, "stick", World(1f), At(100f, 100f));
        Enter(a);
        Exit(a);

        Enter(a);
        scene.Frame();

        Assert.Same(a, LabelPatches.Hovered);
    }

    [Fact]
    public void A_mouse_exit_of_another_entity_does_nothing()
    {
        var a = scene.Item(10, "stick", World(1f), At(100f, 100f));
        Enter(a);

        Exit(LabelScene.NewEntity(20));
        Exit(null);
        scene.Frame();

        Assert.Same(a, LabelPatches.Hovered);
    }

    [Fact]
    public void The_labels_around_a_hover_keep_their_moves_until_the_card_closes()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "rock", World(1f), At(100f, 100f));
        var c = scene.Item(12, "herb", World(1f), At(500f, 500f));
        scene.Frame();
        var card = scene.OpenCard(c, At(500f, 500f), "Herb");
        scene.Labels[12].gameObject.SetActive(false);
        scene.Frame();
        Assert.True(Freeze.Active);

        scene.Labels[10].gameObject.SetActive(false);
        scene.Frame();
        Assert.Equal(At(100f, 120f), PositionOf(11));

        card.gameObject.SetActive(false);
        scene.Frame();
        Assert.False(Freeze.Active);
        Assert.Equal(At(100f, 100f), PositionOf(11));
    }

    // PlaceCard and ReplaceLabel

    [Fact]
    public void The_card_takes_the_place_and_the_color_of_its_label()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        var red = new Color(1f, 0f, 0f, 1f);
        scene.Labels[10].text.color = red;
        scene.Frame();
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal(At(300f, 300f), card.transform.position);
        Assert.Equal(red, card.labelText.color);
    }

    [Fact]
    public void The_card_of_a_hidden_member_takes_the_place_of_its_group_label()
    {
        scene.Item(10, "stick", World(1f), At(300f, 300f));
        var b = scene.Item(11, "stick", World(1.2f), At(400f, 400f));
        scene.Frame();
        var card = scene.OpenCard(b, At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal(At(300f, 300f), card.transform.position);
    }

    [Fact]
    public void The_anchor_is_set_once_so_the_card_follows_the_camera()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        scene.Frame();
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        scene.Frame();
        scene.MoveInGame(card, At(330f, 340f));

        scene.Frame();

        Assert.Equal(At(310f, 300f), card.transform.position);
    }

    [Fact]
    public void A_card_with_no_label_stays_at_its_game_position_and_uses_its_title_bounds()
    {
        var a = LabelScene.NewItem(10, "stick", "Stick", World(1f));
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal(At(320f, 340f), card.transform.position);
        var box = LabelScene.BoxOf(card);
        Assert.Equal(62f, box.sizeDelta.x, 3);
        Assert.Equal(26f, box.sizeDelta.y, 3);
    }

    [Fact]
    public void A_card_with_no_title_uses_the_bounds_of_the_card()
    {
        var a = LabelScene.NewItem(10, "stick", "Stick", World(1f));
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        card.labelText = null;

        scene.Frame();

        Assert.Empty(Plugin.Log.Warnings);
        Assert.Equal(212f, LabelScene.BoxOf(card).sizeDelta.x, 3);
    }

    [Fact]
    public void A_card_with_an_empty_title_gets_no_label_box_and_keeps_its_actions()
    {
        Plugin.Verbose.Value = true;
        var panel = LabelScene.NewHintPanel();
        var card = scene.OpenCard(LabelScene.NewEntity(10), At(320f, 340f), "");
        LabelScene.AddRow(card, "Place down");

        scene.Frame();

        var box = LabelScene.BoxOf(card);
        Assert.True(box == null || !box.gameObject.activeSelf);
        Assert.Contains("Place down", panel.controls1.text);
        Assert.Contains("Hover card 10: empty title, no label box", Plugin.Log.Debugs);
    }

    [Fact]
    public void The_box_of_a_card_turns_off_when_its_title_goes_empty()
    {
        var card = scene.OpenCard(LabelScene.NewEntity(10), At(320f, 340f), "Bottle");
        scene.Frame();
        Assert.True(LabelScene.BoxOf(card).gameObject.activeSelf);

        scene.SetGameText(card, " ");
        scene.Frame();

        Assert.False(LabelScene.BoxOf(card).gameObject.activeSelf);
    }

    [Fact]
    public void An_inactive_card_takes_no_part()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        card.gameObject.SetActive(false);

        scene.Frame();

        Assert.Null(LabelScene.BoxOf(card));
    }

    [Fact]
    public void A_new_card_turns_off_its_action_layout_and_rebuilds_once()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        var rect = card.GetComponent<RectTransform>();

        scene.Frame();
        scene.Frame();

        Assert.False(card.interactionsArea.enabled);
        Assert.Equal(new[] { rect }, UnityEngine.UI.LayoutRebuilder.Rebuilt);
    }

    [Fact]
    public void A_pooled_card_rebuilds_its_layout_after_a_gap()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        scene.Frame();
        Time.frameCount += 5;

        scene.Frame();

        Assert.Equal(2, UnityEngine.UI.LayoutRebuilder.Rebuilt.Count);
        Assert.Equal(2, card.labelText.MeshUpdates);
    }

    [Fact]
    public void The_card_state_of_a_closed_card_goes_away()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        scene.Frame();
        Assert.Equal(1, ((ICollection)CardStates).Count);

        card.gameObject.SetActive(false);
        scene.Frame();

        Assert.Equal(0, ((ICollection)CardStates).Count);
    }

    [Fact]
    public void The_card_state_stays_while_the_entity_is_hovered()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        scene.Frame();
        Enter(a);
        card.gameObject.SetActive(false);

        scene.Frame();

        Assert.Equal(1, ((ICollection)CardStates).Count);
    }

    [Fact]
    public void A_group_of_cards_shows_the_group_text()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        var b = scene.Item(11, "stick", World(1.2f), At(300f, 400f));
        scene.Frame();
        var cardA = scene.OpenCard(a, At(320f, 340f), "Stick");
        var cardB = scene.OpenCard(b, At(320f, 440f), "Stick");

        scene.Frame();

        Assert.Equal("Stick (2)", cardA.labelText.text);
        Assert.NotEqual(HiddenY, cardB.transform.position.y);
    }

    [Fact]
    public void The_title_takes_the_font_size_of_the_item_labels()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        scene.Frame();
        var card = scene.OpenCard(a, At(320f, 340f), "Stick");
        card.labelText.fontSize = 30f;

        scene.Frame();

        Assert.Equal(18f, card.labelText.fontSize);
    }

    [Fact]
    public void The_card_logs_the_title_offset_of_its_first_two_frames_with_verbose_on()
    {
        Plugin.Verbose.Value = true;
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        scene.Frame();
        scene.OpenCard(a, At(320f, 340f), "Stick");

        scene.Frame();
        scene.Frame();
        scene.Frame();

        Assert.Single(Plugin.Log.Debugs, line => line.Contains("title offset"));
        Assert.Contains(Plugin.Log.Debugs, line => line.StartsWith("Hover card 10: label of entity 10"));
    }

    // ShowPickupCount and PickupCountTitle

    private Entity NearbyStick(int id, float x, int quantity = 1)
    {
        var stick = LabelScene.NewItem(id, "stick", "Stick", World(x), quantity);
        var collider = new GameObject("collider").AddComponent<Collider>();
        scene.EntityManager.Entities[collider.gameObject] = stick;
        Physics.Hits.Add(collider);
        return stick;
    }

    [Fact]
    public void A_card_with_no_label_shows_the_count_of_a_radius_pickup()
    {
        var target = NearbyStick(10, 1f);
        NearbyStick(11, 1.5f, quantity: 2);
        var card = scene.OpenCard(target, At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal("Stick (3)", card.labelText.text);
    }

    [Fact]
    public void The_search_runs_once_for_each_card_and_the_count_stays()
    {
        var target = NearbyStick(10, 1f);
        NearbyStick(11, 1.5f);
        var card = scene.OpenCard(target, At(320f, 340f), "Stick");

        scene.Frame();
        scene.Frame();

        Assert.Equal(1, Physics.Calls);
        Assert.Equal("Stick (2)", card.labelText.text);
    }

    [Fact]
    public void A_card_with_no_extra_items_keeps_the_game_text()
    {
        var card = scene.OpenCard(NearbyStick(10, 1f), At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal("Stick", card.labelText.text);
    }

    [Fact]
    public void The_pick_mode_one_keeps_the_game_text()
    {
        Plugin.PickModeEntry.Value = "One";
        var target = NearbyStick(10, 1f);
        NearbyStick(11, 1.5f);
        var card = scene.OpenCard(target, At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal("Stick", card.labelText.text);
        Assert.Equal(0, Physics.Calls);
    }

    [Fact]
    public void A_target_for_the_hands_keeps_the_game_text()
    {
        var target = LabelScene.NewItem(10, "stick", "Stick", World(1f), prefersEquipped: true);
        NearbyStick(11, 1.5f);
        var card = scene.OpenCard(target, At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal("Stick", card.labelText.text);
    }

    [Fact]
    public void No_world_manager_keeps_the_game_text()
    {
        scene.Hud.worldManager = null;
        var card = scene.OpenCard(NearbyStick(10, 1f), At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal("Stick", card.labelText.text);
        Assert.Empty(Plugin.Log.Warnings);
    }

    [Fact]
    public void A_failed_search_keeps_the_game_text_and_goes_to_WarnOnce()
    {
        var target = NearbyStick(10, 1f);
        var card = scene.OpenCard(target, At(320f, 340f), "Stick");
        target.transform = null;

        scene.Frame();

        Assert.Equal("Stick", card.labelText.text);
        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Hover card count: "));
    }

    [Fact]
    public void A_card_that_shows_a_label_runs_no_search()
    {
        var a = scene.Item(10, "stick", World(1f), At(300f, 300f));
        scene.Frame();
        scene.OpenCard(a, At(320f, 340f), "Stick");

        scene.Frame();

        Assert.Equal(0, Physics.Calls);
    }

    // Caches and the frame

    [Fact]
    public void The_data_of_an_entity_is_read_once()
    {
        var a = LabelScene.NewItem(10, "stick", "Stick", World(1f));
        var first = DataOf(a);
        a.GetCachedComponent<IPickupable>(false).Stack.Item.Identifier = "rock";

        Assert.Same(first, DataOf(a));
    }

    [Fact]
    public void The_data_is_read_again_when_a_pointer_comes_back_for_a_new_entity()
    {
        var old = LabelScene.NewItem(10, "stick", "Stick", World(1f));
        DataOf(old);
        var fresh = LabelScene.NewItem(11, "rock", "Rock", World(1f));
        fresh.Pointer = old.Pointer;

        Assert.Equal("rock", StaticState.Member<string>(DataOf(fresh), "Kind"));
    }

    [Fact]
    public void An_entity_that_is_not_an_item_has_no_kind()
    {
        Assert.Null(StaticState.Member<string>(DataOf(LabelScene.NewEntity(10)), "Kind"));
    }

    [Fact]
    public void The_geometry_is_measured_once_and_keeps_the_space_around_the_text()
    {
        var label = scene.AddLabel(LabelScene.NewEntity(20), At(100f, 100f), "Door");

        var first = GeometryOf(label, label.text);

        Assert.Same(first, GeometryOf(label, label.text));
        Assert.Equal(new Vector2(20f, 0f), StaticState.Member<Vector2>(first, "Padding"));
    }

    [Fact]
    public void The_geometry_updates_the_scale()
    {
        var label = scene.AddLabel(LabelScene.NewEntity(20), At(100f, 100f), "Door");
        var geometry = GeometryOf(label, label.text);
        label.transform.lossyScale = new Vector3(2f, 3f, 1f);

        GeometryOf(label, label.text);

        Assert.Equal(new Vector3(2f, 3f, 1f), StaticState.Member<Vector3>(geometry, "Scale"));
    }

    [Fact]
    public void A_component_with_no_rect_has_no_geometry()
    {
        Assert.Null(GeometryOf(new GameObject("plain").AddComponent<UIComponent>(), null));
    }

    [Fact]
    public void The_text_size_is_measured_once_for_each_font_size_and_text()
    {
        var small = scene.AddLabel(LabelScene.NewEntity(20), At(100f, 100f), "Door");
        var big = scene.AddLabel(LabelScene.NewEntity(21), At(100f, 200f), "Gate");
        big.text.fontSize = 30f;
        var smallGeometry = GeometryOf(small, small.text);
        var bigGeometry = GeometryOf(big, big.text);

        Assert.Equal(new Vector2(30f, 20f), TextSize(smallGeometry, "abc"));
        TextSize(smallGeometry, "abc");
        Assert.Equal(1, small.text.Measures);

        TextSize(smallGeometry, "abcd");
        Assert.Equal(2, small.text.Measures);

        TextSize(bigGeometry, "abc");
        Assert.Equal(1, big.text.Measures);
    }

    [Fact]
    public void A_null_text_has_no_size()
    {
        var label = scene.AddLabel(LabelScene.NewEntity(20), At(100f, 100f), "Door");

        Assert.Equal(Vector2.zero, TextSize(GeometryOf(label, label.text), null));
        Assert.Equal(0, label.text.Measures);
    }

    [Fact]
    public void The_text_sizes_clear_at_their_size_limit()
    {
        var label = scene.AddLabel(LabelScene.NewEntity(20), At(100f, 100f), "Door");
        var geometry = GeometryOf(label, label.text);
        var map = Fill("textSizes", 4097, i => (1f, i.ToString()));

        TextSize(geometry, "new");

        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void The_item_data_clears_at_its_size_limit()
    {
        var map = Fill("itemData", 4097, i => new IntPtr(i + 1));

        DataOf(LabelScene.NewEntity(10));

        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void The_geometries_clear_at_their_size_limit()
    {
        var map = Fill("geometries", 2049, i => new IntPtr(i + 1));
        var label = scene.AddLabel(LabelScene.NewEntity(20), At(100f, 100f), "Door");

        GeometryOf(label, label.text);

        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void The_writes_clear_at_their_size_limit()
    {
        var map = Fill("writes", 4096, i => new IntPtr(i + 1));
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Item(11, "rock", World(1f), At(100f, 100f));

        scene.Frame();

        Assert.Equal(0, map.Count);
    }

    [Theory]
    [InlineData("labelColors")]
    [InlineData("labelPlaces")]
    public void The_label_memory_clears_at_its_size_limit(string field)
    {
        var map = Fill(field, 4097, i => i + 1000);
        scene.Item(10, "stick", World(1f), At(100f, 100f));

        scene.Frame();

        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void An_exception_in_the_frame_goes_to_WarnOnce()
    {
        Plugin.PickupRadius = null;

        scene.Frame();

        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Label groups: "));
    }

    [Fact]
    public void A_frame_counts_its_work_and_logs_one_line_after_one_second_with_verbose_on()
    {
        Plugin.Verbose.Value = true;
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Frame();
        Assert.DoesNotContain(Plugin.Log.Debugs, line => line.StartsWith("Labels: "));

        Thread.Sleep(1100);
        scene.Frame();
        scene.Frame();

        var line = Assert.Single(Plugin.Log.Debugs, l => l.StartsWith("Labels: "));
        Assert.Contains("1 item labels", line);
        Assert.Contains("over 1 frames", line);
    }

    [Fact]
    public void No_log_line_with_verbose_off()
    {
        StaticState.Set(typeof(LabelPatches), "frames", 5);

        scene.Frame();

        Assert.Empty(Plugin.Log.Debugs);
        Assert.Equal(6, StaticState.Get<int>(typeof(LabelPatches), "frames"));
    }

    [Fact]
    public void A_slow_frame_is_counted()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        scene.Labels[10].text.Measure = s =>
        {
            Thread.Sleep(5);
            return new Vector2(s.Length * 10f, 20f);
        };

        scene.Frame();

        Assert.Equal(1, StaticState.Get<int>(typeof(LabelPatches), "slowFrames"));
    }

    [Fact]
    public void An_open_hover_card_names_its_entity_also_with_no_label()
    {
        var mushroom = LabelScene.NewEntity(10);
        scene.OpenCard(mushroom, At(300f, 300f), "Spotted Red Mushroom");

        scene.Frame();

        Assert.Same(mushroom, LabelPatches.CardEntity);
    }

    [Fact]
    public void With_no_open_hover_card_no_entity_is_named()
    {
        var card = scene.OpenCard(LabelScene.NewEntity(10), At(300f, 300f), "Herb");
        scene.Frame();
        card.gameObject.SetActive(false);

        scene.Frame();

        Assert.Null(LabelPatches.CardEntity);
    }

    // Labels of quick resources

    private Entity ResourceLabel(int id, string name, float x, Vector3 screen, bool ready = true, float duration = 0.8f)
    {
        var items = new IInventoryManager();
        items.Items["cap"] = new InventoryItem { Identifier = "cap", Name = "Cap" };
        ((WorldManager)scene.Hud.worldManager).inventoryManager = items;
        var entity = GatherPatchesTests.Resource(id, name, x, ready, duration);
        scene.AddLabel(entity, screen, name);
        return entity;
    }

    [Fact]
    public void Quick_resources_of_one_kind_show_one_label_with_their_number()
    {
        ResourceLabel(10, "Spotted Red Mushroom", 1f, At(100f, 100f));
        ResourceLabel(11, "Spotted Red Mushroom", 1.2f, At(100f, 130f));
        ResourceLabel(12, "Spotted Red Mushroom", 1.4f, At(150f, 130f));

        scene.Frame();

        Assert.Equal("Spotted Red Mushroom (3)", scene.Labels[10].text.text);
        Assert.Equal(HiddenY, PositionOf(11).y);
        Assert.True(LabelPatches.TryGetLabelMembers(11, out var members));
        Assert.Equal(new[] { 10, 11, 12 }, members.Select(m => m.id).OrderBy(id => id));
    }

    [Fact]
    public void A_resource_that_is_not_quick_stays_an_other_label()
    {
        ResourceLabel(10, "Blueberry Bush", 1f, At(100f, 100f), duration: 5f);
        ResourceLabel(11, "Blueberry Bush", 1.2f, At(300f, 100f), duration: 5f);

        scene.Frame();

        Assert.Equal("Blueberry Bush", scene.Labels[10].text.text);
        Assert.Equal(At(300f, 100f), PositionOf(11));
        Assert.False(LabelPatches.TryGetLabelMembers(10, out _));
        Assert.Equal(2, StaticState.Get<int>(typeof(LabelPatches), "lastOthers"));
    }

    [Fact]
    public void A_resource_that_is_no_longer_ready_leaves_its_label_group_on_the_next_pass()
    {
        ResourceLabel(10, "Spotted Red Mushroom", 1f, At(100f, 100f));
        var gathered = ResourceLabel(11, "Spotted Red Mushroom", 1.2f, At(300f, 100f));
        ResourceLabel(12, "Spotted Red Mushroom", 1.4f, At(500f, 100f));
        scene.Frame();
        Assert.Equal("Spotted Red Mushroom (3)", scene.Labels[10].text.text);

        gathered.GetCachedComponent<IGatherable>(false).Ready = false;
        scene.Frame();

        Assert.Equal("Spotted Red Mushroom (2)", scene.Labels[10].text.text);
        Assert.NotEqual(HiddenY, PositionOf(11).y);
        Assert.False(LabelPatches.TryGetLabelMembers(11, out _));
    }

    [Fact]
    public void A_stick_item_and_a_stick_resource_do_not_merge()
    {
        scene.Item(10, "stick", World(1f), At(100f, 100f));
        ResourceLabel(11, "Stick", 1.2f, At(300f, 100f));
        ResourceLabel(12, "Stick", 1.4f, At(500f, 100f));

        scene.Frame();

        Assert.Equal("Stick", scene.Labels[10].text.text);
        Assert.Equal("Stick (2)", scene.Labels[11].text.text);
    }

    [Fact]
    public void Resources_with_no_inventory_manager_stay_other_labels()
    {
        ResourceLabel(10, "Spotted Red Mushroom", 1f, At(100f, 100f));
        ResourceLabel(11, "Spotted Red Mushroom", 1.2f, At(300f, 100f));
        ((WorldManager)scene.Hud.worldManager).inventoryManager = null;

        scene.Frame();

        Assert.Equal("Spotted Red Mushroom", scene.Labels[10].text.text);
        Assert.Empty(Plugin.Log.Warnings);
    }

    // A quick resource in the world with no label, found by the radius search.
    private Entity NearbyResource(int id, string name, float x, bool ready = true)
    {
        var items = new IInventoryManager();
        items.Items["cap"] = new InventoryItem { Identifier = "cap", Name = "Cap" };
        ((WorldManager)scene.Hud.worldManager).inventoryManager = items;
        var entity = GatherPatchesTests.Resource(id, name, x, ready);
        var collider = new GameObject("collider").AddComponent<Collider>();
        scene.EntityManager.Entities[collider.gameObject] = entity;
        Physics.Hits.Add(collider);
        return entity;
    }

    [Fact]
    public void A_card_of_a_quick_resource_with_no_label_shows_the_number_of_a_radius_gather()
    {
        var target = NearbyResource(10, "Dandelions", 1f);
        NearbyResource(11, "Dandelions", 1.5f);
        NearbyResource(12, "Dandelions", 2f);
        NearbyResource(13, "Dandelions", 2.2f, ready: false);
        NearbyResource(14, "Wild Garlic", 1.2f);
        var card = scene.OpenCard(target, At(320f, 340f), "Dandelions");

        scene.Frame();

        Assert.Equal("Dandelions (3)", card.labelText.text);
    }

    [Fact]
    public void A_card_of_a_lone_quick_resource_keeps_the_game_text()
    {
        var card = scene.OpenCard(NearbyResource(10, "Dandelions", 1f), At(320f, 340f), "Dandelions");

        scene.Frame();

        Assert.Equal("Dandelions", card.labelText.text);
    }

    [Fact]
    public void A_card_of_a_resource_that_is_not_quick_keeps_the_game_text()
    {
        var target = NearbyResource(10, "Dandelions", 1f, ready: false);
        NearbyResource(11, "Dandelions", 1.5f);
        var card = scene.OpenCard(target, At(320f, 340f), "Dandelions");

        scene.Frame();

        Assert.Equal("Dandelions", card.labelText.text);
        Assert.Equal(0, Physics.Calls);
    }

    [Fact]
    public void A_card_of_a_quick_resource_in_the_pick_mode_one_keeps_the_game_text()
    {
        Plugin.PickModeEntry.Value = "One";
        var target = NearbyResource(10, "Dandelions", 1f);
        NearbyResource(11, "Dandelions", 1.5f);
        var card = scene.OpenCard(target, At(320f, 340f), "Dandelions");

        scene.Frame();

        Assert.Equal("Dandelions", card.labelText.text);
    }

    [Fact]
    public void The_hover_card_of_a_resource_in_a_group_shows_the_group()
    {
        var a = ResourceLabel(10, "Spotted Red Mushroom", 1f, At(300f, 300f));
        ResourceLabel(11, "Spotted Red Mushroom", 1.2f, At(300f, 330f));
        scene.Frame();
        Enter(a);
        var card = scene.OpenCard(a, At(320f, 340f), "Spotted Red Mushroom");

        scene.Frame();

        Assert.Equal("Spotted Red Mushroom (2)", card.labelText.text);
    }
}
