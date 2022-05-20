using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TES3Lib;
using TES3Lib.Base;
using static TES3Merge.Util;

namespace TES3Merge.Commands;

internal static class MultipatchCommand
{
    /// <summary>
    /// Main command wrapper
    /// </summary>
    internal static void Run()
    {
#if DEBUG == false
        try
#endif
        {
            Multipatch();
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

    /// <summary>
    /// Merge LEVI and LEVC
    /// 
    /// </summary>
    /// <exception cref="Exception"></exception>
    private static void Multipatch()
    {
        using var ssw = new ScopedStopwatch();

        MergeCommand.Merge(true, new List<string>() { "LEVI", "LEVC" });

        // TODO implement more multipatch merges
    }
}
