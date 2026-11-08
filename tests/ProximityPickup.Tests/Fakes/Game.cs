// Fake game types in the global namespace, with the types and members that the mod source reaches.
// The test command reads the type names of this file to mark a source file as game-facing, so generic helpers
// belong in other files.

using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InteractionType { PICKUP, OTHER }

public enum InteractionInputType { PRIMARY }

public enum ActionType { DEFAULT }

public enum InteractionSource { WORLD }

public enum ItemTag { EQUIPPABLE, TWO_HANDED, WORN_ON_BACK }

public enum InputAction { Interact = 1, Sprint = 2 }

public static class L
{
    public static readonly Dictionary<string, string> Texts = new Dictionary<string, string>();

    public static string T(string key) => Texts[key];
}

public class InventoryItem : Il2CppObjectBase
{
    public string Identifier;
    public string Name;
    public readonly HashSet<ItemTag> Tags = new HashSet<ItemTag>();

    public bool HasTag(ItemTag tag) => Tags.Contains(tag);
}

public class InventoryItemStack : Il2CppObjectBase
{
    public InventoryItem Item;
    public int Quantity = 1;
    public bool PrefersEquipped;

    public bool PreferEquippedSlot() => PrefersEquipped;
}

public class IPickupable : Il2CppObjectBase
{
    public InventoryItemStack Stack;

    public InventoryItemStack GetItemStack() => Stack;
}

public class Inventory : Il2CppObjectBase
{
    public bool Room = true;

    public bool HasRoom(InventoryItemStack stack) => Room;
}

public class IInventoryHolder : Il2CppObjectBase
{
    public Inventory Inventory;

    public Inventory GetInventory() => Inventory;
}

public class Entity : Il2CppObjectBase
{
    private readonly Dictionary<System.Type, object> cached = new Dictionary<System.Type, object>();

    public int id;
    public bool destroyed;
    public Transform transform;

    public void SetCached<T>(T component) where T : class { cached[typeof(T)] = component; }

    public T GetCachedComponent<T>(bool write) where T : class
    {
        return cached.TryGetValue(typeof(T), out var component) ? (T)component : null;
    }
}

public class IEntityManager : Il2CppObjectBase
{
    public readonly Dictionary<GameObject, Entity> Entities = new Dictionary<GameObject, Entity>();

    public Entity GetEntity(GameObject gameObject)
    {
        return Entities.TryGetValue(gameObject, out var entity) ? entity : null;
    }
}

public class IWorldManager : Il2CppObjectBase { }

public class WorldManager : IWorldManager
{
    public IEntityManager entityManager;
}

public class LocalPlayer : Il2CppObjectBase
{
    public Entity Entity;
    public readonly Dictionary<InputAction, string> KeyNames = new Dictionary<InputAction, string>();

    public Entity GetEntity() => Entity;

    public string GetKeyNamesTextForAction(InputAction action, string prepend, string append, bool shortVersion) => KeyNames[action];
}

public class IUIManager : Il2CppObjectBase
{
    public object Next;

    public T InstantiateUI<T>(Transform parentTransform, System.Action<T> initializer) => (T)Next;
}

public class UIComponent : Behaviour
{
    internal static IUIManager uiManager;
}

public class KeyBindLabel : UIComponent
{
    private const int ModifierFlagOffset = 0x74, ModifierValueOffset = 0x78;

    public bool inputActionPresent;
    public InputAction inputAction;

    // A native block, because the mod reads the modifier at its offsets.
    public KeyBindLabel()
    {
        Pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(0x80);
        SetModifier(0, 0);
    }

    public void SetModifier(byte flag, int value)
    {
        System.Runtime.InteropServices.Marshal.WriteByte(Pointer + ModifierFlagOffset, flag);
        System.Runtime.InteropServices.Marshal.WriteInt32(Pointer + ModifierValueOffset, value);
    }
}

public class HudObjectLabel : UIComponent
{
    public TextMeshProUGUI text;
}

public class HudObjectInteractionUI : UIComponent
{
    public TextMeshProUGUI descriptionText;
    public Image activeBarImage;
    public KeyBindLabel keyBindLabel;
}

public class HudGameObjectUI : UIComponent
{
    public TextMeshProUGUI labelText;
    public LayoutGroup interactionsArea;
}

public class InventoryControlsUI : UIComponent
{
    public TextMeshProUGUI controls1, controls2, controls3, controls4, controls5;
}

public class HudQuickSlotsUI : UIComponent { }

public class TerrainLayerZone : Il2CppObjectBase { }

public class HudUI : UIComponent
{
    public LocalPlayer player;
    public IWorldManager worldManager;
    public Il2CppSystem.Collections.Generic.Dictionary<Entity, HudObjectLabel> entityLabelMap;
    public Il2CppSystem.Collections.Generic.Dictionary<TerrainLayerZone, HudObjectLabel> terrainLayerZoneLabelMap;
    public Il2CppSystem.Collections.Generic.Dictionary<Entity, HudGameObjectUI> entityHudMap;
    public InventoryControlsUI inventoryControlsUI;
    public HudQuickSlotsUI quickSlots;
}

public class InteractionManager : Il2CppObjectBase
{
    public void StartInteraction(InteractionType type, InteractionInputType inputType, ActionType actionType, Entity interactorEntity, InteractionSource source, Entity targetEntity) { }
}

public class IInteraction : Il2CppObjectBase { }

public class PickupInteraction : IInteraction
{
    public Entity targetEntity;
}

public class PickupEffect : Il2CppObjectBase
{
    public struct PickupEffectResult
    {
        public bool success;
        public bool insufficientRoom;
        public Vector3 position;
        public int amount;
    }

    public IEntityManager entityManager;
    public IInteraction Interaction = new IInteraction();
    public ActionType Action = ActionType.DEFAULT;
    public Entity Interactor, Target;

    // Runs for each ExecuteResult call. A test sets it to give a result, or to enter the postfix again.
    public System.Func<PickupEffect, PickupEffectResult> OnExecute = _ => new PickupEffectResult { success = true, amount = 1 };

    public readonly List<Entity> Inits = new List<Entity>();
    public readonly List<Entity> Executed = new List<Entity>();
    public readonly List<PickupEffectResult> Finished = new List<PickupEffectResult>();

    public Entity GetInteractorEntity() => Interactor;
    public Entity GetTargetEntity() => Target;
    public IInteraction GetInteraction() => Interaction;
    public ActionType GetActionType() => Action;

    public void Init(IInteraction interaction, Entity interactorEntity, Entity targetEntity, ActionType actionType)
    {
        Interaction = interaction;
        Interactor = interactorEntity;
        Target = targetEntity;
        Action = actionType;
        Inits.Add(targetEntity);
    }

    public PickupEffectResult ExecuteResult()
    {
        Executed.Add(Target);
        return OnExecute(this);
    }

    public void OnExecuted(PickupEffectResult result) { Finished.Add(result); }
}
