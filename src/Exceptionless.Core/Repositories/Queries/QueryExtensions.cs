using System;
using Exceptionless.Core.Models;
using Foundatio.Repositories;

namespace Exceptionless.Core.Repositories {
    public static class QueryExtensions {
        public static IRepositoryQuery<Event> DateRange(this IRepositoryQuery<Event> query, DateTime? utcStart, DateTime? utcEnd) {
            return query.DateRange(utcStart, utcEnd, (Event e) => e.Date);
        }
        
        public static IRepositoryQuery<Stack> DateRange(this IRepositoryQuery<Stack>  query, DateTime? utcStart, DateTime? utcEnd) {
            return query.DateRange(utcStart, utcEnd, (Stack e) => e.CreatedUtc);
        }
    }
}
