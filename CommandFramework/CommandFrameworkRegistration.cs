using Bloodstone.API;
using HarmonyLib;
using VampireCommandFramework;
using VampireCommandFramework.Basics;

namespace Bloodstone.Features;

internal class CommandFrameworkRegistration
{
    private static Harmony _harmomy;

    public static void Initialize()
    {
        if (!VWorld.IsServer)
        {
            BloodstonePlugin.Logger.LogMessage("Note: Vampire Command Framework is loading on the client but only adds functionality on the server at this time, seeing this message is not a problem or bug.");
            return;
        }

        CommandRegistry.RegisterCommandType(typeof(HelpCommands));
        CommandRegistry.RegisterCommandType(typeof(BepInExConfigCommands));
        CommandRegistry.RegisterCommandType(typeof(Reload));


        _harmomy = Harmony.CreateAndPatchAll(typeof(Bloodstone.CommandFramework.CommandFrameworkChatPatch));
    }

    public static void Uninitialize()
    {
        if (!VWorld.IsServer)
        {
            return;
        }

        _harmomy.UnpatchSelf();

    }
}