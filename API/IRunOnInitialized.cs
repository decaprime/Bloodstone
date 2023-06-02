namespace Bloodstone.API;

/// <summary>
/// Helper interface for plugins that wish to run something once the
/// game has been initialized, instead of during plugin loading time.
/// This is useful because when BepInEx normally loads us, the ECS
/// worlds have yet to be initialized. This is not a problem for hooking,
/// but for other cases some callback to know when the game has initialized
/// is nice to have.
///
/// This interface will run your code both when it is loaded normally (i.e.
/// as a normal BepInEx plugin) and when it is reloaded through the Bloodstone
/// reloading facilities (see also the Reloadable attribute). You do not need
/// to attach an event handler anywhere, we'll magically call your plugin.
///
/// This works on both client and server. The exact time at which the callback
/// will be called is undefined, but it is guaranteed to happen after the
/// ECS system has been initialized.
/// </summary>
public interface IRunOnInitialized
{
    /// <summary>
    /// Callback to be called when the game's ECS system has finished initializing.
    /// See the type documentation for IRunOnInitialized for more details on the
    /// exact semantics of this function.
    /// </summary>
    public void OnGameInitialized();
}