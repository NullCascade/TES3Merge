using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using TES3Lib.Base;
using TES3Lib.Subrecords.LEVI;
using TES3Lib.Subrecords.Shared;
using static TES3Merge.RecordMerger;

namespace TES3Merge.Merger;

internal static class LEVI
{
    private class ITEMComparer : EqualityComparer<(INAM CNAM, INTV INTV)>
    {
        public override bool Equals((INAM CNAM, INTV INTV) x, (INAM CNAM, INTV INTV) y)
        {
            return string.Equals(x.CNAM.ItemEditorId, y.CNAM.ItemEditorId) && x.INTV.PCLevelOfPrevious == y.INTV.PCLevelOfPrevious;
        }

        public override int GetHashCode([DisallowNull] (INAM CNAM, INTV INTV) obj)
        {
            return base.GetHashCode();
        }
    }

    private static readonly ITEMComparer BasicComparer = new();

    internal static bool ITEM(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var currentAsEnumerable = property.GetValue(currentParam) as List<(INAM INAM, INTV INTV)>
            ?? throw new ArgumentException("Current record is of incorrect type.");
        var firstAsEnumerable = property.GetValue(firstParam) as List<(INAM INAM, INTV INTV)>
            ?? throw new ArgumentException("First record is of incorrect type.");
        var nextAsEnumerable = property.GetValue(nextParam) as List<(INAM INAM, INTV INTV)>
            ?? throw new ArgumentException("Next record is of incorrect type.");

        var modified = false;

        // Ensure that we have a current value.
        if (currentAsEnumerable == null)
        {
            if (firstAsEnumerable != null)
            {
                currentAsEnumerable = new List<(INAM INAM, INTV INTV)>(firstAsEnumerable);
                property.SetValue(currentParam, currentAsEnumerable);
            }
            else if (nextAsEnumerable != null)
            {
                currentAsEnumerable = new List<(INAM INAM, INTV INTV)>(nextAsEnumerable);
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
        var union = firstAsEnumerable
            .Union(currentAsEnumerable, BasicComparer)
            .Union(nextAsEnumerable, BasicComparer)
            .Distinct(BasicComparer)
            .OrderBy(x => x.INTV.PCLevelOfPrevious)
            .ThenBy(x => x.Item1.ItemEditorId)
            .ToList();

        // strategy: last guy wins for List Flags
        // strategy: last guy wins for "Chance_None"

        // compare to vanilla
        if (!union.SequenceEqual(firstAsEnumerable))
        {
            property.SetValue(currentParam, union);
            modified = true;
            if (currentParam is TES3Lib.Records.LEVI levi)
            {
                levi.INDX.ItemCount = union.Count;
            }
        }

        return modified;
    }
}
