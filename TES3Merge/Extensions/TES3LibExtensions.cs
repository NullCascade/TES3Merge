using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace TES3Merge
{
    public static class TES3LibExtensions
    {
        #region Generic Property Merging
        class PublicPropertyComparer : EqualityComparer<object>
        {
            public override bool Equals(object a, object b)
            {
                if (a == null && b == null)
                    return true;
                else if (a == null || b == null)
                    return false;

                if (a.GetType().GetType().FullName == "System.RuntimeType")
                {
                    return a.Equals(b);
                }

                return a.PublicInstancePropertiesEqual(b);
            }

            public override int GetHashCode(object b)
            {
                return base.GetHashCode();
            }
        }

        public static bool MergeProperty(PropertyInfo property, object obj, object first, object next)
        {
            var currentValue = obj != null ? property.GetValue(obj) : null;
            var firstValue = first != null ? property.GetValue(first) : null;
            var nextValue = next != null ? property.GetValue(next) : null;

            // Handle collections.
            if (property.PropertyType.IsNonStringEnumerable())
            {
                var currentAsEnumerable = (currentValue as IEnumerable)?.Cast<object>();
                var firstAsEnumerable = (firstValue as IEnumerable)?.Cast<object>();
                var nextAsEnumerable = (nextValue as IEnumerable)?.Cast<object>();

                var comparer = new PublicPropertyComparer();
                bool currentIsUnmodified = currentValue != null && firstValue != null ? currentAsEnumerable.SequenceEqual(firstAsEnumerable, comparer) : currentValue == firstValue;
                bool nextIsUnmodified = nextValue != null && firstValue != null ? nextAsEnumerable.SequenceEqual(firstAsEnumerable, comparer) : nextValue == firstValue;
                if (currentIsUnmodified && !nextIsUnmodified)
                {
                    property.SetValue(obj, nextValue);
                    return true;
                }
            }
            else
            {
                bool currentIsUnmodified = currentValue != null ? currentValue.Equals(firstValue) : firstValue == null;
                bool nextIsModified = !(nextValue != null ? nextValue.Equals(firstValue) : firstValue == null);
                if (currentIsUnmodified && nextIsModified)
                {
                    property.SetValue(obj, nextValue);
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Record Extensions
        public static bool MergeWith(this TES3Lib.Base.Record record, TES3Lib.Base.Record next, TES3Lib.Base.Record first)
        {
            if (first == next)
            {
                return false;
            }

            if (!first.Name.Equals(next.Name))
            {
                throw new Exception("Record types differ!");
            }

            bool modified = false;
            if (record.Flags.SequenceEqual(first.Flags) && !next.Flags.SequenceEqual(first.Flags))
            {
                record.Flags = next.Flags;
                modified = true;
            }

            var properties = next.GetType()
                .GetProperties(BindingFlags.Public |
                               BindingFlags.Instance |
                               BindingFlags.DeclaredOnly)
                               .OrderBy(x => x.MetadataToken)
                               .ToList();

            foreach (PropertyInfo property in properties)
            {
                // Handle null cases.
                var currentValue = record != null ? property.GetValue(record) : null;
                var firstValue = first != null ? property.GetValue(first) : null;
                var nextValue = next != null ? property.GetValue(next) : null;
                if (firstValue == null && currentValue == null && nextValue != null)
                {
                    property.SetValue(record, nextValue);
                    modified = true;
                    continue;
                }
                else if (firstValue != null && nextValue == null)
                {
                    property.SetValue(record, null);
                    modified = true;
                    continue;
                }

                // TODO: Change this to something fancy and reflection-y.
                if (property.PropertyType == typeof(TES3Lib.Subrecords.CLAS.CLDT))
                {
                    if ((currentValue as TES3Lib.Subrecords.CLAS.CLDT).MergeWith(nextValue as TES3Lib.Subrecords.CLAS.CLDT, firstValue as TES3Lib.Subrecords.CLAS.CLDT))
                    {
                        modified = true;
                    }
                }
                else if (property.PropertyType == typeof(TES3Lib.Subrecords.FACT.FADT))
                {
                    if ((currentValue as TES3Lib.Subrecords.FACT.FADT).MergeWith(nextValue as TES3Lib.Subrecords.FACT.FADT, firstValue as TES3Lib.Subrecords.FACT.FADT))
                    {
                        modified = true;
                    }
                }
                else if (property.PropertyType.IsSubclassOf(typeof(TES3Lib.Base.Subrecord)))
                {
                    if ((currentValue as TES3Lib.Base.Subrecord).MergeWith(nextValue as TES3Lib.Base.Subrecord, firstValue as TES3Lib.Base.Subrecord))
                    {
                        modified = true;
                    }
                }
                else if (property.PropertyType == typeof(List<(TES3Lib.Base.IAIPackage AIPackage, TES3Lib.Subrecords.CREA.CNDT)>)
                    || property.PropertyType == typeof(List<(TES3Lib.Base.IAIPackage, TES3Lib.Subrecords.NPC_.CNDT)>))
                {
                    // We can't currently merge AI package lists, so we skip it to prevent errors.
                    // TODO: Make this actually work.
                    continue;
                }
                else
                {
                    try
                    {
                        if (MergeProperty(property, record, first, next))
                        {
                            modified = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Program.WriteToLogAndConsole(string.Format("WARNING: Could not merge property {0} for object {1}: {2}", property.Name, record.GetEditorId().Replace("\0", string.Empty), e.Message));
                        Program.Logger.WriteLine($">> Record: {BitConverter.ToString(record.SerializeRecord()).Replace("-", "")}");
                        Program.Logger.WriteLine($">> First: {BitConverter.ToString(first.SerializeRecord()).Replace("-", "")}");
                        Program.Logger.WriteLine($">> Next: {BitConverter.ToString(next.SerializeRecord()).Replace("-", "")}");
                        Program.WriteToLogAndConsole(">> Please post the TES3Merge.log file to GitHub: https://github.com/NullCascade/TES3Merge/issues");
                        Program.WriteToLogAndConsole(">> Note that some unintended issues may arrise from this merge. You may wish to blacklist the given ID.");
                        continue;
                    }
                }
            }

            return modified;
        }
        #endregion

        #region Subrecord Extensions
        public static bool MergeWith(this TES3Lib.Base.Subrecord subrecord, TES3Lib.Base.Subrecord next, TES3Lib.Base.Subrecord first)
        {
            if (first == next)
            {
                return false;
            }

            var properties = next.GetType()
                .GetProperties(BindingFlags.Public |
                               BindingFlags.Instance |
                               BindingFlags.DeclaredOnly)
                               .OrderBy(x => x.MetadataToken)
                               .ToList();

            bool modified = false;
            foreach (PropertyInfo property in properties)
            {
                if (MergeProperty(property, subrecord, first, next))
                {
                    modified = true;
                }
            }

            return modified;
        }

        public static bool MergeWith(this TES3Lib.Subrecords.CLAS.CLDT subrecord, TES3Lib.Subrecords.CLAS.CLDT next, TES3Lib.Subrecords.CLAS.CLDT first)
        {
            if (first == next)
            {
                return false;
            }

            var properties = next.GetType()
                .GetProperties(BindingFlags.Public |
                               BindingFlags.Instance |
                               BindingFlags.DeclaredOnly)
                               .OrderBy(x => x.MetadataToken)
                               .ToList();

            bool modified = false;
            foreach (PropertyInfo property in properties)
            {
                // Don't merge attributes/skills.
                if (property.PropertyType == typeof(TES3Lib.Enums.Attribute) ||
                    property.PropertyType == typeof(TES3Lib.Enums.Skill))
                {
                    continue;
                }

                if (MergeProperty(property, subrecord, first, next))
                {
                    modified = true;
                }
            }

            // TODO: Smart merge major/minor skills, and attributes.

            return modified;
        }

        public static bool MergeWith(this TES3Lib.Subrecords.FACT.FADT subrecord, TES3Lib.Subrecords.FACT.FADT next, TES3Lib.Subrecords.FACT.FADT first)
        {
            if (first == next)
            {
                return false;
            }

            var properties = next.GetType()
                .GetProperties(BindingFlags.Public |
                               BindingFlags.Instance |
                               BindingFlags.DeclaredOnly)
                               .OrderBy(x => x.MetadataToken)
                               .ToList();

            bool modified = false;
            foreach (PropertyInfo property in properties)
            {
                // Don't merge attributes/skills/rank requirements.
                if (property.PropertyType == typeof(TES3Lib.Enums.Attribute) ||
                    property.PropertyType == typeof(TES3Lib.Enums.Skill[]) ||
                    property.PropertyType == typeof(TES3Lib.Subrecords.FACT.FADT.RankRequirement[]))
                {
                    continue;
                }

                if (MergeProperty(property, subrecord, first, next))
                {
                    modified = true;
                }
            }

            // TODO: Smart merge what we skip above.

            return modified;
        }
        #endregion
    }
}
