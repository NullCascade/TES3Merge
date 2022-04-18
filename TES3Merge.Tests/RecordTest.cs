using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static TES3Merge.Tests.FileLoader;

namespace TES3Merge.Tests.Merger;

public abstract class RecordTest<T> where T : TES3Lib.Base.Record
{
    protected Microsoft.Extensions.Logging.ILogger _logger;
    protected IHost _host;

    public RecordTest()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
        "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var hostBuilder = Host.CreateDefaultBuilder().UseSerilog();

        _host = hostBuilder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<RecordTest<T>>>();
    }

    #region Record Management
    internal static Dictionary<string, T> RecordCache = new();

    internal static T GetCached(string plugin)
    {
        return RecordCache[plugin];
    }

    internal static T CreateMergedRecord(string objectId, params string[] parentFiles)
    {
        // Load files.
        List<TES3Lib.TES3> parents = new();
        foreach (var file in parentFiles)
        {
            var parent = GetPlugin(file) ?? throw new Exception($"Parent file {file} could not be loaded.");
            parents.Add(parent);
        }

        // Find records.
        List<T> records = new();
        foreach (var parent in parents)
        {
            var record = RecordCache.ContainsKey(parent.Path)
                ? RecordCache[parent.Path]
                : parent.FindRecord(objectId) as T ?? throw new Exception($"Parent file {parent.Path} does not have record {objectId}.");
            records.Add(record);
            RecordCache[parent.Path] = record;
        }

        // Create merge.
        var first = records.First();
        var last = records.Last();
        var merged = Activator.CreateInstance(last.GetType(), new object[] { last.SerializeRecord() }) as T ?? throw new Exception("Could not create record.");
        for (var i = records.Count - 2; i > 0; i--)
        {
            RecordMerger.Merge(merged, first, records[i]);
        }
        return merged;
    }
    #endregion

    #region Logging
    internal void LogRecordValue(string property, string plugin)
    {
        LogRecordValue(GetCached(plugin), property, plugin);
    }

    internal void LogRecordValue(T record, string property, string plugin = Utility.MergedObjectsPluginName)
    {
        _logger.LogInformation("{plugin} : {PropertyValue}", plugin, Utility.GetPropertyValue(record, property));
    }

    internal virtual void LogRecordsEffects(T merged, params string[] plugins)
    {
        throw new NotImplementedException();
    }
    internal virtual void LogRecordsAIPackages(T merged, params string[] plugins)
    {
        throw new NotImplementedException();
    }
    internal virtual void LogRecordsInventory(T merged, params string[] plugins)
    {
        throw new NotImplementedException();
    }

    internal void LogRecords(string property, T merged, params string[] plugins)
    {
        foreach (var plugin in plugins)
        {
            LogRecordValue(property, plugin);
        }
        LogRecordValue(merged, property);
    }

    internal void LogRecordsEnumerable(IEnumerable? items)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            _logger.LogInformation("  - {Name}: {@Item}", item.GetType().Name, item);
        }
    }

    #endregion
}
