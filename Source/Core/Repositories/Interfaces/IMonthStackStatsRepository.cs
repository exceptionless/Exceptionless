using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IMonthStackStatsRepository : IRepositoryOwnedByProject<MonthStackStats>, IRepositoryOwnedByStack<MonthStackStats> {
        IList<MonthStackStats> GetRange(string start, string end);
        long IncrementStats(string id, DateTime localDate);
    }
}