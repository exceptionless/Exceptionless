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
            (IEnvironmentAwareReporter)CodeCompareReporter.INSTANCE,
            (IEnvironmentAwareReporter)BeyondCompareReporter.INSTANCE,
            (IEnvironmentAwareReporter)TortoiseDiffReporter.INSTANCE,
            (IEnvironmentAwareReporter)AraxisMergeReporter.INSTANCE,
            (IEnvironmentAwareReporter)P4MergeReporter.INSTANCE,
            (IEnvironmentAwareReporter)WinMergeReporter.INSTANCE,
            (IEnvironmentAwareReporter)KDiffReporter.INSTANCE,
            (IEnvironmentAwareReporter)FrameworkAssertReporter.INSTANCE,
            (IEnvironmentAwareReporter)QuietReporter.INSTANCE) {
        }
    }
}
