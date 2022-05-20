using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using TES3Lib.Base;
using TES3Lib.Subrecords.LEVC;

namespace TES3Merge.Merger;

internal static class LEVC
{
    private class CRITComparer : EqualityComparer<(CNAM CNAM, INTV INTV)>
    {
        public override bool Equals((CNAM CNAM, INTV INTV) x, (CNAM CNAM, INTV INTV) y)
        {
            return string.Equals(x.CNAM.CreatureEditorId, y.CNAM.CreatureEditorId) && x.INTV.PCLevelOfPrevious == y.INTV.PCLevelOfPrevious;
        }

        public override int GetHashCode([DisallowNull] (CNAM CNAM, INTV INTV) obj)
        {
            return base.GetHashCode();
        }
    }

    private static readonly CRITComparer BasicComparer = new();

    internal static bool CRIT(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var currentAsEnumerable = property.GetValue(currentParam) as List<(CNAM CNAM, INTV INTV)>
            ?? throw new ArgumentException("Current record is of incorrect type.");
        var firstAsEnumerable = property.GetValue(firstParam) as List<(CNAM CNAM, INTV INTV)>
            ?? throw new ArgumentException("First record is of incorrect type.");
        var nextAsEnumerable = property.GetValue(nextParam) as List<(CNAM CNAM, INTV INTV)>
            ?? throw new ArgumentException("Next record is of incorrect type.");

        var modified = false;

        // Ensure that we have a current value.
        if (currentAsEnumerable == null)
        {
            if (firstAsEnumerable != null)
            {
                currentAsEnumerable = new List<(CNAM CNAM, INTV INTV)>(firstAsEnumerable);
                property.SetValue(currentParam, currentAsEnumerable);
            }
            else if (nextAsEnumerable != null)
            {
                currentAsEnumerable = new List<(CNAM CNAM, INTV INTV)>(nextAsEnumerable);
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
            .ThenBy(x => x.CNAM.CreatureEditorId)
            .ToList();

        // compare to vanilla
        if (!union.SequenceEqual(firstAsEnumerable))
        {
            property.SetValue(currentParam, union);
            modified = true;
            if (currentParam is TES3Lib.Records.LEVC levi)
            {
                levi.INDX.CreatureCount = union.Count;
            }
        }

        return modified;
    }
}
