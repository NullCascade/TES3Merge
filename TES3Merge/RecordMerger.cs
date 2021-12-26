using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TES3Lib;

namespace TES3Merge
{
    internal static class RecordMerger
    {
        internal class PublicPropertyComparer : EqualityComparer<object>
        {
            public override bool Equals(object? a, object? b)
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

        public static Dictionary<Type, Func<object, object, object, bool>> MergeTypeFunctionMapper = new();
        public static Dictionary<Type, Func<PropertyInfo, object, object, object, bool>> MergePropertyFunctionMapper = new();

        readonly static PublicPropertyComparer BasicComparer = new();

        static RecordMerger()
        {
            // Define type merge behaviors.
            MergeTypeFunctionMapper[typeof(TES3Lib.Base.Record)] = MergeTypeRecord;

            // Define property merge behaviors.
            MergePropertyFunctionMapper[typeof(TES3Lib.Base.Subrecord)] = MergePropertySubrecord;
            MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.CLAS.CLDT)] = Merger.CLAS.CLDT;
            MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.FACT.FADT)] = Merger.FACT.FADT;
            MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.NPC_.NPDT)] = Merger.NPC_.NPDT;
        }

        public static Func<object, object, object, bool>? GetTypeMergeFunction(Type? type)
        {
            while (type != null)
            {
                if (MergeTypeFunctionMapper.TryGetValue(type, out Func<object, object, object, bool>? func))
                {
                    return func;
                }

                type = type.BaseType;
            }

            return null;
        }

        public static Func<PropertyInfo, object, object, object, bool> GetPropertyMergeFunction(Type? type)
        {
            while (type != null)
            {
                if (MergePropertyFunctionMapper.TryGetValue(type, out Func<PropertyInfo, object, object, object, bool>? func))
                {
                    return func;
                }
                type = type.BaseType;
            }

            return MergePropertyBase;
        }

        public static bool Merge(object current, object first, object next)
        {
            // We never want to merge an object redundantly.
            if (first == next)
            {
                return false;
            }

            // Figure out what merge function we will use.
            var mergeFunction = GetTypeMergeFunction(current.GetType());
            if (mergeFunction == null)
            {
                return false;
            }

            return mergeFunction(current, first, next);
        }

        public static bool Merge(PropertyInfo property, object current, object first, object next)
        {
            // We never want to merge an object redundantly.
            if (first == next)
            {
                return false;
            }

            // Figure out what merge function we will use.
            var mergeFunction = GetPropertyMergeFunction(property.PropertyType);
            return mergeFunction(property, current, first, next);
        }

        static bool MergeAllProperties(object? current, object? first, object? next)
        {
            if (next == null)
            {
                return false;
            }

            var modified = false;

            var properties = next.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(x => x.MetadataToken).ToList()!;
            foreach (PropertyInfo property in properties)
            {
                // Handle null cases.
                var currentValue = current != null ? property.GetValue(current) : null;
                var firstValue = first != null ? property.GetValue(first) : null;
                var nextValue = next != null ? property.GetValue(next) : null;
                if (firstValue == null && currentValue == null && nextValue != null)
                {
                    property.SetValue(current, nextValue);
                    modified = true;
                    continue;
                }
                else if (firstValue != null && nextValue == null)
                {
                    property.SetValue(current, null);
                    modified = true;
                    continue;
                }

                // Find a merger and run it.
                if (Merge(property, current!, first!, next!))
                {
                    modified = true;
                }
            }

            return modified;
        }

        static bool MergeTypeRecord(object currentParam, object firstParam, object nextParam)
        {
            // Get the values as their correct type.
            var current = currentParam as TES3Lib.Base.Record ?? throw new ArgumentException("Current record is of incorrect type.");
            var first = firstParam as TES3Lib.Base.Record ?? throw new ArgumentException("First record is of incorrect type.");
            var next = nextParam as TES3Lib.Base.Record ?? throw new ArgumentException("Next record is of incorrect type.");

            // Store modified state.
            bool modified = false;

            // Ensure that the record type hasn't changed.
            if (!first.Name.Equals(next.Name))
            {
                throw new Exception("Record types differ!");
            }

            // Cover the base record flags.
            if (current.Flags.SequenceEqual(first.Flags) && !next.Flags.SequenceEqual(first.Flags))
            {
                current.Flags = next.Flags;
                modified = true;
            }

            // Generically merge all other properties.
            if (MergeAllProperties(current, first, next))
            {
                modified = true;
            }

            return modified;
        }

        static bool MergePropertyBase(PropertyInfo property, object currentParam, object firstParam, object nextParam)
        {
            var currentValue = currentParam != null ? property.GetValue(currentParam) : null;
            var firstValue = firstParam != null ? property.GetValue(firstParam) : null;
            var nextValue = nextParam != null ? property.GetValue(nextParam) : null;

            // Handle collections.
            if (property.PropertyType.IsNonStringEnumerable())
            {
                var currentAsEnumerable = (currentValue as IEnumerable)?.Cast<object>()!;
                var firstAsEnumerable = (firstValue as IEnumerable)?.Cast<object>()!;
                var nextAsEnumerable = (nextValue as IEnumerable)?.Cast<object>()!;

                bool currentIsUnmodified = currentValue != null && firstValue != null ? currentAsEnumerable.SequenceEqual(firstAsEnumerable, BasicComparer) : currentValue == firstValue;
                bool nextIsUnmodified = nextValue != null && firstValue != null ? nextAsEnumerable.SequenceEqual(firstAsEnumerable, BasicComparer) : nextValue == firstValue;
                if (currentIsUnmodified && !nextIsUnmodified)
                {
                    property.SetValue(currentParam, nextValue);
                    return true;
                }
            }
            else
            {
                bool currentIsUnmodified = currentValue != null ? currentValue.Equals(firstValue) : firstValue == null;
                bool nextIsModified = !(nextValue != null ? nextValue.Equals(firstValue) : firstValue == null);
                if (currentIsUnmodified && nextIsModified)
                {
                    property.SetValue(currentParam, nextValue);
                    return true;
                }
            }

            return false;
        }

        static bool MergePropertySubrecord(PropertyInfo property, object currentParam, object firstParam, object nextParam)
        {
            // Get the values as their correct type.
            var currentValue = currentParam != null ? property.GetValue(currentParam) : null;
            var firstValue = firstParam != null ? property.GetValue(firstParam) : null;
            var nextValue = nextParam != null ? property.GetValue(nextParam) : null;

            bool modified = true;

            // Handle null cases.
            if (firstValue == null && currentValue == null && nextValue != null)
            {
                property.SetValue(currentParam, nextValue);
                modified = true;
            }
            else if (firstValue != null && nextValue == null)
            {
                property.SetValue(currentParam, null);
                modified = true;
            }
            else
            {
                if (MergeAllProperties(currentValue, firstValue, nextValue))
                {
                    modified = true;
                }
            }

            return modified;
        }
    }
}
