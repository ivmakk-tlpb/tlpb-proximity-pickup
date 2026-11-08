using System.Reflection;
using System.Runtime.ExceptionServices;

// Calls a private static patch method. A ref argument comes back in the args array.
internal static class PatchCall
{
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static object Run(Type type, string method, params object[] args)
    {
        var info = type.GetMethod(method, AnyStatic) ?? throw new MissingMethodException(type.Name, method);
        try
        {
            return info.Invoke(null, args);
        }
        catch (TargetInvocationException e)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
