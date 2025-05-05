using System;
using System.IO;

namespace Bloodstone.Network;

public interface VNetworkChatMessage
{
    internal static void WriteHeader(BinaryWriter writer, string type, int clientNonce)
    {
        writer.Write(SerializationHooks.BLOODSTONE_NETWORK_EVENT_ID);
        writer.Write(clientNonce);
        writer.Write(type);
    }

    internal static bool ReadHeader(BinaryReader reader, out int clientNonce, out string type)
    {
        type = "";
        clientNonce = 0;
        
        try
        {
            var eventId = reader.ReadInt32();
            clientNonce = reader.ReadInt32();
            type = reader.ReadString();
            
            return eventId == SerializationHooks.BLOODSTONE_NETWORK_EVENT_ID;
        }
        catch (Exception e)
        {
            BloodstonePlugin.Logger.LogDebug($"Failed to read chat message header: {e.Message}");

            return false;
        }
    }
    
    public void Serialize(BinaryWriter writer);

    public void Deserialize(BinaryReader reader);
}