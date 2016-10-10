using System;

namespace Exceptionless.Core.Models.Billing {
    public class ChangePlanResult {
        public bool Success { get; set; }
        public string Message { get; set; }
        
        public static ChangePlanResult FailWithMessage(string message) {
            return new ChangePlanResult { Message = message };
        }
        
        public static ChangePlanResult SuccessWithMessage(string message) {
            return new ChangePlanResult { Success = true, Message = message };
        }
    }
}
