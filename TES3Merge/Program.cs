using System.CommandLine;
using System.Reflection;
using TES3Merge.Commands;
using TES3Merge.Util;
using static TES3Merge.Util.Util;

namespace TES3Merge;

internal class Program
{
    // main entry point to parse commandline options
    private static async Task Main(string[] args)
    {
        var rootCommand = new MergeCommand()
        {
            new MultipatchCommand(),
            new VerifyCommand()
        };

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        WriteToLogAndConsole($"TES3Merge v{version}.");

        // Before anything, load the config.
        LoadConfig();
        if (Configuration is null)
        {
            WriteToLogAndConsole("Could not load TES3Merge's configuration. Aborting.");
            ShowCompletionPrompt();
            return;
        }

        // Get the installation information.
        CurrentInstallation = Installation.CreateFromContext();
        if (CurrentInstallation is null)
        {
            WriteToLogAndConsole("Could not find installation directory. Aborting.");
            ShowCompletionPrompt();
            return;
        }
        else
        {
            WriteToLogAndConsole($"Installation folder: {CurrentInstallation.RootDirectory}");
        }

        await rootCommand.InvokeAsync(args);
    }
}
