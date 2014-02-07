using System;
using System.Reflection;

namespace CodeSmith.Core.Reflection
{
    internal class DynamicMethodHandle
    {
        public DynamicMethodHandle(MethodInfo info, params object[] parameters)
        {
            if (info == null)
                DynamicMethod = null;
            else
            {
                MethodName = info.Name;
                ParameterInfo[] infoParams = info.GetParameters();
                object[] inParams = null;
                
                if (parameters == null)
                    inParams = new object[] {null};
                else
                    inParams = parameters;

                int pCount = infoParams.Length;
                if (pCount > 0 &&
                    ((pCount == 1 && infoParams[0].ParameterType.IsArray) ||
                     (infoParams[pCount - 1].GetCustomAttributes(typeof (ParamArrayAttribute), true).Length > 0)))
                {
                    HasFinalArrayParam = true;
                    MethodParamsLength = pCount;
                    FinalArrayElementType = infoParams[pCount - 1].ParameterType;
                }
                DynamicMethod = DynamicMethodHandlerFactory.CreateMethod(info);
            }
        }

        public string MethodName { get; private set; }
        public DynamicMemberMethod DynamicMethod { get; private set; }
        public bool HasFinalArrayParam { get; private set; }
        public int MethodParamsLength { get; private set; }
        public Type FinalArrayElementType { get; private set; }
    }
}