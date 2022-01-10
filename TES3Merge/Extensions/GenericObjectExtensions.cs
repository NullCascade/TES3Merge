using System.Collections;
using System.Reflection;

namespace TES3Merge;

static class GenericObjectExtensions
{
    public static bool IsNonStringEnumerable(this PropertyInfo pi)
    {
        return pi != null && pi.PropertyType.IsNonStringEnumerable();
    }

    public static bool IsNonStringEnumerable(this object instance)
    {
        return instance != null && instance.GetType().IsNonStringEnumerable();
    }

    public static bool IsNonStringEnumerable(this Type type)
    {
        if (type == null || type == typeof(string))
            return false;
        return typeof(IEnumerable).IsAssignableFrom(type);
    }

    public static bool PublicInstancePropertiesEqual<T>(this T self, T to, params string[] ignore) where T : class
    {
        if (self != null && to != null)
        {
            var type = typeof(T);
            var ignoreList = new List<string>(ignore);
            var unequalProperties =
                from pi in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                where !ignoreList.Contains(pi.Name) && pi.GetUnderlyingType().IsSimpleType() && pi.GetIndexParameters().Length == 0
                let selfValue = type.GetProperty(pi.Name)?.GetValue(self, null)
                let toValue = type.GetProperty(pi.Name)?.GetValue(to, null)
                where selfValue != toValue && (selfValue == null || !selfValue.Equals(toValue))
                select selfValue;
            return !unequalProperties.Any();
        }
        return self == to;
    }

    /// <summary>
    /// Determine whether a type is simple (String, Decimal, DateTime, etc) 
    /// or complex (i.e. custom class with public properties and methods).
    /// </summary>
    /// <see cref="http://stackoverflow.com/questions/2442534/how-to-test-if-type-is-primitive"/>
    public static bool IsSimpleType(this Type type)
    {
        return
           type.IsValueType ||
           type.IsPrimitive ||
           new[]
           {
               typeof(String),
               typeof(Decimal),
               typeof(DateTime),
               typeof(DateTimeOffset),
               typeof(TimeSpan),
               typeof(Guid)
           }.Contains(type) ||
           (Convert.GetTypeCode(type) != TypeCode.Object);
    }

    public static Type GetUnderlyingType(this MemberInfo member)
    {
        if (member == null)
        {
            throw new ArgumentException("Input MemberInfo must not be null.");
        }

        return member.MemberType switch
        {
            MemberTypes.Event => ((EventInfo)member).EventHandlerType ?? throw new ArgumentException("EventInfo does not have EventHandlerType."),
            MemberTypes.Field => ((FieldInfo)member).FieldType,
            MemberTypes.Method => ((MethodInfo)member).ReturnType,
            MemberTypes.Property => ((PropertyInfo)member).PropertyType,
            _ => throw new ArgumentException("Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"),
        };
    }
}
