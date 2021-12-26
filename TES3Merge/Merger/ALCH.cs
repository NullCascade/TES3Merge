using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TES3Merge.Merger
{
    internal static class ALCH
    {
        public static bool MergeEffect(List<TES3Lib.Subrecords.ALCH.ENAM> current, List<TES3Lib.Subrecords.ALCH.ENAM> first, List<TES3Lib.Subrecords.ALCH.ENAM> next, int index)
        {
            var currentValue = current.ElementAtOrDefault(index);
            var firstValue = first.ElementAtOrDefault(index);
            var nextValue = next.ElementAtOrDefault(index);

            // If we have values for everything...
            if (currentValue != null && firstValue != null && nextValue != null)
            {
                // If the effect has changed, override it all.
                if (nextValue.MagicEffect != firstValue.MagicEffect)
                {
                    current[index] = nextValue;
                    return true;
                }
                // Otherwise merge over individual properties.
                else
                {
                    return RecordMerger.MergeAllProperties(currentValue, firstValue, nextValue);
                }
            }

            // If we have no first value, but do have a next value, this is a new property. Add it.
            if (firstValue == null && nextValue != null)
            {
                current.Add(nextValue);
                return true;
            }

            return false;
        }

        public static bool EffectList(PropertyInfo property, object currentParam, object firstParam, object nextParam)
        {
            // Get the values as their correct type.
            var current = property.GetValue(currentParam) as List<TES3Lib.Subrecords.ALCH.ENAM> ?? throw new ArgumentException("Current record is of incorrect type.");
            var first = property.GetValue(firstParam) as List<TES3Lib.Subrecords.ALCH.ENAM> ?? throw new ArgumentException("First record is of incorrect type.");
            var next = property.GetValue(nextParam) as List<TES3Lib.Subrecords.ALCH.ENAM> ?? throw new ArgumentException("Next record is of incorrect type.");

            bool modified = false;

            // 
            for (int i = 0; i < 8; i++)
            {
                if (MergeEffect(current, first, next, i))
                {
                    modified = true;
                }
            }

            return modified;
        }
    }
}
