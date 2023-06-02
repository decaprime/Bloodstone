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

namespace Bloodstone.Network;

/// Contains the serialization hooks for custom packets.
internal static class SerializationHooks
{
    // chosen by fair dice roll
    internal const int Bloodstone_NETWORK_EVENT_ID = 0x000FD00D;

    private static INativeDetour? _serializeDetour;
    private static INativeDetour? _deserializeDetour;

    // Detour events.
    public static void Initialize()
    {
        unsafe
        {
            _serializeDetour = NativeHookUtil.Detour(typeof(NetworkEvents_Serialize), "SerializeEvent", SerializeHook, out SerializeOriginal);
            _deserializeDetour = NativeHookUtil.Detour(typeof(NetworkEvents_Serialize), "DeserializeEvent", DeserializeHook, out DeserializeOriginal);
        }
    }

    // Undo detours.
    public static void Uninitialize()
    {
        _serializeDetour?.Dispose();
        _deserializeDetour?.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate void SerializeEvent(IntPtr entityManager, NetworkEventType networkEventType, ref NetBufferOut netBufferOut, Entity entity);

    public static SerializeEvent? SerializeOriginal;

    public unsafe static void SerializeHook(IntPtr entityManager, NetworkEventType networkEventType, ref NetBufferOut netBufferOut, Entity entity)
    {
        // if this is not a custom event, just call the original function
        if (networkEventType.EventId != SerializationHooks.Bloodstone_NETWORK_EVENT_ID)
        {
            SerializeOriginal!(entityManager, networkEventType, ref netBufferOut, entity);
            return;
        }

        // extract the custom network event
        var data = (CustomNetworkEvent)VWorld.Server.EntityManager.GetComponentObject<Il2CppSystem.Object>(entity, CustomNetworkEvent.ComponentType);

        // write out the event ID and the data
        netBufferOut.Write((uint)SerializationHooks.Bloodstone_NETWORK_EVENT_ID);
        data.Serialize(ref netBufferOut);
    }

    // --------------------------------------------------------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate void DeserializeEvent(IntPtr entityManager, IntPtr commandBuffer, ref NetBufferIn netBuffer, DeserializeNetworkEventParams eventParams);

    public static DeserializeEvent? DeserializeOriginal;

    public unsafe static void DeserializeHook(IntPtr entityManager, IntPtr commandBuffer, ref NetBufferIn netBufferIn, DeserializeNetworkEventParams eventParams)
    {
        var eventId = netBufferIn.ReadUInt32();
        if (eventId != SerializationHooks.Bloodstone_NETWORK_EVENT_ID)
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
}