// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
namespace Xunit.Helpers {
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Permissions;

    /// <summary>
    /// Represents a partial trust sandbox
    /// </summary>
    public class PartialTrustSandbox : IDisposable {
        private static readonly PartialTrustSandbox _default = new PartialTrustSandbox("Default Partial Trust Sandbox");
        private AppDomain _domain;

        /// <summary>
        /// Constructs a new partial trust sandbox
        /// </summary>
        /// <param name="grantReflectionPermission">Specify true to grant unrestricted reflection permission</param>
        /// <param name="configurationFile">Specify an alternate configuration file for the AppDoman. By default, the calling domain's will be used</param>
        /// <remarks>
        /// If you do not need any special configuration, use the <see cref="Default"/> instance.
        /// </remarks>
        public PartialTrustSandbox(bool grantReflectionPermission = false, string configurationFile = null)
            : this("Partial Trust Sandbox " + Guid.NewGuid(), grantReflectionPermission, configurationFile) {
        }

        protected PartialTrustSandbox(string domainName, bool grantReflectionPermission = false, string configurationFile = null) {
            var securityConfig = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "CONFIG",
                                              "web_mediumtrust.config");
            var permissionXml = File.ReadAllText(securityConfig).Replace("$AppDir$", Environment.CurrentDirectory);

            // ASP.NET's configuration files still use the full policy levels rather than just permission sets,
            // so we can either write a lot of code to parse them ourselves, or we can use a deprecated API to
            // load them.
#pragma warning disable 0618
            var grantSet =
                SecurityManager.LoadPolicyLevelFromString(permissionXml, PolicyLevelType.AppDomain).
                    GetNamedPermissionSet("ASP.Net");
#pragma warning restore 0618

            if (grantReflectionPermission) {
                grantSet.AddPermission(new ReflectionPermission(PermissionState.Unrestricted));
            }

            var info = new AppDomainSetup {
                ApplicationBase = Environment.CurrentDirectory,
                PartialTrustVisibleAssemblies = new string[]
                                                           {
                                                               // Add conditional APTCA assemblies that you need to access in partial trust here.
                                                               // Do NOT add System.Web here since at least one test relies on it not being treated as conditionally APTCA.
                                                               "System.ComponentModel.DataAnnotations, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9"
                                                           }
            };

            info.ConfigurationFile = configurationFile == null
                ? AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
                : configurationFile;

            _domain = AppDomain.CreateDomain(domainName, null, info, grantSet, null);
        }

        ~PartialTrustSandbox() {
            Dispose(false);
        }

        public static PartialTrustSandbox Default {
            get { return _default; }
        }

        /// <summary>
        /// Creates a new instance of the specified type in the partial trust sandbox and returns a proxy to it.
        /// </summary>
        /// <typeparam name="T">The type of object to create</typeparam>
        /// <returns>A proxy to the instance created in the partial trust sandbox</returns>
        public T CreateInstance<T>() {
            return (T)CreateInstance(typeof(T));
        }

        /// <summary>
        /// Creates a new instance of the specified type in the partial trust sandbox and returns a proxy to it.
        /// </summary>
        /// <param name="type">The type of object to create</param>
        /// <returns>A proxy to the instance created in the partial trust sandbox</returns>
        public object CreateInstance(Type type) {
            HandleDisposed();

            return _domain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing
                && _domain != null) {
                AppDomain.Unload(_domain);
                _domain = null;
            }
        }

        private void HandleDisposed() {
            if (_domain == null) {
                throw new ObjectDisposedException(null);
            }
        }
    }
}