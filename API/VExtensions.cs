using ProjectM;
using ProjectM.Network;
using Unity.Entities;

namespace Bloodstone.API;

/// <summary>
/// Various extensions to make it easier to work with VRising APIs.
/// </summary>
public static class VExtensions
{
    /// <summary>
    /// Send the given system message to the user.
    /// </summary>
    public static void SendSystemMessage(this User user, string message)
    {
        if (!VWorld.IsServer) throw new System.Exception("SendSystemMessage can only be called on the server.");

        ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, user, message);
    }

    /// <summary>
    /// Modify the given component on the given entity. The argument is passed
    /// as a reference, so it can be modified in place. The resulting struct
    /// is written back to the entity.
    /// </summary>
    public static void WithComponentData<T>(this Entity entity, ActionRef<T> action)
        where T : struct
    {
        var component = VWorld.Game.EntityManager.GetComponentData<T>(entity);
        action(ref component);
        VWorld.Game.EntityManager.SetComponentData<T>(entity, component);
    }

    public delegate void ActionRef<T>(ref T item);
}