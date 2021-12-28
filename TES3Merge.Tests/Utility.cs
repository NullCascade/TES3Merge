using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using static TES3Merge.Tests.FileLoader;

namespace TES3Merge.Tests
{
    internal static class Utility
    {
        const string MergedObjectsPluginName = "Merged Objects.esp";
        internal static TES3Lib.Base.Record? FindRecord(this TES3Lib.TES3 plugin, string id)
        {
            return plugin.Records.FirstOrDefault(r => r.GetEditorId() == $"{id}\0");
        }

        internal static T CreateMergedRecord<T>(string objectId, Dictionary<string, T> loadedParentRecords, params string[] parentFiles) where T : TES3Lib.Base.Record
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
                var record = parent.FindRecord(objectId) as T ?? throw new Exception($"Parent file {parent.Path} does not have record {objectId}.");
                records.Add(record);
                loadedParentRecords[parent.Path] = record;
            }

            // Create merge.
            var first = records.First();
            var last = records.Last();
            var merged = Activator.CreateInstance(last.GetType(), new object[] { last.SerializeRecord() }) as T ?? throw new Exception("Could not create record.");
            for (int i = records.Count - 2; i > 0; i--)
            {
                RecordMerger.Merge(merged, first, records[i]);
            }
            return merged;
        }

        internal static object? GetPropertyValue(object src, string property)
        {
            if (src == null) throw new ArgumentException("Value cannot be null.", nameof(src));
            if (property == null) throw new ArgumentException("Value cannot be null.", nameof(property));

            if (property.Contains('.'))//complex type nested
            {
                var temp = property.Split(new char[] { '.' }, 2);
                var value = GetPropertyValue(src, temp[0]);
                if (value == null) return null;
                return GetPropertyValue(value, temp[1]);
            }
            else
            {
                var prop = src.GetType().GetProperty(property);
                return prop?.GetValue(src, null);
            }
        }

        internal static void LogRecordValue<T>(Dictionary<string, T> pluginCache, string property, string plugin) where T : TES3Lib.Base.Record
        {
            LogRecordValue(pluginCache[plugin], property, plugin);
        }

        internal static void LogRecordValue<T>(T record, string property, string plugin = MergedObjectsPluginName) where T : TES3Lib.Base.Record
        {
            Logger.LogMessage($"{plugin} : {GetPropertyValue(record, property)}");
        }

        internal static void LogRecordsEffects(Dictionary<string, TES3Lib.Records.ALCH> pluginCache, TES3Lib.Records.ALCH merged, params string[] plugins)
        {
            foreach (var parent in plugins)
            {
                var plugin = pluginCache[parent];
                Logger.LogMessage($"{plugin} : {plugin.ENAM?.Count}");
                LogEffects(plugin.ENAM);
            }
            Logger.LogMessage($"{MergedObjectsPluginName} : {merged.ENAM?.Count}");
            LogEffects(merged.ENAM);
        }

        internal static void LogRecordsEffects(Dictionary<string, TES3Lib.Records.ENCH> pluginCache, TES3Lib.Records.ENCH merged, params string[] plugins)
        {
            foreach (var parent in plugins)
            {
                var plugin = pluginCache[parent];
                Logger.LogMessage($"{plugin} : {plugin.ENAM?.Count}");
                LogEffects(plugin.ENAM);
            }
            Logger.LogMessage($"{MergedObjectsPluginName} : {merged.ENAM?.Count}");
            LogEffects(merged.ENAM);
        }

        internal static void LogRecordsEffects(Dictionary<string, TES3Lib.Records.SPEL> pluginCache, TES3Lib.Records.SPEL merged, params string[] plugins)
        {
            foreach (var parent in plugins)
            {
                var plugin = pluginCache[parent];
                Logger.LogMessage($"{plugin} : {plugin.ENAM?.Count}");
                LogEffects(plugin.ENAM);
            }
            Logger.LogMessage($"{MergedObjectsPluginName} : {merged.ENAM?.Count}");
            LogEffects(merged.ENAM);
        }

        internal static void LogRecords<T>(Dictionary<string, T> pluginCache, string property, T merged, params string[] plugins) where T : TES3Lib.Base.Record
        {
            foreach (var plugin in plugins)
            {
                LogRecordValue(pluginCache, property, plugin);
            }
            LogRecordValue(merged, property);
        }

        internal static void LogEffects(List<TES3Lib.Subrecords.Shared.Castable.ENAM>? effects)
        {
            if (effects == null) return;

            foreach (var effect in effects)
            {
                Logger.LogMessage($"  - Effect: {effect.MagicEffect}; Skill: {effect.Skill}; Attribute: {effect.Attribute}; Magnitude: {effect.Magnitude}; Duration: {effect.Duration}");
            }
        }
    }
}
