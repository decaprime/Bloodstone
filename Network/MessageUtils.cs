using System;
using System.Collections.Generic;
using System.IO;
using Bloodstone.API;
using ProjectM;
using ProjectM.Network;

namespace Bloodstone.Network;

public delegate void ClientConnectionMessageHandler(User fromCharacter);

public static class MessageUtils
{
    /// <summary>
    /// This function initialises the client to enable the server to send VNetworkChatMessage messages via the in-game chat mechanism.
    /// This should only be called once the client is ready to receive messages from the server. A suggestion for this is
    /// once the `GameDataManager.GameDataInitialized` is true.
    ///
    /// This gives the client a chance to register any appropriate VNetworkChatMessage types to support reading different
    /// data types.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public static void InitialiseClient()
    {
        if (VWorld.IsServer) throw new System.Exception("InitialiseClient can only be called on the client.");
        VNetwork.SendToServerStruct(new ClientRegister() { clientNonce = ClientNonce });
    }
    
    /// <summary>
    /// The server should add an action on this event to be able to respond with any start-up requests now that the user
    /// is ready for messages.
    /// </summary>
    public static event ClientConnectionMessageHandler? OnClientConnectionEvent;
    
    /// <summary>
    /// Send a VNetworkChatMessage message to the client via the in-game chat mechanism.
    /// If the client has not yet been initialised (via `InitialiseClient`) then this will not send any message.
    /// 
    /// Note: If the client has not registered the VNetworkChatMessage type that we are sending, then they will not
    /// receive that message. 
    /// </summary>
    /// <param name="toCharacter">This is the user that the message will be sent to</param>
    /// <param name="msg">This is the data packet that will be sent to the user</param>
    /// <typeparam name="T"></typeparam>
    public static void SendToClient<T>(User toCharacter, T msg) where T : VNetworkChatMessage
    {
        BloodstonePlugin.Logger.LogDebug("[SERVER] [SEND] VNetworkChatMessage");

        // Note: Bloodstone currently doesn't support sending custom server messages to the client :(
        // VNetwork.SendToClient(toCharacter, msg);
            
        // ... instead we are going to send the user a chat message, as long as we have them in our initialised list.
        if (SupportedUsers.TryGetValue(toCharacter.PlatformId, out var clientNonce))
        {
            ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, toCharacter, $"{SerialiseMessage(msg, clientNonce)}");
        }
        else
        {
            BloodstonePlugin.Logger.LogDebug("user nonce not present in supportedUsers");
        }
    }

    private static readonly int ClientNonce = Random.Shared.Next(); 
    private static readonly Dictionary<ulong, int> SupportedUsers = new();

    private struct ClientRegister
    {
        public int clientNonce;
    }
    
    internal static void RegisterClientInitialisationType()
    {
        VNetworkRegistry.RegisterServerboundStruct((FromCharacter from, ClientRegister register) =>
        {
            var user = VWorld.Server.EntityManager.GetComponentData<User>(from.User);
            SupportedUsers[user.PlatformId] = register.clientNonce;
            
            OnClientConnectionEvent?.Invoke(user);
        });
    }
    
    internal static void UnregisterClientInitialisationType()
    {
        VNetworkRegistry.UnregisterStruct<ClientRegister>();
    }

    public static void RegisterType<T>(Action<T> onServerMessageEvent) where T : VNetworkChatMessage, new()
    {
        MessageChatRegistry.Register<T>(new()
        {
            OnReceiveFromServer = br =>
            {
                var msg = new T();
                msg.Deserialize(br);
                onServerMessageEvent.Invoke(msg);
            }
        });
    }

    private static string SerialiseMessage<T>(T msg, int clientNonce) where T : VNetworkChatMessage
    {
        using var stream = new MemoryStream();
        using var bw = new BinaryWriter(stream);
        
        VNetworkChatMessage.WriteHeader(bw, MessageRegistry.DeriveKey(msg.GetType()), clientNonce);
        
        msg.Serialize(bw);
        return Convert.ToBase64String(stream.ToArray());
    }

    internal static bool DeserialiseMessage(string message)
    {
        var type = "";
        try
        {
            var bytes = Convert.FromBase64String(message);

            using var stream = new MemoryStream(bytes);
            using var br = new BinaryReader(stream);

            // If we can't read the header, it is likely not a VNetworkChatMessage
            if (!VNetworkChatMessage.ReadHeader(br, out var clientNonce, out type)) return false;

            // This is a valid message, but not intended for us.
            if (clientNonce != ClientNonce)
            {
                BloodstonePlugin.Logger.LogWarning($"ClientNonce did not match: [actual: {clientNonce}, expected: {ClientNonce}]");
                return true;
            }

            if (MessageChatRegistry._eventHandlers.TryGetValue(type, out var handler))
            {
                handler.OnReceiveFromServer(br);
            }

            return true;
        }
        catch (FormatException)
        {
            BloodstonePlugin.Logger.LogDebug("Invalid base64");
            return false;
        }
        catch (Exception ex)
        {
            BloodstonePlugin.Logger.LogError($"Error handling incoming network event {type}:");
            BloodstonePlugin.Logger.LogError(ex);
            
            return false;
        }
    }
}