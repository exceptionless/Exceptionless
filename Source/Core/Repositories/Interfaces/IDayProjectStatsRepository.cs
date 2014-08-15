using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IDayProjectStatsRepository: IRepositoryOwnedByProject<DayProjectStats> {
        ICollection<DayProjectStats> GetRange(string start, string end);
        long IncrementStats(string id, string stackId, long timeBucket, bool isNew);
        void DecrementStatsByStackId(string projectId, string stackId);
    }
}