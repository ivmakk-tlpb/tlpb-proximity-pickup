using System;
using System.Collections.Generic;
using HarmonyLib;
using ProximityPickup.Labels;

namespace ProximityPickup.Pickup
{
    // The group pickup. The plan is fixed when the local player starts a pickup (the click), and the extra items go
    // through the game's own PickupEffect after the game picked the target, so the game's inventory rules, messages,
    // and entity removal apply to each item.
    [HarmonyPatch]
    internal static class PickupPatches
    {
        private sealed class StoredPlan
        {
            public int TargetId;
            public string Kind;
            public PickupSource Source;
            // The items of the label at the click. Null when the target has no label.
            public List<Entity> Members;
        }

        // The plan of the last pickup that the local player started. Each start replaces it, so the plan of a
        // cancelled pickup is never used.
        private static StoredPlan current;

        // True while the postfix runs the effect for the extra items, so it does not run for them again.
        private static bool inner;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InteractionManager), nameof(InteractionManager.StartInteraction),
            new[] { typeof(InteractionType), typeof(InteractionInputType), typeof(ActionType), typeof(Entity), typeof(InteractionSource), typeof(Entity) })]
        private static void StartInteractionPrefix(InteractionType type, Entity interactorEntity, Entity targetEntity)
        {
            try
            {
                if (type != InteractionType.PICKUP || interactorEntity == null || targetEntity == null) return;
                var local = LabelPatches.LocalPlayerEntity;
                bool byLocalPlayer = local != null && interactorEntity.id == local.id;
                Plugin.Debug($"Pickup start: interactor {interactorEntity.id}{(byLocalPlayer ? " (player)" : "")}, target {targetEntity.id}");
                if (!byLocalPlayer) return;
                // A middle-click takes one item, also in place of an open group plan of the same target.
                bool takeOne = ClickPatches.TakeOne(targetEntity);
                // The game starts the same pickup again while the character walks to the item. The plan of the
                // click stays.
                if (!takeOne && current != null && current.TargetId == targetEntity.id) return;

                var target = Candidate(targetEntity);
                if (target.Kind == null) return;
                bool hasLabel = LabelPatches.TryGetLabelMembers(targetEntity.id, out var members);
                var source = takeOne ? PickupSource.None : GroupPickupLogic.Plan(Plugin.PickMode, true, target, hasLabel);
                current = new StoredPlan { TargetId = targetEntity.id, Kind = target.Kind, Source = source, Members = members };
                Plugin.Debug($"Pickup plan: target {targetEntity.id}, kind {target.Kind}, source {source}, {members?.Count ?? 0} label members");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Pickup start", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PickupInteraction), "TeardownCommon")]
        private static void TeardownPostfix(PickupInteraction __instance)
        {
            try
            {
                // The teardown runs after the effect and once for each pickup, so a plan that
                // is still open here belongs to a cancelled pickup, and a later click must not reuse it.
                var target = __instance.targetEntity;
                bool open = current != null && target != null && current.TargetId == target.id;
                if (open) current = null;
                Plugin.Debug($"Pickup teardown: target {(target != null ? target.id.ToString() : "none")}, plan {(open ? "cleared" : "none")}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Pickup teardown", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PickupEffect), nameof(PickupEffect.ExecuteResult))]
        private static void ExecuteResultPostfix(PickupEffect __instance, ref PickupEffect.PickupEffectResult __result)
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
                if (!__result.success || __result.insufficientRoom)
                {
                    Plugin.Debug($"Group pickup: source {plan.Source}, kind {plan.Kind}, the target did not fit, 0 extra items picked");
                    return;
                }
                var center = __result.position;
                if (center == UnityEngine.Vector3.zero && target.transform != null) center = target.transform.position;
                int colliders = 0;
                var extras = plan.Source == PickupSource.LabelGroup
                    ? LabelExtras(target.id, plan.Members)
                    : FindWithinRadius(__instance.entityManager, target, plan.Kind, center, out colliders);
                string search = $", center {center}, {colliders} colliders, {extras.Count} matches";
                int picked = PickExtras(__instance, interactor, target, extras, out int amount);
                // The game shows one message and plays one sound for the result of the target, so the result
                // carries the amount of the whole group: "+3 Dandelion Roots" in place of three "+1" messages.
                int own = __result.amount;
                __result.amount = own + amount;
                Plugin.Debug($"Group pickup: source {plan.Source}, kind {plan.Kind}{search}, picked {picked}, " +
                             $"message amount {own} + {amount}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Group pickup", e);
            }
        }

        // Runs the game's pickup for each extra item on the same effect, and stops at the first item that the
        // game does not take fully. Returns the count of items picked, and their summed amount for the message of
        // the target. Only an item that does not fit gets its own game message, so the full-inventory message shows.
        private static int PickExtras(PickupEffect effect, Entity interactor, Entity target, List<Entity> extras, out int amount)
        {
            var interaction = effect.GetInteraction();
            var actionType = effect.GetActionType();
            var inventory = interactor.GetCachedComponent<IInventoryHolder>(false)?.GetInventory();
            int picked = 0;
            amount = 0;
            inner = true;
            try
            {
                foreach (var extra in extras)
                {
                    if (extra == null || extra.destroyed) continue;
                    // Checked again just before the pickup: the item can have changed since the click.
                    var now = Candidate(extra);
                    if (now.Kind == null || now.PrefersEquippedSlot) continue;
                    // When the bag has no room for an equippable item, the game empties the hands to take it.
                    // An extra item never does that: it stays on the ground.
                    var stack = extra.GetCachedComponent<IPickupable>(false)?.GetItemStack();
                    if (stack?.Item != null && stack.Item.HasTag(ItemTag.EQUIPPABLE) && (inventory == null || !inventory.HasRoom(stack)))
                    {
                        Plugin.Debug($"Group pickup: no room for the equippable item {extra.id}, the hands stay as they are");
                        break;
                    }
                    effect.Init(interaction, interactor, extra, actionType);
                    var result = effect.ExecuteResult();
                    if (!result.success || result.insufficientRoom)
                    {
                        effect.OnExecuted(result);
                        break;
                    }
                    amount += result.amount;
                    picked++;
                }
            }
            finally
            {
                effect.Init(interaction, interactor, target, actionType);
                inner = false;
            }
            return picked;
        }

        // The other items of the label of the target that still exist.
        private static List<Entity> LabelExtras(int targetId, List<Entity> members)
        {
            var byId = new Dictionary<int, Entity>();
            var candidates = new List<PickupCandidate>();
            foreach (var member in members)
            {
                if (member == null || member.destroyed) continue;
                byId[member.id] = member;
                candidates.Add(Candidate(member));
            }
            var extras = new List<Entity>();
            foreach (int id in GroupPickupLogic.LabelExtras(targetId, candidates)) extras.Add(byId[id]);
            return extras;
        }

        // The ground items of the same kind within the pickup radius of the picked item: a physics overlap at
        // the place where the item was, each collider mapped to its entity.
        internal static List<Entity> FindWithinRadius(IEntityManager entityManager, Entity target, string kind, UnityEngine.Vector3 center, out int colliders)
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
                found.Add(new RadiusCandidate(Candidate(entity), UnityEngine.Vector3.Distance(entity.transform.position, center)));
            }

            var ids = GroupPickupLogic.WithinRadius(new PickupCandidate(target.id, kind, false), found, radius);
            foreach (int id in ids) extras.Add(byId[id]);
            return extras;
        }

        internal static PickupCandidate Candidate(Entity entity)
        {
            var stack = entity.GetCachedComponent<IPickupable>(false)?.GetItemStack();
            var item = stack?.Item;
            if (item == null) return new PickupCandidate(entity.id, null, false);
            return new PickupCandidate(entity.id, item.Identifier, GoesIntoHands(stack, item));
        }

        // True when PickupEffect.ExecuteResult always can change the hands for this stack: the stack prefers an
        // equipped slot (large, lit fuel, some substances), a two-handed pickup drops what is in the hands, and an
        // item worn on the back unequips that slot. An equippable item changes the hands only when the bag is
        // full, so PickExtras checks the room for it.
        internal static bool GoesIntoHands(InventoryItemStack stack, InventoryItem item)
        {
            return stack.PreferEquippedSlot()
                || item.HasTag(ItemTag.TWO_HANDED)
                || item.HasTag(ItemTag.WORN_ON_BACK);
        }
    }
}
