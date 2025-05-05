using System;
using Bloodstone.Network;
using HarmonyLib;
using ProjectM.Network;
using ProjectM.UI;
using Unity.Collections;

namespace Bloodstone.Hooks;

public static class ClientChat
{
    private static Harmony? _harmony;

    public static void Initialize()
    {
        if (_harmony != null)
            throw new Exception("Detour already initialized. You don't need to call this. The Bloodstone plugin will do it for you.");

        _harmony = Harmony.CreateAndPatchAll(typeof(ClientChat), MyPluginInfo.PLUGIN_GUID);
    }

    public static void Uninitialize()
    {
        if (_harmony == null)
            throw new Exception("Detour wasn't initialized. Are you trying to unload Bloodstone twice?");

        _harmony.UnpatchSelf();
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClientChatSystem), nameof(ClientChatSystem.OnUpdate))]
    private static void OnUpdatePrefix(ClientChatSystem __instance)
    {
        var entities = __instance.__query_172511197_1.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var ev = __instance.EntityManager.GetComponentData<ChatMessageServerEvent>(entity);
            if (ev.MessageType == ServerChatMessageType.System && MessageUtils.DeserialiseMessage(ev.MessageText.ToString()))
            {
                // Remove this as it is an internal message that the user is unlikely wanting to see in their chat
                __instance.EntityManager.DestroyEntity(entity);
            }
        }
    }
}