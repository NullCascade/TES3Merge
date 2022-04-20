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
        var inclusive = new Option<bool>(new[] { "--inclusive-list", "-i" }, "Merge lists inclusively per element (implemented for List<NPCO>)");
        var verify = new Option<bool>(new[] { "--verify", "-v" }, "Verify esps");
        var rootCommand = new RootCommand
        {
            inclusive,
            verify
        };

        rootCommand.SetHandler((bool i) => { MergeCommand.Merge(i); }, inclusive);
        rootCommand.SetHandler((bool v) => { VerifyCommand.Verify(v); }, verify);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Logger.WriteLine($"TES3Merge v{version}.");

        await rootCommand.InvokeAsync(args);
    }
}
