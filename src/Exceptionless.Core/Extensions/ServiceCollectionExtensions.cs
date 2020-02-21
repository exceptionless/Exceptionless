using System;
using System.Collections.Generic;
using System.Reflection;
using Exceptionless.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Exceptionless.Core.Extensions {
    public static class ServiceCollectionExtensions {
        public static IServiceCollection AddScoped(this IServiceCollection services, Type type, params Assembly[] assemblies) {
            return Add(services, type, ServiceLifetime.Scoped, assemblies);
        }

        public static IServiceCollection AddSingleton(this IServiceCollection services, Type type, params Assembly[] assemblies) {
            return Add(services, type, ServiceLifetime.Singleton, assemblies);
        }

        public static IServiceCollection AddTransient(this IServiceCollection services, Type type, params Assembly[] assemblies) {
            return Add(services, type, ServiceLifetime.Transient, assemblies);
        }

        public static IServiceCollection Add(this IServiceCollection services, Type type, ServiceLifetime lifetime, params Assembly[] assemblies) {
            var implementingTypes = new List<Type>();
            implementingTypes.AddRange(type.IsGenericTypeDefinition
                ? TypeHelper.GetAllTypesImplementingOpenGenericType(type, assemblies)
                : TypeHelper.GetDerivedTypes(type, assemblies));

            foreach (var implementingType in implementingTypes) {
                var registrationType = type;
                if (type.IsGenericTypeDefinition) {
                    if (type.IsInterface)
                        registrationType = type.MakeGenericType(implementingType.GetInterface(type.Name).GenericTypeArguments);
                    else
                        registrationType = type.MakeGenericType(implementingType.BaseType.GenericTypeArguments);
                }

                services.Add(new ServiceDescriptor(registrationType, implementingType, lifetime));
                services.Add(new ServiceDescriptor(implementingType, implementingType, lifetime));
            }

            return services;
        }

        public static IServiceCollection AddSingleton<T>(this IServiceCollection services, params Assembly[] assemblies) {
            return AddSingleton(services, typeof(T), assemblies);
        }

        public static IServiceCollection ReplaceSingleton<T>(this IServiceCollection services, T instance) {
            return services.Replace(new ServiceDescriptor(typeof(T), s => instance, ServiceLifetime.Singleton));
        }

        public static IServiceCollection ReplaceSingleton<T>(this IServiceCollection services, object instance) {
            return services.Replace(new ServiceDescriptor(typeof(T), s => instance, ServiceLifetime.Singleton));
        }

        public static IServiceCollection ReplaceSingleton<T>(this IServiceCollection services, Func<IServiceProvider, object> factory) {
            return services.Replace(new ServiceDescriptor(typeof(T), factory, ServiceLifetime.Singleton));
        }

        public static IServiceCollection ReplaceSingleton<T>(this IServiceCollection services, Func<IServiceProvider, T> factory) {
            return services.Replace(new ServiceDescriptor(typeof(T), s => factory(s), ServiceLifetime.Singleton));
        }

        public static IServiceCollection ReplaceSingleton<TService, TInstance>(this IServiceCollection services) {
            return services.Replace(new ServiceDescriptor(typeof(TService), typeof(TInstance), ServiceLifetime.Singleton));
        }
    }
}
