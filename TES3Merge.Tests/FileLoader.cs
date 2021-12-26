using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TES3Merge.Tests
{
    internal static class FileLoader
    {
        static Dictionary<string, TES3Lib.TES3> LoadedPlugins = new();

        static readonly List<string> testedRecords = new(new string[] {
            "ALCH",
        });

        internal static TES3Lib.TES3? GetPlugin(string name)
        {
            if (!LoadedPlugins.ContainsKey(name))
            {
                var loadedPlugin = TES3Lib.TES3.TES3Load(Path.Combine("Plugins", name), testedRecords);
                LoadedPlugins[name] = loadedPlugin;
                return loadedPlugin;
            }
            return LoadedPlugins[name];
        }
    }
}
