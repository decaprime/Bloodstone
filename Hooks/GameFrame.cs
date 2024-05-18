using Il2CppInterop.Runtime.Injection;
using System;
using UnityEngine;

namespace Bloodstone.Hooks;

public delegate void GameFrameUpdateEventHandler();

/// <summary>
/// This class provides hooks for the Update and LateUpdate frame
/// functions invoked by Unity. Using this class is preferable to
/// injecting your own MonoBehavior, as it will allow you to reload
/// your mod without restarting the game (injecting a MonoBehavior
/// into the unmanaged runtime cannot be done multiple times). To
/// use this class, simply subscribe to the Update and/or LateUpdate
/// events.
/// </summary>
public class GameFrame : MonoBehaviour
{
    private static GameFrame? _instance;

    /// <summary>
    /// This event will be emitted on every Update call. It may be
    /// more performant to inject your own MonoBehavior if you do not
    /// need to be invoked every frame.
    /// </summary>
    public static event GameFrameUpdateEventHandler? OnUpdate;

    /// <summary>
    /// This event will be emitted on every LateUpdate call. The same
    /// considerations as with the OnUpdate event apply. 
    /// </summary>
    public static event GameFrameUpdateEventHandler? OnLateUpdate;

    void Update()
    {
        try
        {
            OnUpdate?.Invoke();
        }
        catch (Exception ex)
        {
            BloodstonePlugin.Logger.LogError("Error dispatching OnUpdate event:");
            BloodstonePlugin.Logger.LogError(ex);
        }
    }

    void LateUpdate()
    {
        try
        {
            OnLateUpdate?.Invoke();
        }
        catch (Exception ex)
        {
            BloodstonePlugin.Logger.LogError("Error dispatching OnLateUpdate event:");
            BloodstonePlugin.Logger.LogError(ex);
        }
    }

    public static void Initialize()
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<GameFrame>())
        {
            ClassInjector.RegisterTypeInIl2Cpp<GameFrame>();
        }

        _instance = BloodstonePlugin.Instance.AddComponent<GameFrame>();
    }

    public static unsafe void Uninitialize()
    {
        OnUpdate = null;
        OnLateUpdate = null;
        Destroy(_instance);
        _instance = null;
    }
}