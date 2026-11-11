using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using ProximityPickup.Labels;

namespace ProximityPickup.Pickup
{
    // The group gather. The plan is fixed when the local player starts a gather (the click), and the extra resources
    // go through the game's own GatherEffect after the game gathered the target, so the game's drops, XP, removal,
    // and regrowth apply to each resource.
    [HarmonyPatch]
    internal static class GatherPatches
    {
        private sealed class StoredPlan
        {
            public int TargetId;
            public string Kind;
            public PickupSource Source;
            // The resources of the label at the click. Null when the target has no label.
            public List<Entity> Members;
            // The place of the target at the click: a gather can remove the target.
            public UnityEngine.Vector3 Center;
            public IEntityManager EntityManager;
        }

        // The plan of the last gather that the local player started. Each start on another target replaces it.
        private static StoredPlan current;

        // True while the postfix runs the effect for the extra resources, so it does not run for them again.
        private static bool inner;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InteractionManager), nameof(InteractionManager.StartInteraction),
            new[] { typeof(InteractionType), typeof(InteractionInputType), typeof(ActionType), typeof(Entity), typeof(InteractionSource), typeof(Entity) })]
        private static void StartInteractionPrefix(InteractionManager __instance, InteractionType type, Entity interactorEntity, Entity targetEntity)
        {
            try
            {
                if (type != InteractionType.GATHER || interactorEntity == null || targetEntity == null) return;
                var local = LabelPatches.LocalPlayerEntity;
                if (local == null || interactorEntity.id != local.id) return;
                // A middle-click gathers one resource, also in place of an open group plan of the same target.
                bool takeOne = ClickPatches.TakeOne(targetEntity);
                // The game starts the same gather again while the character walks to the resource. The plan of the
                // click stays.
                if (!takeOne && current != null && current.TargetId == targetEntity.id) return;

                var target = Candidate(targetEntity, __instance.inventoryManager);
                if (target.Kind == null) return;
                bool hasLabel = LabelPatches.TryGetLabelMembers(targetEntity.id, out var members);
                var source = takeOne ? PickupSource.None : GroupPickupLogic.Plan(Plugin.PickMode, true, target, hasLabel);
                current = new StoredPlan
                {
                    TargetId = targetEntity.id, Kind = target.Kind, Source = source, Members = members,
                    Center = targetEntity.transform.position, EntityManager = __instance.entityManager,
                };
                Plugin.Debug($"Gather plan: target {targetEntity.id}, kind {target.Kind}, source {source}, {members?.Count ?? 0} label members");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Gather start", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GatherInteraction), "TeardownCommon")]
        private static void TeardownPostfix(GatherInteraction __instance)
        {
            try
            {
                // The teardown runs after the effect and once for each gather, so a plan that is still open here
                // belongs to a cancelled gather, and a later click must not reuse it.
                var target = __instance.targetEntity;
                bool open = current != null && target != null && current.TargetId == target.id;
                if (open) current = null;
                Plugin.Debug($"Gather teardown: target {(target != null ? target.id.ToString() : "none")}, plan {(open ? "cleared" : "none")}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Gather teardown", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GatherEffect), nameof(GatherEffect.ExecuteResult))]
        private static void ExecuteResultPostfix(GatherEffect __instance, ref GatherEffect.GatherEffectResult __result)
        {
            if (inner) return;
            try
            {
                var interactor = __instance.GetInteractorEntity();
                var target = __instance.GetTargetEntity();
                var local = LabelPatches.LocalPlayerEntity;
                if (interactor == null || target == null || local == null || interactor.id != local.id) return;
                if (current == null || current.TargetId != target.id) return;
                var plan = current;
                current = null;
                if (plan.Source == PickupSource.None) return;
                if (!GroupGatherLogic.ShouldGroupGather(Plugin.PickMode, Plugin.PickupRadius.Value, true, interactor.isHostEntity)) return;
                if (!__result.success || __result.insufficientSpace)
                {
                    Plugin.Debug($"Group gather: source {plan.Source}, kind {plan.Kind}, the target did not fit, 0 extra resources gathered");
                    return;
                }
                var inventoryManager = __instance.inventoryManager;
                int colliders = 0;
                var extras = plan.Source == PickupSource.LabelGroup
                    ? LabelExtras(target.id, plan.Members, inventoryManager)
                    : FindWithinRadius(plan.EntityManager, target.id, plan.Kind, plan.Center, inventoryManager, out colliders);
                GatherExtras(__instance, interactor, target, extras, $"source {plan.Source}, kind {plan.Kind}, {colliders} colliders", ref __result);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Group gather", e);
            }
        }

        // Runs the game's gather for each extra resource on the same effect, up to the first one with no room in
        // the bag, and adds the item counts of each gather to the result of the target, so the game shows one
        // message.
        private static void GatherExtras(GatherEffect effect, Entity interactor, Entity target, List<Entity> extras, string plan, ref GatherEffect.GatherEffectResult first)
        {
            var clock = Stopwatch.StartNew();
            var interaction = effect.GetInteraction();
            var actionType = effect.GetActionType();
            var inventoryManager = effect.inventoryManager;
            var inventory = interactor.GetCachedComponent<IInventoryHolder>(false)?.GetInventory();
            float bonus = interactor.GetCachedComponent<ISkills>(false)?.GetForageBonusAmount() ?? 0f;
            int gathered = 0;
            string stop = "none";
            inner = true;
            try
            {
                // Checked again just before the gather: a resource can have changed since the click.
                var quick = extras.FindAll(extra => extra != null && !extra.destroyed && !Candidate(extra, inventoryManager).PrefersEquippedSlot);
                foreach (var extra in GroupGatherLogic.TakeUntilNoRoom(quick, extra => HasRoom(inventory, inventoryManager, extra, bonus)))
                {
                    effect.Init(interaction, interactor, extra, actionType);
                    var result = effect.ExecuteResult();
                    if (!result.success) continue;
                    gathered++;
                    if (result.insufficientSpace)
                    {
                        // A herb also moves its own items, which the room check leaves out. The items that do not fit
                        // stay in the plant, and the game shows its message.
                        effect.OnExecuted(result);
                        stop = "an extra did not fit";
                        break;
                    }
                    if (result.itemCounts == null) continue;
                    if (NativeItemCounts.Capacity(first.itemCounts) == 0) first.itemCounts = result.itemCounts;
                    else NativeItemCounts.AddCounts(first.itemCounts, result.itemCounts);
                }
                if (stop == "none" && gathered < quick.Count) stop = "no room";
                Plugin.Debug($"Group gather: {plan}, {extras.Count} found, {quick.Count} quick, gathered {gathered}, stop {stop}, " +
                             $"sum {NativeItemCounts.Sum(first.itemCounts)}, {clock.ElapsedMilliseconds} ms");
            }
            finally
            {
                effect.Init(interaction, interactor, target, actionType);
                inner = false;
            }
        }

        // True when the bag has room for each drop of the resource, with the forage bonus of the player.
        private static bool HasRoom(Inventory inventory, IInventoryManager inventoryManager, Entity resource, float bonus)
        {
            var drops = DropsOf(resource, resource.GetCachedComponent<IGatherable>(false));
            if (inventory == null || inventoryManager == null) return false;
            var amounts = new List<(string Item, int Amount)>();
            for (int i = 0; drops != null && i < drops.Count; i++) amounts.Add((drops[i].itemIdentifier, drops[i].amount));
            foreach (var (item, amount) in GroupGatherLogic.RoomNeeded(amounts, bonus))
            {
                var probe = inventoryManager.GetInventoryItemStack(item, 1);
                int free = probe?.Item == null ? 0 : FreeRoom(inventory, probe);
                bool room = free >= amount;
                Plugin.Debug($"Group gather: room for {resource.id} (bonus {bonus:0.##}): {item} {amount} of {free} {(room ? "yes" : "no")}");
                if (!room) return false;
            }
            return true;
        }

        // How many of the item the bag can take: the missing amount of each stack of the item, and a full stack for
        // each empty slot that takes the item. Inventory.HasRoom is true when a slot takes one more, so it cannot
        // tell that a drop of several fits. The hands never count, so an extra never changes them. The slots of an
        // inventory section never count: the gather adds its drops with no section priority. A cell under an item
        // larger than one slot is not empty. An item larger than one slot counts only the stacks, so the count can be low,
        // never high.
        private static int FreeRoom(Inventory inventory, InventoryItemStack probe)
        {
            var item = probe.Item;
            bool oneSlot = item.Width <= 1 && item.Height <= 1;
            int free = 0;
            var counted = new HashSet<IntPtr>();
            var slots = inventory.GetAllSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.inventorySection != null || slot.IsHandheld() || !slot.AcceptsItems() || !slot.IsItemAllowed(item, true)) continue;
                var stack = slot.GetStack();
                if (stack == null)
                {
                    // A cell of an item larger than one slot has no stack of its own, but the item covers it.
                    if (oneSlot && slot.IsEmpty() && !slot.HasOriginStack()) free += probe.GetMaxStackAmount();
                }
                else if (stack.Item != null && stack.Item.Identifier == item.Identifier && counted.Add(stack.Pointer))
                {
                    free += Math.Max(0, stack.GetMaxStackAmount() - stack.Quantity);
                }
            }
            return free;
        }

        // The other resources of the label of the target.
        private static List<Entity> LabelExtras(int targetId, List<Entity> members, IInventoryManager inventoryManager)
        {
            var byId = new Dictionary<int, Entity>();
            var candidates = new List<PickupCandidate>();
            foreach (var member in members)
            {
                if (member == null || member.destroyed) continue;
                byId[member.id] = member;
                candidates.Add(Candidate(member, inventoryManager));
            }
            var extras = new List<Entity>();
            foreach (int id in GroupPickupLogic.LabelExtras(targetId, candidates)) extras.Add(byId[id]);
            return extras;
        }

        // The quick resources of the same kind within the pickup radius of the center: a physics overlap, each
        // collider mapped to its entity.
        internal static List<Entity> FindWithinRadius(IEntityManager entityManager, int targetId, string kind, UnityEngine.Vector3 center, IInventoryManager inventoryManager, out int colliders)
        {
            var extras = new List<Entity>();
            colliders = 0;
            float radius = Plugin.PickupRadius.Value;
            if (radius <= 0f || entityManager == null) return extras;

            var hits = UnityEngine.Physics.OverlapSphere(center, radius, ~0, UnityEngine.QueryTriggerInteraction.Collide);
            colliders = hits.Length;
            var byId = new Dictionary<int, Entity>();
            var found = new List<RadiusCandidate>();
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var entity = entityManager.GetEntity(hit.gameObject);
                if (entity == null && hit.attachedRigidbody != null) entity = entityManager.GetEntity(hit.attachedRigidbody.gameObject);
                if (entity == null || entity.destroyed || byId.ContainsKey(entity.id)) continue;
                byId[entity.id] = entity;
                found.Add(new RadiusCandidate(Candidate(entity, inventoryManager), UnityEngine.Vector3.Distance(entity.transform.position, center)));
            }

            foreach (int id in GroupPickupLogic.WithinRadius(new PickupCandidate(targetId, kind, false), found, radius)) extras.Add(byId[id]);
            return extras;
        }

        // A resource as a candidate of the group logic: its kind, and a quick resource as an item that stays out of
        // the hands, so that only quick resources are extras. The kind is null for an entity that is not a
        // resource.
        internal static PickupCandidate Candidate(Entity entity, IInventoryManager inventoryManager)
        {
            var resource = entity.GetCachedComponent<IResource>(false)?.GetResource();
            var gatherable = entity.GetCachedComponent<IGatherable>(false);
            if (resource == null || gatherable == null) return new PickupCandidate(entity.id, null, false);
            bool quick = GroupGatherLogic.IsQuick(gatherable.CanGather(), gatherable.GetGatherDuration(),
                gatherable.GatherContinuously(), DropsGoIntoHands(entity, gatherable, inventoryManager));
            return new PickupCandidate(entity.id, GroupGatherLogic.KindKey(resource.Name), !quick);
        }

        // The drops that the game gives for a gather: the drops of the resource, or the drops that a growable
        // resource accumulated.
        private static Il2CppSystem.Collections.Generic.List<ItemDrop> DropsOf(Entity entity, IGatherable gatherable)
        {
            var drops = gatherable.GetItemDrops();
            if (drops != null && drops.Count > 0) return drops;
            return entity.GetCachedComponent<IGrowable>(false)?.GetAccumulatedItemDrops();
        }

        // True when a drop can change the hands, by the rule of the group pickup. A drop of an item that the game
        // does not know counts as one, so that resource is gathered only as in the game.
        private static bool DropsGoIntoHands(Entity entity, IGatherable gatherable, IInventoryManager inventoryManager)
        {
            var drops = DropsOf(entity, gatherable);
            if (drops == null) return false;
            for (int i = 0; i < drops.Count; i++)
            {
                var drop = drops[i];
                var stack = inventoryManager?.GetInventoryItemStack(drop.itemIdentifier, Math.Max(1, drop.amount));
                if (stack?.Item == null || PickupPatches.GoesIntoHands(stack, stack.Item)) return true;
            }
            return false;
        }
    }
}
