using System.CommandLine;
using System.Reflection;
using TES3Merge.Commands;
using static TES3Merge.Util;

namespace TES3Merge;

internal class Program
{
    // main entry point to parse commandline options
    private static async Task Main(string[] args)
    {

        var verifyCommand = new Command("verify", "Checks esps for missing file paths.") { };
        {
            verifyCommand.SetHandler(() =>
            {
                VerifyCommand.Verify();
            });
        }

        var dummyCommand = new Command("dummy", "Creates a dummy esp with given masters.") { };
        {
            var inputPath = new Argument<string>("inputPath", () => "", "Input file path");
            dummyCommand.AddArgument(inputPath);
            dummyCommand.SetHandler((string p) =>
            {
                DummyCommand.Dummy(p);
            }, inputPath);
        }

        var rootCommand = new RootCommand
        {
            verifyCommand,
            dummyCommand,
        };
        var inclusive = new Option<bool>(new[] { "--inclusive-list", "-i" }, "Merge lists inclusively per element (implemented for List<NPCO>)");
        rootCommand.AddOption(inclusive);
        rootCommand.SetHandler((bool i) =>
        {
            MergeCommand.Merge(i);
        }, inclusive);


        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Logger.WriteLine($"TES3Merge v{version}.");

        await rootCommand.InvokeAsync(args);
    }
}
