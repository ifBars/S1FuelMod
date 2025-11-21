using MelonLoader;
using System;
using System.Reflection;

namespace S1FuelMod.Utils
{
    internal static class ReflectionUtils
    {
        /// <summary>
        /// Recursively searches for a method by name from a class down to the object type.
        /// </summary>
        /// <param name="type">The type you want to recursively search.</param>
        /// <param name="methodName">The name of the method you're searching for.</param>
        /// <param name="bindingFlags">The binding flags to apply during the search.</param>
        /// <returns>The method info if found, otherwise null.</returns>
        internal static MethodInfo? GetMethod(Type? type, string methodName, BindingFlags bindingFlags)
        {
            while (type != null && type != typeof(object))
            {
                MethodInfo? method = type.GetMethod(methodName, bindingFlags);
                if (method != null)
                    return method;

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// INTERNAL: Attempts to set a field or property on an object using reflection.
        /// Tries field first, then property. Handles both public and non-public members.
        /// </summary>
        /// <param name="target">The target object to set the member on.</param>
        /// <param name="memberName">The name of the field or property.</param>
        /// <param name="value">The value to set.</param>
        /// <returns><c>true</c> if the member was successfully set; otherwise, <c>false</c>.</returns>
        internal static bool TrySetFieldOrProperty(object target, string memberName, object value)
        {
            if (target == null) return false;
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            // Try field first
            var fi = type.GetField(memberName, flags);
            if (fi != null)
            {
                try
                {
                    if (value == null || fi.FieldType.IsInstanceOfType(value))
                    {
                        fi.SetValue(target, value);
                        return true;
                    }
                }
                catch { }
            }
            
            // Try property
            var pi = type.GetProperty(memberName, flags);
            if (pi != null && pi.CanWrite)
            {
                try
                {
                    if (value == null || pi.PropertyType.IsInstanceOfType(value))
                    {
                        pi.SetValue(target, value);
                        return true;
                    }
                }
                catch { }
            }
            
            return false;
        }

        /// <summary>
        /// INTERNAL: Attempts to get a field or property value from an object using reflection.
        /// Tries field first, then property. Handles both public and non-public members.
        /// </summary>
        /// <param name="target">The target object to get the member from.</param>
        /// <param name="memberName">The name of the field or property.</param>
        /// <returns>The value of the member, or <c>null</c> if not found or inaccessible.</returns>
        internal static object TryGetFieldOrProperty(object target, string memberName)
        {
            if (target == null) return null;
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            // Try field first
            var fi = type.GetField(memberName, flags);
            if (fi != null)
            {
                try
                {
                    return fi.GetValue(target);
                }
                catch { }
            }
            
            // Try property
            var pi = type.GetProperty(memberName, flags);
            if (pi != null && pi.CanRead)
            {
                try
                {
                    return pi.GetValue(target);
                }
                catch { }
            }
            
            return null;
        }

        /// <summary>
        /// INTERNAL: Attempts to get a static field or property value from a type using reflection.
        /// Tries field first, then property. Handles both public and non-public members.
        /// Fields on Mono are typically properties on IL2CPP.
        /// </summary>
        /// <param name="type">The type to get the static member from.</param>
        /// <param name="memberName">The name of the field or property.</param>
        /// <returns>The value of the member, or <c>null</c> if not found or inaccessible.</returns>
        internal static object TryGetStaticFieldOrProperty(Type type, string memberName)
        {
            if (type == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            
            // Try field first
            var fi = type.GetField(memberName, flags);
            if (fi != null)
            {
                try
                {
                    return fi.GetValue(null);
                }
                catch { }
            }
            
            // Try property
            var pi = type.GetProperty(memberName, flags);
            if (pi != null && pi.CanRead)
            {
                try
                {
                    return pi.GetValue(null);
                }
                catch { }
            }
            
            return null;
        }

        /// <summary>
        /// INTERNAL: Attempts to set a static field or property value on a type using reflection.
        /// Tries field first, then property. Handles both public and non-public members.
        /// Fields on Mono are typically properties on IL2CPP.
        /// </summary>
        /// <param name="type">The type to set the static member on.</param>
        /// <param name="memberName">The name of the field or property.</param>
        /// <param name="value">The value to set.</param>
        /// <returns><c>true</c> if the member was successfully set; otherwise, <c>false</c>.</returns>
        internal static bool TrySetStaticFieldOrProperty(Type type, string memberName, object value)
        {
            if (type == null) return false;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            
            // Try field first
            var fi = type.GetField(memberName, flags);
            if (fi != null)
            {
                try
                {
                    if (value == null || fi.FieldType.IsInstanceOfType(value))
                    {
                        fi.SetValue(null, value);
                        return true;
                    }
                }
                catch { }
            }
            
            // Try property
            var pi = type.GetProperty(memberName, flags);
            if (pi != null && pi.CanWrite)
            {
                try
                {
                    if (value == null || pi.PropertyType.IsInstanceOfType(value))
                    {
                        pi.SetValue(null, value);
                        return true;
                    }
                }
                catch { }
            }
            
            return false;
        }
    }
}