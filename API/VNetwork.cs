
using System;
using ProjectM.Network;
using Stunlock.Network;
using Bloodstone.Network;

namespace Bloodstone.API;

/// <summary>
/// Interface to be implemented for any type that you'd like to send over
/// the network through Bloodstone. Note that it is not recommended that you
/// implement this, unless you have a good reason to. VNetworkRegistry and
/// VNetwork have built-in support for sending blittable structs without any
/// additional effort.
/// </summary>
public interface VNetworkMessage
{
    /// <summary>Serialize this value to the given writer.</summary>
    void Serialize(ref NetBufferOut writer);

    /// <summary>Deserialize values from the given reader and update the current instance.</summary>
    void Deserialize(NetBufferIn reader);
}

/// <summary>
/// This registry keeps track of all networked types registered. In order
/// to be able to send and receive your own network messages, simply make sure
/// that you register them with the VNetworkRegistry before you send a message
/// or expect to receive one. If your plugin supports reloading, you should
/// also make sure to unregister the types when your plugin is unloaded, as
/// registring the same type twice will cause a runtime error.
///
/// You can register your own VNetworkSerializable instance if you want to handle
/// serialization and deserialization yourself, or you can register a blittable
/// struct and have VNetwork automatically serialize it for you.
/// </summary>
public static class VNetworkRegistry
{
    /// <summary>
    /// Unregister the given type. This will remove all message listeners,
    /// regardless of which direction they were in.
    /// </summary>
    public static void Unregister<T>() => MessageRegistry.Unregister<T>();

    /// <summary>
    /// Unregister the given blittable struct. This will remove all message
    /// listeners, regardless of which direction they were in.
    /// </summary>
    public static void UnregisterStruct<T>()
        where T : unmanaged => Unregister<VBlittableNetworkMessage<T>>();

    /// <summary>
    /// Register the given message type for networking, with the given
    /// callback when a message is received from a client.
    /// </summary>
    public static void RegisterServerbound<T>(Action<FromCharacter, T> onMessageFromClient)
        where T : VNetworkMessage, new() => MessageRegistry.Register<T>(new()
        {
            OnReceiveFromServer = (_) => { },
            OnReceiveFromClient = (user, buf) =>
            {
                var msg = new T();
                msg.Deserialize(buf);
                onMessageFromClient(user, msg);
            }
        });

    /// <summary>
    /// Register the given blittable struct for networking, with the given
    /// callback when a message is received from a client.
    /// </summary>
    public static void RegisterServerboundStruct<T>(Action<FromCharacter, T> onMessageFromClient)
        where T : unmanaged => RegisterServerbound<VBlittableNetworkMessage<T>>((user, msg) =>
        {
            onMessageFromClient(user, msg.Value);
        });

    /// <summary>
    /// Register the given message type for networking, with the given
    /// callback when a message is received from the server.
    /// </summary>
    public static void RegisterClientbound<T>(Action<T> onMessageFromServer)
        where T : VNetworkMessage, new() => MessageRegistry.Register<T>(new()
        {
            OnReceiveFromServer = buf =>
            {
                var msg = new T();
                msg.Deserialize(buf);
                onMessageFromServer(msg);
            },
            OnReceiveFromClient = (_, _) => { }
        });

    /// <summary>
    /// Register the given blittable struct for networking, with the given
    /// callback when a message is received from the server.
    /// </summary>
    public static void RegisterClientboundStruct<T>(Action<T> onMessageFromServer)
        where T : unmanaged => RegisterClientbound<VBlittableNetworkMessage<T>>(msg =>
        {
            onMessageFromServer(msg.Value);
        });

    /// <summary>
    /// Register the given message type for networking, with the first 
    /// callback when a message is received from the server, and the second
    /// callback when a message is received from a client.
    /// </summary>
    public static void RegisterBiDirectional<T>(Action<T> onMessageFromServer, Action<FromCharacter, T> onMessageFromClient)
        where T : VNetworkMessage, new() => MessageRegistry.Register<T>(new()
        {
            OnReceiveFromServer = buf =>
            {
                var msg = new T();
                msg.Deserialize(buf);
                onMessageFromServer(msg);
            },
            OnReceiveFromClient = (user, buf) =>
            {
                var msg = new T();
                msg.Deserialize(buf);
                onMessageFromClient(user, msg);
            }
        });

    /// <summary>
    /// Register the given blittable struct for networking, with the first 
    /// callback when a message is received from the server, and the second
    /// callback when a message is received from a client.
    /// </summary>
    public static void RegisterBiDirectionalStruct<T>(Action<T> onMessageFromServer, Action<FromCharacter, T> onMessageFromClient)
        where T : unmanaged => RegisterBiDirectional<VBlittableNetworkMessage<T>>(
            msg => onMessageFromServer(msg.Value),
            (user, msg) => onMessageFromClient(user, msg.Value)
        );
}

/// <summary>
/// Main class for sending network messages. To send a network message, make
/// sure you register the type with VNetworkRegistry before you send it, then
/// simply call the appropriate send class. Bloodstone will take care of the rest.
/// </summary>
public static class VNetwork
{
    /// <summary>Send the given message to the user with the given index.</summary>
    public static void SendToClient(int userIndex, VNetworkMessage msg) => EventDispatcher.SendToClient(userIndex, msg);

    /// <summary>Send the given message to the given user.</summary>
    public static void SendToClient(User user, VNetworkMessage msg) => EventDispatcher.SendToClient(user.Index, msg);

    /// <summary>Send the given message to the given user.</summary>
    public static void SendToClient(FromCharacter fromCharacter, VNetworkMessage msg)
    {
        var user = VWorld.Server.EntityManager.GetComponentData<User>(fromCharacter.User);
        EventDispatcher.SendToClient(user.Index, msg);
    }

    /// <summary>Send the given blittable struct to the user with the given index.</summary>
    public static void SendToClientStruct<T>(int userIndex, T msg)
        where T : unmanaged => SendToClient(userIndex, new VBlittableNetworkMessage<T>(msg));

    /// <summary>Send the given blittable struct to the given user.</summary>
    public static void SendToClientStruct<T>(User user, T msg)
        where T : unmanaged => SendToClient(user, new VBlittableNetworkMessage<T>(msg));

    /// <summary>Send the given blittable struct to the given user.</summary>
    public static void SendToClientStruct<T>(FromCharacter fromCharacter, T msg)
        where T : unmanaged => SendToClient(fromCharacter, new VBlittableNetworkMessage<T>(msg));

    /// <summary>Send the given message to the server.</summary>
    public static void SendToServer(VNetworkMessage msg) => EventDispatcher.SendToServer(msg);

    /// <summary>Send the given blittable struct to the server.</summary>
    public static void SendToServerStruct<T>(T msg)
        where T : unmanaged => SendToServer(new VBlittableNetworkMessage<T>(msg));
}