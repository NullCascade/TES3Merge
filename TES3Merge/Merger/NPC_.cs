using System.Reflection;

namespace TES3Merge.Merger;

internal static class NPC_
{
    static readonly string[] NPCDataBasicProperties = {
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
        TES3Lib.Subrecords.NPC_.NPDT? current = property.GetValue(currentParam) as TES3Lib.Subrecords.NPC_.NPDT ?? throw new ArgumentException("Current record is of incorrect type.");
        TES3Lib.Subrecords.NPC_.NPDT? first = property.GetValue(firstParam) as TES3Lib.Subrecords.NPC_.NPDT ?? throw new ArgumentException("First record is of incorrect type.");
        TES3Lib.Subrecords.NPC_.NPDT? next = property.GetValue(nextParam) as TES3Lib.Subrecords.NPC_.NPDT ?? throw new ArgumentException("Next record is of incorrect type.");

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
        if (current.Skills is not null && next.Skills is not null)
        {
            if (current.Skills.SequenceEqual(next.Skills))
            {
                return modified;
            }
            if (first.Skills is null)
            {
                first.Skills = next.Skills;
                modified = true;
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
}
