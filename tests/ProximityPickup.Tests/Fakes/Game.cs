// Fake game types in the global namespace, with the types and members that the mod source reaches.
// The test command reads the type names of this file to mark a source file as game-facing, so generic helpers
// belong in other files.

using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InteractionType { NONE, PICKUP, GATHER, OTHER }

public enum InteractionInputType { BEGIN, HOLD, END }

public enum ActionType { PRIMARY, SECONDARY }

public enum InteractionSource { PLAYER, ENTITY }

public enum InteractionOrdinality { PRIMARY, SECONDARY }

public class InteractionEventParams : Il2CppObjectBase
{
    public InteractionSource source;
    public InteractionOrdinality ordinality;
    public ActionType actionType;
    public InteractionInputType inputType;
    public Entity interactorEntity;
    public Entity targetEntity;
    public InteractionType desiredInteractionType;
    public Il2CppSystem.Collections.Generic.HashSet<InteractionType> requiredInteractionTypes;
    public Il2CppSystem.Collections.Generic.HashSet<InteractionType> ignoredInteractionTypes;
    public object data;
}

public class InteractionEventHandler : Il2CppObjectBase
{
    public void Execute(InteractionEventParams eventParams) { }
}

public class InteractionResult : Il2CppObjectBase
{
    public bool possible, started, withinRange;
}

public class IInteractor : Il2CppObjectBase { }

public class PlayerInputView : Il2CppObjectBase
{
    public Entity entity;
    public Entity primaryActionDownEntity;
    public bool primaryActionUpLocked;

    public void Update() { }
}

public enum ItemTag { EQUIPPABLE, TWO_HANDED, WORN_ON_BACK }

public enum SurfaceType { NONE, MUD, CLAY, DIRT, TILLED_SOIL, WET_TILLED_SOIL }

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
    public int Width = 1, Height = 1;
    // The stack size of a stack of this item. A test-only value: the game reads it from the stack.
    public int MaxStack = 1;
    public readonly HashSet<ItemTag> Tags = new HashSet<ItemTag>();

    public bool HasTag(ItemTag tag) => Tags.Contains(tag);
}

public class InventoryItemStack : Il2CppObjectBase
{
    public InventoryItem Item;
    public int Quantity = 1;
    public bool PrefersEquipped;

    public bool PreferEquippedSlot() => PrefersEquipped;
    public int GetMaxStackAmount() => Item?.MaxStack ?? 1;
}

public class IPickupable : Il2CppObjectBase
{
    public InventoryItemStack Stack;

    public InventoryItemStack GetItemStack() => Stack;
}

public class Inventory : Il2CppObjectBase
{
    public bool Room = true;
    public readonly List<InventorySlot> Slots = new List<InventorySlot>();

    public bool HasRoom(InventoryItemStack stack) => Room;

    public Il2CppSystem.Collections.Generic.List<InventorySlot> GetAllSlots()
    {
        var all = new Il2CppSystem.Collections.Generic.List<InventorySlot>();
        foreach (var slot in Slots) all.Add(slot);
        return all;
    }
}

public class IInventorySection : Il2CppObjectBase { }

public class InventorySlot : Il2CppObjectBase
{
    public IInventorySection inventorySection;
    public InventoryItemStack Stack;
    public bool Handheld;
    public bool Accepts = true;
    public bool Allowed = true;
    // True for a cell of a stack larger than one slot: the stack lies in its origin slot.
    public bool Covered;

    public InventoryItemStack GetStack() => Stack;
    public bool HasOriginStack() => Covered || Stack != null;
    public bool IsEmpty() => Stack == null;
    public bool IsHandheld() => Handheld;
    public bool AcceptsItems() => Accepts;
    public bool IsItemAllowed(InventoryItem item, bool checkLinkedSlotRestriction) => Allowed;
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
    public bool isHostEntity = true;
    public World world;
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
    public IInventoryManager inventoryManager;
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
    public IEntityManager entityManager;
    public IInventoryManager inventoryManager;

    public void StartInteraction(InteractionType type, InteractionInputType inputType, ActionType actionType, Entity interactorEntity, InteractionSource source, Entity targetEntity) { }
}

public class IInteraction : Il2CppObjectBase { }

public class ItemDrop : Il2CppObjectBase
{
    public string itemIdentifier;
    public int amount = 1;
}

public class Resource : Il2CppObjectBase
{
    public string Name;
}

public class IResource : Il2CppObjectBase
{
    public Resource Resource;

    public Resource GetResource() => Resource;
}

public class IGatherable : Il2CppObjectBase
{
    public bool Ready = true;
    public float Duration = 0.8f;
    public bool Continuous;
    public Il2CppSystem.Collections.Generic.List<ItemDrop> Drops = new Il2CppSystem.Collections.Generic.List<ItemDrop>();

    public bool CanGather() => Ready;
    public float GetGatherDuration() => Duration;
    public bool GatherContinuously() => Continuous;
    public Il2CppSystem.Collections.Generic.List<ItemDrop> GetItemDrops() => Drops;
}

public class IGrowable : Il2CppObjectBase
{
    public Il2CppSystem.Collections.Generic.List<ItemDrop> Accumulated = new Il2CppSystem.Collections.Generic.List<ItemDrop>();

    public Il2CppSystem.Collections.Generic.List<ItemDrop> GetAccumulatedItemDrops() => Accumulated;
}

public class ISkills : Il2CppObjectBase
{
    public float ForageBonus;

    public float GetForageBonusAmount() => ForageBonus;
}

public class IInventoryManager : Il2CppObjectBase
{
    public readonly Dictionary<string, InventoryItem> Items = new Dictionary<string, InventoryItem>();

    public InventoryItem GetItem(string identifier) => Items.TryGetValue(identifier, out var item) ? item : null;

    public InventoryItemStack GetInventoryItemStack(string identifier, int quantity)
    {
        var item = GetItem(identifier);
        return item == null ? null : new InventoryItemStack { Item = item, Quantity = quantity };
    }
}

public class GatherInteraction : BaseInteraction
{
    public Entity targetEntity;
}

public class GatherEffect : Il2CppObjectBase
{
    public struct GatherEffectResult
    {
        public bool success;
        public bool insufficientSpace;
        public Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<int, int>> itemCounts;
    }

    public IInventoryManager inventoryManager = new IInventoryManager();
    public IInteraction Interaction = new GatherInteraction();
    public ActionType Action = ActionType.PRIMARY;
    public Entity Interactor, Target;

    // Runs for each ExecuteResult call. A test sets it to give a result, or to enter the postfix again.
    public System.Func<GatherEffect, GatherEffectResult> OnExecute;

    public readonly List<Entity> Inits = new List<Entity>();
    public readonly List<Entity> Executed = new List<Entity>();
    public readonly List<GatherEffectResult> Finished = new List<GatherEffectResult>();

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

    public GatherEffectResult ExecuteResult()
    {
        Executed.Add(Target);
        return OnExecute(this);
    }

    public void OnExecuted(GatherEffectResult result) { Finished.Add(result); }
}

public class PlayerManager : Il2CppObjectBase { }

public class World : Il2CppObjectBase
{
    // The getter of the game reads maxVisibleLevel < 0. A test can make it throw.
    public System.Func<bool> Underground = () => false;
    public bool UndergroundVisible => Underground();

    public void GetEntitiesAroundPointWithAnyComponents(Vector3 position, float range, object result, object requiredComponentNames) { }

    public Il2CppSystem.Collections.Generic.HashSet<SurfaceType> specialGatherSurfaceTypes = new Il2CppSystem.Collections.Generic.HashSet<SurfaceType>();
    public float Height = 5f;
    public System.Func<Vector3, SurfaceType> Surface = _ => SurfaceType.CLAY;
    public System.Func<Vector3, bool> HasDrops = _ => true;
    public System.Func<Vector3, bool> Solid = _ => false;
    public float Deepness;

    public Vector3 GetPositionOnTerrain(Vector3 sourcePosition) => new Vector3(sourcePosition.x, Height, sourcePosition.z);
    public SurfaceType GetSurfaceTypeAtTerrainPosition(Vector3 position) => Surface(position);
    public bool IsSolidObjectTakenPosition(Vector3 position) => Solid(position);
    public float GetTerrainDeepness(Vector3 position, float radius) => Deepness;

    public Il2CppSystem.Collections.Generic.List<ItemDrop> GetGatherItemDropsAtPosition(Vector3 position)
    {
        var drops = new Il2CppSystem.Collections.Generic.List<ItemDrop>();
        if (HasDrops(position)) drops.Add(new ItemDrop());
        return drops;
    }
}

public class BaseInteraction : IInteraction
{
    public uint id;
    public int Ends;

    public void End() { Ends++; }
}

public class TerrainGatherInteraction : BaseInteraction
{
    public Vector3 targetPosition;
    public SurfaceType targetSurfaceType;
}

public class DigTerrainEffect : Il2CppObjectBase
{
    public struct DigTerrainEffectResult
    {
        public bool success;
        public bool insufficientSpace;
        public Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<int, int>> itemCounts;
    }

    public IInteraction Interaction;
    public Entity Interactor;

    private const int ParamsOffset = 0x30;

    // Runs for each ExecuteResult call, with the position and the surface of the params in native memory.
    public System.Func<Vector3, SurfaceType, DigTerrainEffectResult> OnExecute;

    public readonly List<(Vector3 Position, SurfaceType Surface)> Executed = new List<(Vector3, SurfaceType)>();
    public readonly List<DigTerrainEffectResult> Finished = new List<DigTerrainEffectResult>();

    // A native block, because the mod reads and writes the params at their offsets.
    public DigTerrainEffect()
    {
        Pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(0x60);
    }

    public void SetParams(Vector3 position, SurfaceType surface)
    {
        System.Runtime.InteropServices.Marshal.WriteInt32(Pointer + ParamsOffset, System.BitConverter.SingleToInt32Bits(position.x));
        System.Runtime.InteropServices.Marshal.WriteInt32(Pointer + ParamsOffset + 4, System.BitConverter.SingleToInt32Bits(position.y));
        System.Runtime.InteropServices.Marshal.WriteInt32(Pointer + ParamsOffset + 8, System.BitConverter.SingleToInt32Bits(position.z));
        System.Runtime.InteropServices.Marshal.WriteInt32(Pointer + ParamsOffset + 12, (int)surface);
    }

    public (Vector3 Position, SurfaceType Surface) ReadParams()
    {
        float Read(int offset) => System.BitConverter.Int32BitsToSingle(System.Runtime.InteropServices.Marshal.ReadInt32(Pointer + ParamsOffset + offset));
        return (new Vector3(Read(0), Read(4), Read(8)), (SurfaceType)System.Runtime.InteropServices.Marshal.ReadInt32(Pointer + ParamsOffset + 12));
    }

    public IInteraction GetInteraction() => Interaction;
    public Entity GetInteractorEntity() => Interactor;

    public DigTerrainEffectResult ExecuteResult()
    {
        var current = ReadParams();
        Executed.Add(current);
        return OnExecute(current.Position, current.Surface);
    }

    public void OnExecuted(DigTerrainEffectResult result) { Finished.Add(result); }
}

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
    public ActionType Action = ActionType.PRIMARY;
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
