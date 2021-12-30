using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using static TES3Merge.Tests.FileLoader;

namespace TES3Merge.Tests.Merger
{
    public abstract class RecordTest<T> where T : TES3Lib.Base.Record
    {
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
                var record = RecordCache.ContainsKey(parent.Path) ? RecordCache[parent.Path] : parent.FindRecord(objectId) as T ?? throw new Exception($"Parent file {parent.Path} does not have record {objectId}.");
                records.Add(record);
                RecordCache[parent.Path] = record;
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
        #endregion

        #region Logging
        internal static void LogRecordValue(string property, string plugin)
        {
            LogRecordValue(GetCached(plugin), property, plugin);
        }

        internal static void LogRecordValue(T record, string property, string plugin = Utility.MergedObjectsPluginName)
        {
            Logger.LogMessage($"{plugin} : {Utility.GetPropertyValue(record, property)}");
        }

        internal virtual void LogRecordsEffects(T merged, params string[] plugins)
        {
            throw new NotImplementedException();
        }

        internal static void LogRecords(string property, T merged, params string[] plugins)
        {
            foreach (var plugin in plugins)
            {
                LogRecordValue(property, plugin);
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
        #endregion
    }
}
