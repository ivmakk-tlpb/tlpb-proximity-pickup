using System.Collections;
using ProximityPickup;
using ProximityPickup.Labels;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

public class LabelDrawingTests : GameTestBase
{
    private const float HiddenY = -10000f;

    private readonly LabelScene scene = new LabelScene();
    private readonly Color red = new Color(1f, 0f, 0f, 1f);

    private static Vector3 At(float x, float y) => new Vector3(x, y, 0f);

    private static Transform Parent(float scale = 1f)
    {
        var parent = LabelScene.NewRect<UIComponent>(new Rect(-10f, -10f, 20f, 20f), Vector3.zero).transform;
        parent.lossyScale = new Vector3(scale, scale, 1f);
        return parent;
    }

    private static object PlaceBox(Transform parent, ScreenRect bounds = default, Vector3 game = default, float alpha = 0.5f)
    {
        return PatchCall.Run(typeof(LabelPatches), "PlaceBox", parent, bounds, game, alpha);
    }

    private static T Part<T>(object box, string field) => StaticState.Member<T>(box, field);

    private static RectTransform RectOf(object box) => Part<RectTransform>(box, "Rect");

    private static void SetBorder(object box, Color color, bool log = false)
    {
        PatchCall.Run(typeof(LabelPatches), "SetBorder", box, color, 7, log);
    }

    private static void SetFill(object box, float progress, Color color)
    {
        PatchCall.Run(typeof(LabelPatches), "SetFill", box, progress, color);
    }

    private static float HideRows(HudUI hud, HudGameObjectUI card, bool hovered)
    {
        return (float)PatchCall.Run(typeof(LabelPatches), "HideRows", hud, card, hovered);
    }

    private static void AssertRect(float xMin, float yMin, float xMax, float yMax, ScreenRect rect)
    {
        Assert.Equal(xMin, rect.XMin, 3);
        Assert.Equal(yMin, rect.YMin, 3);
        Assert.Equal(xMax, rect.XMax, 3);
        Assert.Equal(yMax, rect.YMax, 3);
    }

    private HudGameObjectUI Card() => scene.OpenCard(LabelScene.NewEntity(50), At(300f, 300f), "Stick");

    private static TMPro.TextMeshProUGUI NewText(Rect rect, Vector3 position)
    {
        var tmp = LabelScene.NewRect<TMPro.TextMeshProUGUI>(rect, position);
        tmp.text = "abc";
        return tmp;
    }

    // PlaceBox and HideBox

    [Fact]
    public void The_box_position_and_size_are_relative_to_the_parent_at_a_ui_scale()
    {
        var box = PlaceBox(Parent(2f), new ScreenRect(100f, 200f, 140f, 260f), At(90f, 190f));

        Assert.Equal(At(5f, 5f), RectOf(box).localPosition);
        Assert.Equal(new Vector2(20f, 30f), RectOf(box).sizeDelta);
    }

    [Fact]
    public void The_box_has_the_given_alpha_on_a_black_fill()
    {
        var parent = Parent();
        var box = PlaceBox(parent, alpha: 0.5f);
        Assert.Equal(new Color(0f, 0f, 0f, 0.5f), Part<Image>(box, "Image").color);

        PlaceBox(parent, alpha: 0.75f);
        Assert.Equal(new Color(0f, 0f, 0f, 0.75f), Part<Image>(box, "Image").color);
    }

    [Fact]
    public void A_new_box_is_the_first_child_catches_the_mouse_and_ignores_the_layout()
    {
        var parent = Parent();
        var other = new GameObject("text").transform;
        other.SetParent(parent, false);

        var box = PlaceBox(parent);

        Assert.Same(RectOf(box), parent.children[0]);
        Assert.True(Part<Image>(box, "Image").raycastTarget);
        Assert.True(RectOf(box).GetComponent<LayoutElement>().ignoreLayout);
        Assert.Equal(new Vector2(0.5f, 0.5f), RectOf(box).anchorMin);
        Assert.Equal(Vector2.zero, RectOf(box).pivot);
    }

    [Fact]
    public void The_edge_strip_that_catches_no_mouse_follows_the_ui_scale()
    {
        var box = PlaceBox(Parent(2f));
        var padding = Part<Image>(box, "Image").raycastPadding;

        Assert.Equal(1.5f, padding.y, 3);
        Assert.Equal(1.5f, padding.w, 3);
        Assert.Equal(0f, padding.x, 3);
    }

    [Fact]
    public void Each_parent_has_one_box()
    {
        var parent = Parent();

        var first = PlaceBox(parent);

        Assert.Same(first, PlaceBox(parent));
        Assert.Single(parent.children);
        Assert.NotSame(first, PlaceBox(Parent()));
    }

    [Fact]
    public void A_box_that_is_gone_is_made_again()
    {
        var parent = Parent();
        var first = PlaceBox(parent);
        RectOf(first).UnityDestroyed = true;

        Assert.NotSame(first, PlaceBox(parent));
    }

    [Fact]
    public void A_hidden_box_shows_again()
    {
        var parent = Parent();
        var box = PlaceBox(parent);
        RectOf(box).gameObject.SetActive(false);

        PlaceBox(parent);

        Assert.True(RectOf(box).gameObject.activeSelf);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void A_zero_scale_writes_no_position_and_no_size(float x, float y)
    {
        var parent = Parent();
        parent.lossyScale = new Vector3(x, y, 1f);

        var box = PlaceBox(parent, new ScreenRect(100f, 200f, 140f, 260f), At(90f, 190f));

        Assert.Equal(Vector3.zero, RectOf(box).localPosition);
        Assert.Equal(Vector2.zero, RectOf(box).sizeDelta);
    }

    [Fact]
    public void Only_the_destroyed_boxes_go_away_at_the_size_limit()
    {
        var map = StaticState.Get<IDictionary>(typeof(LabelPatches), "boxes");
        var gone = PlaceBox(Parent());
        for (int i = 0; i < 4096; i++) PlaceBox(Parent());
        RectOf(gone).UnityDestroyed = true;
        Assert.Equal(4097, map.Count);

        PlaceBox(Parent());

        Assert.Equal(4097, map.Count);
        Assert.DoesNotContain(gone, map.Values.Cast<object>());
    }

    [Fact]
    public void A_pooled_zone_label_has_its_box_hidden()
    {
        var zone = scene.AddZone(At(100f, 100f), "Forest");
        var box = PlaceBox(zone.transform);

        scene.Frame();

        Assert.False(RectOf(box).gameObject.activeSelf);
        Assert.Empty(Plugin.Log.Warnings);
    }

    [Fact]
    public void Hiding_a_label_with_no_box_does_nothing()
    {
        var zone = scene.AddZone(At(100f, 100f), "Forest");

        scene.Frame();

        Assert.Null(LabelScene.BoxOf(zone));
    }

    [Fact]
    public void A_label_gets_a_box_around_its_rectangle_at_the_written_place()
    {
        scene.Item(10, "stick", new Vector3(1f, 0f, 0f), At(100f, 100f));

        scene.Frame();

        var box = LabelScene.BoxOf(scene.Labels[10]);
        Assert.Equal(new Vector2(82f, 26f), box.sizeDelta);
        Assert.Equal(new Vector3(-41f, -13f, 0f), box.localPosition);
    }

    [Fact]
    public void A_hover_card_box_is_stronger_than_a_label_box()
    {
        var card = Card();

        scene.Frame();

        Assert.Equal(0.75f, LabelScene.BoxOf(card).GetComponent<Image>().color.a);
    }

    // SetBorder, Edge, and SetFill

    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(2f, 0.5f)]
    [InlineData(0.001f, 100f)]
    public void The_four_edges_are_one_screen_pixel_wide_at_each_ui_scale(float scale, float width)
    {
        var box = PlaceBox(Parent(scale));

        SetBorder(box, red);

        var edges = Part<Image[]>(box, "Edges");
        Assert.Equal(4, edges.Length);
        Assert.Equal(new Vector2(0f, width), edges[0].rectTransform.sizeDelta);
        Assert.Equal(new Vector2(0f, width), edges[1].rectTransform.sizeDelta);
        Assert.Equal(new Vector2(width, 0f), edges[2].rectTransform.sizeDelta);
        Assert.Equal(new Vector2(width, 0f), edges[3].rectTransform.sizeDelta);
    }

    [Fact]
    public void The_edges_lie_along_the_top_the_bottom_the_left_and_the_right()
    {
        var box = PlaceBox(Parent());

        SetBorder(box, red);

        var edges = Part<Image[]>(box, "Edges");
        Assert.Equal(new Vector2(0f, 1f), edges[0].rectTransform.anchorMin);
        Assert.Equal(new Vector2(1f, 1f), edges[0].rectTransform.anchorMax);
        Assert.Equal(new Vector2(0.5f, 0f), edges[1].rectTransform.pivot);
        Assert.Equal(new Vector2(0f, 1f), edges[2].rectTransform.anchorMax);
        Assert.Equal(new Vector2(1f, 0f), edges[3].rectTransform.anchorMin);
        Assert.False(edges[0].raycastTarget);
    }

    [Fact]
    public void The_edges_take_the_color_of_the_title_at_the_border_alpha()
    {
        var box = PlaceBox(Parent());

        SetBorder(box, red);

        Assert.All(Part<Image[]>(box, "Edges"), edge => Assert.Equal(new Color(1f, 0f, 0f, 0.7f), edge.color));
    }

    [Fact]
    public void A_second_border_keeps_the_edges_and_updates_the_width()
    {
        var parent = Parent();
        var box = PlaceBox(parent);
        SetBorder(box, red);
        var edges = Part<Image[]>(box, "Edges");
        parent.lossyScale = new Vector3(2f, 2f, 1f);

        SetBorder(box, red);

        Assert.Same(edges, Part<Image[]>(box, "Edges"));
        Assert.Equal(0.5f, Part<float>(box, "EdgeWidth"));
    }

    [Fact]
    public void New_edges_come_after_the_old_ones_are_destroyed()
    {
        var box = PlaceBox(Parent());
        SetBorder(box, red);
        var old = Part<Image[]>(box, "Edges");
        old[0].UnityDestroyed = true;

        SetBorder(box, red);

        var edges = Part<Image[]>(box, "Edges");
        Assert.NotSame(old, edges);
        Assert.Equal(1f, edges[0].rectTransform.sizeDelta.y);
    }

    [Fact]
    public void The_border_logs_its_color_with_verbose_on_when_asked()
    {
        Plugin.Verbose.Value = true;
        var box = PlaceBox(Parent());

        SetBorder(box, red);
        Assert.Empty(Plugin.Log.Debugs);

        SetBorder(box, red, log: true);
        Assert.Equal(new[] { "Hover card 7: border color FF0000 at 0.70 opacity" }, Plugin.Log.Debugs);
    }

    [Fact]
    public void The_fill_width_comes_from_the_progress_with_the_title_color_at_the_fill_alpha()
    {
        var box = PlaceBox(Parent());

        SetFill(box, 0.4f, red);

        var fill = Part<Image>(box, "Fill");
        Assert.True(fill.gameObject.activeSelf);
        Assert.Equal(new Vector2(0.4f, 1f), fill.rectTransform.anchorMax);
        Assert.Equal(new Color(1f, 0f, 0f, 0.35f), fill.color);
        Assert.False(fill.raycastTarget);
    }

    [Fact]
    public void No_progress_turns_the_fill_off_and_a_new_progress_turns_it_on()
    {
        var box = PlaceBox(Parent());
        SetFill(box, 0.4f, red);
        var fill = Part<Image>(box, "Fill");

        SetFill(box, -1f, red);
        Assert.False(fill.gameObject.activeSelf);
        Assert.Equal(-1, StaticState.Get<int>(typeof(LabelPatches), "lastProgressStep"));

        SetFill(box, 0.8f, red);
        Assert.True(fill.gameObject.activeSelf);
        Assert.Same(fill, Part<Image>(box, "Fill"));
    }

    [Fact]
    public void No_progress_on_a_box_with_no_fill_makes_no_fill()
    {
        var box = PlaceBox(Parent());

        SetFill(box, -1f, red);

        Assert.Null(Part<Image>(box, "Fill"));
    }

    [Fact]
    public void The_progress_logs_each_tenth_once_with_verbose_on()
    {
        Plugin.Verbose.Value = true;
        var box = PlaceBox(Parent());

        SetFill(box, 0.41f, red);
        SetFill(box, 0.42f, red);
        SetFill(box, 0.51f, red);

        Assert.Equal(new[] { "Hold progress: 0.41", "Hold progress: 0.51" }, Plugin.Log.Debugs);
    }

    [Fact]
    public void A_hover_card_with_a_hold_action_fills_its_box()
    {
        var card = Card();
        LabelScene.NewHintPanel();
        LabelScene.AddRow(card, "Pick up", progress: 0.6f);

        scene.Frame();

        var fill = LabelScene.BoxOf(card).children[0].GetComponent<Image>();
        Assert.Equal(0.6f, fill.rectTransform.anchorMax.x, 3);
    }

    // HideRows and ShowInHintPanel

    [Fact]
    public void The_rows_go_off_the_screen_and_the_action_layout_turns_off()
    {
        LabelScene.NewHintPanel();
        var card = Card();
        var row = LabelScene.AddRow(card, "Pick up");

        HideRows(scene.Hud, card, true);

        Assert.Equal(new Vector3(10f, HiddenY, 30f), row.transform.position);
        Assert.False(card.interactionsArea.enabled);
    }

    [Fact]
    public void The_highest_progress_of_the_active_bars_is_returned()
    {
        LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "Pick up", progress: 0.7f);
        LabelScene.AddRow(card, "Eat", progress: 0.3f);
        LabelScene.AddRow(card, "Drop");

        Assert.Equal(0.7f, HideRows(scene.Hud, card, true), 3);
    }

    [Fact]
    public void A_card_with_no_active_bar_has_no_progress()
    {
        LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "Drop");

        Assert.Equal(-1f, HideRows(scene.Hud, card, true));
    }

    [Fact]
    public void A_card_with_no_rows_shows_nothing()
    {
        var card = Card();

        Assert.Equal(-1f, HideRows(scene.Hud, card, true));
        Assert.Empty(Plugin.Log.Warnings);
    }

    [Fact]
    public void A_card_with_an_inactive_or_missing_action_area_shows_nothing()
    {
        var card = Card();
        card.interactionsArea.gameObject.SetActive(false);
        Assert.Equal(-1f, HideRows(scene.Hud, card, true));

        card.interactionsArea = null;
        Assert.Equal(-1f, HideRows(scene.Hud, card, true));
    }

    [Fact]
    public void A_row_becomes_a_line_of_the_hint_panel()
    {
        var panel = LabelScene.NewHintPanel();
        var card = Card();
        var row = LabelScene.AddRow(card, "Pick up");
        row.descriptionText.color = red;
        row.keyBindLabel.inputActionPresent = true;
        row.keyBindLabel.inputAction = InputAction.Interact;
        scene.Hud.player.KeyNames[InputAction.Interact] = "E";

        HideRows(scene.Hud, card, true);

        Assert.Equal("<b>E</b> <color=#FF0000>Pick up</color>", panel.controls1.text);
    }

    [Fact]
    public void The_panel_is_created_once_with_no_game_updates_and_no_mouse()
    {
        var panel = LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "Pick up");

        HideRows(scene.Hud, card, true);
        UIComponent.uiManager = null;
        HideRows(scene.Hud, card, true);

        Assert.Empty(Plugin.Log.Warnings);
        Assert.False(panel.enabled);
        Assert.False(panel.GetComponent<CanvasGroup>().blocksRaycasts);
    }

    [Fact]
    public void The_card_of_the_hovered_thing_wins_over_another_open_card()
    {
        var panel = LabelScene.NewHintPanel();
        var first = Card();
        LabelScene.AddRow(first, "Pick up");
        var second = Card();
        LabelScene.AddRow(second, "Open");

        HideRows(scene.Hud, first, false);
        HideRows(scene.Hud, second, false);
        Assert.Contains("Pick up", panel.controls1.text);

        HideRows(scene.Hud, second, true);
        Assert.Contains("Open", panel.controls1.text);
    }

    [Fact]
    public void At_most_five_lines_show()
    {
        var panel = LabelScene.NewHintPanel();
        var card = Card();
        for (int i = 1; i <= 7; i++) LabelScene.AddRow(card, "Action" + i);

        HideRows(scene.Hud, card, true);

        Assert.Empty(Plugin.Log.Warnings);
        Assert.Contains("Action5", panel.controls5.text);
    }

    [Fact]
    public void The_lines_past_the_rows_turn_off_and_come_back_with_more_rows()
    {
        var panel = LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "One");
        LabelScene.AddRow(card, "Two");

        HideRows(scene.Hud, card, true);
        Assert.True(panel.controls2.gameObject.activeSelf);
        Assert.False(panel.controls3.gameObject.activeSelf);
        Assert.False(panel.controls5.gameObject.activeSelf);

        LabelScene.AddRow(card, "Three");
        HideRows(scene.Hud, card, true);
        Assert.True(panel.controls3.gameObject.activeSelf);
    }

    [Fact]
    public void A_row_with_a_hidden_description_takes_no_line()
    {
        var panel = LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "Hidden").descriptionText.gameObject.SetActive(false);
        LabelScene.AddRow(card, "Shown");

        HideRows(scene.Hud, card, true);

        Assert.Contains("Shown", panel.controls1.text);
        Assert.False(panel.controls2.gameObject.activeSelf);
    }

    [Fact]
    public void The_panel_sits_above_the_quick_slots_at_the_center_of_the_screen()
    {
        var panel = LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "Pick up");

        HideRows(scene.Hud, card, true);

        Assert.Equal(At(960f, 103f), panel.transform.position);
    }

    [Fact]
    public void The_panel_sits_above_the_open_inventory_controls()
    {
        var panel = LabelScene.NewHintPanel();
        scene.Hud.inventoryControlsUI = LabelScene.NewRect<InventoryControlsUI>(new Rect(-100f, -30f, 200f, 60f), At(960f, 200f));
        var card = Card();
        LabelScene.AddRow(card, "Pick up");

        HideRows(scene.Hud, card, true);

        Assert.Equal(At(960f, 258f), panel.transform.position);
    }

    [Fact]
    public void The_panel_ignores_closed_inventory_controls()
    {
        var panel = LabelScene.NewHintPanel();
        scene.Hud.inventoryControlsUI = LabelScene.NewRect<InventoryControlsUI>(new Rect(-100f, -30f, 200f, 60f), At(960f, 200f));
        scene.Hud.inventoryControlsUI.gameObject.SetActive(false);
        var card = Card();
        LabelScene.AddRow(card, "Pick up");

        HideRows(scene.Hud, card, true);

        Assert.Equal(At(960f, 103f), panel.transform.position);
    }

    [Fact]
    public void The_panel_shows_while_a_card_uses_it_and_turns_off_after()
    {
        var panel = LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "Pick up");

        scene.Frame();
        Assert.True(panel.gameObject.activeSelf);

        card.gameObject.SetActive(false);
        scene.Frame();
        Assert.False(panel.gameObject.activeSelf);
    }

    [Fact]
    public void A_panel_failure_goes_to_WarnOnce_and_the_labels_go_on()
    {
        var card = Card();
        var row = LabelScene.AddRow(card, "Pick up", progress: 0.5f);

        float progress = HideRows(scene.Hud, card, true);

        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Hint panel hotkeys: "));
        Assert.Equal(0.5f, progress, 3);
        Assert.Equal(HiddenY, row.transform.position.y);
    }

    [Fact]
    public void The_panel_logs_a_changed_line_with_verbose_on()
    {
        Plugin.Verbose.Value = true;
        LabelScene.NewHintPanel();
        var card = Card();
        LabelScene.AddRow(card, "Pick up");

        HideRows(scene.Hud, card, true);
        HideRows(scene.Hud, card, true);

        Assert.Single(Plugin.Log.Debugs, line => line.StartsWith("Hint panel hotkeys: <color="));
    }

    // ReadKeys

    private KeyBindLabel Key(InputAction action = InputAction.Interact)
    {
        var label = new GameObject("key").AddComponent<KeyBindLabel>();
        label.inputActionPresent = true;
        label.inputAction = action;
        scene.Hud.player.KeyNames[InputAction.Interact] = "E or F";
        scene.Hud.player.KeyNames[InputAction.Sprint] = "Shift";
        return label;
    }

    private (string Modifier, string Key) ReadKeys(KeyBindLabel label)
    {
        var args = new object[] { scene.Hud.player, label, null, null };
        PatchCall.Run(typeof(LabelPatches), "ReadKeys", args);
        return ((string)args[2], (string)args[3]);
    }

    [Fact]
    public void The_flag_off_gives_no_modifier_and_the_first_binding_of_the_key()
    {
        var keys = ReadKeys(Key());

        Assert.Null(keys.Modifier);
        Assert.Equal("E", keys.Key);
        Assert.Empty(Plugin.Log.Warnings);
    }

    [Fact]
    public void The_flag_on_gives_the_name_of_the_modifier()
    {
        var label = Key();
        label.SetModifier(1, (int)InputAction.Sprint);

        var keys = ReadKeys(label);

        Assert.Equal("Shift", keys.Modifier);
        Assert.Equal("E", keys.Key);
    }

    [Theory]
    [InlineData(1, 999)]
    [InlineData(2, 2)]
    public void A_bad_modifier_gives_a_warning_and_keeps_the_key(int flag, int value)
    {
        var label = Key();
        label.SetModifier((byte)flag, value);

        var keys = ReadKeys(label);

        Assert.Null(keys.Modifier);
        Assert.Equal("E", keys.Key);
        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Hint panel hotkeys: InvalidOperationException"));
    }

    [Fact]
    public void A_label_with_no_input_action_has_no_keys()
    {
        var label = Key();
        label.inputActionPresent = false;

        var keys = ReadKeys(label);

        Assert.Null(keys.Modifier);
        Assert.Null(keys.Key);
    }

    [Fact]
    public void A_row_with_no_key_label_has_no_keys()
    {
        var keys = ReadKeys(null);

        Assert.Null(keys.Modifier);
        Assert.Null(keys.Key);
    }

    // GlyphOffset, TitleBounds, and BoundsOf

    private static ScreenPoint GlyphOffset(TMPro.TextMeshProUGUI text, Transform root)
    {
        return (ScreenPoint)PatchCall.Run(typeof(LabelPatches), "GlyphOffset", text, root);
    }

    private static ScreenRect TitleBounds(HudGameObjectUI card, Vector3 game)
    {
        return (ScreenRect)PatchCall.Run(typeof(LabelPatches), "TitleBounds", card, game);
    }

    private static Transform Root(float x, float y)
    {
        return LabelScene.NewRect<UIComponent>(new Rect(0f, 0f, 1f, 1f), At(x, y)).transform;
    }

    [Fact]
    public void A_text_with_a_mesh_gives_the_center_of_its_letters()
    {
        var text = NewText(new Rect(-10f, -5f, 20f, 10f), At(100f, 50f));
        text.textBounds = new Bounds(At(5f, 2f), At(10f, 4f));

        var offset = GlyphOffset(text, Root(90f, 40f));

        Assert.Equal(15f, offset.X, 3);
        Assert.Equal(12f, offset.Y, 3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(40000f)]
    public void A_text_with_no_valid_mesh_gives_the_center_of_its_rect(float glyphWidth)
    {
        var text = NewText(new Rect(-10f, -4f, 20f, 12f), At(100f, 50f));
        text.textBounds = new Bounds(At(5f, 2f), At(glyphWidth, 4f));

        var offset = GlyphOffset(text, Root(90f, 40f));

        Assert.Equal(10f, offset.X, 3);
        Assert.Equal(12f, offset.Y, 3);
    }

    [Fact]
    public void The_title_width_comes_from_the_preferred_size_and_the_bounds_shift_to_the_game_position()
    {
        var card = LabelScene.NewRect<HudGameObjectUI>(new Rect(-100f, -40f, 200f, 80f), At(300f, 300f));
        card.labelText = NewText(new Rect(-50f, -10f, 100f, 20f), At(300f, 310f));
        card.labelText.transform.lossyScale = new Vector3(2f, 2f, 1f);

        AssertRect(290f, 295f, 350f, 335f, TitleBounds(card, At(320f, 305f)));
    }

    [Fact]
    public void A_card_with_no_title_uses_its_own_rect()
    {
        var card = LabelScene.NewRect<HudGameObjectUI>(new Rect(-100f, -40f, 200f, 80f), At(300f, 300f));

        AssertRect(220f, 265f, 420f, 345f, TitleBounds(card, At(320f, 305f)));
    }

    [Fact]
    public void The_bounds_of_a_rect_use_its_position_and_scale()
    {
        var rect = LabelScene.NewRect<UIComponent>(new Rect(-5f, -5f, 10f, 10f), At(10f, 20f)).GetComponent<RectTransform>();
        rect.lossyScale = new Vector3(2f, 2f, 1f);

        AssertRect(0f, 10f, 20f, 30f, (ScreenRect)PatchCall.Run(typeof(LabelPatches), "BoundsOf", rect));
    }
}
