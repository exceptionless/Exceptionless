#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Module = Exceptionless.Models.Data.Module;

namespace Exceptionless.Extensions {
    internal static class ToSimpleErrorModelExtensions {
        private static readonly Dictionary<string, Module> _moduleCache = new Dictionary<string, Module>();
        private static readonly string[] _exceptionExclusions = { "HelpLink", "InnerException", "Message", "Source", "StackTrace", "TargetSite", "HResult" };

        public static SimpleError ToSimpleErrorModel(this Exception exception) {
            Type type = exception.GetType();

            var error = new SimpleError {
                Message = exception.GetMessage(),
                Modules = GetLoadedModules(),
                Type = type.FullName,
                StackTrace = exception.StackTrace
            };

            try {
                PropertyInfo info = type.GetProperty("HResult", BindingFlags.NonPublic | BindingFlags.Instance);
                if (info != null)
                    error.Code = info.GetValue(exception, null).ToString();
            } catch (Exception) {}

            // TODO: Test adding non-serializable objects to ExtendedData and see what happens
            try {
                Dictionary<string, object> extraProperties = type.GetPublicProperties().Where(p => !_exceptionExclusions.Contains(p.Name)).ToDictionary(p => p.Name, p => {
                    try {
                        return p.GetValue(exception, null);
                    } catch {}
                    return null;
                });

                extraProperties = extraProperties.Where(kvp => !ValueIsEmpty(kvp.Value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (extraProperties.Count > 0 && !error.Data.ContainsKey(SimpleError.KnownDataKeys.ExtraProperties)) {
                    error.AddObject(new ExtendedDataInfo {
                        Data = extraProperties,
                        Name = SimpleError.KnownDataKeys.ExtraProperties,
                        IgnoreSerializationErrors = true,
                        MaxDepthToSerialize = 5
                    });
                }
            } catch {}

            if (exception.InnerException != null)
                error.Inner = exception.InnerException.ToSimpleErrorModel();

            return error;
        }

        private static bool ValueIsEmpty(object value) {
            if (value == null)
                return true;

            if (value is IEnumerable) {
                if (!(value as IEnumerable).Cast<Object>().Any())
                    return true;
            }

            return false;
        }

        private static readonly List<string> _msPublicKeyTokens = new List<string> {
            "b77a5c561934e089",
            "b03f5f7f11d50a3a",
            "31bf3856ad364e35"
        };

        private static string GetMessage(this Exception exception) {
            string defaultMessage = String.Format("Exception of type '{0}' was thrown.", exception.GetType().FullName);
            string message = !String.IsNullOrEmpty(exception.Message) ? String.Join(" ", exception.Message.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim() : null;

            return !String.IsNullOrEmpty(message) ? message : defaultMessage;
        }

        private static ModuleCollection GetLoadedModules(bool includeSystem = false, bool includeDynamic = false) {
            var modules = new ModuleCollection();
            var assemblies = new List<Assembly> { Assembly.GetCallingAssembly() };
            if (assemblies[0] != Assembly.GetExecutingAssembly())
                assemblies.Add(Assembly.GetExecutingAssembly());

            int id = 1;
            foreach (Assembly assembly in assemblies) {
                if (!includeDynamic && assembly.IsDynamic)
                    continue;

                if (!includeSystem) {
                    try {
                        string publicKeyToken = assembly.GetAssemblyName().GetPublicKeyToken().ToHex();
                        if (_msPublicKeyTokens.Contains(publicKeyToken))
                            continue;

                        object[] attrs = assembly.GetCustomAttributes(typeof(GeneratedCodeAttribute), true);
                        if (attrs.Length > 0)
                            continue;
                    } catch {}
                }

                var module = assembly.ToModuleInfo();
                module.ModuleId = id++;
                modules.Add(module);
            }

            return modules;
        }

        private static Module ToModuleInfo(this Assembly assembly) {
            if (assembly == null)
                return null;

            Module module;
            if (!_moduleCache.TryGetValue(assembly.FullName, out module)) {
                module = new Module();
                AssemblyName name = assembly.GetAssemblyName();
                if (name != null) {
                    module.Name = name.Name;
                    module.Version = name.Version.ToString();
                    byte[] pkt = name.GetPublicKeyToken();
                    if (pkt.Length > 0)
                        module.Data["PublicKeyToken"] = pkt.ToHex();
                }

                string infoVersion = assembly.GetInformationalVersion();
                if (!String.IsNullOrEmpty(infoVersion) && infoVersion != module.Version)
                    module.Data["ProductVersion"] = infoVersion;

                string fileVersion = assembly.GetFileVersion();
                if (!String.IsNullOrEmpty(fileVersion) && fileVersion != module.Version)
                    module.Data["FileVersion"] = fileVersion;

                //DateTime? creationTime = assembly.GetCreationTime();
                //if (creationTime.HasValue)
                //    module.CreatedDate = creationTime.Value;

                //DateTime? lastWriteTime = assembly.GetLastWriteTime();
                //if (lastWriteTime.HasValue)
                //    module.ModifiedDate = lastWriteTime.Value;
            }

            if (module != null) {
                if (assembly == Assembly.GetCallingAssembly())
                    module.IsEntry = true;
            }

            return module;
        }
    }
}