using System.Reflection;
using TES3Lib.Base;
using TES3Lib.Subrecords.Shared;

namespace TES3Merge.Merger;

// TODO duplicated code. fix this with some interfaces or something
internal static class CREA
{
    /// <summary>
    /// Merger for the AIPackage list in CREA
    /// </summary>
    /// <param name="property"></param>
    /// <param name="currentParam"></param>
    /// <param name="firstParam"></param>
    /// <param name="nextParam"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NullReferenceException"></exception>
    internal static bool AIPackage(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var current = property.GetValue(currentParam) as List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)>
            ?? throw new ArgumentException("Current record is of incorrect type.");
        var first = property.GetValue(firstParam) as List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)>
            ?? throw new ArgumentException("First record is of incorrect type.");
        var next = property.GetValue(nextParam) as List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)>
            ?? throw new ArgumentException("Next record is of incorrect type.");

        var modified = false;

        // Ensure that we have a current value.
        if (current == null)
        {
            if (first != null)
            {
                current = new List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)>(first);
                property.SetValue(currentParam, current);
            }
            else if (next != null)
            {
                current = new List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)>(next);
                property.SetValue(currentParam, current);
            }
            else
            {
                return false;
            }
        }

        if (first == null)
        {
            throw new NullReferenceException(nameof(first));
        }

        // for now we only merge the wander package
        if (current.Count + first.Count + next.Count > 0)
        {
            modified = MergeWanderPackage(current, first, next);
        }

        return modified;

        static bool MergeWanderPackage(
            List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)> current,
            List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)>? first,
            List<(IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT CNDT)>? next)
        {
            // only merge one wander package
            var currentValue = current.FirstOrDefault(x => x.AIPackage.GetType() == typeof(AI_W)).AIPackage as AI_W;
            var firstValue = first?.FirstOrDefault(x => x.AIPackage.GetType() == typeof(AI_W)).AIPackage as AI_W;
            var nextValue = next?.FirstOrDefault(x => x.AIPackage.GetType() == typeof(AI_W)).AIPackage as AI_W;

            // TODO remove multiple wander packages?

            // we always have a current value

            // If we have no first value, but do have a next value, this is a new property. Add it.
            if (firstValue is null && nextValue is not null && nextValue is not null)
            {
                currentValue = nextValue;
                return true;
            }
            // If we have values for everything...
            if (firstValue is not null && nextValue is not null)
            {
                var result = RecordMerger.MergeAllProperties(currentValue, firstValue, nextValue);
                return result;
            }

            return false;
        }
    }

    // list of summoned creatures for multipatch in lowercase!
    public static List<string> SummonedCreatures = new()
    {
        "centurion_fire_dead",
        "wraith_sul_senipul",
        "ancestor_ghost_summon",
        "atronach_flame_summon",
        "atronach_frost_summon",
        "atronach_storm_summon",
        "bonelord_summon",
        "bonewalker_summon",
        "bonewalker_greater_summ",
        "centurion_sphere_summon",
        "clannfear_summon",
        "daedroth_summon",
        "dremora_summon",
        "golden saint_summon",
        "hunger_summon",
        "scamp_summon",
        "skeleton_summon",
        "ancestor_ghost_variner",
        "fabricant_summon",
        "bm_bear_black_summon",
        "bm_wolf_grey_summon",
        "bm_wolf_bone_summon"
    };

}
