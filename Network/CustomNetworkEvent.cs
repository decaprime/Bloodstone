using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Stunlock.Network;
using Unity.Entities;
using Bloodstone.API;

namespace Bloodstone.Network;

/// <summary>
/// Custom network event component. We attach this to entities to trick
/// VRising into sending it over the network. If entities with this 
/// component are ever saved to disk, it will brick the save. Take caution!
/// </summary>
internal class CustomNetworkEvent : Il2CppSystem.Object
{
    private static bool _isInitialized = false;
    private static ComponentType _componentType;

    public static ComponentType ComponentType
    {
        get
        {
            if (!_isInitialized)
            {
                Inject();
            }

            return _componentType;
        }
    }

    public VNetworkMessage? Message;

    public CustomNetworkEvent() : base(ClassInjector.DerivedConstructorPointer<CustomNetworkEvent>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    // Serialize the entire event to the net writer, except event ID.
    internal void Serialize(ref NetBufferOut netBuffer)
    {
        if (Message == null)
            throw new System.Exception("Tried to serialize a CustomNetworkEvent with no message");

        var key = MessageRegistry.DeriveKey(Message.GetType());
        netBuffer.Write(key);

        try
        {
            Message.Serialize(ref netBuffer);
        }
        catch (System.Exception ex)
        {
            BloodstonePlugin.Logger.LogError($"Failed to serialize network event {key}:");
            BloodstonePlugin.Logger.LogError(ex);
        }
    }

    // Inject this component into the unmanaged domain. Only call this once.
    private static void Inject()
    {
        ClassInjector.RegisterTypeInIl2Cpp(typeof(CustomNetworkEvent));

        var il2cppty = Il2CppType.From(typeof(CustomNetworkEvent));

        var typeIndex = TypeManager.FindTypeIndex(il2cppty);
        if (typeIndex.Value < 1)
        {
			var info = TypeManager.BuildComponentType(il2cppty, new(1));
            TypeManager.AddTypeInfoToTables(il2cppty, info, nameof(CustomNetworkEvent), 0);
        }

        _componentType = new(il2cppty);
        _isInitialized = true;
    }
}