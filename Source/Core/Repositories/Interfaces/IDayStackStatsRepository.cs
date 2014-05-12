using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IDayStackStatsRepository: IRepositoryOwnedByProjectAndStack<DayStackStats> {
        IList<DayStackStats> GetRange(string start, string end);
        long IncrementStats(string id, long getTimeBucket);
    }
}