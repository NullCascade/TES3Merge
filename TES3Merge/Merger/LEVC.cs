using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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

    private class KeyValuePairComparer : EqualityComparer<KeyValuePair<(CNAM, INTV INTV), int>>
    {
        public override bool Equals(KeyValuePair<(CNAM, INTV INTV), int> x, KeyValuePair<(CNAM, INTV INTV), int> y)
        {
            return CritComparer.Equals(x.Key, y.Key) && x.Value == y.Value;
        }

        public override int GetHashCode([DisallowNull] KeyValuePair<(CNAM, INTV INTV), int> obj)
        {
            return base.GetHashCode();
        }
    }

    private static readonly CRITComparer CritComparer = new();
    private static readonly KeyValuePairComparer kvpComparer = new();

    internal static bool CRIT(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var current = property.GetValue(currentParam) as List<(CNAM CNAM, INTV INTV)>
            ?? throw new ArgumentException("Current record is of incorrect type.");
        var first = property.GetValue(firstParam) as List<(CNAM CNAM, INTV INTV)>
            ?? throw new ArgumentException("First record is of incorrect type.");
        var next = property.GetValue(nextParam) as List<(CNAM CNAM, INTV INTV)>
            ?? throw new ArgumentException("Next record is of incorrect type.");

        var modified = false;

        // Ensure that we have a current value.
        if (current == null)
        {
            if (first != null)
            {
                current = new List<(CNAM CNAM, INTV INTV)>(first);
                property.SetValue(currentParam, current);
            }
            else if (next != null)
            {
                current = new List<(CNAM CNAM, INTV INTV)>(next);
                property.SetValue(currentParam, current);
            }
            else
            {
                return false;
            }
        }

        if (first == null)
        {
            throw new ArgumentNullException(nameof(first));
        }

        // minimal distinct inclusive list merge
        // map occurences of items in each plugin
        var fmap = first.ToLookup(x => x, CritComparer).ToDictionary(x => x.Key, y => y.Count());
        var cmap = current.ToLookup(x => x, CritComparer).ToDictionary(x => x.Key, y => y.Count());
        var nmap = next.ToLookup(x => x, CritComparer).ToDictionary(x => x.Key, y => y.Count());

        // gather all
        var map = fmap
            .Union(cmap, kvpComparer)
            .Union(nmap, kvpComparer)
            .Distinct(kvpComparer)
            .ToLookup(x => x.Key, CritComparer)
            .ToDictionary(x => x.Key, y => y.Select(x => x.Value).Max());

        // add by minimal count
        var union = new List<(CNAM CNAM, INTV INTV)>();
        foreach (var (item, cnt) in map)
        {
            for (var i = 0; i < cnt; i++)
            {
                union.Add(item);
            }
        }

        // order
        union = union
            .OrderBy(x => x.INTV.PCLevelOfPrevious)
            .ThenBy(x => x.CNAM.CreatureEditorId)
            .ToList();

        // compare to vanilla
        if (!union.SequenceEqual(first))
        {
            property.SetValue(currentParam, union);
            modified = true;

            // Update list count.
            var levc = currentParam as TES3Lib.Records.LEVC ?? throw new ArgumentException("Object is not of expected type.");

            if (levc.INDX is null)
            {
                levc.INDX = new INDX();
            }
            levc.INDX.CreatureCount = union.Count;
        }

        return modified;
    }
}
