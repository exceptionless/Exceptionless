using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Exceptionless.Core.Extensions {
    internal static class ILGeneratorExtensions {
        public static void PushInstance(this ILGenerator generator, Type type) {
            generator.Emit(OpCodes.Ldarg_0);
            if (type.IsValueType)
                generator.Emit(OpCodes.Unbox, type);
            else
                generator.Emit(OpCodes.Castclass, type);
        }

        public static void BoxIfNeeded(this ILGenerator generator, Type type) {
            if (type.IsValueType)
                generator.Emit(OpCodes.Box, type);
            else
                generator.Emit(OpCodes.Castclass, type);
        }

        public static void UnboxIfNeeded(this ILGenerator generator, Type type) {
            if (type.IsValueType)
                generator.Emit(OpCodes.Unbox_Any, type);
            else
                generator.Emit(OpCodes.Castclass, type);
        }

        public static void CallMethod(this ILGenerator generator, MethodInfo methodInfo) {
            if (methodInfo.IsFinal || !methodInfo.IsVirtual)
                generator.Emit(OpCodes.Call, methodInfo);
            else
                generator.Emit(OpCodes.Callvirt, methodInfo);
        }

        public static void Return(this ILGenerator generator) {
            generator.Emit(OpCodes.Ret);
        }
    }
}