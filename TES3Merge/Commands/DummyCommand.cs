using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TES3Lib;
using TES3Lib.Base;
using static TES3Merge.Util;

namespace TES3Merge.Commands
{
    // TODO make this a proper commamd?
    internal static class DummyCommand
    {

        internal static void Dummy(string path)
        {
            // Main execution attempt.
#if DEBUG == false
            try
#endif
            {
                using var ssw = new ScopedStopwatch();
                LoadConfig();
                ArgumentNullException.ThrowIfNull(Configuration);

                // Find out where Morrowind lives.
                var morrowindPath = GetMorrowindFolder();
                if (string.IsNullOrEmpty(morrowindPath))
                {
                    throw new Exception($"ERROR: Could not resolve Morrowind directory. Install TES3Merge into Morrowind\\TES3Merge\\TES3Merge.exe or reinstall Morrowind to fix registry values.");
                }
                Logger.WriteLine($"Morrowind found at '{morrowindPath}'.");

                // read input
                if (!File.Exists(path))
                {
                    Logger.WriteLine($"Input file not found at '{path}'.");
                    return;
                }

                var esps = File.ReadAllLines(path).Distinct().ToList();
                Logger.WriteLine($"Found {esps.Count} esps");

                var mergedObjects = new TES3();
                var mergedObjectsHeader = new TES3Lib.Records.TES3
                {
                    HEDR = new TES3Lib.Subrecords.TES3.HEDR()
                    {
                        CompanyName = "TES3Merge",
                        Description = $"Dummy generated at {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}.",
                        Version = 1.3f,
                    }
                };
                mergedObjects.Records.Add(mergedObjectsHeader);
                mergedObjectsHeader.Masters = new List<(TES3Lib.Subrecords.TES3.MAST MAST, TES3Lib.Subrecords.TES3.DATA DATA)>();
                foreach (var gameFile in esps)
                {
                    var esp = new FileInfo(Path.Combine(morrowindPath, "Data Files", $"{gameFile}"));
                    if (esp.Exists)
                    {
                        var size = esp.Length;
                        mergedObjectsHeader.Masters.Add((new TES3Lib.Subrecords.TES3.MAST { Filename = $"{gameFile}\0" }, new TES3Lib.Subrecords.TES3.DATA { MasterDataSize = size }));
                    }
                }

                // Save the dummy file.
                Logger.WriteLine("Saving dummy.esp ...");
                mergedObjectsHeader.HEDR.NumRecords = mergedObjects.Records.Count - 1;
                mergedObjects.TES3Save(Path.Combine(morrowindPath, "Data Files", "dummy.esp"));
            }
#if DEBUG == false
            catch (Exception e)
            {
                Console.WriteLine("A serious error has occurred. Please post the TES3Merge.log file to GitHub: https://github.com/NullCascade/TES3Merge/issues");
                Logger.WriteLine("An unhandled exception has occurred. Traceback:");
                Logger.WriteLine(e.Message);
                Logger.WriteLine(e.StackTrace);
                
            }
#endif

            ShowCompletionPrompt();
        }
    }
}
