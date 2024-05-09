
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using ProjectM;
using ProjectM.UI;
using Stunlock.Localization;
using StunShared.UI;
using TMPro;
using UnityEngine;
using Bloodstone.API;
using Stunlock.Core;

namespace Bloodstone.Hooks;

/// <summary>
/// Properly hooking keybinding menu in V Rising is a major pain in the ass. The
/// geniuses over at Stunlock studios decided to make the keybindings a flag enum.
/// This sounds decent, but it locks you to a whopping 64 unique keybindings. Guess
/// how many the game uses? 64 exactly.
///
/// As a result we can't just hook into the same system and add a new control, since
/// we don't actually have any free keybinding codes we could re-use. If we tried to
/// do that, it would mean that if a user used one of our keybinds, they would also
/// use at least one of the pre-configured game keybinds (since the IsKeyDown check
/// only checks whether the specific bit in the current input bitfield is set). As a
/// result we have to work around this by carefully avoiding that our custom invalid
/// keybinding flags are never serialized to the input system that V Rising uses, so
/// we have to implement quite a bit ourselves. This will probably break at some point
/// since I doubt Stunlock will be content with 64 unique input settings for the rest
/// of the game's lifetime. Good luck for who will end up needing to fix it.
/// </summary>
static class Keybindings
{
#nullable disable
    private static Harmony _harmony;
    private static INativeDetour _detour;
#nullable enable

    public static void Initialize()
    {
        if (!VWorld.IsClient) return;

        BloodstonePlugin.Logger.LogWarning("Client Keybinding support has been disabled for 1.0 release change compatability. It may be rewritten in the future as needed.");
        _harmony = Harmony.CreateAndPatchAll(typeof(Keybindings));
    }

    public static void Uninitialize()
    {
        if (!VWorld.IsClient) return;

        _detour.Dispose();
        _harmony.UnpatchSelf();
    }
}