using System;

namespace Exceptionless.Core.Models {
    public class UsageInfo {
        public DateTime Date { get; set; }
        public int Total { get; set; }
        public int Blocked { get; set; }
        public int Limit { get; set; }
        public int TooBig { get; set; }
    }
}