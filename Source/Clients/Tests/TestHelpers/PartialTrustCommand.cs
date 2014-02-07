// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
namespace Xunit.Helpers {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Xml;
    using Xunit;
    using Xunit.Sdk;

    internal class PartialTrustCommand : ITestCommand {
        private readonly ITestCommand _command;
        private readonly IDictionary<MethodInfo, object> _fixtures;

        public PartialTrustCommand(ITestCommand command, IDictionary<MethodInfo, object> fixtures = null) {
            _command = command;
            _fixtures = fixtures;
        }

        public string DisplayName {
            get { return _command.DisplayName; }
        }

        public bool ShouldCreateInstance {
            get { return _command.ShouldCreateInstance; }
        }

        public int Timeout {
            get { return _command.Timeout; }
        }

        public MethodResult Execute(object testClass) {
            object sandboxedClass = null;

            if (testClass != null) {
                var testClassType = testClass.GetType();

                if (!typeof(MarshalByRefObject).IsAssignableFrom(testClassType)) {
                    throw new InvalidOperationException(
                        string.Format(
                            "In order to use the partial trust attributes here, '{0}' must derive from MarshalByRefObject.",
                            testClassType.FullName));
                }

                sandboxedClass = PartialTrustSandbox.Default.CreateInstance(testClassType);
                ApplyFixtures(sandboxedClass);
            } else {
                Assert.IsType<SkipCommand>(_command);
            }

            return _command.Execute(sandboxedClass);
        }

        public XmlNode ToStartXml() {
            return _command.ToStartXml();
        }

        private void ApplyFixtures(object testClass) {
            if (_fixtures == null) {
                return;
            }

            foreach (var fixture in _fixtures) {
                fixture.Key.Invoke(testClass, new object[] { fixture.Value });
            }
        }
    }
}