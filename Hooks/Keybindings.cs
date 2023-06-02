
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using ProjectM;
using ProjectM.UI;
using Stunlock.Localization;
using StunShared.UI;
using TMPro;
using UnityEngine;
using Bloodstone.API;

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
    private static TryGetInputFlagLocalization_t TryGetInputFlagLocalization_Original;
    private static Harmony _harmony;
    private static INativeDetour _detour;
#nullable enable

    public static void Initialize()
    {
        if (!VWorld.IsClient) return;

        unsafe
        {
            _detour = Bloodstone.Util.NativeHookUtil.Detour(typeof(InputSystem), "TryGetInputFlagLocalization", TryGetInputFlagLocalization_Hook, out TryGetInputFlagLocalization_Original);
        }

        _harmony = Harmony.CreateAndPatchAll(typeof(Keybindings));
    }

    public static void Uninitialize()
    {
        if (!VWorld.IsClient) return;

        _detour.Dispose();
        _harmony.UnpatchSelf();
    }

    // Hook #1: when the controls panel opens, we append our own custom keybinding elements.
    [HarmonyPatch(typeof(Options_ControlsPanel), "Start")]
    [HarmonyPostfix]
    public static void Start(Options_ControlsPanel __instance)
    {
        // for every category
        foreach (var grouped in KeybindManager._keybindingsById.Values.GroupBy(x => x.Description.Category))
        {
            // create a header
            var categoryHeader = UIHelper.InstantiatePrefabUnderAnchor(__instance.CategoryHeaderPrefab, __instance.ContentNode);
            categoryHeader.GetComponentInChildren<TextMeshProUGUI>().text = grouped.Key;

            foreach (var entry in grouped)
            {
                // add an entry. This does not actually persist the keybinding, just adds
                // it to the list of entries so that RefreshAll actually, you know, refreshes all
                __instance.AddEntry(entry.InputFlag);
            }
        }
    }

    // Hook #2: when a keybinding is changed, we update our internal keybindings.
    [HarmonyPatch(typeof(InputSystem), "ModifyKeyInputSetting")]
    [HarmonyPrefix]
    public static bool ModifyKeyInputSetting(InputFlag inputFlag, KeyCode newKey, bool primary)
    {
        var customKeybinding = KeybindManager._keybindingsByFlags.GetValueOrDefault(inputFlag);

        if (customKeybinding != null)
        {
            if (primary)
            {
                KeybindManager._keybindingValues[customKeybinding.Description.Id].Primary = newKey;
            }
            else
            {
                KeybindManager._keybindingValues[customKeybinding.Description.Id].Secondary = newKey;
            }

            KeybindManager.Save();

            // don't involve the native keybinding system
            return false;
        }

        return true;
    }


    // Hook #3: properly display the current keybinding value text/icon for
    // our custom keybindings.
    [HarmonyPatch(typeof(InputSystem), "GetKeyInputMap")]
    [HarmonyPrefix]
    public static bool GetKeyInputMap(InputFlag input, ref string inputText, ref Sprite inputIcon, bool primary, InputSystem __instance)
    {
        var customKeybinding = KeybindManager._keybindingsByFlags.GetValueOrDefault(input);

        if (customKeybinding != null)
        {
            var keybindingValue = primary ? customKeybinding.Primary : customKeybinding.Secondary;
            // special-case none to return an empty string/icon
            if (keybindingValue == KeyCode.None)
            {
                return false;
            }

            // we assume we only map keys for now. Check if there's a nice sprite for
            // this one, or an override. Else, return the textual representation.
            var entry = __instance._ControlsVisualMapping.KeysData.FirstOrDefault(x => x.KeyCode == keybindingValue);
            if (entry != null)
            {
                inputText = Localization.Get(entry.TextKey, true);
                inputIcon = entry.KeySprite;
            }
            else
            {
                inputText = __instance.GetKeyCodeString(keybindingValue);
            }

            return false;
        }

        return true;
    }

    // Hook #4: when the localization system wants to translate the localization key for
    // our custom keybindings, return the appropriate name
    [HarmonyPatch(typeof(Localization), "Get", typeof(AssetGuid), typeof(bool))]
    [HarmonyPrefix]
    public static bool Get(AssetGuid guid, ref string __result)
    {
        var customKeybinding = KeybindManager._keybindingsByGuid.GetValueOrDefault(guid._a);

        if (customKeybinding != null)
        {
            __result = customKeybinding.Description.Name;
            return false;
        }

        return true;
    }

    // Hook #5: reset our own keybindings when the reset keybindings button is used
    [HarmonyPatch(typeof(Options_ControlsPanel), "OnResetButtonClicked")]
    [HarmonyPrefix]
    public static bool OnResetButtonClicked()
    {
        foreach (var (id, value) in KeybindManager._keybindingValues)
        {
            value.Primary = KeybindManager._keybindingsById[id].Description.DefaultKeybinding;
            value.Secondary = KeyCode.None;
        }

        KeybindManager.Save();

        return true;
    }


    // Hook #6 (requires a native hook since it takes a ref struct): when the input system
    // wants to know the localizable name of our input, return a custom localization key
    // that we later intercept in hook #4.
    private unsafe delegate bool TryGetInputFlagLocalization_t(IntPtr instance, InputFlag inputFlag, LocalizationKey* key);

    private static unsafe bool TryGetInputFlagLocalization_Hook(IntPtr instance, InputFlag inputFlag, LocalizationKey* key)
    {
        var customKeybinding = KeybindManager._keybindingsByFlags.GetValueOrDefault(inputFlag);

        if (customKeybinding != null)
        {
            *key = new LocalizationKey(new ProjectM.AssetGuid() { _a = customKeybinding.AssetGuid });
            return true;
        }

        return TryGetInputFlagLocalization_Original(instance, inputFlag, key);
    }
}