﻿using System.Reflection;
using static TES3Merge.RecordMerger;

namespace TES3Merge.Merger;

internal static class Shared
{
    static readonly PublicPropertyComparer BasicComparer = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This function signature must match other merge functions.")]
    internal static bool NoMerge(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        return false;
    }

    internal static bool MergeEffect(List<TES3Lib.Subrecords.Shared.Castable.ENAM> current, List<TES3Lib.Subrecords.Shared.Castable.ENAM>? first, List<TES3Lib.Subrecords.Shared.Castable.ENAM>? next, int index)
    {
        TES3Lib.Subrecords.Shared.Castable.ENAM? currentValue = current.ElementAtOrDefault(index);
        TES3Lib.Subrecords.Shared.Castable.ENAM? firstValue = first?.ElementAtOrDefault(index);
        TES3Lib.Subrecords.Shared.Castable.ENAM? nextValue = next?.ElementAtOrDefault(index);

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
                return MergeAllProperties(currentValue, firstValue, nextValue);
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

        var modified = false;

        // remove this for now until refactor
        // Handle null cases.
        //if (!current.NullableSequenceEqual(next) && next is not null)
        //{
        //    current = new List<TES3Lib.Subrecords.Shared.Castable.ENAM>(next);
        //    property.SetValue(currentParam, current);
        //    modified = true;
        //}

        // Ensure that we have a current value.
        if (current == null)
        {
            if (first != null)
            {
                current = new List<TES3Lib.Subrecords.Shared.Castable.ENAM>(first);
                property.SetValue(currentParam, current);
            }
            else if (next != null)
            {
                current = new List<TES3Lib.Subrecords.Shared.Castable.ENAM>(next);
                property.SetValue(currentParam, current);
            }
            else
            {
                return false;
            }
        }

        // 
        for (var i = 0; i < 8; i++)
        {
            if (MergeEffect(current, first, next, i))
            {
                modified = true;
            }
        }

        return modified;
    }

    internal static bool ItemsList(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        if (property.GetValue(firstParam) is not List<TES3Lib.Subrecords.Shared.NPCO> firstAsEnumerable)
        {
            return false;
        }
        if (property.GetValue(nextParam) is not List<TES3Lib.Subrecords.Shared.NPCO> nextAsEnumerable)
        {
            return false;
        }
        if (property.GetValue(currentParam) is not List<TES3Lib.Subrecords.Shared.NPCO> currentAsEnumerable)
        {
            return false;
        }
        if (firstAsEnumerable == null || nextAsEnumerable == null)
        {
            return false;
        }

        var modified = false;

        // Ensure that we have a current value.
        if (currentAsEnumerable == null)
        {
            if (firstAsEnumerable != null)
            {
                currentAsEnumerable = new List<TES3Lib.Subrecords.Shared.NPCO>(firstAsEnumerable);
                property.SetValue(currentParam, currentAsEnumerable);
            }
            else if (nextAsEnumerable != null)
            {
                currentAsEnumerable = new List<TES3Lib.Subrecords.Shared.NPCO>(nextAsEnumerable);
                property.SetValue(currentParam, currentAsEnumerable);
            }
            else
            {
                return false;
            }
        }

        if (firstAsEnumerable == null)
        {
            throw new ArgumentNullException(nameof(firstAsEnumerable));
        }

        // inclusive list merge
        IEnumerable<object>? inclusiveValue = firstAsEnumerable
            .Union(currentAsEnumerable, BasicComparer)
            .Union(nextAsEnumerable, BasicComparer)
            .Distinct(BasicComparer);

        if (!inclusiveValue.SequenceEqual(firstAsEnumerable, BasicComparer))
        {
            var inclusiveValueAsList = inclusiveValue
                .Cast<TES3Lib.Subrecords.Shared.NPCO>()
                .ToList();
            property.SetValue(currentParam, inclusiveValueAsList);
            modified = true;
        }

        return modified;
    }
}
