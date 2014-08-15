using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IMonthProjectStatsRepository: IRepositoryOwnedByProject<MonthProjectStats> {
        ICollection<MonthProjectStats> GetRange(string start, string end);
        long IncrementStats(string id, string stackId, DateTime localDate, bool isNew);
        void DecrementStatsByStackId(string projectId, string stackId);
    }
}