using IniParser.Model;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace TES3Merge
{
    internal static class Util
    {
        public static StreamWriter Logger = new("TES3Merge.log", false)
        {
            AutoFlush = true
        };

        public static IniData? Configuration;


        /// <summary>
        /// Finds the relevant Morrowind directory. It will prefer a directory that is shares or is parent to the current folder.
        /// </summary>
        /// <returns>A path to the directory where Morrowind.exe resides, or null if it could not be determined.</returns>
        internal static string? GetMorrowindFolder()
        {
            if (File.Exists("Morrowind.exe"))
            {
                return Directory.GetCurrentDirectory();
            }
            else if (File.Exists(Path.Combine("..", "Morrowind.exe")))
            {
                return Directory.GetParent(Directory.GetCurrentDirectory())?.FullName;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var registryValue = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\bethesda softworks\\Morrowind", "Installed Path", null) as string;
                if (!string.IsNullOrEmpty(registryValue) && File.Exists(Path.Combine(registryValue, "Morrowind.exe")))
                {
                    return registryValue;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a list that is a copy of the load order, filtered to certain results.
        /// </summary>
        /// <param name="loadOrder">The base sorted load order collection.</param>
        /// <param name="filter">The filter to include elements from.</param>
        /// <returns>A copy of <paramref name="loadOrder"/>, filtered to only elements that match with <paramref name="filter"/>.</returns>
        internal static List<string> GetFilteredLoadList(List<string> loadOrder, IEnumerable<string> filter)
        {
            var result = new List<string>();

            foreach (var file in loadOrder)
            {
                if (filter.Contains(file))
                {
                    result.Add(file);
                }
            }

            return result;
        }

        internal static void ShowCompletionPrompt()
        {
            if (Configuration is not null && bool.TryParse(Configuration["General"]["PauseOnCompletion"], out var pauseOnCompletion) && pauseOnCompletion)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Writes to both the console and the log file.
        /// </summary>
        /// <param name="Message">Message to write.</param>
        internal static void WriteToLogAndConsole(string Message)
        {
            Logger.WriteLine(Message);
            Console.WriteLine(Message);
        }
    }
}
