using System.Collections;
using System.Reflection;

namespace TES3Merge;

internal static class RecordMerger
{
    internal class PublicPropertyComparer : EqualityComparer<object>
    {
        public override bool Equals(object? a, object? b)
        {
            if (a is null && b is null)
            {
                return true;
            }
            else if (a is null || b is null)
            {
                return false;
            }

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

    public static readonly Dictionary<Type, Func<object, object, object, bool>> MergeTypeFunctionMapper = new();
    public static readonly Dictionary<Type, Func<PropertyInfo, object, object, object, bool>> MergePropertyFunctionMapper = new();

    static readonly PublicPropertyComparer BasicComparer = new();

    static RecordMerger()
    {
        // Define type merge behaviors.
        MergeTypeFunctionMapper[typeof(TES3Lib.Base.Record)] = MergeTypeRecord;

        // Define property merge behaviors.
        MergePropertyFunctionMapper[typeof(List<(TES3Lib.Base.IAIPackage, TES3Lib.Subrecords.CREA.CNDT)>)] = Merger.Shared.NoMerge;
        MergePropertyFunctionMapper[typeof(List<(TES3Lib.Base.IAIPackage, TES3Lib.Subrecords.NPC_.CNDT)>)] = Merger.Shared.NoMerge;

        MergePropertyFunctionMapper[typeof(List<TES3Lib.Subrecords.Shared.Castable.ENAM>)] = Merger.Shared.EffectList;
        //MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.Shared.SCRI)] = Merger.Shared.SCRI;

        MergePropertyFunctionMapper[typeof(TES3Lib.Base.Subrecord)] = MergePropertySubrecord;
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.CLAS.CLDT)] = Merger.CLAS.CLDT;
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.FACT.FADT)] = Merger.FACT.FADT;
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.NPC_.NPDT)] = Merger.NPC_.NPDT;

    }

    public static Func<object, object, object, bool>? GetTypeMergeFunction(Type? type)
    {
        while (type is not null)
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
        while (type is not null)
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
        Func<object, object, object, bool>? mergeFunction = GetTypeMergeFunction(current.GetType());
        if (mergeFunction is null)
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
        Func<PropertyInfo, object, object, object, bool>? mergeFunction = GetPropertyMergeFunction(property.PropertyType);
        return mergeFunction(property, current, first, next);
    }

    public static bool MergeAllProperties(object? current, object? first, object? next)
    {
        if (next is null)
        {
            return false;
        }

        var modified = false;

        List<PropertyInfo>? properties = next.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(x => x.MetadataToken).ToList()!;
        foreach (PropertyInfo property in properties)
        {
            // Handle null cases.
            var currentValue = current is not null ? property.GetValue(current) : null;
            var firstValue = first is not null ? property.GetValue(first) : null;
            var nextValue = next is not null ? property.GetValue(next) : null;
            if (firstValue is null && currentValue is null && nextValue is not null)
            {
                property.SetValue(current, nextValue); //???
                modified = true;
                continue;
            }
            else if (firstValue is not null && nextValue is null)
            {
                // dbg
                // Console.WriteLine($"*? {(current as TES3Lib.Base.Record).GetEditorId()} {property.Name} - firstValue is not null - nextValue is");

                // if the base value is not null, but some plugin later in the load order does set the value to null
                // then I want to retain the latest value
                //property.SetValue(current, null); // this is wrong: it uses values lower in the load order... 
                property.SetValue(current, currentValue);
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
        TES3Lib.Base.Record? current = currentParam as TES3Lib.Base.Record ?? throw new ArgumentException("Current record is of incorrect type.");
        TES3Lib.Base.Record? first = firstParam as TES3Lib.Base.Record ?? throw new ArgumentException("First record is of incorrect type.");
        TES3Lib.Base.Record? next = nextParam as TES3Lib.Base.Record ?? throw new ArgumentException("Next record is of incorrect type.");

        // Store modified state.
        var modified = false;

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
        var currentValue = currentParam is not null ? property.GetValue(currentParam) : null;
        var firstValue = firstParam is not null ? property.GetValue(firstParam) : null;
        var nextValue = nextParam is not null ? property.GetValue(nextParam) : null;

        // Handle collections.
        if (property.PropertyType.IsNonStringEnumerable())
        {
            var currentAsEnumerable = (currentValue as IEnumerable)?.Cast<object>()!;
            var firstAsEnumerable = (firstValue as IEnumerable)?.Cast<object>()!;
            var nextAsEnumerable = (nextValue as IEnumerable)?.Cast<object>()!;

            bool currentIsUnmodified = currentValue is not null && firstValue is not null ? currentAsEnumerable.SequenceEqual(firstAsEnumerable, BasicComparer) : currentValue == firstValue;
            bool nextIsUnmodified = nextValue is not null && firstValue is not null ? nextAsEnumerable.SequenceEqual(firstAsEnumerable, BasicComparer) : nextValue == firstValue;

            if (currentIsUnmodified && !nextIsUnmodified)
            {
                property.SetValue(currentParam, nextValue);
                return true;
            }
        }
        else
        {
            bool currentIsUnmodified = currentValue is not null ? currentValue.Equals(firstValue) : firstValue is null;
            bool nextIsModified = !(nextValue is not null ? nextValue.Equals(firstValue) : firstValue is null);

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
        var currentValue = currentParam is not null ? property.GetValue(currentParam) : null;
        var firstValue = firstParam is not null ? property.GetValue(firstParam) : null;
        var nextValue = nextParam is not null ? property.GetValue(nextParam) : null;

        var modified = true;

        // Handle null cases.
        if (firstValue is null && currentValue is null && nextValue is not null)
        {
            property.SetValue(currentParam, nextValue);
            modified = true;
        }
        else if (firstValue is not null && nextValue is null)
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

    public static bool MergeNamedProperties(in string[] propertyNames, object current, object first, object next)
    {
        var modified = false;

        foreach (var propertyName in propertyNames)
        {
            PropertyInfo? subProperty = current.GetType().GetProperty(propertyName) ?? throw new Exception($"Property '{propertyName}' does not exist for type {current.GetType().FullName}.");
            if (Merge(subProperty, current, first, next))
            {
                modified = true;
            }
        }

        return modified;
    }
}
