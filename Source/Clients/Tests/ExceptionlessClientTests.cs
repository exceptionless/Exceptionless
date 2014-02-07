#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Queue;
using Xunit;
using Xunit.Helpers;

namespace Exceptionless.Client.Tests {
    public class ExceptionlessClientTests : MarshalByRefObject {
        [Fact]
        public void CanSubmit() {
            ExceptionlessClient client = GetClient();

            try {
                throw new ApplicationException();
            } catch (Exception ex) {
                client.SubmitError(ex);
            }

            List<Manifest> manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(1, manifests.Count);
            Error error = client.Queue.GetError(manifests[0].Id);
            Assert.NotNull(error);

            client.ProcessQueue();
            manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(0, manifests.Count);
        }

        [PartialTrustFact]
        public void CanSubmitInMediumTrust() {
            ExceptionlessClient client = GetClient();

            try {
                throw new ApplicationException();
            } catch (Exception ex) {
                client.SubmitError(ex);
            }

            List<Manifest> manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(1, manifests.Count);
            Error error = client.Queue.GetError(manifests[0].Id);
            Assert.NotNull(error);

            client.ProcessQueue();
            manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(0, manifests.Count);
        }

        [Fact]
        public void IgnoreDuplicate() {
            ExceptionlessClient client = GetClient();

            try {
                throw new ApplicationException();
            } catch (Exception ex) {
                client.SubmitError(ex);
                client.SubmitError(ex);
            }

            List<Manifest> manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(1, manifests.Count);
        }

        [Fact]
        public void DuplicatesAllowedAfter2Seconds() {
            ExceptionlessClient client = GetClient();

            try {
                throw new ApplicationException();
            } catch (Exception ex) {
                client.SubmitError(ex);
                Thread.Sleep(TimeSpan.FromSeconds(2));
                client.SubmitError(ex);
            }

            List<Manifest> manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(2, manifests.Count);
        }

        [Fact]
        public void IgnoresDuplicateWrappedErrors() {
            ExceptionlessClient client = GetClient();

            try {
                ThrowAndReportAndRethrowWrappedException(client);
            } catch (Exception ex) {
                client.SubmitError(ex);
            }

            List<Manifest> manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(1, manifests.Count);
        }

        [Fact(Skip = "We do not currently support this because the stack traces aren't the same.")]
        public void IgnoreDuplicateErrorsWithSimilarStackTrace() {
            ExceptionlessClient client = GetClient();

            try {
                ThrowAndReportAndRethrowException(client);
            } catch (Exception ex) {
                client.SubmitError(ex);
            }

            List<Manifest> manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(1, manifests.Count);
        }

        [Fact(Skip = "This is not working yet.")]
        public void CanSubmitFromAppDomain() {
            var client = new ExceptionlessClient();

            var domainSetup = new AppDomainSetup {
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                ApplicationName = AppDomain.CurrentDomain.SetupInformation.ApplicationName,
                LoaderOptimization = LoaderOptimization.MultiDomainHost
            };
            AppDomain domain = AppDomain.CreateDomain("test", null, domainSetup);
            domain.UnhandledException += (sender, args) => Debug.WriteLine("Hello");

            client.Startup(domain);
            var remoteClass = new RemoteClass();
            try {
                domain.DoCallBack(() => {
                    try {
                        throw new ApplicationException();
                    } catch (Exception ex) {
                        client.SubmitError(ex);
                        throw;
                    }
                });
            } catch {}

            List<Manifest> manifests = client.Queue.GetManifests().ToList();
            Assert.Equal(1, manifests.Count);
        }

        private ExceptionlessClient GetClient() {
            var client = new ExceptionlessClient();
            client.Log = new TraceExceptionlessLog();
            client.Configuration.TestMode = true;

            return client;
        }

        private void ThrowAndReportAndRethrowException(ExceptionlessClient client) {
            try {
                throw new ApplicationException(Guid.NewGuid().ToString());
            } catch (Exception ex) {
                client.SubmitError(ex);
                throw;
            }
        }

        private void ThrowAndReportAndRethrowWrappedException(ExceptionlessClient client) {
            try {
                throw new ApplicationException(Guid.NewGuid().ToString());
            } catch (Exception ex) {
                client.SubmitError(ex);
                throw new Exception("Wrapped", ex);
            }
        }
    }

    [Serializable]
    public class RemoteClass {
        public void SomeMethod() {
            try {
                throw new ApplicationException();
            } catch (Exception ex) {
                ExceptionlessClient.Current.SubmitError(ex);
                throw;
            }
        }
    }
}