using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TES3Merge.Merger
{
    internal static class CLAS
    {
        static readonly string[] ClassDataBasicProperties = { "IsPlayable", "Specialization" };

        public static bool CLDT(PropertyInfo property, object currentParam, object firstParam, object nextParam)
        {
            // Get the values as their correct type.
            var current = property.GetValue(currentParam) as TES3Lib.Subrecords.CLAS.CLDT ?? throw new ArgumentException("Current record is of incorrect type.");
            var first = property.GetValue(firstParam) as TES3Lib.Subrecords.CLAS.CLDT ?? throw new ArgumentException("First record is of incorrect type.");
            var next = property.GetValue(nextParam) as TES3Lib.Subrecords.CLAS.CLDT ?? throw new ArgumentException("Next record is of incorrect type.");

            bool modified = false;

            // Perform basic merges.
            foreach (var propertyName in ClassDataBasicProperties)
            {
                var subProperty = typeof(TES3Lib.Subrecords.CLAS.CLDT).GetProperty(propertyName) ?? throw new Exception($"Property '{propertyName}' does not exist.");
                if (RecordMerger.Merge(subProperty, current, first, next))
                {
                    modified = true;
                }
            }

            return modified;
        }
    }
}
