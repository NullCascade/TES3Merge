using System;
using System.Collections.Generic;
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
        if (src is null) throw new ArgumentException("Value cannot be null.", nameof(src));
        if (property is null) throw new ArgumentException("Value cannot be null.", nameof(property));

        if (property.Contains('.')) //complex type nested
        {
            var temp = property.Split(new char[] { '.' }, 2);
            var value = GetPropertyValue(src, temp[0]);
            if (value is null) return null;
            return GetPropertyValue(value, temp[1]);
        }
        else
        {
            return src.GetType().GetProperty(property)?.GetValue(src, null);
        }
    }
}
