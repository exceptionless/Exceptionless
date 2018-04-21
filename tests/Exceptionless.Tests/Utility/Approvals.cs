using System;
using ApprovalTests;
using ApprovalTests.Writers;

namespace Exceptionless.Tests.Utility {
    public static class ApprovalsUtility {
        public static void VerifyFile(string approvedFilePath, object receivedData) {
            Approvals.Verify(new ConfigurableTempTextFileWriter(approvedFilePath, "" + receivedData));
        }
    }
}
