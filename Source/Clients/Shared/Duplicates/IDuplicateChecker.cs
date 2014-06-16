using System;
using Exceptionless.Models;

namespace Exceptionless.Duplicates {
    public interface IDuplicateChecker {
        bool IsDuplicate(Event ev);
    }
}
