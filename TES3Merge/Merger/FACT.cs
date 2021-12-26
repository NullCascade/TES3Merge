using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TES3Merge.Merger
{
    internal static class FACT
    {
        static readonly string[] FactionDataBasicProperties = { "IsHiddenFromPlayer" };

        public static bool FADT(PropertyInfo property, object currentParam, object firstParam, object nextParam)
        {
            // Get the values as their correct type.
            var current = property.GetValue(currentParam) as TES3Lib.Subrecords.FACT.FADT ?? throw new ArgumentException("Current record is of incorrect type.");
            var first = property.GetValue(firstParam) as TES3Lib.Subrecords.FACT.FADT ?? throw new ArgumentException("First record is of incorrect type.");
            var next = property.GetValue(nextParam) as TES3Lib.Subrecords.FACT.FADT ?? throw new ArgumentException("Next record is of incorrect type.");

            bool modified = false;

            // Perform basic merges.
            if (RecordMerger.MergeNamedProperties(FactionDataBasicProperties, current, first, next))
            {
                modified = true;
            }

            return modified;
        }
    }
}
