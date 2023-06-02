
using System;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Bloodstone.API;

namespace Bloodstone.Hooks;

public static class Chat
{
    public delegate void ChatEventHandler(VChatEvent e);

    /// <summary>
    /// Event emitted whenever a chat message is received by the server. Will
    /// not be emitted when running on the client.
    /// </summary>
    public static event ChatEventHandler? OnChatMessage;

    private static Harmony? _harmony;

    public static unsafe void Initialize()
    {
        if (_harmony != null)
            throw new Exception("Detour already initialized. You don't need to call this. The Bloodstone plugin will do it for you.");

        _harmony = Harmony.CreateAndPatchAll(typeof(Chat));
    }

    public static unsafe void Uninitialize()
    {
        if (_harmony == null)
            throw new Exception("Detour wasn't initialized. Are you trying to unload Bloodstone twice?");

        _harmony.UnpatchSelf();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ChatMessageSystem), nameof(ChatMessageSystem.OnUpdate))]
    public static void OnUpdatePrefix(ChatMessageSystem __instance)
    {
        var entities = __instance._ChatMessageQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            var chatMessage = VWorld.Server.EntityManager.GetComponentData<ChatMessageEvent>(entity);
            var fromCharacter = VWorld.Server.EntityManager.GetComponentData<FromCharacter>(entity);
            var ev = new VChatEvent(fromCharacter.User, fromCharacter.Character, chatMessage.MessageText.ToString(), chatMessage.MessageType);

            BloodstonePlugin.Logger.LogInfo($"[Chat] [{ev.Type}] {ev.User.CharacterName}: {ev.Message}");

            try
            {
                OnChatMessage?.Invoke(ev);

                if (ev.Cancelled)
                    VWorld.Server.EntityManager.DestroyEntity(entity);
            }
            catch (Exception ex)
            {
                BloodstonePlugin.Logger.LogError("Error dispatching chat event:");
                BloodstonePlugin.Logger.LogError(ex);
            }
        }
    }
}

/// <summary>
/// Represents a chat message sent by a user.
/// </summary>
public class VChatEvent
{
    /// <summary>
    /// The user entity of the user that sent the message. This contains the `User` component.
    /// </summary>
    public Entity SenderUserEntity { get; }
    /// <summary>
    /// The character entity of the user that sent the message. This contains the character
    /// instances, such as its position, health, etc.
    /// </summary>
    public Entity SenderCharacterEntity { get; }
    /// <summary>
    /// The message that was sent.
    /// </summary>
    public string Message { get; }
    /// <summary>
    /// The type of message that was sent.
    /// </summary>
    public ChatMessageType Type { get; }

    /// <summary>
    /// Whether this message was cancelled. Cancelled messages will not be
    /// forwarded to the normal VRising chat system and will not be sent to
    /// any other clients. Use the Cancel() function to set this flag. Note
    /// that cancelled events will still be forwarded to other plugins that
    /// have subscribed to this event.
    /// </summary>
    public bool Cancelled { get; private set; } = false;

    /// <summary>
    /// The user component instance of the user that sent the message.
    /// </summary>
    public User User => VWorld.Server.EntityManager.GetComponentData<User>(SenderUserEntity);

    internal VChatEvent(Entity userEntity, Entity characterEntity, string message, ChatMessageType type)
    {
        SenderUserEntity = userEntity;
        SenderCharacterEntity = characterEntity;
        Message = message;
        Type = type;
    }

    /// <summary>
    /// Cancel this message. Cancelled messages will not be forwarded to the
    /// normal VRising chat system and will not be sent to any other clients.
    /// Note that cancelled events will still be forwarded to other plugins 
    /// that have subscribed to this event.
    /// </summary>
    public void Cancel()
    {
        Cancelled = true;
    }
}