using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Mono.Cecil;
using UnityEngine;
using Bloodstone.API;
using Bloodstone.Hooks;
using VampireCommandFramework;

namespace Bloodstone.Features;

internal static class Reload
{
#nullable disable
    private static string _reloadCommand;
    private static string _reloadPluginsFolder;
    private static ReloadBehaviour _clientBehavior;
    private static Keybinding _clientReloadKeybinding;
#nullable enable

    internal static List<BasePlugin> _loadedPlugins = new();

    internal static void Initialize(string reloadPluginsFolder)
    {
        _reloadPluginsFolder = reloadPluginsFolder;

        if (VWorld.IsClient)
        {
            _clientReloadKeybinding = KeybindManager.Register(new()
            {
                Id = "gg.deca.Bloodstone.reload",
                Category = "Bloodstone",
                Name = "Reload Plugins",
                DefaultKeybinding = KeyCode.F6,
            });
            _clientBehavior = BloodstonePlugin.Instance.AddComponent<ReloadBehaviour>();
        }

        LoadPlugins();
    }

    internal static void Uninitialize()
    {
        if (_clientBehavior != null)
        {
            UnityEngine.Object.Destroy(_clientBehavior);
        }
    }

    [Command("reload","re", adminOnly:true)]
	public static void HandleReloadCommand(ICommandContext ctx)
	{
		UnloadPlugins();
		var loaded = LoadPlugins();

		if (loaded.Count > 0)
		{
			ctx.Reply($"Reloaded {string.Join(", ", loaded)}. See console for details.");
		}
		else
		{
			ctx.Reply($"Did not reload any plugins because no reloadable plugins were found. Check the console for more details.");
		}
	}

    private static void UnloadPlugins()
    {
        for (int i = _loadedPlugins.Count - 1; i >= 0; i--)
        {
            var plugin = _loadedPlugins[i];

            if (!plugin.Unload())
            {
                BloodstonePlugin.Logger.LogWarning($"Plugin {plugin.GetType().FullName} does not support unloading, skipping...");
            }
            else
            {
                _loadedPlugins.RemoveAt(i);
            }
        }
    }

    private static List<string> LoadPlugins()
    {
        if (!Directory.Exists(_reloadPluginsFolder)) return new();

        return Directory.GetFiles(_reloadPluginsFolder, "*.dll").SelectMany(LoadPlugin).ToList();
    }

    private static List<string> LoadPlugin(string path)
    {
        var defaultResolver = new DefaultAssemblyResolver();
        defaultResolver.AddSearchDirectory(_reloadPluginsFolder);
        defaultResolver.AddSearchDirectory(Paths.ManagedPath);
        defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);
        defaultResolver.AddSearchDirectory(Path.Combine(Paths.BepInExRootPath, "interop"));

        using var dll = AssemblyDefinition.ReadAssembly(path, new() { AssemblyResolver = defaultResolver });
        dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

        using var ms = new MemoryStream();
        dll.Write(ms);

        var loaded = new List<string>();

        var assembly = Assembly.Load(ms.ToArray());
        foreach (var pluginType in assembly.GetTypes().Where(x => typeof(BasePlugin).IsAssignableFrom(x)))
        {
            // skip plugins not marked as reloadable
            if (!pluginType.GetCustomAttributes<ReloadableAttribute>().Any())
            {
                BloodstonePlugin.Logger.LogWarning($"Plugin {pluginType.FullName} is not marked as reloadable, skipping...");
                continue;
            }

            // skip plugins already loaded
            if (_loadedPlugins.Any(x => x.GetType() == pluginType)) continue;

            try
            {
                // we skip chainloader here and don't check dependencies. Fast n dirty.
                var plugin = (BasePlugin)Activator.CreateInstance(pluginType);
                var metadata = MetadataHelper.GetMetadata(plugin);
                _loadedPlugins.Add(plugin);
                plugin.Load();
                loaded.Add(metadata.Name);

                // ensure initialize hook runs even if we reload far after initialization is already done
                if (Hooks.OnInitialize.HasInitialized && plugin is IRunOnInitialized runOnInitialized)
                {
                    runOnInitialized.OnGameInitialized();
                }

                BloodstonePlugin.Logger.LogInfo($"Loaded plugin {pluginType.FullName}");
            }
            catch (Exception ex)
            {
                BloodstonePlugin.Logger.LogError($"Plugin {pluginType.FullName} threw an exception during initialization:");
                BloodstonePlugin.Logger.LogError(ex);
            }
        }

        return loaded;
    }

    private class ReloadBehaviour : UnityEngine.MonoBehaviour
    {
        private void Update()
        {
            if (_clientReloadKeybinding.IsPressed)
            {
                BloodstonePlugin.Logger.LogInfo("Reloading client plugins...");

                UnloadPlugins();
                LoadPlugins();
            }
        }
    }
}