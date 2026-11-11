using ProximityPickup;
using ProximityPickup.Labels;
using ProximityPickup.Pickup;
using UnityEngine.InputSystem;
using Xunit;

public class ClickPatchesTests : GameTestBase
{
    private readonly Entity player = LabelScene.NewEntity(1);
    private readonly Entity other = LabelScene.NewEntity(2);
    private readonly Entity mushroom = GatherPatchesTests.Resource(10, "Spotted Red Mushroom");
    private readonly PlayerInputView view;

    public ClickPatchesTests()
    {
        LabelPatches.LocalPlayerEntity = player;
        view = new PlayerInputView { entity = player };
        Plugin.Verbose.Value = true;
    }

    private static void Execute(InteractionEventParams eventParams)
    {
        PatchCall.Run(typeof(ClickPatches), "ExecutePostfix", eventParams);
    }

    private void Update() => PatchCall.Run(typeof(ClickPatches), "UpdatePostfix", view);

    private static void Handled(bool started, bool withinRange, InteractionType type)
    {
        PatchCall.Run(typeof(ClickPatches), "HandleResultPostfix", new InteractionResult { possible = true, started = started, withinRange = withinRange }, type);
    }

    private InteractionEventParams Click(Entity interactor, Entity target)
    {
        return new InteractionEventParams { inputType = InteractionInputType.END, interactorEntity = interactor, targetEntity = target };
    }

    // The log of the click params

    [Fact]
    public void A_click_of_the_player_on_a_resource_logs_its_params()
    {
        Execute(Click(player, mushroom));

        Assert.Equal(new[] { "Click params: interactor 1, target 10, source PLAYER, ordinality PRIMARY, action PRIMARY, input END, desired NONE, required null, ignored null, data null" },
            Plugin.Log.Debugs);
    }

    [Fact]
    public void A_click_on_a_ground_item_logs_its_params()
    {
        Execute(Click(player, LabelScene.NewItem(11, "stick", "Stick", default)));
        Assert.Single(Plugin.Log.Debugs);
    }

    [Fact]
    public void A_click_of_another_character_logs_nothing()
    {
        Execute(Click(other, mushroom));
        Assert.Empty(Plugin.Log.Debugs);
    }

    [Fact]
    public void A_click_on_a_thing_with_no_pickup_or_gather_logs_nothing()
    {
        Execute(Click(player, LabelScene.NewEntity(12)));
        Execute(Click(player, null));
        Assert.Empty(Plugin.Log.Debugs);
    }

    [Fact]
    public void With_verbose_off_a_click_logs_nothing()
    {
        Plugin.Verbose.Value = false;
        Execute(Click(player, mushroom));
        Assert.Empty(Plugin.Log.Debugs);
    }

    // The result of an interaction start

    [Fact]
    public void The_start_of_a_gather_logs_its_result()
    {
        Handled(true, false, InteractionType.GATHER);
        Assert.Equal(new[] { "Interaction result: GATHER, possible True, started True, withinRange False" }, Plugin.Log.Debugs);
    }

    [Fact]
    public void The_start_of_another_type_logs_nothing()
    {
        Handled(true, true, InteractionType.OTHER);
        Assert.Empty(Plugin.Log.Debugs);
    }

    // The middle-click

    private void Press(float x = 500f, float y = 300f)
    {
        var mouse = Mouse.current;
        mouse.position.Value = new UnityEngine.Vector2(x, y);
        mouse.middleButton.wasPressedThisFrame = true;
        Update();
        mouse.middleButton.wasPressedThisFrame = false;
    }

    private void Release(float seconds = 0.1f, float x = 500f, float y = 300f)
    {
        var mouse = Mouse.current;
        UnityEngine.Time.unscaledTime += seconds;
        mouse.position.Value = new UnityEngine.Vector2(x, y);
        mouse.middleButton.wasReleasedThisFrame = true;
        Update();
        mouse.middleButton.wasReleasedThisFrame = false;
    }

    private void MiddleClick()
    {
        Press();
        Release();
    }

    [Fact]
    public void A_middle_click_on_a_hovered_resource_starts_the_game_click_on_it_as_the_take_one_target()
    {
        view.primaryActionUpLocked = true;
        LabelPatches.Hovered = mushroom;

        MiddleClick();

        Assert.Same(mushroom, view.primaryActionDownEntity);
        Assert.False(view.primaryActionUpLocked);
        Assert.Same(mushroom, ClickPatches.TakeOneTarget);
        Assert.Equal(new[] { "Middle-click: entity 10, gather, 100 ms" }, Plugin.Log.Debugs);
    }

    [Fact]
    public void The_thing_under_the_cursor_at_the_press_is_taken_when_the_press_ends_the_hover()
    {
        LabelPatches.Hovered = mushroom;
        Press();
        LabelPatches.Hovered = null;
        Release();

        Assert.Same(mushroom, ClickPatches.TakeOneTarget);
    }

    [Fact]
    public void A_hover_card_that_closed_in_the_frame_of_the_press_still_gives_the_target()
    {
        LabelPatches.CardEntity = mushroom;
        Update();
        LabelPatches.CardEntity = null;

        MiddleClick();

        Assert.Same(mushroom, view.primaryActionDownEntity);
    }

    [Fact]
    public void A_hover_that_ended_long_before_the_press_gives_no_target()
    {
        LabelPatches.Hovered = mushroom;
        Update();
        LabelPatches.Hovered = null;
        UnityEngine.Time.unscaledTime += 1f;

        MiddleClick();

        Assert.Null(view.primaryActionDownEntity);
    }

    [Fact]
    public void A_middle_click_on_a_ground_item_takes_it()
    {
        var stick = LabelScene.NewItem(11, "stick", "Stick", default);
        LabelPatches.Hovered = stick;

        MiddleClick();

        Assert.Same(stick, ClickPatches.TakeOneTarget);
        Assert.Contains("Middle-click: entity 11, pickup, 100 ms", Plugin.Log.Debugs);
    }

    [Fact]
    public void With_the_labels_off_the_middle_click_takes_the_thing_of_the_open_hover_card()
    {
        LabelPatches.CardEntity = mushroom;

        MiddleClick();

        Assert.Same(mushroom, view.primaryActionDownEntity);
    }

    [Fact]
    public void The_middle_click_works_with_verbose_off()
    {
        Plugin.Verbose.Value = false;
        LabelPatches.Hovered = mushroom;

        MiddleClick();

        Assert.Same(mushroom, view.primaryActionDownEntity);
        Assert.Empty(Plugin.Log.Debugs);
    }

    [Fact]
    public void A_longer_hold_keeps_the_camera_action_and_takes_nothing()
    {
        LabelPatches.Hovered = mushroom;

        Press();
        Release(seconds: 0.5f);

        Assert.Null(view.primaryActionDownEntity);
        Assert.Null(ClickPatches.TakeOneTarget);
    }

    [Fact]
    public void A_drag_keeps_the_camera_action_and_takes_nothing()
    {
        LabelPatches.Hovered = mushroom;

        Press(500f, 300f);
        Release(x: 520f, y: 300f);

        Assert.Null(view.primaryActionDownEntity);
    }

    [Fact]
    public void A_release_with_no_press_takes_nothing()
    {
        LabelPatches.Hovered = mushroom;

        Release();

        Assert.Null(view.primaryActionDownEntity);
    }

    [Fact]
    public void A_middle_click_on_a_thing_with_no_pickup_or_gather_does_nothing()
    {
        LabelPatches.Hovered = LabelScene.NewEntity(12);
        MiddleClick();
        LabelPatches.Hovered = null;
        UnityEngine.Time.unscaledTime += 1f;
        MiddleClick();

        Assert.Null(view.primaryActionDownEntity);
        Assert.Null(ClickPatches.TakeOneTarget);
        Assert.Equal(new[]
        {
            "Middle-click: no thing with a pickup or a gather under the cursor (hovered 12)",
            "Middle-click: no thing with a pickup or a gather under the cursor (hovered none)",
        }, Plugin.Log.Debugs);
    }

    [Fact]
    public void The_middle_click_does_nothing_with_no_mouse()
    {
        Mouse.current = null;
        LabelPatches.Hovered = mushroom;

        Update();

        Assert.Null(view.primaryActionDownEntity);
        Assert.Empty(Plugin.Log.Warnings);
    }

    [Fact]
    public void The_middle_click_does_nothing_for_a_view_of_another_character()
    {
        view.entity = other;
        LabelPatches.Hovered = mushroom;

        MiddleClick();

        Assert.Null(view.primaryActionDownEntity);
    }

    [Fact]
    public void The_take_one_target_clears_when_the_view_no_longer_holds_it()
    {
        LabelPatches.Hovered = mushroom;
        MiddleClick();

        view.primaryActionDownEntity = null;
        Update();

        Assert.Null(ClickPatches.TakeOneTarget);
    }

    [Fact]
    public void The_take_one_target_clears_when_the_view_holds_another_thing()
    {
        LabelPatches.Hovered = mushroom;
        MiddleClick();

        view.primaryActionDownEntity = LabelScene.NewEntity(13);
        Update();

        Assert.Null(ClickPatches.TakeOneTarget);
    }

    [Fact]
    public void The_take_one_target_stays_while_the_view_holds_it()
    {
        LabelPatches.Hovered = mushroom;
        MiddleClick();

        Update();

        Assert.Same(mushroom, ClickPatches.TakeOneTarget);
    }

    [Fact]
    public void TakeOne_is_true_once_and_only_for_the_take_one_target()
    {
        ClickPatches.TakeOneTarget = mushroom;

        Assert.False(ClickPatches.TakeOne(LabelScene.NewEntity(13)));
        Assert.True(ClickPatches.TakeOne(mushroom));
        Assert.False(ClickPatches.TakeOne(mushroom));
        Assert.Null(ClickPatches.TakeOneTarget);
    }

    [Fact]
    public void An_exception_in_the_middle_click_goes_to_WarnOnce()
    {
        PatchCall.Run(typeof(ClickPatches), "UpdatePostfix", (PlayerInputView)null);

        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Middle-click: "));
    }
}
