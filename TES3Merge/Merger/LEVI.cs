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

    private class KeyValuePairComparer : EqualityComparer<KeyValuePair<(INAM, INTV INTV), int>>
    {
        public override bool Equals(KeyValuePair<(INAM, INTV INTV), int> x, KeyValuePair<(INAM, INTV INTV), int> y)
        {
            return ItemComparer.Equals(x.Key, y.Key) && x.Value == y.Value;
        }

        public override int GetHashCode([DisallowNull] KeyValuePair<(INAM, INTV INTV), int> obj)
        {
            return base.GetHashCode();
        }
    }

    private static readonly ITEMComparer ItemComparer = new();
    private static readonly KeyValuePairComparer kvpComparer = new();

    internal static bool ITEM(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var current = property.GetValue(currentParam) as List<(INAM INAM, INTV INTV)>
            ?? throw new ArgumentException("Current record is of incorrect type.");
        var first = property.GetValue(firstParam) as List<(INAM INAM, INTV INTV)>
            ?? throw new ArgumentException("First record is of incorrect type.");
        var next = property.GetValue(nextParam) as List<(INAM INAM, INTV INTV)>
            ?? throw new ArgumentException("Next record is of incorrect type.");

        var modified = false;

        // Ensure that we have a current value.
        if (current == null)
        {
            if (first != null)
            {
                current = new List<(INAM INAM, INTV INTV)>(first);
                property.SetValue(currentParam, current);
            }
            else if (next != null)
            {
                current = new List<(INAM INAM, INTV INTV)>(next);
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

        /*
         * some special cases:
         * 
         * 1) chance
         * 2) list flags
         * mod A sets chance_none to 75 and changes the list to only one item
         * mod B keeps chance_none at 50 but adds more items
         * 
         * naive outcome:
         * chance 75 but a lot of items
         * 
         * desired outcome:
         * priority -> needs community rules
         * -> but retain the naive merge by default
         * 
         * 2) handle duplicate items
         * mod A,B and C all have different lists but keep one item from vanilla 
         * mod D adds one item 10 times
         * 
         * desired outcome: minimal distinct items
         * 
         */

        // minimal distinct inclusive list merge
        // map occurences of items in each plugin
        var fmap = first.ToLookup(x => x, ItemComparer).ToDictionary(x => x.Key, y => y.Count());
        var cmap = current.ToLookup(x => x, ItemComparer).ToDictionary(x => x.Key, y => y.Count());
        var nmap = next.ToLookup(x => x, ItemComparer).ToDictionary(x => x.Key, y => y.Count());

        // gather all
        var map = fmap
            .Union(cmap, kvpComparer)
            .Union(nmap, kvpComparer)
            .Distinct(kvpComparer)
            .ToLookup(x => x.Key, ItemComparer)
            .ToDictionary(x => x.Key, y => y.Select(x => x.Value).Max());

        // add by minimal count
        var union = new List<(INAM INAM, INTV INTV)>();
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
            .ThenBy(x => x.INAM.ItemEditorId)
            .ToList();

        // naive distinct union
        //var union = first
        //    .Union(current, BasicComparer)
        //    .Union(next, BasicComparer)
        //    .OrderBy(x => x.INTV.PCLevelOfPrevious)
        //    .ThenBy(x => x.Item1.ItemEditorId)
        //    .ToList();

        if (currentParam is TES3Lib.Records.LEVI l)
        {
            if (l.NAME.EditorId == "random_pearl\0")
            {

            }
        }

        // compare to vanilla
        if (!union.SequenceEqual(first))
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
