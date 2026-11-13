using System;
using HarmonyLib;
using ProximityPickup.Labels;

namespace ProximityPickup.Pickup
{
    // The click path of the game: the params of a click of the player, the result of the interaction start, and the
    // middle-click that takes one thing through the game's own click.
    [HarmonyPatch]
    internal static class ClickPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractionEventHandler), nameof(InteractionEventHandler.Execute))]
        private static void ExecutePostfix(InteractionEventParams eventParams)
        {
            if (!Plugin.Verbose.Value) return;
            try
            {
                var interactor = eventParams.interactorEntity;
                var target = eventParams.targetEntity;
                var local = LabelPatches.LocalPlayerEntity;
                if (interactor == null || target == null || local == null || interactor.id != local.id) return;
                bool pickup = target.GetCachedComponent<IPickupable>(false) != null;
                bool gather = target.GetCachedComponent<IGatherable>(false) != null;
                if (!pickup && !gather) return;
                Plugin.Debug($"Click params: interactor {interactor.id}, target {target.id}, source {eventParams.source}, " +
                             $"ordinality {eventParams.ordinality}, action {eventParams.actionType}, input {eventParams.inputType}, " +
                             $"desired {eventParams.desiredInteractionType}, required {NullOr(eventParams.requiredInteractionTypes)}, " +
                             $"ignored {NullOr(eventParams.ignoredInteractionTypes)}, data {NullOr(eventParams.data)}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Click params", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractionEventHandler), "handleInteractionResult")]
        private static void HandleResultPostfix(InteractionResult result, InteractionType interactionType)
        {
            if (!Plugin.Verbose.Value) return;
            try
            {
                if (interactionType != InteractionType.PICKUP && interactionType != InteractionType.GATHER) return;
                Plugin.Debug($"Interaction result: {interactionType}, possible {result.possible}, started {result.started}, withinRange {result.withinRange}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Interaction result", e);
            }
        }

        // The thing of the last middle-click, until the game starts its pickup or gather (the start prefixes take
        // it) or drops the click. A start on it is a pickup or a gather of that one thing.
        internal static Entity TakeOneTarget;

        private static bool middleDown;
        private static float pressTime;
        private static UnityEngine.Vector2 pressAt;
        private static Entity pressTarget;

        // The last thing under the cursor and when it was seen. The middle press starts the game's camera rotation,
        // which can close the hover card in the frame of the press, so the press takes the thing of the frames before.
        private static Entity lastHovered;
        private static float lastHoveredTime;
        private const float HoverGraceSeconds = 0.2f;

        // True once when the target is the take-one target, which then clears.
        internal static bool TakeOne(Entity target)
        {
            if (TakeOneTarget == null || target == null || TakeOneTarget.id != target.id) return false;
            TakeOneTarget = null;
            return true;
        }

        // A middle-click on a hovered thing with a pickup or a gather starts the game's click on it, as the
        // take-one target. A longer hold or a drag of the middle mouse button keeps its camera action.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerInputView), "Update")]
        private static void UpdatePostfix(PlayerInputView __instance)
        {
            try
            {
                var player = __instance.entity;
                var local = LabelPatches.LocalPlayerEntity;
                if (player == null || local == null || player.id != local.id) return;
                var held = __instance.primaryActionDownEntity;
                if (TakeOneTarget != null && (held == null || held.id != TakeOneTarget.id)) TakeOneTarget = null;

                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null) return;
                var button = mouse.middleButton;
                float now = UnityEngine.Time.unscaledTime;
                var hovered = LabelPatches.Hovered ?? LabelPatches.CardEntity;
                if (hovered != null)
                {
                    lastHovered = hovered;
                    lastHoveredTime = now;
                }
                if (button.wasPressedThisFrame)
                {
                    middleDown = true;
                    pressTime = now;
                    pressAt = mouse.position.ReadValue();
                    pressTarget = now - lastHoveredTime <= HoverGraceSeconds ? lastHovered : null;
                }
                if (!button.wasReleasedThisFrame || !middleDown) return;
                middleDown = false;
                float moved = UnityEngine.Vector2.Distance(pressAt, mouse.position.ReadValue());
                if (!MiddleClick.IsClick(pressTime, now, moved)) return;

                var target = pressTarget;
                string type = TypeOf(target);
                if (type == null)
                {
                    Plugin.Debug($"Middle-click: no thing with a pickup or a gather under the cursor (hovered {target?.id.ToString() ?? "none"})");
                    return;
                }
                TakeOneTarget = target;
                StartClick(__instance, target);
                Plugin.Debug($"Middle-click: entity {target.id}, {type}, {(now - pressTime) * 1000f:0} ms");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Middle-click", e);
            }
        }

        // "pickup" or "gather" for a thing that a click picks up or gathers, or null.
        private static string TypeOf(Entity target)
        {
            if (target == null || target.destroyed) return null;
            if (target.GetCachedComponent<IPickupable>(false) != null) return "pickup";
            if (target.GetCachedComponent<IGatherable>(false) != null) return "gather";
            return null;
        }

        // A primary click as the game continues it after the press: while the view holds an entity as the target of
        // the primary action and the button is up, the view sends the game's own click on it in each frame, walks
        // the character to it, and clears the target when the interaction starts.
        private static void StartClick(PlayerInputView view, Entity target)
        {
            view.primaryActionUpLocked = false;
            view.primaryActionDownEntity = target;
        }

        private static string NullOr(object value) => value == null ? "null" : "set";
    }
}
