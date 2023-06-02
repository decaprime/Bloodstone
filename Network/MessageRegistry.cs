using System;
using System.Collections.Generic;
using ProjectM.Network;
using Stunlock.Network;

namespace Bloodstone.Network;

// Tracks internal registered message types and their event handlers.
internal class MessageRegistry
{
    internal static Dictionary<string, RegisteredEventHandler> _eventHandlers = new();

    // Derive a unique but predictable identifier for a message type.
    internal static string DeriveKey(Type name) => name.ToString(); // FullName contains assembly info which we don't want

    internal static void Register<T>(RegisteredEventHandler handler)
    {
        var key = DeriveKey(typeof(T));

        if (_eventHandlers.ContainsKey(key))
            throw new Exception($"Network event {key} is already registered");

        _eventHandlers.Add(key, handler);
    }

    internal static void Unregister<T>()
    {
        var key = DeriveKey(typeof(T));

        // don't throw if it doesn't exist
        _eventHandlers.Remove(key);
    }
}

internal class RegisteredEventHandler
{
#nullable disable
    internal Action<NetBufferIn> OnReceiveFromServer { get; init; }
    internal Action<FromCharacter, NetBufferIn> OnReceiveFromClient { get; init; }
#nullable enable
}