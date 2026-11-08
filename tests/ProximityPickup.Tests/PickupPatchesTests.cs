using ProximityPickup;
using ProximityPickup.Labels;
using ProximityPickup.Pickup;
using UnityEngine;
using Xunit;
using Result = PickupEffect.PickupEffectResult;

public class PickupPatchesTests : GameTestBase
{
    private readonly Entity player = LabelScene.NewEntity(1);
    private readonly Entity other = LabelScene.NewEntity(2);
    private readonly IEntityManager entities = new IEntityManager();

    public PickupPatchesTests()
    {
        LabelPatches.LocalPlayerEntity = player;
    }

    private static Entity Stick(int id, float x = 0f, bool prefersEquipped = false, params ItemTag[] tags)
    {
        return LabelScene.NewItem(id, "stick", "Stick", new Vector3(x, 0f, 0f), 1, prefersEquipped, tags);
    }

    private static object Plan => StaticState.Get<object>(typeof(PickupPatches), "current");

    private static PickupSource Source => StaticState.Member<PickupSource>(Plan, "Source");

    private static void Start(InteractionType type, Entity interactor, Entity target)
    {
        PatchCall.Run(typeof(PickupPatches), "StartInteractionPrefix", type, interactor, target);
    }

    private void Click(Entity target) => Start(InteractionType.PICKUP, player, target);

    private static void Teardown(Entity target)
    {
        PatchCall.Run(typeof(PickupPatches), "TeardownPostfix", new PickupInteraction { targetEntity = target });
    }

    private PickupEffect Effect(Entity target, Entity interactor = null)
    {
        return new PickupEffect { Interactor = interactor ?? player, Target = target, entityManager = entities };
    }

    private static Result Fitted(float x = 0f, int amount = 1)
    {
        return new Result { success = true, amount = amount, position = new Vector3(x, 1f, 0f) };
    }

    private static Result Finish(PickupEffect effect, Result result)
    {
        var args = new object[] { effect, result };
        PatchCall.Run(typeof(PickupPatches), "ExecuteResultPostfix", args);
        return (Result)args[1];
    }

    private static int PickExtras(PickupEffect effect, Entity interactor, Entity target, List<Entity> extras, out int amount)
    {
        var args = new object[] { effect, interactor, target, extras, 0 };
        int picked = (int)PatchCall.Run(typeof(PickupPatches), "PickExtras", args);
        amount = (int)args[4];
        return picked;
    }

    private Collider ColliderOn(Entity entity)
    {
        var collider = new GameObject("collider").AddComponent<Collider>();
        entities.Entities[collider.gameObject] = entity;
        Physics.Hits.Add(collider);
        return collider;
    }

    private Entity NearbyStick(int id, float x)
    {
        var stick = Stick(id, x);
        ColliderOn(stick);
        return stick;
    }

    private static List<Entity> FindWithinRadius(IEntityManager manager, Entity target, Vector3 center, out int colliders)
    {
        return PickupPatches.FindWithinRadius(manager, target, "stick", center, out colliders);
    }

    // StartInteractionPrefix

    [Fact]
    public void Another_interaction_type_makes_no_plan()
    {
        Start(InteractionType.OTHER, player, Stick(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_null_interactor_makes_no_plan()
    {
        Start(InteractionType.PICKUP, null, Stick(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_null_target_makes_no_plan()
    {
        Start(InteractionType.PICKUP, player, null);
        Assert.Null(Plan);
    }

    [Fact]
    public void Another_character_makes_no_plan()
    {
        Start(InteractionType.PICKUP, other, Stick(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void No_local_player_makes_no_plan()
    {
        LabelPatches.LocalPlayerEntity = null;
        Click(Stick(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_target_that_is_not_an_item_makes_no_plan()
    {
        Click(LabelScene.NewEntity(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_target_with_a_label_group_plans_the_members_of_the_label()
    {
        var scene = new LabelScene();
        var a = scene.Item(10, "stick", new Vector3(0f, 0f, 0f), new Vector3(100f, 100f, 0f));
        var b = scene.Item(11, "stick", new Vector3(1f, 0f, 0f), new Vector3(100f, 100f, 0f));
        scene.Frame();
        LabelPatches.LocalPlayerEntity = scene.Player;

        Start(InteractionType.PICKUP, scene.Player, a);

        Assert.Equal(PickupSource.LabelGroup, Source);
        Assert.Equal(new[] { a, b }, StaticState.Member<List<Entity>>(Plan, "Members"));
    }

    [Fact]
    public void A_target_with_no_label_plans_a_radius_search()
    {
        Click(Stick(10));
        Assert.Equal(PickupSource.Radius, Source);
        Assert.Null(StaticState.Member<List<Entity>>(Plan, "Members"));
    }

    [Fact]
    public void The_pick_mode_one_plans_no_extra_items()
    {
        Plugin.PickModeEntry.Value = "One";
        Click(Stick(10));
        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_target_for_the_hands_plans_no_extra_items()
    {
        Click(Stick(10, prefersEquipped: true));
        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_second_start_on_the_same_target_keeps_the_plan_of_the_click()
    {
        var stick = Stick(10);
        Click(stick);
        Plugin.PickModeEntry.Value = "One";

        Click(stick);

        Assert.Equal(PickupSource.Radius, Source);
    }

    [Fact]
    public void A_start_on_another_target_replaces_the_plan()
    {
        Click(Stick(10));
        Plugin.PickModeEntry.Value = "One";

        Click(Stick(11));

        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void An_exception_at_the_start_goes_to_WarnOnce()
    {
        Plugin.PickModeEntry = null;

        Click(Stick(10));

        Assert.Null(Plan);
        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Pickup start: "));
    }

    // TeardownPostfix

    [Fact]
    public void A_teardown_of_the_same_target_clears_the_open_plan()
    {
        var stick = Stick(10);
        Click(stick);

        Teardown(stick);

        Assert.Null(Plan);
    }

    [Fact]
    public void A_later_click_after_a_teardown_makes_a_new_plan()
    {
        var stick = Stick(10);
        Click(stick);
        Teardown(stick);
        Plugin.PickModeEntry.Value = "One";

        Click(stick);

        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_teardown_of_another_target_keeps_the_plan()
    {
        Click(Stick(10));
        Teardown(Stick(11));
        Assert.NotNull(Plan);
    }

    [Fact]
    public void A_teardown_with_no_target_keeps_the_plan()
    {
        Click(Stick(10));
        Teardown(null);
        Assert.NotNull(Plan);
    }

    [Fact]
    public void An_exception_in_a_teardown_goes_to_WarnOnce()
    {
        Plugin.Verbose = null;

        Teardown(Stick(10));

        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Pickup teardown: "));
    }

    // ExecuteResultPostfix

    [Fact]
    public void A_result_of_another_character_picks_no_extra_items()
    {
        var target = Stick(10);
        NearbyStick(11, 1f);
        Click(target);
        var effect = Effect(target, other);

        var result = Finish(effect, Fitted());

        Assert.Empty(effect.Inits);
        Assert.Equal(1, result.amount);
    }

    [Fact]
    public void A_result_with_no_plan_picks_no_extra_items()
    {
        var target = Stick(10);
        NearbyStick(11, 1f);
        var effect = Effect(target);

        Finish(effect, Fitted());

        Assert.Empty(effect.Inits);
    }

    [Fact]
    public void A_result_of_another_target_picks_no_extra_items_and_keeps_the_plan()
    {
        Click(Stick(10));
        NearbyStick(12, 1f);
        var effect = Effect(Stick(11));

        Finish(effect, Fitted());

        Assert.Empty(effect.Inits);
        Assert.NotNull(Plan);
    }

    [Fact]
    public void The_source_none_picks_no_extra_items_and_uses_the_plan()
    {
        Plugin.PickModeEntry.Value = "One";
        var target = Stick(10);
        NearbyStick(11, 1f);
        Click(target);
        var effect = Effect(target);

        Finish(effect, Fitted());

        Assert.Empty(effect.Inits);
        Assert.Null(Plan);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void A_target_that_did_not_fit_picks_no_extra_items(bool success, bool insufficientRoom)
    {
        var target = Stick(10);
        NearbyStick(11, 1f);
        Click(target);
        var effect = Effect(target);

        var result = Finish(effect, new Result { success = success, insufficientRoom = insufficientRoom, amount = 1, position = Vector3.one });

        Assert.Empty(effect.Inits);
        Assert.Equal(1, result.amount);
        Assert.Null(Plan);
    }

    [Fact]
    public void The_search_runs_around_the_position_of_the_result()
    {
        var target = Stick(10, 50f);
        var near = NearbyStick(11, 3f);
        Click(target);
        var effect = Effect(target);

        Finish(effect, new Result { success = true, amount = 1, position = new Vector3(2f, 0f, 0f) });

        Assert.Equal(new[] { near }, effect.Executed);
    }

    [Fact]
    public void A_zero_result_position_falls_back_to_the_target_position()
    {
        var target = Stick(10, 50f);
        var near = NearbyStick(11, 51f);
        NearbyStick(12, 1f);
        Click(target);
        var effect = Effect(target);

        Finish(effect, new Result { success = true, amount = 1, position = Vector3.zero });

        Assert.Equal(new[] { near }, effect.Executed);
    }

    [Fact]
    public void The_amount_of_the_result_is_the_sum_of_the_group()
    {
        var target = Stick(10);
        NearbyStick(11, 1f);
        NearbyStick(12, 1.5f);
        Click(target);
        var effect = Effect(target);
        effect.OnExecute = _ => new Result { success = true, amount = 2 };

        var result = Finish(effect, Fitted(amount: 3));

        Assert.Equal(7, result.amount);
    }

    [Fact]
    public void A_label_group_picks_the_members_of_the_click_with_no_search()
    {
        var scene = new LabelScene();
        var a = scene.Item(10, "stick", new Vector3(0f, 0f, 0f), new Vector3(100f, 100f, 0f));
        var b = scene.Item(11, "stick", new Vector3(1f, 0f, 0f), new Vector3(100f, 100f, 0f));
        scene.Frame();
        LabelPatches.LocalPlayerEntity = scene.Player;
        Start(InteractionType.PICKUP, scene.Player, a);
        var effect = Effect(a, scene.Player);

        var result = Finish(effect, Fitted());

        Assert.Equal(new[] { b }, effect.Executed);
        Assert.Equal(2, result.amount);
        Assert.Equal(0, Physics.Calls);
    }

    [Fact]
    public void The_extra_items_do_not_run_the_postfix_a_second_time()
    {
        var target = Stick(10);
        NearbyStick(11, 1f);
        NearbyStick(12, 1.5f);
        Click(target);
        var effect = Effect(target);
        // The first extra item has a plan of its own, so only the guard for the extra items can stop a second run.
        effect.OnExecute = e =>
        {
            var inner = Fitted();
            if (e.Executed.Count == 1)
            {
                Click(e.GetTargetEntity());
                inner = Finish(e, inner);
            }
            return inner;
        };

        var result = Finish(effect, Fitted());

        Assert.Equal(2, effect.Executed.Count);
        Assert.Equal(3, result.amount);
    }

    [Fact]
    public void An_exception_in_the_group_pickup_goes_to_WarnOnce()
    {
        var target = Stick(10);
        Click(target);
        Plugin.PickupRadius = null;

        var result = Finish(Effect(target), Fitted());

        Assert.Equal(1, result.amount);
        Assert.Contains(Plugin.Log.Warnings, line => line.StartsWith("Group pickup: "));
    }

    // PickExtras

    [Fact]
    public void A_destroyed_item_and_a_null_item_are_skipped()
    {
        var target = Stick(10);
        var gone = Stick(11);
        gone.destroyed = true;
        var kept = Stick(12);
        var effect = Effect(target);

        int picked = PickExtras(effect, player, target, new List<Entity> { null, gone, kept }, out _);

        Assert.Equal(1, picked);
        Assert.Equal(new[] { kept }, effect.Executed);
    }

    [Fact]
    public void An_item_for_the_hands_and_an_item_with_no_stack_are_skipped()
    {
        var target = Stick(10);
        var hands = Stick(11, prefersEquipped: true);
        var empty = LabelScene.NewEntity(12);
        var kept = Stick(13);
        var effect = Effect(target);

        PickExtras(effect, player, target, new List<Entity> { hands, empty, kept }, out _);

        Assert.Equal(new[] { kept }, effect.Executed);
    }

    [Fact]
    public void An_equippable_item_with_no_room_stops_the_run_with_no_Init_for_it()
    {
        player.SetCached(new IInventoryHolder { Inventory = new Inventory { Room = false } });
        var target = Stick(10);
        var first = Stick(11);
        var equippable = Stick(12, tags: ItemTag.EQUIPPABLE);
        var last = Stick(13);
        var effect = Effect(target);

        int picked = PickExtras(effect, player, target, new List<Entity> { first, equippable, last }, out _);

        Assert.Equal(1, picked);
        Assert.Equal(new[] { first }, effect.Executed);
        Assert.DoesNotContain(equippable, effect.Inits);
    }

    [Fact]
    public void An_equippable_item_with_no_inventory_stops_the_run()
    {
        var target = Stick(10);
        var effect = Effect(target);

        int picked = PickExtras(effect, player, target, new List<Entity> { Stick(11, tags: ItemTag.EQUIPPABLE) }, out _);

        Assert.Equal(0, picked);
        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void An_equippable_item_with_room_is_picked()
    {
        player.SetCached(new IInventoryHolder { Inventory = new Inventory { Room = true } });
        var target = Stick(10);
        var equippable = Stick(11, tags: ItemTag.EQUIPPABLE);
        var effect = Effect(target);

        PickExtras(effect, player, target, new List<Entity> { equippable }, out _);

        Assert.Equal(new[] { equippable }, effect.Executed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void An_item_that_does_not_fit_gets_OnExecuted_and_stops_the_run(bool success, bool insufficientRoom)
    {
        var target = Stick(10);
        var fits = Stick(11);
        var tooMuch = Stick(12);
        var last = Stick(13);
        var effect = Effect(target);
        var refused = new Result { success = success, insufficientRoom = insufficientRoom, amount = 4 };
        effect.OnExecute = e => e.GetTargetEntity() == tooMuch ? refused : new Result { success = true, amount = 2 };

        int picked = PickExtras(effect, player, target, new List<Entity> { fits, tooMuch, last }, out int amount);

        Assert.Equal(1, picked);
        Assert.Equal(2, amount);
        Assert.Equal(new[] { fits, tooMuch }, effect.Executed);
        Assert.Equal(new[] { refused }, effect.Finished);
    }

    [Fact]
    public void An_item_that_fits_gets_no_OnExecuted_of_its_own()
    {
        var target = Stick(10);
        var effect = Effect(target);

        PickExtras(effect, player, target, new List<Entity> { Stick(11) }, out _);

        Assert.Empty(effect.Finished);
    }

    [Fact]
    public void Each_extra_item_uses_the_interaction_and_the_action_of_the_effect()
    {
        var target = Stick(10);
        var effect = Effect(target);
        effect.Action = (ActionType)7;
        var interaction = effect.Interaction;
        IInteraction seen = null;
        ActionType seenAction = ActionType.DEFAULT;
        effect.OnExecute = e => { seen = e.GetInteraction(); seenAction = e.GetActionType(); return Fitted(); };

        PickExtras(effect, player, target, new List<Entity> { Stick(11) }, out _);

        Assert.Same(interaction, seen);
        Assert.Equal((ActionType)7, seenAction);
    }

    [Fact]
    public void The_effect_goes_back_to_the_target()
    {
        var target = Stick(10);
        var effect = Effect(target);

        PickExtras(effect, player, target, new List<Entity> { Stick(11) }, out _);

        Assert.Same(target, effect.GetTargetEntity());
        Assert.False(StaticState.Get<bool>(typeof(PickupPatches), "inner"));
    }

    [Fact]
    public void The_effect_goes_back_to_the_target_after_an_exception()
    {
        var target = Stick(10);
        var effect = Effect(target);
        effect.OnExecute = _ => throw new InvalidOperationException("broken");

        Assert.Throws<InvalidOperationException>(() => PickExtras(effect, player, target, new List<Entity> { Stick(11) }, out _));

        Assert.Same(target, effect.GetTargetEntity());
        Assert.False(StaticState.Get<bool>(typeof(PickupPatches), "inner"));
    }

    // LabelExtras

    private static List<Entity> LabelExtras(int targetId, List<Entity> members)
    {
        return (List<Entity>)PatchCall.Run(typeof(PickupPatches), "LabelExtras", targetId, members);
    }

    [Fact]
    public void The_label_extras_leave_out_the_target_and_each_destroyed_member()
    {
        var target = Stick(10);
        var gone = Stick(11);
        gone.destroyed = true;
        var kept = Stick(12);

        Assert.Equal(new[] { kept }, LabelExtras(10, new List<Entity> { target, gone, null, kept }));
    }

    [Fact]
    public void The_label_extras_leave_out_an_item_for_the_hands()
    {
        Assert.Empty(LabelExtras(10, new List<Entity> { Stick(10), Stick(11, prefersEquipped: true) }));
    }

    // FindWithinRadius

    [Fact]
    public void A_collider_maps_to_its_entity()
    {
        var stick = NearbyStick(11, 1f);

        var found = FindWithinRadius(entities, Stick(10), Vector3.zero, out int colliders);

        Assert.Equal(new[] { stick }, found);
        Assert.Equal(1, colliders);
    }

    [Fact]
    public void The_search_covers_the_center_and_the_radius_on_all_layers_with_triggers()
    {
        Plugin.PickupRadius.Value = 3f;

        FindWithinRadius(entities, Stick(10), new Vector3(4f, 5f, 6f), out _);

        Assert.Equal((new Vector3(4f, 5f, 6f), 3f, ~0, QueryTriggerInteraction.Collide), Physics.LastCall);
    }

    [Fact]
    public void A_collider_with_no_entity_maps_to_the_entity_of_its_rigidbody()
    {
        var stick = Stick(11, 1f);
        var body = new GameObject("body").AddComponent<Rigidbody>();
        entities.Entities[body.gameObject] = stick;
        var collider = new GameObject("child").AddComponent<Collider>();
        collider.attachedRigidbody = body;
        Physics.Hits.Add(collider);

        Assert.Equal(new[] { stick }, FindWithinRadius(entities, Stick(10), Vector3.zero, out _));
    }

    [Fact]
    public void A_collider_with_no_entity_and_no_rigidbody_is_skipped()
    {
        Physics.Hits.Add(new GameObject("wall").AddComponent<Collider>());
        Assert.Empty(FindWithinRadius(entities, Stick(10), Vector3.zero, out _));
    }

    [Fact]
    public void One_entity_with_several_colliders_counts_once()
    {
        var stick = NearbyStick(11, 1f);
        ColliderOn(stick);

        var found = FindWithinRadius(entities, Stick(10), Vector3.zero, out int colliders);

        Assert.Equal(new[] { stick }, found);
        Assert.Equal(2, colliders);
    }

    [Fact]
    public void A_destroyed_collider_and_a_destroyed_entity_are_skipped()
    {
        ColliderOn(Stick(11, 1f)).UnityDestroyed = true;
        NearbyStick(12, 1f).destroyed = true;

        Assert.Empty(FindWithinRadius(entities, Stick(10), Vector3.zero, out _));
    }

    [Fact]
    public void An_entity_past_the_radius_is_left_out()
    {
        NearbyStick(11, 2.5f);
        Assert.Empty(FindWithinRadius(entities, Stick(10), Vector3.zero, out _));
    }

    [Fact]
    public void A_radius_of_zero_runs_no_search()
    {
        Plugin.PickupRadius.Value = 0f;
        NearbyStick(11, 0f);

        Assert.Empty(FindWithinRadius(entities, Stick(10), Vector3.zero, out _));
        Assert.Equal(0, Physics.Calls);
    }

    [Fact]
    public void No_entity_manager_runs_no_search()
    {
        NearbyStick(11, 0f);

        Assert.Empty(FindWithinRadius(null, Stick(10), Vector3.zero, out _));
        Assert.Equal(0, Physics.Calls);
    }

    // Candidate and GoesIntoHands

    [Fact]
    public void An_entity_with_no_stack_has_no_kind()
    {
        Assert.Null(PickupPatches.Candidate(LabelScene.NewEntity(10)).Kind);
    }

    [Fact]
    public void A_stack_with_no_item_has_no_kind()
    {
        var entity = LabelScene.NewEntity(10);
        entity.SetCached(new IPickupable { Stack = new InventoryItemStack() });
        Assert.Null(PickupPatches.Candidate(entity).Kind);
    }

    [Fact]
    public void A_plain_item_does_not_go_into_the_hands()
    {
        var candidate = PickupPatches.Candidate(Stick(10));
        Assert.Equal("stick", candidate.Kind);
        Assert.False(candidate.PrefersEquippedSlot);
    }

    [Fact]
    public void A_stack_that_prefers_an_equipped_slot_goes_into_the_hands()
    {
        Assert.True(PickupPatches.Candidate(Stick(10, prefersEquipped: true)).PrefersEquippedSlot);
    }

    [Theory]
    [InlineData(ItemTag.TWO_HANDED)]
    [InlineData(ItemTag.WORN_ON_BACK)]
    public void A_tag_for_the_hands_goes_into_the_hands(ItemTag tag)
    {
        Assert.True(PickupPatches.Candidate(Stick(10, tags: tag)).PrefersEquippedSlot);
    }

    [Fact]
    public void The_tag_for_an_equippable_item_alone_does_not_go_into_the_hands()
    {
        Assert.False(PickupPatches.Candidate(Stick(10, tags: ItemTag.EQUIPPABLE)).PrefersEquippedSlot);
    }
}
