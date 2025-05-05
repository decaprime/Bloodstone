using System;
using System.Collections.Generic;
using System.IO;

namespace Bloodstone.Network;

// Tracks internal registered message types and their event handlers.
internal class MessageChatRegistry
{
    internal static Dictionary<string, RegisteredChatEventHandler> _eventHandlers = new();

    internal static void Register<T>(RegisteredChatEventHandler handler)
    {
        var key = MessageRegistry.DeriveKey(typeof(T));

        if (_eventHandlers.ContainsKey(key))
            throw new Exception($"Network event {key} is already registered");

        _eventHandlers.Add(key, handler);
    }

    internal static void Unregister<T>()
    {
        var key = MessageRegistry.DeriveKey(typeof(T));

        // don't throw if it doesn't exist
        _eventHandlers.Remove(key);
    }
}

internal class RegisteredChatEventHandler
{
#nullable disable
    internal Action<BinaryReader> OnReceiveFromServer { get; init; }
#nullable enable
}