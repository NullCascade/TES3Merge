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

        var inclusive = new Option<bool>(new[] { "--inclusive-list", "-i" }, "Merge lists inclusively per element (implemented for List<NPCO>)");
        var basePluginsOption = new Option<string[]>(
            new[] { "--base", "-b" },
            "Include a list of fallback esp names to treat as base instead of the Morrowind masters"
            )
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };
        var rootCommand = new RootCommand
        {
            verifyCommand,
            inclusive,
            basePluginsOption
        };
        verifyCommand.SetHandler(() =>
        {
            VerifyCommand.Verify();
        });
        rootCommand.SetHandler((bool i, string[] b) =>
        {
            MergeCommand.Merge(i, b);
        }, inclusive, basePluginsOption);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Logger.WriteLine($"TES3Merge v{version}.");

        await rootCommand.InvokeAsync(args);
    }
}
