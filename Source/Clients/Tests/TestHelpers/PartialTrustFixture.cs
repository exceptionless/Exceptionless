// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace Xunit.Helpers {
    using System;
    using Xunit;

    /// <summary>
    /// Attribute used to decorate a test fixture that is run with the test runner
    /// in partial trust.
    /// </summary>
    /// <remarks>
    /// The test class must derive from <see cref="MarshalByRefObject" />.
    /// 
    /// If the test class is decorated using <see cref="IUseFixture<>" />, the fixture data is
    /// instantiated in the main <see cref="AppDomain" /> and then serialized to the partial trust
    /// sandbox.
    /// 
    /// Individual methods within the test class can be marked with
    /// <see cref="FullTrustAttribute" /> to indicate that they should not be run in partial trust.
    /// </remarks>
    public class PartialTrustFixtureAttribute : RunWithAttribute {
        public PartialTrustFixtureAttribute()
            : base(typeof(PartialTrustClassCommand)) {
        }
    }
}