﻿using System.Reflection;
using System.Reflection.Emit;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Reflection;

public delegate object LateBoundMethod(object target, params object[] arguments);
public delegate object? LateBoundGet(object target);
public delegate void LateBoundSet(object target, object? value);
public delegate object LateBoundConstructor();

public static class DelegateFactory
{
    private static DynamicMethod CreateDynamicMethod(string name, Type returnType, Type[] parameterTypes, Type owner)
    {
        var dynamicMethod = !owner.IsInterface
          ? new DynamicMethod(name, returnType, parameterTypes, owner, true)
          : new DynamicMethod(name, returnType, parameterTypes, owner.Assembly.ManifestModule, true);

        return dynamicMethod;
    }

    public static LateBoundMethod CreateMethod(MethodBase method)
    {
        var dynamicMethod = CreateDynamicMethod(method.ToString() ?? throw new InvalidOperationException(),
            typeof(object), new[] { typeof(object), typeof(object[]) },
            method.DeclaringType ?? throw new InvalidOperationException());
        var generator = dynamicMethod.GetILGenerator();

        var args = method.GetParameters();

        var argsOk = generator.DefineLabel();

        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Ldlen);
        generator.Emit(OpCodes.Ldc_I4, args.Length);
        generator.Emit(OpCodes.Beq, argsOk);

        generator.Emit(OpCodes.Newobj, typeof(TargetParameterCountException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
        generator.Emit(OpCodes.Throw);

        generator.MarkLabel(argsOk);

        if (!method.IsConstructor && !method.IsStatic)
            generator.PushInstance(method.DeclaringType);

        for (int i = 0; i < args.Length; i++)
        {
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldc_I4, i);
            generator.Emit(OpCodes.Ldelem_Ref);

            generator.UnboxIfNeeded(args[i].ParameterType);
        }

        if (method.IsConstructor)
            generator.Emit(OpCodes.Newobj, (ConstructorInfo)method);
        else if (method.IsFinal || !method.IsVirtual)
            generator.CallMethod((MethodInfo)method);

        var returnType = method.IsConstructor
          ? method.DeclaringType
          : ((MethodInfo)method).ReturnType;

        if (returnType != typeof(void))
            generator.BoxIfNeeded(returnType);
        else
            generator.Emit(OpCodes.Ldnull);

        generator.Return();

        return (LateBoundMethod)dynamicMethod.CreateDelegate(typeof(LateBoundMethod));
    }

    public static LateBoundConstructor CreateConstructor(Type type)
    {
        var dynamicMethod = CreateDynamicMethod("Create" + type.FullName, typeof(object), Type.EmptyTypes, type);
        dynamicMethod.InitLocals = true;
        var generator = dynamicMethod.GetILGenerator();

        if (type.IsValueType)
        {
            generator.DeclareLocal(type);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Box, type);
        }
        else
        {
            var constructorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (constructorInfo is null)
                throw new InvalidOperationException($"Could not get constructor for {type}.");

            generator.Emit(OpCodes.Newobj, constructorInfo);
        }

        generator.Return();

        return (LateBoundConstructor)dynamicMethod.CreateDelegate(typeof(LateBoundConstructor));
    }

    public static LateBoundGet CreateGet(PropertyInfo propertyInfo)
    {
        var getMethod = propertyInfo.GetGetMethod(true);
        if (getMethod is null)
            throw new InvalidOperationException($"Property '{propertyInfo.Name}' does not have a getter.");

        var dynamicMethod = CreateDynamicMethod("Get" + propertyInfo.Name, typeof(object), new[] { typeof(object) }, propertyInfo.DeclaringType ?? throw new InvalidOperationException());
        var generator = dynamicMethod.GetILGenerator();

        if (!getMethod.IsStatic)
            generator.PushInstance(propertyInfo.DeclaringType);

        generator.CallMethod(getMethod);
        generator.BoxIfNeeded(propertyInfo.PropertyType);
        generator.Return();

        return (LateBoundGet)dynamicMethod.CreateDelegate(typeof(LateBoundGet));
    }

    public static LateBoundGet CreateGet(FieldInfo fieldInfo)
    {
        var dynamicMethod = CreateDynamicMethod("Get" + fieldInfo.Name, typeof(object), new[] { typeof(object) }, fieldInfo.DeclaringType ?? throw new InvalidOperationException());

        var generator = dynamicMethod.GetILGenerator();

        if (!fieldInfo.IsStatic)
            generator.PushInstance(fieldInfo.DeclaringType);

        generator.Emit(OpCodes.Ldfld, fieldInfo);
        generator.BoxIfNeeded(fieldInfo.FieldType);
        generator.Return();

        return (LateBoundGet)dynamicMethod.CreateDelegate(typeof(LateBoundGet));
    }

    public static LateBoundSet CreateSet(PropertyInfo propertyInfo)
    {
        var setMethod = propertyInfo.GetSetMethod(true);
        if (setMethod is null)
            throw new InvalidOperationException($"Property '{propertyInfo.Name}' does not have a setter.");

        var dynamicMethod = CreateDynamicMethod("Set" + propertyInfo.Name, null!, new[] { typeof(object), typeof(object) }, propertyInfo.DeclaringType ?? throw new InvalidOperationException());
        var generator = dynamicMethod.GetILGenerator();

        if (!setMethod.IsStatic)
            generator.PushInstance(propertyInfo.DeclaringType);

        generator.Emit(OpCodes.Ldarg_1);
        generator.UnboxIfNeeded(propertyInfo.PropertyType);
        generator.CallMethod(setMethod);
        generator.Return();

        return (LateBoundSet)dynamicMethod.CreateDelegate(typeof(LateBoundSet));
    }

    public static LateBoundSet CreateSet(FieldInfo fieldInfo)
    {
        var dynamicMethod = CreateDynamicMethod("Set" + fieldInfo.Name, null!, new[] { typeof(object), typeof(object) }, fieldInfo.DeclaringType ?? throw new InvalidOperationException());
        var generator = dynamicMethod.GetILGenerator();

        if (!fieldInfo.IsStatic)
            generator.PushInstance(fieldInfo.DeclaringType);

        generator.Emit(OpCodes.Ldarg_1);
        generator.UnboxIfNeeded(fieldInfo.FieldType);
        generator.Emit(OpCodes.Stfld, fieldInfo);
        generator.Return();

        return (LateBoundSet)dynamicMethod.CreateDelegate(typeof(LateBoundSet));
    }
}
