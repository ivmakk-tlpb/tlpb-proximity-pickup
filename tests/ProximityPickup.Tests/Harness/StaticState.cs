using System.Diagnostics;
using System.Reflection;
using ProximityPickup;
using ProximityPickup.Labels;
using ProximityPickup.Pickup;

// Puts the static state of the patch classes back to its state at class load, and reads and sets private members.
internal static class StaticState
{
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type[] PatchClasses = { typeof(LabelPatches), typeof(PickupPatches), typeof(GatherPatches), typeof(ClickPatches), typeof(DigPatches), typeof(Plugin) };

    // Read in the static constructor, which runs before the first test changes a field.
    private static readonly List<(FieldInfo Field, object Value)> AtLoad = ReadFieldsAtLoad();

    private static List<(FieldInfo, object)> ReadFieldsAtLoad()
    {
        var fields = new List<(FieldInfo, object)>();
        foreach (var type in PatchClasses)
            foreach (var field in type.GetFields(AnyStatic))
                if (!field.IsLiteral) fields.Add((field, field.GetValue(null)));
        return fields;
    }

    public static void Reset()
    {
        foreach (var (field, value) in AtLoad)
        {
            if (!field.IsInitOnly) field.SetValue(null, value);
            var current = field.GetValue(null);
            if (current is Stopwatch clock) clock.Restart();
            else if (current is HoverFreeze freeze) ResetFreeze(freeze);
            else Clear(current);
        }
    }

    // A static readonly object cannot be replaced, so its private state goes back by hand.
    private static void ResetFreeze(HoverFreeze freeze)
    {
        foreach (var field in typeof(HoverFreeze).GetFields(AnyInstance))
        {
            if (field.FieldType == typeof(bool)) field.SetValue(freeze, false);
            else Clear(field.GetValue(freeze));
        }
    }

    private static void Clear(object collection)
    {
        collection?.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(collection, null);
    }

    public static T Get<T>(Type type, string field) => (T)FieldOf(type, field, AnyStatic).GetValue(null);

    public static void Set(Type type, string field, object value) => FieldOf(type, field, AnyStatic).SetValue(null, value);

    public static T Member<T>(object target, string field) => (T)FieldOf(target.GetType(), field, AnyInstance).GetValue(target);

    private static FieldInfo FieldOf(Type type, string field, BindingFlags flags)
    {
        return type.GetField(field, flags) ?? throw new MissingFieldException(type.Name, field);
    }
}
