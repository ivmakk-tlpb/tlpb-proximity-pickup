// Fake BepInEx and Harmony types. They hold what a test sets or reads, and they load nothing.

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BepInPlugin : Attribute
    {
        public BepInPlugin(string guid, string name, string version) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BepInProcess : Attribute
    {
        public BepInProcess(string processName) { }
    }
}

namespace BepInEx.Configuration
{
    public class AcceptableValueBase { }

    public sealed class AcceptableValueList<T> : AcceptableValueBase
    {
        public readonly T[] AcceptableValues;

        public AcceptableValueList(params T[] values) { AcceptableValues = values; }
    }

    public sealed class ConfigDescription
    {
        public readonly string Description;
        public readonly AcceptableValueBase AcceptableValues;

        public ConfigDescription(string description, AcceptableValueBase acceptableValues = null)
        {
            Description = description;
            AcceptableValues = acceptableValues;
        }
    }

    public sealed class ConfigEntry<T>
    {
        public T Value;
        public readonly ConfigDescription Description;

        public ConfigEntry(T defaultValue, ConfigDescription description)
        {
            Value = defaultValue;
            Description = description;
        }
    }

    public sealed class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription description)
        {
            return new ConfigEntry<T>(defaultValue, description);
        }

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            return Bind(section, key, defaultValue, new ConfigDescription(description));
        }
    }
}

namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Debugs = new List<string>();

        public void LogInfo(object data) { Infos.Add(data.ToString()); }
        public void LogWarning(object data) { Warnings.Add(data.ToString()); }
        public void LogDebug(object data) { Debugs.Add(data.ToString()); }
    }
}

namespace BepInEx.Unity.IL2CPP
{
    public abstract class BasePlugin
    {
        public BepInEx.Logging.ManualLogSource Log { get; } = new BepInEx.Logging.ManualLogSource();
        public BepInEx.Configuration.ConfigFile Config { get; } = new BepInEx.Configuration.ConfigFile();

        public abstract void Load();
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string method) { }
        public HarmonyPatch(Type type, string method, Type[] parameters) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPrefix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute { }

    // Applies no patch: it records each call. A test sets ThrowFor to make PatchAll fail for one patch class.
    public sealed class Harmony
    {
        public static readonly List<Type> Patched = new List<Type>();
        public static readonly List<string> Unpatched = new List<string>();
        public static Type ThrowFor;

        public readonly string Id;

        public Harmony(string id) { Id = id; }

        public void PatchAll(Type type)
        {
            if (type == ThrowFor) throw new InvalidOperationException("Target not found");
            Patched.Add(type);
        }

        public void UnpatchSelf() { Unpatched.Add(Id); }
    }
}
