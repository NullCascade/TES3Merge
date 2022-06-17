using System.CommandLine;
using static TES3Merge.Util.Util;

namespace TES3Merge.Commands;

public class MultipatchCommand : Command
{
    private new const string Description = "Create a multipatch that merges levelled lists and fixes various other bugs";
    private new const string Name = "multipatch";

    public MultipatchCommand() : base(Name, Description)
    {
        this.SetHandler(() => MultipatchAction.Run());
    }
}

internal static class MultipatchAction
{
    /// <summary>
    /// Main command wrapper
    /// </summary>
    internal static void Run()
    {
#if DEBUG == false
        try
#else
        //Console.WriteLine("Press any button to continue...");
        //Console.ReadLine();
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
    /// tes3cmd multipatch
    /// Merge LEVI and LEVC
    /// </summary>
    /// <exception cref="Exception"></exception>
    private static void Multipatch()
    {
        using var ssw = new ScopedStopwatch();

        MergeAction.Merge(
            new MergeAction.Settings(
                true,
                new List<string>() { "LEVI", "LEVC", "CREA", "CELL" },
                null,
                Util.EPatch.All,
                false,
                true,
                "multipatch.esp"));
    }
}
