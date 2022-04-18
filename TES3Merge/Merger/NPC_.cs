using System.Reflection;
using TES3Lib.Base;
using TES3Lib.Subrecords.Shared;
using static TES3Merge.RecordMerger;

namespace TES3Merge.Merger;

internal static class NPC_
{
    private static readonly PublicPropertyComparer BasicComparer = new();
    private static readonly string[] NPCDataBasicProperties = {
        "Agility",
        "Disposition",
        "Endurance",
        "Fatigue",
        "Gold",
        "Health",
        "Intelligence",
        "Level",
        "Luck",
        "Personality",
        "Rank",
        "Reputation",
        "Speed",
        "SpellPts",
        "Strength",
        "Unknown1",
        "Unknown2",
        "Unknown3",
        "Willpower"
    };

    public static bool NPDT(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        TES3Lib.Subrecords.NPC_.NPDT? current = property.GetValue(currentParam) as TES3Lib.Subrecords.NPC_.NPDT
            ?? throw new ArgumentException("Current record is of incorrect type.");
        TES3Lib.Subrecords.NPC_.NPDT? first = property.GetValue(firstParam) as TES3Lib.Subrecords.NPC_.NPDT
            ?? throw new ArgumentException("First record is of incorrect type.");
        TES3Lib.Subrecords.NPC_.NPDT? next = property.GetValue(nextParam) as TES3Lib.Subrecords.NPC_.NPDT
            ?? throw new ArgumentException("Next record is of incorrect type.");

        var modified = false;

        // Perform basic merges.
        if (RecordMerger.MergeNamedProperties(NPCDataBasicProperties, current, first, next))
        {
            modified = true;
        }

        // Ensure that we always have skills, in case that we change the autocalc flag.
        if (current.Skills is null && next.Skills is not null)
        {
            current.Skills = next.Skills;
            modified = true;
        }

        // element-wise merge
        if (current.Skills != null && next.Skills != null)
        {
            if (current.Skills.SequenceEqual(next.Skills))
            {
                return modified;
            }

            // TODO length check

            for (var i = 0; i < current.Skills.Length; i++)
            {
                var skill = current.Skills[i];
                var firstSkill = first.Skills[i];
                var nextSkill = next.Skills[i];

                var currentIsModified = firstSkill != skill;
                var nextIsModified = firstSkill != nextSkill;

                if (!currentIsModified && nextIsModified)
                {
                    current.Skills[i] = nextSkill;
                }
            }
        }
        return modified;
    }

    internal static bool AIPackage(PropertyInfo property, object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>? current = property.GetValue(currentParam) as List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>
            ?? throw new ArgumentException("Current record is of incorrect type.");
        List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>? first = property.GetValue(firstParam) as List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>
            ?? throw new ArgumentException("First record is of incorrect type.");
        List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>? next = property.GetValue(nextParam) as List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>
            ?? throw new ArgumentException("Next record is of incorrect type.");

        var modified = false;

        // Ensure that we have a current value.
        if (current == null)
        {
            if (first != null)
            {
                current = new List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>(first);
                property.SetValue(currentParam, current);
            }
            else if (next != null)
            {
                current = new List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>(next);
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

        // for now we only merge the wander package
        if (current.Count + first.Count + next.Count > 0)
        {
            modified = MergeWanderPackage(current, first, next);
        }

        return modified;

        static bool MergeWanderPackage(
            List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)> current,
            List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>? first,
            List<(IAIPackage AIPackage, TES3Lib.Subrecords.NPC_.CNDT CNDT)>? next)
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
}
