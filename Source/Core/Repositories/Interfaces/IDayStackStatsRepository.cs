using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IDayStackStatsRepository: IRepositoryOwnedByProjectAndStack<DayStackStats> {
        ICollection<DayStackStats> GetRange(string start, string end);
        long IncrementStats(string id, long getTimeBucket);
    }
}