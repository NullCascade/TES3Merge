using System;
using System.Linq;

namespace TES3Merge.Tests;

internal static class Utility
{
    internal const string MergedObjectsPluginName = "Merged Objects.esp";

    internal static TES3Lib.Base.Record? FindRecord(this TES3Lib.TES3 plugin, string id)
    {
        return plugin.Records.FirstOrDefault(r => r.GetEditorId() == $"{id}\0");
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
}
