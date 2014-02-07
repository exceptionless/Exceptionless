using System;
using System.Reflection;
using System.Reflection.Emit;

namespace CodeSmith.Core.Reflection
{
    /// <summary>
    /// Delegate for a dynamic constructor method.
    /// </summary>
    public delegate object DynamicConstructor();

    /// <summary>
    /// Delegate for a dynamic method.
    /// </summary>
    /// <param name="target">
    /// Object containg method to invoke.
    /// </param>
    /// <param name="args">
    /// Parameters passed to method.
    /// </param>
    public delegate object DynamicMemberMethod(object target, object[] args);

    /// <summary>
    /// Delegate for getting a value.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <returns>The value from the target.</returns>
    public delegate object DynamicMemberGetter(object target);

    /// <summary>
    /// Delegate for setting a value.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <param name="arg">Argument value.</param>
    public delegate void DynamicMemberSetter(object target, object arg);

    internal static class DynamicMethodHandlerFactory
    {
        public static DynamicConstructor CreateConstructor(ConstructorInfo constructor)
        {
            if (constructor == null)
                throw new ArgumentNullException("constructor");
            if (constructor.GetParameters().Length > 0)
                throw new NotSupportedException("Constructor with parameters are not supported.");

            var dm = new System.Reflection.Emit.DynamicMethod(
                "ctor",
                MethodAttributes.Static | MethodAttributes.Public,
                CallingConventions.Standard,
                constructor.DeclaringType,
                Type.EmptyTypes,
                (Type)null,
                true);

            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);

            return (DynamicConstructor) dm.CreateDelegate(typeof (DynamicConstructor));
        }

        public static DynamicMemberMethod CreateMethod(MethodInfo method)
        {
            ParameterInfo[] pi = method.GetParameters();

            var dm = new System.Reflection.Emit.DynamicMethod("dm", typeof (object),
                                       new[] {typeof (object), typeof (object[])},
                                       typeof (DynamicMethodHandlerFactory), true);

            ILGenerator il = dm.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);

            for (int index = 0; index < pi.Length; index++)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, index);

                Type parameterType = pi[index].ParameterType;
                if (parameterType.IsByRef)
                {
                    parameterType = parameterType.GetElementType();
                    if (parameterType.IsValueType)
                    {
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Unbox, parameterType);
                    }
                    else
                        il.Emit(OpCodes.Ldelema, parameterType);
                }
                else
                {
                    il.Emit(OpCodes.Ldelem_Ref);

                    if (parameterType.IsValueType)
                    {
                        il.Emit(OpCodes.Unbox, parameterType);
                        il.Emit(OpCodes.Ldobj, parameterType);
                    }
                }
            }

            if ((method.IsAbstract || method.IsVirtual)
                && !method.IsFinal && !method.DeclaringType.IsSealed)
                il.Emit(OpCodes.Callvirt, method);
            else
                il.Emit(OpCodes.Call, method);

            if (method.ReturnType == typeof (void))
                il.Emit(OpCodes.Ldnull);
            else if (method.ReturnType.IsValueType)
                il.Emit(OpCodes.Box, method.ReturnType);
            il.Emit(OpCodes.Ret);

            return (DynamicMemberMethod) dm.CreateDelegate(typeof (DynamicMemberMethod));
        }

        public static DynamicMemberGetter CreatePropertyGetter(PropertyInfo property)
        {
            if (property == null)
                throw new ArgumentNullException("property");

            if (!property.CanRead)
                return null;

            MethodInfo getMethod = property.GetGetMethod();
            if (getMethod == null) //maybe is private
                getMethod = property.GetGetMethod(true);

            var dm = new System.Reflection.Emit.DynamicMethod("propg", typeof (object),
                                       new[] {typeof (object)},
                                       property.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!getMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Callvirt, getMethod, null);
            }
            else
                il.EmitCall(OpCodes.Call, getMethod, null);

            if (property.PropertyType.IsValueType)
                il.Emit(OpCodes.Box, property.PropertyType);

            il.Emit(OpCodes.Ret);

            return (DynamicMemberGetter) dm.CreateDelegate(typeof (DynamicMemberGetter));
        }

        public static DynamicMemberSetter CreatePropertySetter(PropertyInfo property)
        {
            if (property == null)
                throw new ArgumentNullException("property");

            if (!property.CanWrite)
                return null;

            MethodInfo setMethod = property.GetSetMethod();
            if (setMethod == null) //maybe is private
                setMethod = property.GetSetMethod(true);

            var dm = new System.Reflection.Emit.DynamicMethod("props", null,
                                       new[] {typeof (object), typeof (object)},
                                       property.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!setMethod.IsStatic)
                il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            EmitCastToReference(il, property.PropertyType);
            if (!setMethod.IsStatic && !property.DeclaringType.IsValueType)
                il.EmitCall(OpCodes.Callvirt, setMethod, null);
            else
                il.EmitCall(OpCodes.Call, setMethod, null);

            il.Emit(OpCodes.Ret);

            return (DynamicMemberSetter) dm.CreateDelegate(typeof (DynamicMemberSetter));
        }

        public static DynamicMemberGetter CreateFieldGetter(FieldInfo field)
        {
            if (field == null)
                throw new ArgumentNullException("field");

            var dm = new System.Reflection.Emit.DynamicMethod("fldg", typeof (object),
                                       new[] {typeof (object)},
                                       field.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!field.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);

                EmitCastToReference(il, field.DeclaringType); //to handle struct object

                il.Emit(OpCodes.Ldfld, field);
            }
            else
                il.Emit(OpCodes.Ldsfld, field);

            if (field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);

            return (DynamicMemberGetter) dm.CreateDelegate(typeof (DynamicMemberGetter));
        }

        public static DynamicMemberSetter CreateFieldSetter(FieldInfo field)
        {
            if (field == null)
                throw new ArgumentNullException("field");

            var dm = new System.Reflection.Emit.DynamicMethod("flds", null,
                                       new[] {typeof (object), typeof (object)},
                                       field.DeclaringType, true);

            ILGenerator il = dm.GetILGenerator();

            if (!field.IsStatic)
                il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            EmitCastToReference(il, field.FieldType);

            if (!field.IsStatic)
                il.Emit(OpCodes.Stfld, field);
            else
                il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);

            return (DynamicMemberSetter) dm.CreateDelegate(typeof (DynamicMemberSetter));
        }

        private static void EmitCastToReference(ILGenerator il, Type type)
        {
            if (type.IsValueType)
                il.Emit(OpCodes.Unbox_Any, type);
            else
                il.Emit(OpCodes.Castclass, type);
        }
    }
}