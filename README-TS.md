![bloodstone-banner](https://i.imgur.com/Py0MwUL.png)

---

Bloodstone is a modding library for both client and server mods for V Rising. By itself, it does not do much except allow you to reload plugins you've put in the Bloodstone plugins folder.

### Installation

- Install [BepInEx](https://v-rising.thunderstore.io/package/BepInEx/BepInExPack_V_Rising/).
- Extract _Bloodstone.dll_ into _`(VRising folder)/BepInEx/plugins`_.
- Optional: extract any reloadable additional plugins into _`(VRising folder)/BepInEx/BloodstonePlugins`_.

### Configuration

Bloodstone supports the following configuration settings, available in `BepInEx/config/gg.deca.Bloodstone.cfg`.

**Client/Server Options:**
- `EnableReloading` [default `false`]: Whether the reloading feature is enabled.
- `ReloadablePluginsFolder` [default `BepInEx/BloodstonePlugins`]: The path to the directory where reloadable plugins should be searched. Relative to the game directory.

**Client Options:**
- Bloodstone keybinding can be configured through the in-game settings screen.

**Server Options:**
- `ReloadCommand` [default `!reload`]: Which text command (sent in chat) should be used to trigger reloading of plugins.

### Support

Join the [modding community](https://vrisingmods.com/discord), and ping `@deca#9999`.

Post an issue on the [GitHub repository](https://github.com/decaprime/Bloodstone). 

### Changelog

- 0.2.2:
    - Exposed LoadedPlugins in public API
- 0.2.1: Initial release for V Rising 1.0
    - Note: **Partial functionality** no clientside Keybinds or CustomNetworkEvent support yet.
    - See https://github.com/decaprime/Bloodstone/pull/6 for more details