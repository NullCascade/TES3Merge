using System.Reflection;

namespace TES3Merge.Merger;

internal static class Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This function signature must match other merge functions.")]
    internal static bool NoMerge(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        return false;
    }

    internal static bool MergeEffect(List<TES3Lib.Subrecords.Shared.Castable.ENAM>? current, List<TES3Lib.Subrecords.Shared.Castable.ENAM>? first, List<TES3Lib.Subrecords.Shared.Castable.ENAM>? next, int index)
    {
        var currentValue = current.ElementAtOrDefault(index);
        var firstValue = first?.ElementAtOrDefault(index);
        var nextValue = next?.ElementAtOrDefault(index);

        // If we have values for everything...
        if (currentValue is not null && firstValue is not null && nextValue is not null)
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
        if (firstValue is null && nextValue is not null)
        {
            current.Add(nextValue);
            return true;
        }

        return false;
    }

    internal static bool EffectList(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var current = property.GetValue(currentParam) as List<TES3Lib.Subrecords.Shared.Castable.ENAM>;
        var first = property.GetValue(firstParam) as List<TES3Lib.Subrecords.Shared.Castable.ENAM>;
        var next = property.GetValue(nextParam) as List<TES3Lib.Subrecords.Shared.Castable.ENAM>;

        bool modified = false;

        // Handle null cases.
        if (!current.NullableSequenceEqual(next) && next is not null)
        {
            current = new List<TES3Lib.Subrecords.Shared.Castable.ENAM>(next);
            property.SetValue(currentParam, current);
            modified = true;
        }

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
