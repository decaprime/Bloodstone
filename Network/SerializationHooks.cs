using System;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Hook;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Collections;
using Unity.Entities;
using Bloodstone.API;
using Bloodstone.Util;
using static NetworkEvents_Serialize;
using HarmonyLib;
using ProjectM;

namespace Bloodstone.Network;

/// Contains the serialization hooks for custom packets.
internal static class SerializationHooks
{
    // chosen by fair dice roll
    internal const int BLOODSTONE_NETWORK_EVENT_ID = 0x000FD00D;

    private static INativeDetour? _serializeDetour;
    private static INativeDetour? _deserializeDetour;
    private static INativeDetour? _eventsReceivedDetour;

    private static Harmony? _harmony;

    // Detour events.
    public static void Initialize()
    {
        unsafe
        {
            _serializeDetour = NativeHookUtil.Detour(typeof(NetworkEvents_Serialize), "SerializeEvent", SerializeHook, out SerializeOriginal);
            _deserializeDetour = NativeHookUtil.Detour(typeof(NetworkEvents_Serialize), "DeserializeEvent", DeserializeHook, out DeserializeOriginal);
        }

        if (VWorld.IsClient)
            _eventsReceivedDetour = NativeHookUtil.Detour(typeof(ClientBootstrapSystem), nameof(ClientBootstrapSystem.OnReliableEventsReceived), EventsReceivedHook, out EventsReceivedOriginal);
        else if (VWorld.IsServer)
            _harmony = Harmony.CreateAndPatchAll(typeof(SerializeAndSendServerEventsSystem_Patch));
    }

    // Undo detours.
    public static void Uninitialize()
    {
        _serializeDetour?.Dispose();
        _deserializeDetour?.Dispose();
        _eventsReceivedDetour?.Dispose();
        _harmony?.UnpatchSelf();
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate void SerializeEvent(IntPtr entityManager, NetworkEventType networkEventType, ref NetBufferOut netBufferOut, Entity entity);

    public static SerializeEvent? SerializeOriginal;

    public unsafe static void SerializeHook(IntPtr entityManager, NetworkEventType networkEventType, ref NetBufferOut netBufferOut, Entity entity)
    {
        // if this is not a custom event, just call the original function
        if (networkEventType.EventId != SerializationHooks.BLOODSTONE_NETWORK_EVENT_ID)
        {
            SerializeOriginal!(entityManager, networkEventType, ref netBufferOut, entity);
            return;
        }

        // extract the custom network event
        var data = (CustomNetworkEvent)VWorld.Server.EntityManager.GetComponentObject<Il2CppSystem.Object>(entity, CustomNetworkEvent.ComponentType);

        // write out the event ID and the data
        netBufferOut.Write((uint)SerializationHooks.BLOODSTONE_NETWORK_EVENT_ID);
        data.Serialize(ref netBufferOut);
    }

    // --------------------------------------------------------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate void DeserializeEvent(IntPtr entityManager, IntPtr commandBuffer, ref NetBufferIn netBuffer, DeserializeNetworkEventParams eventParams);

    public static DeserializeEvent? DeserializeOriginal;

    public unsafe static void DeserializeHook(IntPtr entityManager, IntPtr commandBuffer, ref NetBufferIn netBufferIn, DeserializeNetworkEventParams eventParams)
    {
        var eventId = netBufferIn.ReadUInt32();
        if (eventId != SerializationHooks.BLOODSTONE_NETWORK_EVENT_ID)
        {
            // rewind the buffer
            netBufferIn.m_readPosition -= 32;

            DeserializeOriginal!(entityManager, commandBuffer, ref netBufferIn, eventParams);
            return;
        }

        var typeName = netBufferIn.ReadString(Allocator.Temp);
        if (MessageRegistry._eventHandlers.ContainsKey(typeName))
        {
            var handler = MessageRegistry._eventHandlers[typeName];
            var isFromServer = eventParams.FromCharacter.User == Entity.Null;

            try
            {
                if (isFromServer)
                    handler.OnReceiveFromServer(netBufferIn);
                else
                    handler.OnReceiveFromClient(eventParams.FromCharacter, netBufferIn);
            }
            catch (Exception ex)
            {
                BloodstonePlugin.Logger.LogError($"Error handling incoming network event {typeName}:");
                BloodstonePlugin.Logger.LogError(ex);
            }
        }
    }

    // --------------------------------------------------------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate void EventsReceived(IntPtr _this, ref NetBufferIn netBuffer);

    public static EventsReceived? EventsReceivedOriginal;

    public unsafe static void EventsReceivedHook(IntPtr _this, ref NetBufferIn netBuffer)
    {
        var eventId = netBuffer.ReadInt32();
        if (eventId != SerializationHooks.BLOODSTONE_NETWORK_EVENT_ID)
        {
            netBuffer.m_readPosition -= 32;
            EventsReceivedOriginal!(_this, ref netBuffer);
            return;
        }

        var typeName = netBuffer.ReadString(Allocator.Temp);
        if (MessageRegistry._eventHandlers.ContainsKey(typeName))
        {
            var handler = MessageRegistry._eventHandlers[typeName];

            try
            {
                handler.OnReceiveFromServer(netBuffer);
            }
            catch (Exception ex)
            {
                BloodstonePlugin.Logger.LogError($"Error handling incoming network event {typeName}:");
                BloodstonePlugin.Logger.LogError(ex);
            }
        }
    }

    // --------------------------------------------------------------------------------------

    private static class SerializeAndSendServerEventsSystem_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SerializeAndSendServerEventsSystem), nameof(SerializeAndSendServerEventsSystem.OnUpdate))]
        private static void OnUpdatePrefix(SerializeAndSendServerEventsSystem __instance)
        {
            var entities = __instance.EntityQueries[1].ToEntityArray(Allocator.Temp);
            var toUserTypeIndex = ComponentType.ReadOnly<SendEventToUser>().TypeIndex;

            foreach (var entity in entities)
            {
                var eventType = __instance.EntityManager.GetComponentData<NetworkEventType>(entity);
                if (eventType.EventId == SerializationHooks.BLOODSTONE_NETWORK_EVENT_ID)
                {
                    var data = __instance.EntityManager.GetComponentObject<CustomNetworkEvent>(entity);
                    unsafe
                    {
                        SendEventToUser toUser = *(SendEventToUser*)__instance.EntityManager.GetComponentDataRawRO(entity, toUserTypeIndex);

                        var netBuffer = new NetBufferOut(new NativeArray<byte>(16384, Allocator.Temp));
                        netBuffer.Write((byte)PacketType.Events);
                        netBuffer.Write(eventType.EventId);
                        data.Serialize(ref netBuffer);

                        // TODO: This throws after 1.0
                        // __instance._ServerBootstrapSystem.SendPacketToUser(netBuffer.Data, netBuffer.Data.Length * 8, toUser.UserIndex, true, true);
                        BloodstonePlugin.Logger.LogWarning($"Functionality Disabled: Not sending CustomNetworkEvent to user {toUser.UserIndex}");
                    }
                    __instance.EntityManager.DestroyEntity(entity);
                }
            }
        }
    }
}