using System;
using Nest;

namespace Exceptionless.Core.Extensions {
    public static class ElasticExtensions {
        public static TextPropertyDescriptor<T> Alias<T>(this TextPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static KeywordPropertyDescriptor<T> Alias<T>(this KeywordPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static NumberPropertyDescriptor<T> Alias<T>(this NumberPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static DatePropertyDescriptor<T> Alias<T>(this DatePropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static BooleanPropertyDescriptor<T> Alias<T>(this BooleanPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static BinaryPropertyDescriptor<T> Alias<T>(this BinaryPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static AttachmentPropertyDescriptor<T> Alias<T>(this AttachmentPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static ObjectTypeDescriptor<T, TChild> Alias<T, TChild>(this ObjectTypeDescriptor<T, TChild> descriptor, string alias) where TChild : class where T : class {
            return descriptor;
        }

        public static NestedPropertyDescriptor<T, TChild> Alias<T, TChild>(this NestedPropertyDescriptor<T, TChild> descriptor, string alias) where TChild : class where T : class {
            return descriptor;
        }

        public static IpPropertyDescriptor<T> Alias<T>(this IpPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static GeoPointPropertyDescriptor<T> Alias<T>(this GeoPointPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static GeoShapePropertyDescriptor<T> Alias<T>(this GeoShapePropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static CompletionPropertyDescriptor<T> Alias<T>(this CompletionPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static Murmur3HashPropertyDescriptor<T> Alias<T>(this Murmur3HashPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static PercolatorPropertyDescriptor<T> Alias<T>(this PercolatorPropertyDescriptor<T> descriptor, string alias) where T : class {
            return descriptor;
        }

        public static IProperty Alias(this IProperty property, string alias) {
            return property;
        }
    }
}