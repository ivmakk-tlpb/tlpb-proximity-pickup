using ProximityPickup.Labels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A fake HUD with item labels, zone labels, and hover cards, and the frame that the game runs for them.
internal sealed class LabelScene
{
    private sealed class Placed
    {
        public Transform Transform;
        public TextMeshProUGUI Text;
        public string GameText;
        public Vector3 At;
    }

    private readonly List<Placed> placed = new List<Placed>();
    private readonly Dictionary<Component, Placed> placeOf = new Dictionary<Component, Placed>();

    public readonly HudUI Hud;
    public readonly Entity Player;
    public readonly IEntityManager EntityManager = new IEntityManager();
    public readonly Dictionary<int, HudObjectLabel> Labels = new Dictionary<int, HudObjectLabel>();

    public LabelScene()
    {
        Hud = new GameObject("hud").AddComponent<HudUI>();
        Player = NewEntity(1);
        Hud.player = new LocalPlayer { Entity = Player };
        Hud.worldManager = new WorldManager { entityManager = EntityManager };
        Hud.entityLabelMap = new Il2CppSystem.Collections.Generic.Dictionary<Entity, HudObjectLabel>();
        Hud.terrainLayerZoneLabelMap = new Il2CppSystem.Collections.Generic.Dictionary<TerrainLayerZone, HudObjectLabel>();
        Hud.entityHudMap = new Il2CppSystem.Collections.Generic.Dictionary<Entity, HudGameObjectUI>();
        Hud.quickSlots = NewRect<HudQuickSlotsUI>(new Rect(-100f, -25f, 200f, 50f), new Vector3(960f, 50f, 0f));
    }

    public static Entity NewEntity(int id, Vector3 world = default)
    {
        var entity = new Entity { id = id, transform = new GameObject("entity").transform };
        entity.transform.position = world;
        return entity;
    }

    public static Entity NewItem(int id, string kind, string name, Vector3 world, int quantity = 1, bool prefersEquipped = false, params ItemTag[] tags)
    {
        var entity = NewEntity(id, world);
        var item = new InventoryItem { Identifier = kind, Name = name };
        foreach (var tag in tags) item.Tags.Add(tag);
        entity.SetCached(new IPickupable { Stack = new InventoryItemStack { Item = item, Quantity = quantity, PrefersEquipped = prefersEquipped } });
        return entity;
    }

    // A component on a new object with a RectTransform that is centered on its position.
    public static T NewRect<T>(Rect rect, Vector3 position) where T : Component, new()
    {
        var go = new GameObject(typeof(T).Name);
        var rt = go.AddComponent<RectTransform>();
        rt.rect = rect;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = position;
        return go.AddComponent<T>();
    }

    private static TextMeshProUGUI NewText(string text, Vector3 position)
    {
        float width = text.Length * 10f;
        var tmp = NewRect<TextMeshProUGUI>(new Rect(-width / 2f, -10f, width, 20f), position);
        tmp.text = text;
        tmp.fontSize = 18f;
        return tmp;
    }

    // An item in the world and the label that the game draws for it.
    public Entity Item(int id, string kind, Vector3 world, Vector3 screen, int quantity = 1)
    {
        string name = char.ToUpperInvariant(kind[0]) + kind.Substring(1);
        var entity = NewItem(id, kind, name, world, quantity);
        AddLabel(entity, screen, name);
        return entity;
    }

    public HudObjectLabel AddLabel(Entity entity, Vector3 screen, string text)
    {
        var label = NewLabel(screen, text);
        Hud.entityLabelMap.Add(entity, label);
        Labels[entity.id] = label;
        return label;
    }

    public HudObjectLabel AddZone(Vector3 screen, string text)
    {
        var label = NewLabel(screen, text);
        Hud.terrainLayerZoneLabelMap.Add(new TerrainLayerZone(), label);
        return label;
    }

    private HudObjectLabel NewLabel(Vector3 screen, string text)
    {
        float width = text.Length * 10f + 20f;
        var label = NewRect<HudObjectLabel>(new Rect(-width / 2f, -10f, width, 20f), screen);
        label.text = NewText(text, screen);
        label.text.transform.SetParent(label.transform, false);
        Track(label, label.text, screen);
        return label;
    }

    public HudGameObjectUI OpenCard(Entity entity, Vector3 screen, string title)
    {
        var card = NewRect<HudGameObjectUI>(new Rect(-100f, -40f, 200f, 80f), screen);
        card.labelText = NewText(title, screen);
        card.labelText.transform.SetParent(card.transform, false);
        var area = new GameObject("area").AddComponent<LayoutGroup>();
        area.transform.SetParent(card.transform, false);
        card.interactionsArea = area;
        Track(card, card.labelText, screen);
        Hud.entityHudMap.Add(entity, card);
        return card;
    }

    // One action row of a hover card. progress: the bar of a hold action, or null for a row with no active bar.
    public static HudObjectInteractionUI AddRow(HudGameObjectUI card, string description, float? progress = null)
    {
        var row = new GameObject("row").AddComponent<HudObjectInteractionUI>();
        row.transform.SetParent(card.interactionsArea.transform, false);
        row.transform.position = new Vector3(10f, 20f, 30f);
        row.descriptionText = NewText(description, Vector3.zero);
        row.descriptionText.transform.SetParent(row.transform, false);
        row.keyBindLabel = new GameObject("key").AddComponent<KeyBindLabel>();
        row.activeBarImage = NewRect<Image>(new Rect(0f, 0f, 10f, 10f), Vector3.zero);
        row.activeBarImage.enabled = progress.HasValue;
        row.activeBarImage.rectTransform.anchorMax = new Vector2(progress ?? 0f, 1f);
        return row;
    }

    // The copy of the inventory controls panel that the fake UI manager gives to the mod.
    public static InventoryControlsUI NewHintPanel()
    {
        var panel = NewRect<InventoryControlsUI>(new Rect(-200f, -20f, 400f, 40f), Vector3.zero);
        panel.controls1 = NewText("1", Vector3.zero);
        panel.controls2 = NewText("2", Vector3.zero);
        panel.controls3 = NewText("3", Vector3.zero);
        panel.controls4 = NewText("4", Vector3.zero);
        panel.controls5 = NewText("5", Vector3.zero);
        UIComponent.uiManager = new IUIManager { Next = panel };
        return panel;
    }

    // The label box of a label or a hover card, or null.
    public static RectTransform BoxOf(Component owner)
    {
        foreach (var child in owner.transform.children)
            if (child.gameObject.name == "ProximityPickupLabelBox") return (RectTransform)child;
        return null;
    }

    private void Track(Component owner, TextMeshProUGUI text, Vector3 at)
    {
        var place = new Placed { Transform = owner.transform, Text = text, GameText = text.text, At = at };
        placed.Add(place);
        placeOf[owner] = place;
    }

    // The place where the game puts the label or the card from the next frame on.
    public void MoveInGame(Component owner, Vector3 at) => placeOf[owner].At = at;

    // One game frame: the game places each label and writes its own text, and then the postfix of the mod runs.
    // gamePlaces false: the game did not place the labels this frame, so they stay where the mod wrote them.
    public void Frame(bool gamePlaces = true)
    {
        Time.frameCount++;
        if (gamePlaces)
        {
            foreach (var place in placed)
            {
                place.Transform.position = place.At;
                place.Text.transform.position = place.At;
                place.Text.text = place.GameText;
            }
        }
        PatchCall.Run(typeof(LabelPatches), "UpdatePostfix", Hud);
    }
}
