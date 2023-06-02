using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using System;
using System.Reflection;

namespace Bloodstone.Util;

public static class NativeHookUtil
{
    public static INativeDetour Detour<T>(string typeName, string methodName, T to, out T original) where T : System.Delegate?
    {
        return Detour(Type.GetType(typeName), methodName, to, out original);
    }

    public static INativeDetour Detour<T>(Type type, string methodName, T to, out T original) where T : System.Delegate?
    {
        var method = type.GetMethod(methodName, AccessTools.all);
        return Detour(method, to, out original);
    }

    public static INativeDetour Detour<T>(MethodInfo method, T to, out T original) where T : System.Delegate?
    {
        var address = Il2CppMethodResolver.ResolveFromMethodInfo(method);
        BloodstonePlugin.Logger.LogInfo($"Detouring {method.DeclaringType?.FullName}.{method.Name} at {address.ToString("X")}");
        return INativeDetour.CreateAndApply(address, to, out original);
    }
}
