using System;
using ApprovalTests;
using ApprovalTests.Reporters;
using ApprovalTests.Writers;

namespace Exceptionless.Api.Tests.Utility {
    public static class ApprovalsUtility {
        public static void VerifyFile(string approvedFilePath, object receivedData) {
            Approvals.Verify(new ConfigurableTempTextFileWriter(approvedFilePath, "" + receivedData));
        }
    }

    public class SmartReporter : FirstWorkingReporter {
        public SmartReporter() : base(BeyondCompareReporter.INSTANCE, NUnitReporter.INSTANCE) {}
    }
}
