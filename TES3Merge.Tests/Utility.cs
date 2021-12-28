using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TES3Merge.Tests
{
    internal static class Utility
    {
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
                var parent = FileLoader.GetPlugin(file) ?? throw new Exception($"Parent file {file} could not be loaded.");
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
            Logger.LogMessage($"{plugin} : {GetPropertyValue(pluginCache[plugin], property)}");
        }

        internal static void LogRecordValue<T>(T record, string property, string plugin = "Merged Objects.esp") where T : TES3Lib.Base.Record
        {
            Logger.LogMessage($"{plugin} : {GetPropertyValue(record, property)}");
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
