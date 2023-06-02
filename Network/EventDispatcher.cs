
using System;
using ProjectM.Network;
using Unity.Entities;
using Bloodstone.API;

namespace Bloodstone.Network;

// Helper class that creates entities to dispatch 
// network events to VRising's network system, so that
// our serialization hooks can later handle them.
internal static class EventDispatcher
{
    internal static void SendToClient(int userIndex, VNetworkMessage msg)
    {
        if (!VWorld.IsServer)
            throw new Exception("Cannot send network messages to client if not on the server");

        var key = MessageRegistry.DeriveKey(msg.GetType());

        if (!MessageRegistry._eventHandlers.ContainsKey(key))
            throw new Exception($"Network event {key} is not registered");

        var em = VWorld.Server.EntityManager;
        var entity = em.CreateEntity(
            ComponentType.ReadOnly<NetworkEventType>(),
            ComponentType.ReadOnly<SendEventToUser>(),
            CustomNetworkEvent.ComponentType
        );

        em.SetComponentData<SendEventToUser>(entity, new()
        {
            UserIndex = userIndex
        });

        em.SetComponentData<NetworkEventType>(entity, new()
        {
            EventId = SerializationHooks.Bloodstone_NETWORK_EVENT_ID,
            IsAdminEvent = false,
            IsDebugEvent = false
        });

        em.SetComponentObject(entity, CustomNetworkEvent.ComponentType, new CustomNetworkEvent()
        {
            Message = msg
        });
    }

    internal static void SendToServer(VNetworkMessage msg)
    {
        if (!VWorld.IsClient)
            throw new Exception("Cannot send network messages to server if not on the client");

        var key = MessageRegistry.DeriveKey(msg.GetType());

        if (!MessageRegistry._eventHandlers.ContainsKey(key))
            throw new Exception($"Network event {key} is not registered");

        var em = VWorld.Client.EntityManager;
        var entity = em.CreateEntity(
            ComponentType.ReadOnly<NetworkEventType>(),
            ComponentType.ReadOnly<SendNetworkEventTag>(),
            CustomNetworkEvent.ComponentType
        );

        em.SetComponentData<NetworkEventType>(entity, new()
        {
            EventId = SerializationHooks.Bloodstone_NETWORK_EVENT_ID,
            IsAdminEvent = false,
            IsDebugEvent = false
        });

        em.SetComponentObject(entity, CustomNetworkEvent.ComponentType, new CustomNetworkEvent()
        {
            Message = msg
        });
    }
}