using System.Collections.Generic;
using System.IO;

namespace TES3Merge.Tests;

internal static class FileLoader
{
    /// <summary>
    /// A map of loaded plugins. This is lazy-filled as requested.
    /// </summary>
    readonly static Dictionary<string, TES3Lib.TES3> LoadedPlugins = new();

    /// <summary>
    /// A filter for all the types we will load. This optimizes loading so we don't load records we will never test.
    /// </summary>
    static readonly List<string> testedRecords = new(new string[] {
            "ALCH",
        });

    /// <summary>
    /// Lazy-loads a plugin in the Plugins folder. Ensure that the plugin is set to copy over to the output folder.
    /// </summary>
    /// <param name="name">The name of the plugin file, including the file extension, relative to the plugins folder.</param>
    /// <returns></returns>
    internal static TES3Lib.TES3? GetPlugin(string name)
    {
        if (!LoadedPlugins.ContainsKey(name))
        {
            var loadedPlugin = TES3Lib.TES3.TES3Load(Path.Combine("Plugins", name), testedRecords);
            loadedPlugin.Path = name; // Override path to remove prefix.
            LoadedPlugins[name] = loadedPlugin;
            return loadedPlugin;
        }
        return LoadedPlugins[name];
    }

    /// <summary>
    /// Lazy-loads a plugin through <see cref="GetPlugin(string)"/>, and returns a record from it with the given <paramref name="id"/>.
    /// </summary>
    /// <param name="pluginName">The full file name of the plugin, including file extension, relative to the plugins folder.</param>
    /// <param name="id">The id of the record to find. It does not need to manually specify a null terminator.</param>
    /// <returns>The found record, or null if the plugin could not be loaded or if the record does not exist.</returns>
    internal static TES3Lib.Base.Record? FindRecord(string pluginName, string id)
    {
        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            return null;
        }
        return plugin.FindRecord(id);
    }
}
