using System;
using System.Reflection;

namespace CodeSmith.Core.Reflection
{
    internal class DynamicMemberHandle
    {
        public DynamicMemberHandle(string memberName, Type memberType, DynamicMemberGetter dynamicMemberGet, DynamicMemberSetter dynamicMemberSet)
        {
            MemberName = memberName;
            MemberType = memberType;
            DynamicMemberGet = dynamicMemberGet;
            DynamicMemberSet = dynamicMemberSet;
        }

        public DynamicMemberHandle(PropertyInfo info)
            : this(info.Name, info.PropertyType, DynamicMethodHandlerFactory.CreatePropertyGetter(info), DynamicMethodHandlerFactory.CreatePropertySetter(info))
        {}

        public DynamicMemberHandle(FieldInfo info)
            : this(info.Name, info.FieldType, DynamicMethodHandlerFactory.CreateFieldGetter(info), DynamicMethodHandlerFactory.CreateFieldSetter(info))
        {}

        public string MemberName { get; private set; }
        public Type MemberType { get; private set; }
        public DynamicMemberGetter DynamicMemberGet { get; private set; }
        public DynamicMemberSetter DynamicMemberSet { get; private set; }
    }
}