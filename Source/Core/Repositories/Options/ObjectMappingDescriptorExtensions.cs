using Nest;

namespace Exceptionless.Core.Repositories {
    public static class ObjectMappingDescriptorExtensions {
        public static ObjectMappingDescriptor<TParent, TChild> RootPath<TParent, TChild>(this ObjectMappingDescriptor<TParent, TChild> t) where TParent : class where TChild : class {
            return t.Path("just_name");
        }
    }
}