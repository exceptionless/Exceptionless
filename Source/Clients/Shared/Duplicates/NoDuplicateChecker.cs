using System;
using Exceptionless.Models;

namespace Exceptionless.Duplicates {
    public class NoDuplicateChecker : IDuplicateChecker {
        public bool IsDuplicate(Event ev) {
            return false;
        }
    }
}
