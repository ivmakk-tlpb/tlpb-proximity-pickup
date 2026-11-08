using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProximityPickup.Labels
{
    // Merges the item labels of the same kind that lie close together in the world into label groups, moves all labels
    // apart, and draws the label boxes and the hint panel hotkeys. Runs after the game placed its labels and its
    // hover card for this frame (HudUI.Update), so the game's own text, color, and position are the input each
    // frame.
    [HarmonyPatch]
    internal static class LabelPatches
    {
        // The label group of each item in the last frame, only for groups that show a label. The hover card freezes
        // the group of the hovered item.
        private static readonly Dictionary<int, LabelGroup> labelGroupOf = new Dictionary<int, LabelGroup>();

        // The items that the label of an item stood for in the last frame. False when the item has no label.
        internal static bool TryGetLabelMembers(int id, out List<Entity> members)
        {
            members = null;
            if (!labelGroupOf.TryGetValue(id, out var group)) return false;
            members = new List<Entity>(group.MemberIds.Count);
            foreach (int member in group.MemberIds) members.Add(slots[member].Entity);
            return true;
        }

        // The entity of the label or hover card under the cursor, or null.
        internal static Entity Hovered;

        // The entity of the local player, from the HUD of the last frame.
        internal static Entity LocalPlayerEntity;

        // A hidden member of a label group goes this far below the screen, where the cursor cannot reach it. The
        // game places each label again in each frame, so a label that is no longer hidden comes back by itself.
        private const float HiddenY = -10000f;

        private const float LabelBoxAlpha = 0.5f, HoverBoxAlpha = 0.75f, BorderAlpha = 0.7f, FillAlpha = 0.35f;
        private const float BorderPixels = 1f;
        private const float HintPanelGap = 8f;

        // The interop getter of the Nullable field KeyBindLabel.modifier throws, so the mod reads it at its offset.
        private const int ModifierFlagOffset = 0x74, ModifierValueOffset = 0x78;
        private static readonly Predicate<int> IsDefinedAction = value => Enum.IsDefined(typeof(InputAction), value);

        // How far the layout moved each label in this frame and in the last frame, by layout box id: the entity id,
        // or the negative id of a terrain zone label.
        private static Dictionary<int, float> lastMove = new Dictionary<int, float>();
        private static Dictionary<int, float> prevMove = new Dictionary<int, float>();
        // The items that had a label on the screen at some point, by entity id.
        private static readonly HashSet<int> everLabeled = new HashSet<int>();
        private static readonly HashSet<int> openCards = new HashSet<int>();
        private static readonly HoverFreeze freeze = new HoverFreeze();

        // The rectangle, the letter center, and the layout move of each label on the screen in the last frame, and
        // its text color.
        private static readonly Dictionary<int, LabelPlace> labelPlaces = new Dictionary<int, LabelPlace>();
        private static readonly Dictionary<int, Color> labelColors = new Dictionary<int, Color>();
        private const int MaxRemembered = 4096;

        // What a hover card keeps from the label that it replaced, by entity id.
        private static readonly Dictionary<int, CardState> cardStates = new Dictionary<int, CardState>();

        // The position that the mod wrote to a transform, and the game position that it replaced. A transform
        // that is still at the written position was not placed by the game this frame.
        private static readonly Dictionary<IntPtr, (Vector3 written, Vector3 game)> writes = new Dictionary<IntPtr, (Vector3, Vector3)>();

        private static readonly Dictionary<(float, string), Vector2> textSizes = new Dictionary<(float, string), Vector2>();
        private static readonly Dictionary<IntPtr, Geometry> geometries = new Dictionary<IntPtr, Geometry>();
        private static readonly Dictionary<IntPtr, ItemData> itemData = new Dictionary<IntPtr, ItemData>();

        private static readonly Stopwatch stopwatch = new Stopwatch();
        private static readonly Stopwatch logClock = Stopwatch.StartNew();
        private static double sumMs, maxMs;
        private static int frames, slowFrames;
        private static int gcAtLog = GC.CollectionCount(0);

        // Per frame work lists, kept to avoid an allocation in each frame.
        private static readonly List<LabelInput> inputs = new List<LabelInput>();
        private static readonly List<LayoutBox> movable = new List<LayoutBox>();
        private static readonly List<LayoutBox> obstacles = new List<LayoutBox>();
        private static readonly Dictionary<int, Slot> slots = new Dictionary<int, Slot>();
        private static readonly List<Slot> slotPool = new List<Slot>();
        private static int slotsUsed;

        // The parts of a label that do not change between frames.
        private sealed class Geometry
        {
            public RectTransform Rect;
            public TextMeshProUGUI Text;
            public Vector2 Pivot;
            public Vector2 Padding;
            public Vector3 Scale;
            public float FontSize;
        }

        // What the mod needs of a ground item. Null Kind: the entity is not an item.
        private sealed class ItemData
        {
            public string Kind;
            public string Name;
            public InventoryItemStack Stack;
            // The entity the data was read from: the interop pointer of a collected entity can come back for a
            // new entity.
            public int EntityId;
        }

        // One label of this frame.
        private sealed class Slot
        {
            public Entity Entity;
            public Transform Transform;
            public Geometry Geometry;
            public Vector3 Position;
            public ScreenRect Rect;
            public bool IsItem;
            public bool IsCard;
            public ScreenPoint TextOffset;
        }

        private readonly struct LabelPlace
        {
            public readonly ScreenRect Rect;
            public readonly ScreenPoint Text;
            public readonly float Move;

            public LabelPlace(ScreenRect rect, ScreenPoint text, float move)
            {
                Rect = rect; Text = text; Move = move;
            }
        }

        private sealed class CardState
        {
            public bool Replaced;
            public bool HasLabel;
            public ScreenRect LabelRect;
            public ScreenPoint LabelText;
            public float Move;
            public bool HasColor;
            public Color Color;
            public bool Anchored;
            public ScreenPoint Anchor;
            public int LastFrame;
            public int TitleFrames;
            public ScreenPoint FirstTitle;
            public bool CountRead;
            public string CountTitle;
        }

        // The objects of one label box. The game pools labels and cards, so they stay on the object for its next use.
        private sealed class BoxParts
        {
            public RectTransform Rect;
            public Image Image;
            public float CatchInset;
            public Image[] Edges;
            public float EdgeWidth;
            public Image Fill;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HudUI), "Update")]
        private static void UpdatePostfix(HudUI __instance)
        {
            stopwatch.Restart();
            try
            {
                Run(__instance);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Label groups", e);
            }
            stopwatch.Stop();
            double ms = stopwatch.Elapsed.TotalMilliseconds;
            sumMs += ms;
            if (ms > maxMs) maxMs = ms;
            if (ms > 2.0) slowFrames++;
            frames++;
        }

        // True when the last frame had a label on the screen that is not a hover card.
        private static bool labelsOn;

        private static int hiddenCount, labelBoxCount, lastHidden, lastItems, lastOthers, lastGroups, lastLargest, lastLabelBoxes;

        private static void Run(HudUI hud)
        {
            hintPanelUsed = false;
            hiddenCount = 0;
            labelBoxCount = 0;
            inputs.Clear();
            movable.Clear();
            obstacles.Clear();
            slots.Clear();
            slotsUsed = 0;
            var swap = prevMove;
            prevMove = lastMove;
            lastMove = swap;
            lastMove.Clear();

            var player = hud.player?.GetEntity();
            LocalPlayerEntity = player;
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            float radius = Plugin.PickupRadius.Value;
            int others = 0;
            int frame = Time.frameCount;

            // The hover card is a fixed obstacle at the place of its label. Its item takes part in the grouping
            // and is the representative of its group.
            openCards.Clear();
            var cards = hud.entityHudMap;
            if (cards != null)
            {
                var c = cards.GetEnumerator();
                while (c.MoveNext())
                {
                    var entity = c.Current.Key;
                    var ui = c.Current.Value;
                    if (entity == null || ui == null || !ui.gameObject.activeInHierarchy) continue;
                    openCards.Add(entity.id);
                    var geometry = GeometryOf(ui, null);
                    if (geometry == null) continue;
                    // The title takes the font size of the item labels, so the hover card needs less room.
                    if (labelFontSize > 0 && ui.labelText != null && ui.labelText.fontSize != labelFontSize)
                        ui.labelText.fontSize = labelFontSize;
                    PlaceCard(hud, entity, ui, geometry, frame);
                }
            }
            // The game removes a hovered label when its hover card opens, and that fires the mouse exit. So the
            // hover ends only when the cursor left the label and the hover card of the entity is closed.
            if (exitPending && Hovered != null && !openCards.Contains(Hovered.id))
            {
                Hovered = null;
                exitPending = false;
                frozen.Clear();
            }
            if (freeze.EndFrame(openCards, Hovered != null))
            {
                frozen.Clear();
                Plugin.Debug("Freeze: end");
            }
            if (cardStates.Count > 0)
                foreach (var id in new List<int>(cardStates.Keys))
                    if (!openCards.Contains(id) && (Hovered == null || Hovered.id != id)) cardStates.Remove(id);

            int cardInputs = inputs.Count;
            var labels = hud.entityLabelMap;
            if (labels != null)
            {
                var e = labels.GetEnumerator();
                while (e.MoveNext())
                {
                    var entity = e.Current.Key;
                    var label = e.Current.Value;
                    if (entity == null || label == null || !label.gameObject.activeInHierarchy) continue;
                    if (slots.ContainsKey(entity.id)) continue;
                    var geometry = GeometryOf(label, label.text);
                    if (geometry == null) continue;
                    var slot = NextSlot(entity, label.transform, geometry);
                    slot.TextOffset = GlyphOffset(geometry.Text, label.transform);
                    freeze.LabelSeen(entity.id, frame);
                    if (labelColors.Count > MaxRemembered) labelColors.Clear();
                    labelColors[entity.id] = geometry.Text.color;
                    var data = DataOf(entity);
                    slots[entity.id] = slot;
                    if (data.Kind == null)
                    {
                        slot.Rect = RectAt(geometry, slot.Position, TextSize(geometry, geometry.Text.text) + geometry.Padding);
                        movable.Add(new LayoutBox(entity.id, slot.Rect));
                        others++;
                        continue;
                    }
                    slot.IsItem = true;
                    if (labelFontSize <= 0) labelFontSize = geometry.Text.fontSize;
                    var size = TextSize(geometry, geometry.Text.text) + geometry.Padding;
                    everLabeled.Add(entity.id);
                    slot.Rect = RectAt(geometry, slot.Position, size);
                    var world = entity.transform.position;
                    float distance = Vector3.Distance(world, playerPos);
                    inputs.Add(new LabelInput(entity.id, data.Kind, data.Name, new WorldPoint(world.x, world.y, world.z), data.Stack.Quantity, distance, true));
                }
            }

            var zones = hud.terrainLayerZoneLabelMap;
            if (zones != null)
            {
                int zoneId = -2;
                var z = zones.GetEnumerator();
                while (z.MoveNext())
                {
                    var label = z.Current.Value;
                    if (label == null || !label.gameObject.activeInHierarchy) continue;
                    var geometry = GeometryOf(label, label.text);
                    if (geometry == null) continue;
                    var slot = NextSlot(null, label.transform, geometry);
                    slot.Rect = RectAt(geometry, slot.Position, TextSize(geometry, geometry.Text.text) + geometry.Padding);
                    slots[zoneId] = slot;
                    movable.Add(new LayoutBox(zoneId, slot.Rect));
                    HideBox(label.transform);
                    zoneId--;
                    others++;
                }
            }

            labelsOn = others > 0 || inputs.Count > cardInputs;
            labelGroupOf.Clear();
            var groups = LabelGrouping.Group(inputs, radius, frozen);
            int largest = 0, merged = 0;
            foreach (var group in groups)
            {
                if (group.ShowsLabel)
                    foreach (int id in group.MemberIds) labelGroupOf[id] = group;
                var rep = slots[group.RepresentativeId];
                bool many = group.MemberIds.Count > 1;
                if (many)
                {
                    merged++;
                    foreach (int id in group.MemberIds)
                    {
                        var member = slots[id];
                        if (id != group.RepresentativeId && !member.IsCard)
                        {
                            hiddenCount++;
                            Write(member.Transform, member.Position, new Vector3(member.Position.x, HiddenY, member.Position.z));
                        }
                    }
                }
                if (group.MemberIds.Count > largest) largest = group.MemberIds.Count;

                if (rep.IsCard)
                {
                    // A group of hover cards with no label keeps the count of the radius, which a click takes.
                    if (many && group.ShowsLabel)
                    {
                        SetCardTitle(rep, group.Text);
                        if (cardStates.TryGetValue(group.RepresentativeId, out var repState)) repState.CountTitle = null;
                    }
                    continue;
                }
                if (many)
                {
                    rep.Geometry.Text.text = group.Text;
                    rep.Rect = RectAt(rep.Geometry, rep.Position, TextSize(rep.Geometry, group.Text) + rep.Geometry.Padding);
                }
                movable.Add(new LayoutBox(group.RepresentativeId, rep.Rect));
            }

            var placed = LabelLayout.Place(movable, obstacles, 0f, Screen.height);
            foreach (var box in movable)
            {
                var slot = slots[box.Id];
                float move = placed[box.Id].YMin - box.Rect.YMin;
                // During a hover each label keeps the place it had when the hover started.
                if (freeze.TryGetMove(box.Id, out float keptMove)) move = keptMove;
                lastMove[box.Id] = move;
                var at = slot.Position;
                at.y += move;
                Write(slot.Transform, slot.Position, at);
                if (slot.Entity == null) continue;
                if (labelPlaces.Count > MaxRemembered) labelPlaces.Clear();
                labelPlaces[slot.Entity.id] = new LabelPlace(
                    new ScreenRect(box.Rect.XMin, box.Rect.YMin + move, box.Rect.XMax, box.Rect.YMax + move),
                    new ScreenPoint(slot.Position.x + slot.TextOffset.X, slot.Position.y + move + slot.TextOffset.Y),
                    move);
                PlaceBox(slot.Transform, HoverGeometry.LabelBox(box.Rect), slot.Position, LabelBoxAlpha);
                labelBoxCount++;
            }

            if (hintPanel != null && hintPanel.gameObject.activeSelf != hintPanelUsed) hintPanel.gameObject.SetActive(hintPanelUsed);

            lastItems = inputs.Count;
            lastHidden = hiddenCount;
            lastOthers = others;
            lastGroups = merged;
            lastLargest = largest;
            lastLabelBoxes = labelBoxCount;
            LogOncePerSecond();
        }

        // Places one open hover card at the label that it replaced, with its label box and its hint panel hotkeys.
        private static void PlaceCard(HudUI hud, Entity entity, HudGameObjectUI ui, Geometry geometry, int frame)
        {
            var slot = NextSlot(entity, ui.transform, geometry);
            slot.IsCard = true;
            var title = ui.labelText;

            bool isNew = !cardStates.TryGetValue(entity.id, out var state);
            if (isNew)
            {
                state = new CardState();
                cardStates[entity.id] = state;
            }
            bool wasFrozen = freeze.Active;
            if (freeze.CardOpen(entity.id, frame, prevMove) && !state.Replaced) ReplaceLabel(entity.id, state);
            if (!wasFrozen && freeze.Active) Plugin.Debug($"Freeze: start for entity {entity.id}");

            if (state.HasColor && title != null) title.color = state.Color;
            // The labels can go off while the card stays open, and then the card stands for no label.
            bool showsLabel = state.Replaced && labelsOn;
            if (!showsLabel && title != null) ShowPickupCount(hud, entity, title, state);
            // A pooled card holds the layout and the text mesh of its last use.
            if (title != null && (isNew || frame - state.LastFrame > 1))
            {
                if (ui.interactionsArea != null) ui.interactionsArea.enabled = false;
                LayoutRebuilder.ForceRebuildLayoutImmediate(geometry.Rect);
                title.ForceMeshUpdate(false, false);
            }
            state.LastFrame = frame;

            ScreenRect bounds;
            var shift = new ScreenPoint(0f, state.Move);
            if (title == null || !state.HasLabel)
            {
                bounds = TitleBounds(ui, slot.Position);
            }
            else
            {
                var game = new ScreenPoint(slot.Position.x, slot.Position.y);
                var titleCenter = GlyphOffset(title, ui.transform);
                if (!state.Anchored)
                {
                    state.Anchor = HoverGeometry.Anchor(state.LabelText, game);
                    state.Anchored = true;
                }
                shift = HoverGeometry.CardShift(state.Anchor, titleCenter);
                bounds = HoverGeometry.CardBox(state.LabelRect, state.LabelText, game, titleCenter);
                LogTitleOffset(entity.id, state, titleCenter);
            }
            var at = slot.Position;
            at.x += shift.X;
            at.y += shift.Y;
            Write(slot.Transform, slot.Position, at);
            obstacles.Add(new LayoutBox(-1, new ScreenRect(bounds.XMin + shift.X, bounds.YMin + shift.Y, bounds.XMax + shift.X, bounds.YMax + shift.Y)));

            var box = PlaceBox(ui.transform, HoverGeometry.LabelBox(bounds), slot.Position, HoverBoxAlpha);
            float progress = HideRows(hud, ui, Hovered != null && Hovered.id == entity.id);
            if (title != null)
            {
                SetBorder(box, title.color, entity.id, isNew);
                SetFill(box, progress, title.color);
            }

            var data = DataOf(entity);
            if (data.Kind == null || !everLabeled.Contains(entity.id)) return;
            slot.IsItem = true;
            slots[entity.id] = slot;
            var world = entity.transform.position;
            // A negative distance makes the hovered item the representative of its group.
            inputs.Add(new LabelInput(entity.id, data.Kind, data.Name, new WorldPoint(world.x, world.y, world.z), data.Stack.Quantity, -1f, showsLabel));
        }

        // A hover card with no label shows what a pickup of its item takes. The search runs once for each card.
        private static void ShowPickupCount(HudUI hud, Entity entity, TextMeshProUGUI title, CardState state)
        {
            if (!state.CountRead)
            {
                state.CountRead = true;
                state.CountTitle = PickupCountTitle(hud, entity);
                if (state.CountTitle != null) Plugin.Debug($"Hover card {entity.id}: no label, title {state.CountTitle}");
            }
            if (state.CountTitle == null || title.text == state.CountTitle) return;
            title.text = state.CountTitle;
            title.ForceMeshUpdate(false, false);
        }

        // Null keeps the game text. A throw here must not stop the frame, so a failed search also keeps it.
        private static string PickupCountTitle(HudUI hud, Entity entity)
        {
            try
            {
                var target = Pickup.PickupPatches.Candidate(entity);
                if (target.Kind == null || GroupPickupLogic.Plan(Plugin.PickMode, true, target, false) != PickupSource.Radius) return null;
                var entityManager = hud.worldManager?.TryCast<WorldManager>()?.entityManager;
                if (entityManager == null) return null;
                var extras = Pickup.PickupPatches.FindWithinRadius(entityManager, entity, target.Kind, entity.transform.position, out _);
                var counts = new List<int>(extras.Count);
                foreach (var extra in extras) counts.Add(DataOf(extra).Stack.Quantity);
                var data = DataOf(entity);
                return GroupPickupLogic.CardTitle(data.Name, data.Stack.Quantity, counts);
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Hover card count", e);
                return null;
            }
        }

        // Takes the place and the color of the label that the card replaced. The label of a hidden member of a
        // label group is the label of its group.
        private static void ReplaceLabel(int id, CardState state)
        {
            state.Replaced = true;
            int source = FreezeGroupOf(id);
            if (labelPlaces.TryGetValue(source, out var place))
            {
                state.HasLabel = true;
                state.LabelRect = place.Rect;
                state.LabelText = place.Text;
                state.Move = place.Move;
            }
            state.HasColor = labelColors.TryGetValue(id, out state.Color);
            if (Plugin.Verbose.Value)
                Plugin.Debug($"Hover card {id}: label of entity {source}, title color {(state.HasColor ? ColorUtility.ToHtmlStringRGB(state.Color) : "of the game")}, move {state.Move:0} px, group of {Math.Max(1, frozen.Count)}");
        }

        // Keeps the label group of an item while its hover card is open. Returns the entity id of the label that
        // shows the item: the representative of its group in the last frame, or the item itself.
        private static int FreezeGroupOf(int id)
        {
            frozen.Clear();
            if (labelGroupOf.TryGetValue(id, out var group))
            {
                frozen.UnionWith(group.MemberIds);
                return group.RepresentativeId;
            }
            frozen.Add(id);
            return id;
        }

        private static void LogTitleOffset(int id, CardState state, ScreenPoint title)
        {
            if (state.TitleFrames >= 2 || !Plugin.Verbose.Value) return;
            if (state.TitleFrames == 1)
                Plugin.Debug($"Hover card {id}: title offset ({state.FirstTitle.X:0.0}, {state.FirstTitle.Y:0.0}) then ({title.X:0.0}, {title.Y:0.0})");
            else
                state.FirstTitle = title;
            state.TitleFrames++;
        }

        // The objects of the label box of each label and hover card.
        private static readonly Dictionary<IntPtr, BoxParts> boxes = new Dictionary<IntPtr, BoxParts>();

        // Places the label box. It is a child of the label or the card, so it moves with it. It catches the mouse
        // in the same rectangle on the label and on the card, so the cursor stays on the card that replaces a label.
        private static BoxParts PlaceBox(Transform parent, ScreenRect bounds, Vector3 gamePosition, float alpha)
        {
            if (!boxes.TryGetValue(parent.Pointer, out var box) || box.Rect == null)
            {
                var go = new GameObject("ProximityPickupLabelBox");
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.SetAsFirstSibling();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = Vector2.zero;
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                box = new BoxParts { Rect = rt, Image = go.AddComponent<Image>() };
                box.Image.raycastTarget = true;
                // A box of a live label stays on the object, so only the boxes that are gone can be forgotten.
                if (boxes.Count > MaxRemembered)
                    foreach (var key in new List<IntPtr>(boxes.Keys))
                        if (boxes[key].Rect == null) boxes.Remove(key);
                boxes[parent.Pointer] = box;
            }
            if (!box.Rect.gameObject.activeSelf) box.Rect.gameObject.SetActive(true);
            if (box.Image.color.a != alpha) box.Image.color = new Color(0f, 0f, 0f, alpha);
            var s = parent.lossyScale;
            if (s.x == 0 || s.y == 0) return box;
            // Relative to the parent, so the box moves with it when the mod moves it.
            box.Rect.localPosition = new Vector3((bounds.XMin - gamePosition.x) / s.x, (bounds.YMin - gamePosition.y) / s.y, 0f);
            box.Rect.sizeDelta = new Vector2((bounds.XMax - bounds.XMin) / s.x, (bounds.YMax - bounds.YMin) / s.y);
            float inset = HoverGeometry.EdgeCatchInset / s.y;
            if (inset != box.CatchInset)
            {
                box.CatchInset = inset;
                box.Image.raycastPadding = new Vector4(0f, inset, 0f, inset);
            }
            return box;
        }

        // The game can pool a label of an entity for a terrain zone, which has no label box.
        private static void HideBox(Transform parent)
        {
            if (boxes.TryGetValue(parent.Pointer, out var box) && box.Rect != null && box.Rect.gameObject.activeSelf)
                box.Rect.gameObject.SetActive(false);
        }

        // Draws a border inside the label box of a hover card, as four thin images along the box edges, so the box
        // keeps its size. The width stays one screen pixel at each UI scale.
        private static void SetBorder(BoxParts box, Color titleColor, int id, bool log)
        {
            if (box.Edges == null || box.Edges[0] == null)
            {
                box.Edges = new[]
                {
                    Edge(box.Rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f)),
                    Edge(box.Rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f)),
                    Edge(box.Rect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)),
                    Edge(box.Rect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f)),
                };
                box.EdgeWidth = 0f;
            }
            float width = BorderPixels / Math.Max(0.01f, box.Rect.lossyScale.y);
            if (width != box.EdgeWidth)
            {
                box.EdgeWidth = width;
                box.Edges[0].rectTransform.sizeDelta = new Vector2(0f, width);
                box.Edges[1].rectTransform.sizeDelta = new Vector2(0f, width);
                box.Edges[2].rectTransform.sizeDelta = new Vector2(width, 0f);
                box.Edges[3].rectTransform.sizeDelta = new Vector2(width, 0f);
            }
            var color = titleColor;
            color.a = BorderAlpha;
            foreach (var edge in box.Edges) if (edge.color != color) edge.color = color;
            if (log && Plugin.Verbose.Value) Plugin.Debug($"Hover card {id}: border color {ColorUtility.ToHtmlStringRGB(color)} at {color.a:0.00} opacity");
        }

        private static Image Edge(RectTransform box, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject("ProximityPickupLabelBoxBorder");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(box, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static int lastProgressStep = -1;

        // Fills the label box from the left with the progress of a hold action, behind the text and the border.
        private static void SetFill(BoxParts box, float progress, Color titleColor)
        {
            float width = HoverGeometry.FillWidth(progress);
            if (width <= 0f)
            {
                if (box.Fill != null && box.Fill.gameObject.activeSelf) box.Fill.gameObject.SetActive(false);
                lastProgressStep = -1;
                return;
            }
            if (box.Fill == null)
            {
                var go = new GameObject("ProximityPickupLabelBoxFill");
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(box.Rect, false);
                rt.SetAsFirstSibling();
                rt.anchorMin = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                box.Fill = go.AddComponent<Image>();
                box.Fill.raycastTarget = false;
            }
            if (!box.Fill.gameObject.activeSelf) box.Fill.gameObject.SetActive(true);
            box.Fill.rectTransform.anchorMax = new Vector2(width, 1f);
            var color = titleColor;
            color.a = FillAlpha;
            if (box.Fill.color != color) box.Fill.color = color;
            int step = (int)(width * 10f);
            if (step != lastProgressStep)
            {
                lastProgressStep = step;
                Plugin.Debug($"Hold progress: {width:0.00}");
            }
        }

        // The center of the drawn letters of a text relative to a root transform, so a title and a label line up by
        // their letters. Falls back to the center of the text rect while the text has no mesh.
        private static ScreenPoint GlyphOffset(TextMeshProUGUI text, Transform root)
        {
            var glyphs = text.textBounds;
            Vector3 center;
            if (glyphs.size.x > 0 && glyphs.size.x < 30000f) center = text.transform.TransformPoint(glyphs.center);
            else
            {
                var r = BoundsOf(text.rectTransform);
                center = new Vector3((r.XMin + r.XMax) / 2f, (r.YMin + r.YMax) / 2f, 0f);
            }
            var at = root.position;
            return new ScreenPoint(center.x - at.x, center.y - at.y);
        }

        // The screen rectangle of the title text of a hover card at its game position.
        private static ScreenRect TitleBounds(HudGameObjectUI card, Vector3 gamePosition)
        {
            var root = card.GetComponent<RectTransform>();
            var text = card.labelText;
            var bounds = text != null ? BoundsOf(text.rectTransform) : BoundsOf(root);
            if (text != null)
            {
                float width = text.GetPreferredValues(text.text).x * text.rectTransform.lossyScale.x;
                float center = (bounds.XMin + bounds.XMax) / 2f;
                bounds = new ScreenRect(center - width / 2f, bounds.YMin, center + width / 2f, bounds.YMax);
            }
            // The rect above is where the card was in the last write; shift it to the game position.
            float dy = gamePosition.y - root.position.y, dx = gamePosition.x - root.position.x;
            return new ScreenRect(bounds.XMin + dx, bounds.YMin + dy, bounds.XMax + dx, bounds.YMax + dy);
        }

        // Shows the action rows of a card in the hint panel hotkeys and puts the rows off the screen. The rows stay
        // alive, because the game keeps their text and their progress up to date. Returns the progress of the hold
        // action of the card, or a negative value.
        private static float HideRows(HudUI hud, HudGameObjectUI card, bool hovered)
        {
            var area = card.interactionsArea;
            if (area == null || !area.gameObject.activeInHierarchy) return -1f;
            if (area.enabled) area.enabled = false;
            var rows = area.GetComponentsInChildren<HudObjectInteractionUI>(false);
            if (rows.Length == 0) return -1f;
            // The card of the hovered thing wins over another open card, such as the card of the focused entity.
            if (!hintPanelUsed || hovered)
            {
                // A copy of a game window can break with a game update, and that must not stop the labels.
                try
                {
                    ShowInHintPanel(hud, rows);
                }
                catch (Exception e)
                {
                    Plugin.WarnOnce("Hint panel hotkeys", e);
                }
            }
            float progress = -1f;
            foreach (var row in rows)
            {
                var bar = row.activeBarImage;
                if (bar != null && bar.enabled) progress = Math.Max(progress, bar.rectTransform.anchorMax.x);
                var p = row.transform.position;
                row.transform.position = new Vector3(p.x, HiddenY, p.z);
            }
            return progress;
        }

        // The mod's own copy of the game's inventory controls panel. It is not shown as a window, so it takes no
        // cursor or input focus, and its component is off, so the game does not write the inventory hints to it.
        private static InventoryControlsUI hintPanel;
        private static bool hintPanelUsed;

        private static void ShowInHintPanel(HudUI hud, Il2CppArrayBase<HudObjectInteractionUI> rows)
        {
            if (hintPanel == null)
            {
                hintPanel = UIComponent.uiManager.InstantiateUI<InventoryControlsUI>((Transform)null, null);
                hintPanel.enabled = false;
                hintPanel.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
                Plugin.Debug("Hint panel hotkeys: created the panel");
            }
            var lines = new[] { hintPanel.controls1, hintPanel.controls2, hintPanel.controls3, hintPanel.controls4, hintPanel.controls5 };
            int used = 0;
            bool changed = false;
            foreach (var row in rows)
            {
                var description = row.descriptionText;
                if (used == lines.Length || description == null || !description.gameObject.activeInHierarchy) continue;
                ReadKeys(hud.player, row.keyBindLabel, out string modifier, out string key);
                string line = HintText.Line(modifier, key, ColorUtility.ToHtmlStringRGB(description.color), description.text);
                if (lines[used].text != line)
                {
                    lines[used].text = line;
                    changed = true;
                }
                if (!lines[used].gameObject.activeSelf) lines[used].gameObject.SetActive(true);
                used++;
            }
            for (int i = used; i < lines.Length; i++)
                if (lines[i].gameObject.activeSelf) lines[i].gameObject.SetActive(false);
            hintPanelUsed = used > 0;
            if (changed && Plugin.Verbose.Value)
            {
                var texts = new List<string>(used);
                for (int i = 0; i < used; i++) texts.Add(lines[i].text);
                Plugin.Debug("Hint panel hotkeys: " + string.Join(" | ", texts));
            }

            float bottom = BoundsOf(hud.quickSlots.GetComponent<RectTransform>()).YMax + HintPanelGap;
            var native = hud.inventoryControlsUI;
            if (native != null && native.gameObject.activeInHierarchy)
                bottom = BoundsOf(native.GetComponent<RectTransform>()).YMax + HintPanelGap;
            var rt = hintPanel.GetComponent<RectTransform>();
            float w = rt.rect.width * rt.lossyScale.x, h = rt.rect.height * rt.lossyScale.y;
            rt.position = new Vector3(Screen.width / 2f + (rt.pivot.x - 0.5f) * w, bottom + rt.pivot.y * h, rt.position.z);
        }

        private static string FirstBinding(LocalPlayer player, InputAction action)
        {
            return HintText.FirstBinding(player.GetKeyNamesTextForAction(action, "", "", true), L.T("ui.or"));
        }

        private static void ReadKeys(LocalPlayer player, KeyBindLabel label, out string modifier, out string key)
        {
            modifier = null;
            key = null;
            if (label == null || !label.inputActionPresent) return;
            key = FirstBinding(player, label.inputAction);
            int flag = Marshal.ReadByte(label.Pointer + ModifierFlagOffset);
            int value = Marshal.ReadInt32(label.Pointer + ModifierValueOffset);
            if (!HintText.DecodeModifier(flag, value, IsDefinedAction, out int? action))
                Plugin.WarnOnce("Hint panel hotkeys", new InvalidOperationException("The modifier key of an action is not readable, so the action shows its key only."));
            if (action.HasValue) modifier = FirstBinding(player, (InputAction)action.Value);
        }

        private static ScreenRect BoundsOf(RectTransform rt)
        {
            var r = rt.rect;
            var s = rt.lossyScale;
            var p = rt.position;
            return new ScreenRect(p.x + r.xMin * s.x, p.y + r.yMin * s.y, p.x + r.xMax * s.x, p.y + r.yMax * s.y);
        }

        private static void SetCardTitle(Slot card, string title)
        {
            var ui = card.Transform.GetComponent<HudGameObjectUI>();
            var text = ui?.labelText;
            if (text != null) text.text = title;
        }

        private static Slot NextSlot(Entity entity, Transform transform, Geometry geometry)
        {
            if (slotsUsed == slotPool.Count) slotPool.Add(new Slot());
            var slot = slotPool[slotsUsed++];
            slot.Entity = entity;
            slot.Transform = transform;
            slot.Geometry = geometry;
            slot.Position = GamePosition(transform);
            slot.IsItem = false;
            slot.IsCard = false;
            return slot;
        }

        // The position where the game placed the transform this frame.
        private static Vector3 GamePosition(Transform transform)
        {
            var pos = transform.position;
            if (writes.TryGetValue(transform.Pointer, out var w) && w.written == pos) return w.game;
            return pos;
        }

        private static void Write(Transform transform, Vector3 game, Vector3 at)
        {
            if (at == game)
            {
                if (writes.Remove(transform.Pointer)) transform.position = game;
                return;
            }
            transform.position = at;
            writes[transform.Pointer] = (at, game);
            if (writes.Count > 4096) writes.Clear();
        }

        // The geometry of a label or a hover card, measured once for each pooled object. The space around the
        // text is the label size minus the text size: both come from the same layout pass, so the difference
        // holds although the label rect updates only at the next layout rebuild.
        private static Geometry GeometryOf(Component component, TextMeshProUGUI text)
        {
            var key = component.Pointer;
            if (geometries.TryGetValue(key, out var geometry))
            {
                // The canvas scale changes with the resolution and the UI scale setting.
                geometry.Scale = geometry.Rect.lossyScale;
                return geometry;
            }
            var rt = component.GetComponent<RectTransform>();
            if (rt == null) return null;
            var padding = Vector2.zero;
            if (text != null)
            {
                var labelRect = rt.rect;
                var textRect = text.rectTransform.rect;
                padding = new Vector2(Math.Max(0f, labelRect.width - textRect.width), Math.Max(0f, labelRect.height - textRect.height));
            }
            geometry = new Geometry { Rect = rt, Text = text, Pivot = rt.pivot, Padding = padding, Scale = rt.lossyScale, FontSize = text != null ? text.fontSize : 0f };
            if (geometries.Count > 2048) geometries.Clear();
            geometries[key] = geometry;
            return geometry;
        }

        private static ItemData DataOf(Entity entity)
        {
            var key = entity.Pointer;
            if (itemData.TryGetValue(key, out var data) && data.EntityId == entity.id) return data;
            data = new ItemData { EntityId = entity.id };
            var stack = entity.GetCachedComponent<IPickupable>(false)?.GetItemStack();
            var item = stack?.Item;
            if (item != null)
            {
                data.Kind = item.Identifier;
                data.Name = item.Name;
                data.Stack = stack;
            }
            if (itemData.Count > 4096) itemData.Clear();
            itemData[key] = data;
            return data;
        }

        // The preferred size of a text in the label font, measured once for each font size and text string.
        private static Vector2 TextSize(Geometry geometry, string text)
        {
            if (text == null) return Vector2.zero;
            var key = (geometry.FontSize, text);
            if (textSizes.TryGetValue(key, out var size)) return size;
            size = geometry.Text.GetPreferredValues(text);
            if (textSizes.Count > 4096) textSizes.Clear();
            textSizes[key] = size;
            return size;
        }

        // The screen rectangle of a label of the given size (in canvas units) with its pivot at the position.
        private static ScreenRect RectAt(Geometry geometry, Vector3 position, Vector2 size)
        {
            float w = size.x * geometry.Scale.x, h = size.y * geometry.Scale.y;
            float x = position.x - geometry.Pivot.x * w, y = position.y - geometry.Pivot.y * h;
            return new ScreenRect(x, y, x + w, y + h);
        }

        private static void LogOncePerSecond()
        {
            if (!Plugin.Verbose.Value || logClock.ElapsedMilliseconds < 1000) return;
            double mean = frames > 0 ? sumMs / frames : 0;
            int gc = GC.CollectionCount(0);
            Plugin.Debug($"Labels: radius {Plugin.PickupRadius.Value:0.##} m, {lastItems} item labels, {lastOthers} other labels, {lastGroups} label groups, largest {lastLargest}, " +
                         $"{lastHidden} hidden, {lastLabelBoxes} label boxes; postfix {mean:0.000} ms mean, {maxMs:0.000} ms max over {frames} frames, " +
                         $"{slowFrames} over 2 ms, {gc - gcAtLog} GCs");
            logClock.Restart();
            sumMs = 0;
            maxMs = 0;
            frames = 0;
            slowFrames = 0;
            gcAtLog = gc;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HudUI), "onLabelMouseEnter")]
        private static void MouseEnterPostfix(Entity targetEntity)
        {
            try
            {
                Hovered = targetEntity;
                exitPending = false;
                if (targetEntity == null)
                {
                    frozen.Clear();
                    return;
                }
                FreezeGroupOf(targetEntity.id);
                if (!Plugin.Verbose.Value) return;
                var data = DataOf(targetEntity);
                Plugin.Debug($"Hover: entity {targetEntity.id}, kind {data.Kind ?? "(not an item)"}, label group {Math.Max(1, frozen.Count)}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce("Label hover", e);
            }
        }

        // The game removes a hovered label and opens its hover card, so the cursor leaves the label while the
        // entity stays hovered. The entity stays hovered while its hover card is open.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HudUI), "onLabelMouseExit")]
        private static void MouseExitPostfix(Entity targetEntity)
        {
            if (Hovered == null || targetEntity == null || Hovered.id != targetEntity.id) return;
            exitPending = true;
        }

        private static bool exitPending;

        // The label group of the hovered item, kept while its hover card is open.
        private static readonly HashSet<int> frozen = new HashSet<int>();

        // The font size of the item labels, read from the first item label that the mod sees.
        private static float labelFontSize;
    }
}
