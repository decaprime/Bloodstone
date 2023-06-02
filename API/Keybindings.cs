using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using BepInEx;
using ProjectM;
using Standart.Hash.xxHash;
using UnityEngine;

namespace Bloodstone.API;

/// <summary>
/// Manager class responsible for tracking custom plugin keybindings in 
/// client-side mods. Generally, you just need to `Register` your custom
/// keybinding. Bloodstone will ensure that it gets persisted and shows
/// up in the in-client keybindings menu. In order to check whether the
/// keybinding is pressed, and what it is mapped to, you can use the 
/// appropriate fields on the Keybinding instance returned from the
/// `Register` method.
///
/// You don't NEED to unregister the keybinding when you unload your
/// plugin, but it is generally good practice to do so. Not unregistering
/// will keep your keybinding as an option even if the plugin is unloaded.
/// Unregistering will not reset/remove the configured keybinding.
/// </summary>
public static class KeybindManager
{
    private static string KeybindingsPath = Path.Join(Paths.ConfigPath, "keybindings.json");

    internal static Dictionary<InputFlag, Keybinding> _keybindingsByFlags = new();
    internal static Dictionary<int, Keybinding> _keybindingsByGuid = new();
    internal static Dictionary<string, Keybinding> _keybindingsById = new();

    internal static Dictionary<string, KeybindingBindings> _keybindingValues = new();

    /// <summary>
    /// Register a new keybinding with the given description. The ID will be used
    /// to identify the keybinding in storage, so it should be unique and constant
    /// across versions. You are recommended to prefix it with your plugin name/id,
    /// so that it cannot conflict with other plugins.
    ///
    /// It is recommended to use a single category for all your plugin's keybindings.
    /// If you use multiple, prefix them with the name of your plugin so that users
    /// know which plugin a keybinding belongs to.
    ///
    /// The returned Keybinding instance can be used to retrieve the current value
    /// of the keybinding at any time. On first use, the primary keybinding will be
    /// initialized to the default keybinding.
    /// </summary>
    public static Keybinding Register(KeybindingDescription description)
    {
        if (_keybindingsById.ContainsKey(description.Id))
            throw new ArgumentException($"Keybinding with id {description.Id} already registered");

        var keybinding = new Keybinding(description);
        _keybindingsById.Add(description.Id, keybinding);
        _keybindingsByGuid.Add(keybinding.AssetGuid, keybinding);
        _keybindingsByFlags.Add(keybinding.InputFlag, keybinding);

        if (!_keybindingValues.ContainsKey(description.Id))
        {
            _keybindingValues.Add(description.Id, new()
            {
                Id = description.Id,
                Primary = description.DefaultKeybinding,
                Secondary = KeyCode.None,
            });
        }

        return keybinding;
    }

    /// <summary>
    /// Unregister the binding. This is recommended if your plugin is reloadable,
    /// but even if it is not it is the good thing to do. Unregistering a keybinding
    /// will stop it from showing up in the settings, but it won't remove the keys
    /// that were assigned to it.
    /// </summary>
    public static void Unregister(Keybinding keybinding)
    {
        if (!_keybindingsById.ContainsKey(keybinding.Description.Id))
            throw new ArgumentException($"There was no keybinding with id {keybinding.Description.Id} registered");

        _keybindingsByFlags.Remove(keybinding.InputFlag);
        _keybindingsByGuid.Remove(keybinding.AssetGuid);
        _keybindingsById.Remove(keybinding.Description.Id);
    }

    internal static void Load()
    {
        try
        {
            if (File.Exists(KeybindingsPath))
            {
                var content = File.ReadAllText(KeybindingsPath);
                var deserialized = JsonSerializer.Deserialize<Dictionary<string, KeybindingBindings>>(content);
                if (deserialized != null)
                    _keybindingValues = deserialized;
            }
        }
        catch (Exception ex)
        {
            BloodstonePlugin.Logger.LogError("Error loading keybindings, using defaults: ");
            BloodstonePlugin.Logger.LogError(ex);

            _keybindingValues = new();
        }
    }

    internal static void Save()
    {
        try
        {
            var serialized = JsonSerializer.Serialize(_keybindingValues);
            File.WriteAllText(KeybindingsPath, serialized);
        }
        catch (Exception ex)
        {
            BloodstonePlugin.Logger.LogError("Error saving custom keybindings: ");
            BloodstonePlugin.Logger.LogError(ex);
        }
    }
}

/// <summary>
/// Represents a keybinding option.
/// </summary>
public struct KeybindingDescription
{
    /// <summary>
    /// The ID of the keybinding. This should be unique across all keybindings.
    /// </summary>
    public string Id;

    /// <summary>
    /// The title of the category in which this keybinding should appear.
    /// </summary>
    public string Category;

    /// <summary>
    /// The name of the keybinding setting, as shown in the settings menu.
    /// </summary>
    public string Name;

    /// <summary>
    /// The default keycode for this keybinding. Use KeyCode.NONE if you do not
    /// want the keybinding to be bound by default. The secondary keybinding will
    /// always be set to none when the user has not explicitly set it.
    /// </summary>
    public KeyCode DefaultKeybinding;
}

/// <summary>
/// A bound keybinding instance. You can use this to observe the current
/// value of the keybinding, and to check whether or not the keybinding
/// is currently pressed.
/// </summary>
public class Keybinding
{
    /// <summary>
    /// The description of the keybinding.
    /// </summary>
    public KeybindingDescription Description { get; }

    /// <summary>
    /// The current primary key bound to this keybinding. `None` if not bound.
    /// </summary>
    public KeyCode Primary => KeybindManager._keybindingValues[Description.Id].Primary;
    /// <summary>
    /// The current secondary key bound to this keybinding. `None` if not bound.
    /// </summary>
    public KeyCode Secondary => KeybindManager._keybindingValues[Description.Id].Secondary;

    /// <summary>
    /// Utility method for checking whether the keybinding is currently pressed using
    /// UnityEngine.Input.GetKeyDown. If you need more control, such as checking whether
    /// the key is being held, you can manually query the key state using the Primary
    /// and Secondary fields.
    /// </summary>
    public bool IsPressed => UnityEngine.Input.GetKeyDown(Primary) || UnityEngine.Input.GetKeyDown(Secondary);

    // Unique XXHash-based inputflag for identification.
    internal InputFlag InputFlag { get; private set; }
    // Unique XXHash-based quarter-of-an-assetguid for identification.
    internal int AssetGuid { get; private set; }

    public Keybinding(KeybindingDescription description)
    {
        Description = description;

        ComputeInputFlag();
        ComputeAssetGuid();
    }

    // Stubborn V Rising internals expect us to have a unique InputFlag
    // for every input. We deterministically generate a random one here,
    // and ensure it is not already in use (by the game).
    private void ComputeInputFlag()
    {
        var idBytes = Encoding.UTF8.GetBytes(Description.Id);
        var hash = xxHash64.ComputeHash(idBytes, idBytes.Length);
        var invalid = false;
        do
        {
            invalid = false;
            foreach (var entry in Enum.GetValues<InputFlag>())
            {
                if (hash == (ulong)entry)
                {
                    invalid = true;
                    hash -= 1;
                }
            }
        } while (invalid);

        InputFlag = (InputFlag)hash;
    }

    // Ditto, but for asset GUIDs.
    private void ComputeAssetGuid()
    {
        var idBytes = Encoding.UTF8.GetBytes(Description.Id);
        AssetGuid = (int)xxHash32.ComputeHash(idBytes, idBytes.Length);
    }
}

// Internal class used for data persistence.
internal class KeybindingBindings
{
#nullable disable
    public string Id { get; set; }
    public KeyCode Primary { get; set; }
    public KeyCode Secondary { get; set; }
#nullable enable
}