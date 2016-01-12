using System;
using ApprovalTests;
using ApprovalTests.Core;
using ApprovalTests.Reporters;
using ApprovalTests.Writers;

namespace Exceptionless.Api.Tests.Utility {
    public static class ApprovalsUtility {
        public static void VerifyFile(string approvedFilePath, object receivedData) {
            Approvals.Verify(new ConfigurableTempTextFileWriter(approvedFilePath, "" + receivedData));
        }
    }

    public class HappyDiffReporter : FirstWorkingReporter {
        public HappyDiffReporter()
            : base(
            CodeCompareReporter.INSTANCE,
            BeyondCompareReporter.INSTANCE,
            TortoiseDiffReporter.INSTANCE,
            AraxisMergeReporter.INSTANCE,
            P4MergeReporter.INSTANCE,
            WinMergeReporter.INSTANCE,
            KDiffReporter.INSTANCE,
            FrameworkAssertReporter.INSTANCE) {
        }
    }
}
