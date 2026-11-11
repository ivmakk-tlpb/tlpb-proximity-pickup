using ProximityPickup;
using ProximityPickup.Labels;
using ProximityPickup.Pickup;
using UnityEngine;
using Xunit;
using Result = GatherEffect.GatherEffectResult;

public class GatherPatchesTests : GameTestBase
{
    private readonly Entity player = LabelScene.NewEntity(1);
    private readonly Entity other = LabelScene.NewEntity(2);
    private readonly IEntityManager entities = new IEntityManager();
    private readonly IInventoryManager items = new IInventoryManager();
    private readonly InteractionManager manager;

    public GatherPatchesTests()
    {
        LabelPatches.LocalPlayerEntity = player;
        items.Items["cap"] = new InventoryItem { Identifier = "cap", Name = "Cap" };
        items.Items["large_stone"] = new InventoryItem { Identifier = "large_stone", Name = "Large Stone", Tags = { ItemTag.TWO_HANDED } };
        manager = new InteractionManager { entityManager = entities, inventoryManager = items };
    }

    internal static Entity Resource(int id, string name, float x = 0f, bool ready = true, float duration = 0.8f, string drop = "cap", int amount = 1)
    {
        var entity = LabelScene.NewEntity(id, new Vector3(x, 0f, 0f));
        entity.SetCached(new IResource { Resource = new Resource { Name = name } });
        var gatherable = new IGatherable { Ready = ready, Duration = duration };
        gatherable.Drops.Add(new ItemDrop { itemIdentifier = drop, amount = amount });
        entity.SetCached(gatherable);
        return entity;
    }

    private static Entity Mushroom(int id, float x = 0f, bool ready = true) => Resource(id, "Spotted Red Mushroom", x, ready);

    private static object Plan => StaticState.Get<object>(typeof(GatherPatches), "current");

    private static PickupSource Source => StaticState.Member<PickupSource>(Plan, "Source");

    private static int TargetId => StaticState.Member<int>(Plan, "TargetId");

    private void Start(InteractionType type, Entity interactor, Entity target)
    {
        PatchCall.Run(typeof(GatherPatches), "StartInteractionPrefix", manager, type, interactor, target);
    }

    private void Click(Entity target) => Start(InteractionType.GATHER, player, target);

    private static void Teardown(Entity target)
    {
        PatchCall.Run(typeof(GatherPatches), "TeardownPostfix", new GatherInteraction { targetEntity = target });
    }

    // StartInteractionPrefix

    [Fact]
    public void A_gather_of_a_quick_resource_with_no_label_plans_the_radius()
    {
        Click(Mushroom(10));

        Assert.Equal(10, TargetId);
        Assert.Equal(PickupSource.Radius, Source);
        Assert.Equal("resource:Spotted Red Mushroom", StaticState.Member<string>(Plan, "Kind"));
    }

    [Fact]
    public void Another_interaction_type_makes_no_plan()
    {
        Start(InteractionType.PICKUP, player, Mushroom(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_gather_of_another_character_makes_no_plan()
    {
        Start(InteractionType.GATHER, other, Mushroom(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_null_interactor_or_target_makes_no_plan()
    {
        Start(InteractionType.GATHER, null, Mushroom(10));
        Start(InteractionType.GATHER, player, null);
        Assert.Null(Plan);
    }

    [Fact]
    public void No_local_player_makes_no_plan()
    {
        LabelPatches.LocalPlayerEntity = null;
        Click(Mushroom(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_target_that_is_not_a_resource_makes_no_plan()
    {
        Click(LabelScene.NewEntity(10));
        Assert.Null(Plan);
    }

    [Fact]
    public void A_timed_gather_plans_no_extras()
    {
        Click(Resource(10, "Blueberry Bush", duration: 5f));
        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_resource_whose_drop_goes_into_the_hands_plans_no_extras()
    {
        Click(Resource(10, "Rock", drop: "large_stone"));
        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_drop_that_the_game_does_not_know_plans_no_extras()
    {
        Click(Resource(10, "Odd Plant", drop: "unknown"));
        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_continuous_gather_plans_no_extras()
    {
        var bush = Resource(10, "Bush");
        bush.GetCachedComponent<IGatherable>(false).Continuous = true;
        Click(bush);
        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void The_pick_mode_One_plans_no_extras()
    {
        Plugin.PickModeEntry.Value = "One";
        Click(Mushroom(10));
        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_growable_resource_with_no_own_drops_uses_its_accumulated_drops()
    {
        var herb = Resource(10, "Chamomile");
        herb.GetCachedComponent<IGatherable>(false).Drops = new Il2CppSystem.Collections.Generic.List<ItemDrop>();
        var growable = new IGrowable();
        growable.Accumulated.Add(new ItemDrop { itemIdentifier = "large_stone" });
        herb.SetCached(growable);

        Click(herb);

        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_target_with_a_label_plans_the_members_of_the_label()
    {
        var scene = new LabelScene();
        var a = scene.Item(10, "stick", new Vector3(0f, 0f, 0f), new Vector3(100f, 100f, 0f));
        var b = scene.Item(11, "stick", new Vector3(1f, 0f, 0f), new Vector3(100f, 100f, 0f));
        var resource = Resource(99, "Stick");
        a.SetCached(resource.GetCachedComponent<IResource>(false));
        a.SetCached(resource.GetCachedComponent<IGatherable>(false));
        scene.Frame();
        LabelPatches.LocalPlayerEntity = scene.Player;

        Start(InteractionType.GATHER, scene.Player, a);

        Assert.Equal(PickupSource.LabelGroup, Source);
        Assert.Equal(new[] { 10, 11 }, StaticState.Member<List<Entity>>(Plan, "Members").Select(e => e.id));
    }

    [Fact]
    public void A_restart_on_the_same_target_keeps_the_plan()
    {
        var target = Mushroom(10);
        Click(target);
        var first = Plan;
        Plugin.PickModeEntry.Value = "One";

        Click(target);

        Assert.Same(first, Plan);
        Assert.Equal(PickupSource.Radius, Source);
    }

    [Fact]
    public void A_start_on_another_target_replaces_the_plan()
    {
        Click(Mushroom(10));
        Click(Mushroom(11));
        Assert.Equal(11, TargetId);
    }

    [Fact]
    public void A_start_on_the_take_one_target_plans_one_resource_and_clears_the_target()
    {
        var target = Mushroom(10);
        ClickPatches.TakeOneTarget = target;

        Click(target);

        Assert.Equal(PickupSource.None, Source);
        Assert.Null(ClickPatches.TakeOneTarget);
    }

    [Fact]
    public void A_restart_on_the_same_target_after_a_middle_click_stays_one_resource()
    {
        var target = Mushroom(10);
        ClickPatches.TakeOneTarget = target;
        Click(target);

        Click(target);

        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_middle_click_on_a_target_with_an_open_group_plan_changes_it_to_one_resource()
    {
        var target = Mushroom(10);
        Click(target);
        Assert.Equal(PickupSource.Radius, Source);
        ClickPatches.TakeOneTarget = target;

        Click(target);

        Assert.Equal(PickupSource.None, Source);
    }

    [Fact]
    public void A_start_by_another_character_keeps_the_take_one_target()
    {
        var target = Mushroom(10);
        ClickPatches.TakeOneTarget = target;

        Start(InteractionType.GATHER, other, target);
        Start(InteractionType.PICKUP, player, target);

        Assert.Same(target, ClickPatches.TakeOneTarget);
    }

    // TeardownPostfix

    [Fact]
    public void The_teardown_of_the_planned_gather_clears_the_plan()
    {
        var target = Mushroom(10);
        Click(target);
        Teardown(target);
        Assert.Null(Plan);
    }

    [Fact]
    public void The_teardown_of_another_gather_keeps_the_plan()
    {
        Click(Mushroom(10));
        Teardown(Mushroom(11));
        Teardown(null);
        Assert.Equal(10, TargetId);
    }

    // ExecuteResultPostfix

    private const int CapId = 7;

    private readonly Inventory bag = new Inventory();
    private readonly ISkills skills = new ISkills();

    // The bag has this count of empty slots, and nothing else.
    private void FreeSlots(int count)
    {
        bag.Slots.Clear();
        for (int i = 0; i < count; i++) bag.Slots.Add(new InventorySlot());
    }

    // The game puts the cap of a gather into an empty slot.
    private Result GatherOneCap()
    {
        var slot = bag.Slots.FirstOrDefault(s => s.IsEmpty());
        if (slot != null) slot.Stack = new InventoryItemStack { Item = items.Items["cap"] };
        return new Result { success = true, itemCounts = NativeCounts.Make((CapId, 1)) };
    }

    private void Equip()
    {
        if (bag.Slots.Count == 0) FreeSlots(10);
        player.SetCached(new IInventoryHolder { Inventory = bag });
        player.SetCached(skills);
    }

    private Entity NearbyMushroom(int id, float x, bool ready = true)
    {
        var mushroom = Mushroom(id, x, ready);
        var collider = new GameObject("collider").AddComponent<Collider>();
        entities.Entities[collider.gameObject] = mushroom;
        Physics.Hits.Add(collider);
        return mushroom;
    }

    // Each gather gives one cap, as the game's result of one mushroom.
    private GatherEffect Effect(Entity target, Entity interactor = null)
    {
        Equip();
        return new GatherEffect
        {
            Interactor = interactor ?? player, Target = target, inventoryManager = items,
            OnExecute = _ => GatherOneCap(),
        };
    }

    private static Result Gathered(int caps = 1) => new Result { success = true, itemCounts = NativeCounts.Make((CapId, caps)) };

    private static Result Finish(GatherEffect effect, Result result)
    {
        var args = new object[] { effect, result };
        PatchCall.Run(typeof(GatherPatches), "ExecuteResultPostfix", args);
        return (Result)args[1];
    }

    // The target at x 0, the extras of the radius search at x 1 and x 1.5.
    private (Entity Target, GatherEffect Effect) RadiusGather()
    {
        var target = Mushroom(10);
        NearbyMushroom(11, 1f);
        NearbyMushroom(12, 1.5f);
        Click(target);
        return (target, Effect(target));
    }

    [Fact]
    public void A_gather_with_no_label_gathers_the_quick_resources_of_the_kind_in_the_radius()
    {
        var target = Mushroom(10);
        NearbyMushroom(11, 1f);
        NearbyMushroom(12, 1.5f);
        NearbyMushroom(13, 3f);
        NearbyMushroom(14, 1f, ready: false);
        var garlic = Resource(15, "Wild Garlic", 1f);
        var collider = new GameObject("collider").AddComponent<Collider>();
        entities.Entities[collider.gameObject] = garlic;
        Physics.Hits.Add(collider);
        Click(target);
        var effect = Effect(target);

        var result = Finish(effect, Gathered());

        Assert.Equal(new[] { 11, 12 }, effect.Executed.Select(e => e.id));
        Assert.Equal(new[] { (CapId, 3) }, NativeCounts.Read(result.itemCounts));
        Assert.Same(target, effect.Target);
        Assert.Null(Plan);
    }

    [Fact]
    public void The_radius_search_starts_at_the_place_of_the_target_at_the_click()
    {
        var (target, effect) = RadiusGather();
        target.transform.position = new Vector3(50f, 0f, 0f);

        Finish(effect, Gathered());

        Assert.Equal(Vector3.zero, Physics.LastCall.Position);
        Assert.Equal(2, effect.Executed.Count);
    }

    [Fact]
    public void A_gather_from_a_label_gathers_the_other_members_of_the_label()
    {
        var target = Mushroom(10);
        var member = Mushroom(11, 30f);
        var gone = Mushroom(12, 31f);
        gone.destroyed = true;
        Click(target);
        SetPlan(PickupSource.LabelGroup, target, member, gone, null);
        var effect = Effect(target);

        Finish(effect, Gathered());

        Assert.Equal(new[] { 11 }, effect.Executed.Select(e => e.id));
    }

    private static void SetPlan(PickupSource source, params Entity[] members)
    {
        var plan = Plan;
        plan.GetType().GetField("Source").SetValue(plan, source);
        plan.GetType().GetField("Members").SetValue(plan, members.ToList());
    }

    [Fact]
    public void A_member_that_is_no_longer_quick_stays()
    {
        var target = Mushroom(10);
        var picked = Mushroom(11, 1f, ready: false);
        Click(target);
        SetPlan(PickupSource.LabelGroup, target, picked);
        var effect = Effect(target);

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_gather_with_no_plan_gathers_nothing_more()
    {
        var target = Mushroom(10);
        NearbyMushroom(11, 1f);
        var effect = Effect(target);

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_gather_of_another_target_than_the_plan_gathers_nothing_more()
    {
        RadiusGather();
        var effect = Effect(Mushroom(20));

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
        Assert.NotNull(Plan);
    }

    [Fact]
    public void A_gather_of_another_character_gathers_nothing_more()
    {
        var (target, _) = RadiusGather();
        var effect = Effect(target, other);

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_null_target_gathers_nothing_more()
    {
        RadiusGather();
        var effect = Effect(null);

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_plan_of_no_extras_gathers_nothing_more()
    {
        Plugin.PickModeEntry.Value = "One";
        var (_, effect) = RadiusGather();

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
        Assert.Null(Plan);
    }

    [Fact]
    public void A_radius_of_0_after_the_click_gathers_nothing_more()
    {
        var (_, effect) = RadiusGather();
        Plugin.PickupRadius.Value = 0f;

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_player_who_is_not_the_host_gathers_nothing_more()
    {
        var (_, effect) = RadiusGather();
        player.isHostEntity = false;

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_failed_clicked_gather_gathers_nothing_more()
    {
        var (_, effect) = RadiusGather();

        Finish(effect, new Result { success = false });

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_clicked_gather_that_did_not_fit_gathers_nothing_more()
    {
        var (_, effect) = RadiusGather();

        Finish(effect, new Result { success = true, insufficientSpace = true, itemCounts = NativeCounts.Make() });

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_full_bag_stops_before_the_extra_with_no_room()
    {
        var (_, effect) = RadiusGather();
        FreeSlots(1);

        var result = Finish(effect, Gathered());

        Assert.Equal(new[] { 11 }, effect.Executed.Select(e => e.id));
        Assert.Equal(new[] { (CapId, 2) }, NativeCounts.Read(result.itemCounts));
        Assert.Empty(effect.Finished);
    }

    [Fact]
    public void The_room_check_counts_the_forage_bonus()
    {
        var (_, effect) = RadiusGather();
        FreeSlots(2);
        skills.ForageBonus = 0.5f;

        Finish(effect, Gathered());

        Assert.Equal(new[] { 11 }, effect.Executed.Select(e => e.id));
    }

    // The room in the bag

    private void PutStack(string item, int quantity, int max)
    {
        items.Items[item].MaxStack = max;
        bag.Slots.Add(new InventorySlot { Stack = new InventoryItemStack { Item = items.Items[item], Quantity = quantity } });
    }

    private bool HasRoomForOneMushroom()
    {
        var (_, effect) = RadiusGather();
        Finish(effect, Gathered());
        return effect.Executed.Count > 0;
    }

    [Fact]
    public void A_stack_of_the_item_that_is_not_full_is_room()
    {
        FreeSlots(0);
        PutStack("cap", 3, 4);
        Assert.True(HasRoomForOneMushroom());
    }

    [Fact]
    public void A_full_stack_of_the_item_is_no_room()
    {
        FreeSlots(0);
        PutStack("cap", 4, 4);
        Assert.False(HasRoomForOneMushroom());
    }

    [Fact]
    public void A_stack_of_another_item_is_no_room()
    {
        FreeSlots(0);
        PutStack("large_stone", 1, 4);
        Assert.False(HasRoomForOneMushroom());
    }

    [Fact]
    public void The_room_of_a_stack_is_too_small_for_more_drops()
    {
        var mushroom = Resource(10, "Spotted Red Mushroom", amount: 3);
        var extra = Resource(11, "Spotted Red Mushroom", 1f, amount: 3);
        var collider = new GameObject("collider").AddComponent<Collider>();
        entities.Entities[collider.gameObject] = extra;
        Physics.Hits.Add(collider);
        Click(mushroom);
        var effect = Effect(mushroom);
        FreeSlots(0);
        PutStack("cap", 198, 200);

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_hand_slot_a_closed_slot_and_a_slot_that_refuses_the_item_are_no_room()
    {
        FreeSlots(0);
        bag.Slots.Add(new InventorySlot { Handheld = true });
        bag.Slots.Add(new InventorySlot { Accepts = false });
        bag.Slots.Add(new InventorySlot { Allowed = false });
        Assert.False(HasRoomForOneMushroom());
    }

    [Fact]
    public void An_empty_slot_under_a_large_item_is_no_room()
    {
        FreeSlots(0);
        bag.Slots.Add(new InventorySlot { Covered = true });
        Assert.False(HasRoomForOneMushroom());
    }

    [Fact]
    public void A_slot_of_an_inventory_section_is_no_room()
    {
        FreeSlots(0);
        bag.Slots.Add(new InventorySlot { inventorySection = new IInventorySection() });
        Assert.False(HasRoomForOneMushroom());
    }

    [Fact]
    public void An_empty_slot_is_no_room_for_an_item_larger_than_one_slot()
    {
        FreeSlots(3);
        items.Items["cap"].Width = 2;
        Assert.False(HasRoomForOneMushroom());
    }

    [Fact]
    public void A_stack_that_covers_several_slots_counts_once()
    {
        FreeSlots(0);
        PutStack("cap", 3, 4);
        bag.Slots.Add(new InventorySlot { Stack = bag.Slots[0].Stack });
        var mushroom = Resource(10, "Spotted Red Mushroom", amount: 2);
        var extra = Resource(11, "Spotted Red Mushroom", 1f, amount: 2);
        var collider = new GameObject("collider").AddComponent<Collider>();
        entities.Entities[collider.gameObject] = extra;
        Physics.Hits.Add(collider);
        Click(mushroom);
        var effect = Effect(mushroom);

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_drop_that_the_game_does_not_know_is_no_room()
    {
        var mushroom = Resource(10, "Spotted Red Mushroom");
        var extra = Resource(11, "Spotted Red Mushroom", 1f);
        var collider = new GameObject("collider").AddComponent<Collider>();
        entities.Entities[collider.gameObject] = extra;
        Physics.Hits.Add(collider);
        Click(mushroom);
        var effect = Effect(mushroom);
        items.Items.Remove("cap");

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void A_player_with_no_bag_gathers_nothing_more()
    {
        var (_, effect) = RadiusGather();
        player.SetCached<IInventoryHolder>(null);

        Finish(effect, Gathered());

        Assert.Empty(effect.Executed);
    }

    [Fact]
    public void An_extra_that_does_not_fit_shows_the_game_message_and_stops()
    {
        var (_, effect) = RadiusGather();
        effect.OnExecute = _ => new Result { success = true, insufficientSpace = true, itemCounts = NativeCounts.Make((CapId, 1)) };

        var result = Finish(effect, Gathered());

        Assert.Equal(new[] { 11 }, effect.Executed.Select(e => e.id));
        Assert.Single(effect.Finished);
        Assert.Equal(new[] { (CapId, 1) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void A_failed_extra_adds_nothing_and_the_next_extra_runs()
    {
        var (_, effect) = RadiusGather();
        effect.OnExecute = e => e.Target.id == 11
            ? new Result { success = false }
            : new Result { success = true, itemCounts = NativeCounts.Make((CapId, 1)) };

        var result = Finish(effect, Gathered());

        Assert.Equal(new[] { 11, 12 }, effect.Executed.Select(e => e.id));
        Assert.Equal(new[] { (CapId, 2) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void A_clicked_result_with_no_counts_takes_the_counts_of_the_extras()
    {
        var (_, effect) = RadiusGather();

        var result = Finish(effect, new Result { success = true, itemCounts = NativeCounts.Make(0) });

        Assert.Equal(new[] { (CapId, 2) }, NativeCounts.Read(result.itemCounts));
    }

    [Fact]
    public void The_gathers_of_the_extras_do_not_start_a_group_gather_again()
    {
        var (_, effect) = RadiusGather();
        int depth = 0;
        effect.OnExecute = e =>
        {
            depth++;
            var inner = Gathered();
            Finish(e, inner);
            return inner;
        };

        Finish(effect, Gathered());

        Assert.Equal(2, depth);
        Assert.Equal(2, effect.Executed.Count);
    }

    [Fact]
    public void An_error_in_an_extra_restores_the_target()
    {
        var (target, effect) = RadiusGather();
        effect.OnExecute = _ => throw new InvalidOperationException("boom");

        Finish(effect, Gathered());

        Assert.Same(target, effect.Target);
        Assert.False(StaticState.Get<bool>(typeof(GatherPatches), "inner"));
    }

    // Verbose lines

    [Fact]
    public void The_plan_has_a_verbose_line()
    {
        Plugin.Verbose.Value = true;
        Click(Mushroom(10));
        Assert.Contains("Gather plan: target 10, kind resource:Spotted Red Mushroom, source Radius, 0 label members", Plugin.Log.Debugs);
    }

    [Fact]
    public void The_group_gather_has_a_verbose_line()
    {
        var (_, effect) = RadiusGather();
        Plugin.Verbose.Value = true;
        FreeSlots(1);

        Finish(effect, Gathered());

        Assert.Contains(Plugin.Log.Debugs, line => line.StartsWith("Group gather: source Radius, kind resource:Spotted Red Mushroom, 2 colliders, 2 found, 2 quick, gathered 1, stop no room, sum 2, ") && line.EndsWith(" ms"));
    }

    [Fact]
    public void Each_room_check_has_a_verbose_line()
    {
        var (_, effect) = RadiusGather();
        Plugin.Verbose.Value = true;
        skills.ForageBonus = 0.5f;
        FreeSlots(2);

        Finish(effect, Gathered());

        Assert.Contains("Group gather: room for 11 (bonus 0.5): cap 2 of 2 yes", Plugin.Log.Debugs);
        Assert.Contains("Group gather: room for 12 (bonus 0.5): cap 2 of 1 no", Plugin.Log.Debugs);
    }

    [Fact]
    public void A_clicked_gather_that_did_not_fit_has_a_verbose_line()
    {
        var (_, effect) = RadiusGather();
        Plugin.Verbose.Value = true;

        Finish(effect, new Result { success = false });

        Assert.Contains("Group gather: source Radius, kind resource:Spotted Red Mushroom, the target did not fit, 0 extra resources gathered", Plugin.Log.Debugs);
    }

    [Fact]
    public void The_teardown_has_a_verbose_line()
    {
        var target = Mushroom(10);
        Click(target);
        Plugin.Verbose.Value = true;

        Teardown(target);

        Assert.Contains("Gather teardown: target 10, plan cleared", Plugin.Log.Debugs);
    }
}
