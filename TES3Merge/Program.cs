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
        verifyCommand.SetHandler(() => VerifyCommand.Run());

        var multipatchCommand = new Command("multipatch", "Alias for merging LEVI and LEVC.") { };
        multipatchCommand.SetHandler(() => MultipatchCommand.Run());

        var rootCommand = new RootCommand
        {
            verifyCommand,
            multipatchCommand,
        };
        {
            var inclusiveOption = new Option<bool>(new[] { "--inclusive-list", "-i" }, "Merge lists inclusively per element (implemented for List<NPCO>)");
            var recordsOption = new Option<IEnumerable<string>>(new[] { "--records", "-r" }, "Merge only specified records.")
            {
                AllowMultipleArgumentsPerToken = true
            };
            var ignoreRecordsOption = new Option<IEnumerable<string>>(new[] { "--ignore-records", "--ir" }, "Ignore specified records.")
            {
                AllowMultipleArgumentsPerToken = true
            };
            rootCommand.AddOption(inclusiveOption);
            rootCommand.AddOption(recordsOption);
            rootCommand.AddOption(ignoreRecordsOption);
            rootCommand.SetHandler((bool i, IEnumerable<string> r, IEnumerable<string> ir) =>
            {
                MergeCommand.Run(i, r, ir);
            }, inclusiveOption, recordsOption, ignoreRecordsOption);
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Logger.WriteLine($"TES3Merge v{version}.");

        await rootCommand.InvokeAsync(args);
    }
}
