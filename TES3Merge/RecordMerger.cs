﻿using System.Collections;
using System.Reflection;
using TES3Lib.Base;

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

            return a.GetType().GetType().FullName == "System.RuntimeType" ? a.Equals(b) : a.PublicInstancePropertiesEqual(b);
        }

        public override int GetHashCode(object b)
        {
            return base.GetHashCode();
        }
    }

    public static readonly Dictionary<Type, Func<object, object, object, bool>> MergeTypeFunctionMapper = new();
    public static readonly Dictionary<Type, Func<PropertyInfo, object, object, object, bool>> MergePropertyFunctionMapper = new();
    private static readonly PublicPropertyComparer BasicComparer = new();

    static RecordMerger()
    {
        // Define type merge behaviors.
        MergeTypeFunctionMapper[typeof(Record)] = MergeTypeRecord;

        MergeTypeFunctionMapper[typeof(TES3Lib.Records.CELL)] = Merger.CELL.Merge;


        // Define property merge behaviors.
        MergePropertyFunctionMapper[typeof(Subrecord)] = MergePropertySubrecord;

        // Shared
        MergePropertyFunctionMapper[typeof(List<TES3Lib.Subrecords.Shared.Castable.ENAM>)] = Merger.Shared.EffectList;

        // CREA
        MergePropertyFunctionMapper[typeof(List<(IAIPackage, TES3Lib.Subrecords.CREA.CNDT)>)] = Merger.CREA.AIPackage;

        // NPC_
        MergePropertyFunctionMapper[typeof(List<(IAIPackage, TES3Lib.Subrecords.NPC_.CNDT)>)] = Merger.NPC_.AIPackage;
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.NPC_.NPDT)] = Merger.NPC_.NPDT;

        // LEVI
        MergePropertyFunctionMapper[typeof(List<(TES3Lib.Subrecords.LEVI.INAM INAM, TES3Lib.Subrecords.LEVI.INTV INTV)>)] = Merger.LEVI.ITEM;
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.LEVI.INDX)] = Merger.Shared.NoMerge;

        // LEVC
        MergePropertyFunctionMapper[typeof(List<(TES3Lib.Subrecords.LEVC.CNAM CNAM, TES3Lib.Subrecords.LEVC.INTV INTV)>)] = Merger.LEVC.CRIT;
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.LEVC.INDX)] = Merger.Shared.NoMerge;

        // CLAS
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.CLAS.CLDT)] = Merger.CLAS.CLDT;

        // FACT
        MergePropertyFunctionMapper[typeof(TES3Lib.Subrecords.FACT.FADT)] = Merger.FACT.FADT;
    }

    public static Func<object, object, object, bool>? GetTypeMergeFunction(Type? type)
    {
        while (type is not null)
        {
            if (MergeTypeFunctionMapper.TryGetValue(type, out var func))
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
            if (MergePropertyFunctionMapper.TryGetValue(type, out var func))
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
        return mergeFunction is not null && mergeFunction(current, first, next);
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

    public static bool MergeAllProperties(object? current, object? first, object? next)
    {
        if (next is null)
        {
            return false;
        }

        var modified = false;

        var properties = next.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(x => x.MetadataToken).ToList()!;
        foreach (var property in properties)
        {
            // Handle null cases.
            var currentValue = current is not null ? property.GetValue(current) : null;
            var firstValue = first is not null ? property.GetValue(first) : null;
            var nextValue = next is not null ? property.GetValue(next) : null;

            if (firstValue is null && currentValue is null && nextValue is not null)
            {
                property.SetValue(current, nextValue);
                modified = true;
                continue;
            }
            else if (firstValue is not null && nextValue is null)
            {
                // if the base value is not null, but some plugin later in the load order does set the value to null
                // then retain the latest value
                // TODO set null?
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

    private static bool MergeTypeRecord(object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var current = currentParam as Record ?? throw new ArgumentException("Current record is of incorrect type.");
        var first = firstParam as Record ?? throw new ArgumentException("First record is of incorrect type.");
        var next = nextParam as Record ?? throw new ArgumentException("Next record is of incorrect type.");

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

    private static bool MergePropertyBase(PropertyInfo property, object currentParam, object firstParam, object nextParam)
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

            var currentIsUnmodified = currentValue is not null && firstValue is not null ? currentAsEnumerable.SequenceEqual(firstAsEnumerable, BasicComparer) : currentValue == firstValue;
            var nextIsUnmodified = nextValue is not null && firstValue is not null ? nextAsEnumerable.SequenceEqual(firstAsEnumerable, BasicComparer) : nextValue == firstValue;

            if (currentIsUnmodified && !nextIsUnmodified)
            {
                property.SetValue(currentParam, nextValue);
                return true;
            }
        }
        else
        {
            var currentIsUnmodified = currentValue is not null ? currentValue.Equals(firstValue) : firstValue is null;
            var nextIsModified = !(nextValue is not null ? nextValue.Equals(firstValue) : firstValue is null);

            if (currentIsUnmodified && nextIsModified)
            {
                property.SetValue(currentParam, nextValue);
                return true;
            }
        }

        return false;
    }

    public static bool MergePropertySubrecord(PropertyInfo property, object currentParam, object firstParam, object nextParam)
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
            var subProperty = current.GetType().GetProperty(propertyName) ?? throw new Exception($"Property '{propertyName}' does not exist for type {current.GetType().FullName}.");
            if (Merge(subProperty, current, first, next))
            {
                modified = true;
            }
        }

        return modified;
    }
}
