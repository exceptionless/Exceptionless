﻿using System.Collections.Concurrent;
using System.Reflection;

namespace Exceptionless.Core.Reflection;

/// <summary>
/// A class for late bound operations on a type.
/// </summary>
public static class LateBinder
{
    private static readonly ConcurrentDictionary<Type, TypeAccessor> _accessorCache = new();

    /// <summary>
    /// Searches for the public property with the specified name.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to search for the property in.</param>
    /// <param name="name">The name of the property to find.</param>
    /// <returns>
    /// An <see cref="IMemberAccessor"/> instance for the property if found; otherwise <c>null</c>.
    /// </returns>
    public static IMemberAccessor? FindProperty(Type type, string name)
    {
        return FindProperty(type, name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
    }

    /// <summary>
    /// Searches for the specified property, using the specified binding constraints.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to search for the property in.</param>
    /// <param name="name">The name of the property to find.</param>
    /// <param name="flags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <returns>
    /// An <see cref="IMemberAccessor"/> instance for the property if found; otherwise <c>null</c>.
    /// </returns>
    public static IMemberAccessor? FindProperty(Type type, string name, BindingFlags flags)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));
        if (String.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var currentType = type;
        TypeAccessor typeAccessor;
        IMemberAccessor? memberAccessor = null;

        // support nested property
        string[] parts = name.Split('.');
        foreach (string part in parts)
        {
            if (memberAccessor is not null)
                currentType = memberAccessor.MemberType;

            typeAccessor = GetAccessor(currentType);
            memberAccessor = typeAccessor.FindProperty(part, flags);
        }

        return memberAccessor;
    }

    /// <summary>
    /// Searches for the specified property, using the specified binding constraints.
    /// </summary>
    /// <param name="property">The property to create an accessor for.</param>
    /// <returns>
    /// An <see cref="IMemberAccessor"/> instance for the property.
    /// </returns>
    public static IMemberAccessor GetPropertyAccessor(PropertyInfo property)
    {
        if (property is null)
            throw new ArgumentNullException(nameof(property));

        return new PropertyAccessor(property);
    }

    /// <summary>
    /// Searches for the specified property, using the specified binding constraints.
    /// </summary>
    /// <param name="field">The field to create an accessor for.</param>
    /// <returns>
    /// An <see cref="IMemberAccessor"/> instance for the property.
    /// </returns>
    public static IMemberAccessor GetFieldAccessor(FieldInfo field)
    {
        if (field is null)
            throw new ArgumentNullException(nameof(field));

        return new FieldAccessor(field);
    }


    /// <summary>
    /// Searches for the field with the specified name.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to search for the field in.</param>
    /// <param name="name">The name of the field to find.</param>
    /// <returns>
    /// An <see cref="IMemberAccessor"/> instance for the field if found; otherwise <c>null</c>.
    /// </returns>
    public static IMemberAccessor? FindField(Type type, string name)
    {
        return FindField(type, name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    /// <summary>
    /// Searches for the field, using the specified binding constraints.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to search for the field in.</param>
    /// <param name="name">The name of the field to find.</param>
    /// <param name="flags">A bitmask comprised of one or more <see cref="BindingFlags"/> that specify how the search is conducted.</param>
    /// <returns>
    /// An <see cref="IMemberAccessor"/> instance for the field if found; otherwise <c>null</c>.
    /// </returns>
    public static IMemberAccessor? FindField(Type type, string name, BindingFlags flags)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));
        if (String.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var typeAccessor = GetAccessor(type);
        var memberAccessor = typeAccessor.FindField(name, flags);

        return memberAccessor;
    }

    /// <summary>
    /// Sets the property value with the specified name.
    /// </summary>
    /// <param name="target">The object whose property value will be set.</param>
    /// <param name="name">The name of the property to set.</param>
    /// <param name="value">The new value to be set.</param>
    public static void SetProperty(object target, string name, object value)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));
        if (String.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var rootType = target.GetType();
        var currentType = rootType;
        object currentTarget = target;

        TypeAccessor typeAccessor;
        IMemberAccessor? memberAccessor = null;

        // support nested property
        string[] parts = name.Split('.');
        foreach (string part in parts)
        {
            if (memberAccessor is not null)
            {
                currentTarget = memberAccessor.GetValue(currentTarget) ?? throw new InvalidOperationException();
                currentType = memberAccessor.MemberType;
            }

            typeAccessor = GetAccessor(currentType);
            memberAccessor = typeAccessor.FindProperty(part);
        }

        if (memberAccessor is null)
            throw new InvalidOperationException($"Could not find property '{name}' in type '{rootType.Name}'.");

        memberAccessor.SetValue(currentTarget, value);
    }

    /// <summary>
    /// Sets the field value with the specified name.
    /// </summary>
    /// <param name="target">The object whose field value will be set.</param>
    /// <param name="name">The name of the field to set.</param>
    /// <param name="value">The new value to be set.</param>
    public static void SetField(object target, string name, object value)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));
        if (String.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var rootType = target.GetType();
        var memberAccessor = FindField(rootType, name);

        if (memberAccessor is null)
            throw new InvalidOperationException($"Could not find field '{name}' in type '{rootType.Name}'.");

        memberAccessor.SetValue(target, value);
    }

    /// <summary>
    /// Returns the value of the property with the specified name.
    /// </summary>
    /// <param name="target">The object whose property value will be returned.</param>
    /// <param name="name">The name of the property to read.</param>
    /// <returns>The value of the property.</returns>
    public static object? GetProperty(object target, string name)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));
        if (String.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var rootType = target.GetType();
        var currentType = rootType;
        object currentTarget = target;

        TypeAccessor typeAccessor;
        IMemberAccessor? memberAccessor = null;

        // support nested property
        string[] parts = name.Split('.');
        foreach (string part in parts)
        {
            if (memberAccessor is not null)
            {
                currentTarget = memberAccessor.GetValue(currentTarget) ?? throw new InvalidOperationException();
                currentType = memberAccessor.MemberType;
            }

            typeAccessor = GetAccessor(currentType);
            memberAccessor = typeAccessor.FindProperty(part);
        }

        if (memberAccessor is null)
            throw new InvalidOperationException($"Could not find property '{name}' in type '{rootType.Name}'.");

        return memberAccessor.GetValue(currentTarget);
    }

    /// <summary>
    /// Returns the value of the field with the specified name.
    /// </summary>
    /// <param name="target">The object whose field value will be returned.</param>
    /// <param name="name">The name of the field to read.</param>
    /// <returns>The value of the field.</returns>
    public static object? GetField(object target, string name)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));
        if (String.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var rootType = target.GetType();
        var memberAccessor = FindField(rootType, name);
        if (memberAccessor is null)
            throw new InvalidOperationException($"Could not find field '{name}' in type '{rootType.Name}'.");

        return memberAccessor.GetValue(target);
    }

    /// <summary>
    /// Creates an instance of the specified type.
    /// </summary>
    /// <param name="type">The type to create.</param>
    /// <returns>A new instance of the specified type.</returns>
    public static object CreateInstance(Type type)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));

        var typeAccessor = GetAccessor(type);
        if (typeAccessor is null)
            throw new InvalidOperationException($"Could not find constructor for {type.Name}.");

        return typeAccessor.Create();
    }

    private static TypeAccessor GetAccessor(Type type)
    {
        return _accessorCache.GetOrAdd(type, t => new TypeAccessor(t));
    }
}
