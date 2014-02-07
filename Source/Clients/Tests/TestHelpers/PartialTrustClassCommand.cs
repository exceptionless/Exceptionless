// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
namespace Xunit.Helpers {
    using System;
    using System.Collections.Generic;
    using Xunit.Sdk;

    internal class PartialTrustClassCommand : ITestClassCommand {
        private readonly ITestClassCommand _classCommand = new TestClassCommand();

        public int ChooseNextTest(ICollection<IMethodInfo> testsLeftToRun) {
            return _classCommand.ChooseNextTest(testsLeftToRun);
        }

        public Exception ClassFinish() {
            return _classCommand.ClassFinish();
        }

        public Exception ClassStart() {
            return _classCommand.ClassStart();
        }

        public IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo testMethod) {
            foreach (var testCommand in _classCommand.EnumerateTestCommands(testMethod)) {
                if (testMethod.HasAttribute(typeof(FullTrustAttribute))
                    || testCommand is PartialTrustCommand) {
                    yield return testCommand;
                    continue;
                }

                yield return new PartialTrustCommand(testCommand);
            }
        }

        public IEnumerable<IMethodInfo> EnumerateTestMethods() {
            return _classCommand.EnumerateTestMethods();
        }

        public bool IsTestMethod(IMethodInfo testMethod) {
            return _classCommand.IsTestMethod(testMethod);
        }

        public object ObjectUnderTest {
            get { return _classCommand.ObjectUnderTest; }
        }

        public ITypeInfo TypeUnderTest {
            get { return _classCommand.TypeUnderTest; }
            set { _classCommand.TypeUnderTest = value; }
        }
    }
}