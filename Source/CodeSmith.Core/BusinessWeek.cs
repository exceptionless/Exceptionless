using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeSmith.Core
{
    /// <summary>
    /// A class representing a business week.
    /// </summary>
    public class BusinessWeek
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessWeek"/> class.
        /// </summary>
        public BusinessWeek()
        {
            BusinessDays = new List<BusinessDay>();
        }

        /// <summary>
        /// Gets the business days for the week.
        /// </summary>
        /// <value>The business days for the week.</value>
        public IList<BusinessDay> BusinessDays { get; private set; }

        /// <summary>
        /// Determines whether the specified date falls on a business day.
        /// </summary>
        /// <param name="date">The date to check.</param>
        /// <returns>
        /// 	<c>true</c> if the specified date falls on a business day; otherwise, <c>false</c>.
        /// </returns>
        public bool IsBusinessDay(DateTime date)
        {
            return BusinessDays.Any(day => day.IsBusinessDay(date));
        }

        /// <summary>
        /// Gets the business time between the start date and end date.
        /// </summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <returns>
        /// A TimeSpan of the amount of business time between the start and end date.
        /// </returns>
        /// <remarks>
        /// Business time is calculated by adding only the time that falls inside the business day range.
        /// If all the time between the start and end date fall outside the business day, the time will be zero.
        /// </remarks>
        public TimeSpan GetBusinessTime(DateTime startDate, DateTime endDate)
        {
            Validate(true);

            var businessTime = TimeSpan.Zero;
            var workingDate = startDate;

            while (workingDate < endDate)
            {
                DateTime businessStart;
                BusinessDay businessDay;

                // get start date
                if (!NextBusinessDay(workingDate, out businessStart, out businessDay))
                    break;

                // business start after end date
                if (businessStart > endDate)
                    break;

                if (businessDay == null)
                    break;

                TimeSpan timeToEndOfDay = businessDay.EndTime.Subtract(businessStart.TimeOfDay);
                DateTime businessEnd = businessStart.Add(timeToEndOfDay);

                if (endDate <= businessEnd)
                {
                    timeToEndOfDay = endDate.TimeOfDay.Subtract(businessStart.TimeOfDay);
                    businessTime = businessTime.Add(timeToEndOfDay);
                    return businessTime;
                }

                // still more time left, use business end date
                businessTime = businessTime.Add(timeToEndOfDay);
                workingDate = businessEnd;
            }

            return businessTime;
        }

        /// <summary>
        /// Gets the business end date using the specified time.
        /// </summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="businessTime">The business time.</param>
        /// <returns></returns>
        public DateTime GetBusinessEndDate(DateTime startDate, TimeSpan businessTime)
        {
            Validate(true);

            var endDate = startDate;
            var remainingTime = businessTime;

            while (remainingTime > TimeSpan.Zero)
            {
                DateTime businessStart;
                BusinessDay businessDay;

                // get start date
                if (!NextBusinessDay(endDate, out businessStart, out businessDay))
                    break;

                TimeSpan timeForDay = businessDay.EndTime.Subtract(businessStart.TimeOfDay);
                if (remainingTime <= timeForDay)
                    return businessStart.Add(remainingTime);

                // still more time left
                remainingTime = remainingTime.Subtract(timeForDay);
                endDate = businessStart.Add(timeForDay);
            }

            return endDate;
        }

        /// <summary>
        /// Validates the business week.
        /// </summary>
        /// <param name="throwExcption">if set to <c>true</c> throw excption if invalid.</param>
        /// <returns><c>true</c> if valid; otherwise <c>false</c>.</returns>
        protected virtual bool Validate(bool throwExcption)
        {
            if (BusinessDays.Any())
                return true;

            if (throwExcption)
                throw new InvalidOperationException("The BusinessDays property must have at least one BusinessDay.");

            return false;
        }

        internal bool NextBusinessDay(DateTime startDate, out DateTime nextDate, out BusinessDay businessDay)
        {
            nextDate = startDate;
            businessDay = null;

            var tree = GetDayTree();

            // loop no more then 7 times
            for (int x = 0; x < 7; x++)
            {
                DayOfWeek dayOfWeek = nextDate.DayOfWeek;

                if (!tree.ContainsKey(dayOfWeek))
                {
                    // no business days on this day of the week
                    nextDate = nextDate.AddDays(1).Date;
                    continue;
                }

                IList<BusinessDay> businessDays = tree[dayOfWeek];
                if (businessDays == null)
                    continue;

                foreach (BusinessDay day in businessDays)
                {
                    if (day == null)
                        continue;

                    TimeSpan timeOfDay = nextDate.TimeOfDay;

                    if (timeOfDay >= day.StartTime && timeOfDay < day.EndTime)
                    {
                        // working date in range
                        businessDay = day;
                        return true;
                    }

                    // past this business day, try other for this day
                    if (timeOfDay >= day.StartTime)
                        continue;

                    // move to start time.
                    businessDay = day;
                    nextDate = nextDate.Date.Add(day.StartTime);

                    return true;
                }

                // next day
                nextDate = nextDate.AddDays(1).Date;
            }

            // should never reach here
            return false;
        }

        private Dictionary<DayOfWeek, IList<BusinessDay>> _dayTree;

        private Dictionary<DayOfWeek, IList<BusinessDay>> GetDayTree()
        {
            if (_dayTree != null)
                return _dayTree;

            _dayTree = new Dictionary<DayOfWeek, IList<BusinessDay>>();
            var days = BusinessDays
                .OrderBy(b => b.DayOfWeek)
                .ThenBy(b => b.StartTime)
                .ToList();

            foreach (var day in days)
            {
                if (!_dayTree.ContainsKey(day.DayOfWeek))
                    _dayTree.Add(day.DayOfWeek, new List<BusinessDay>());

                _dayTree[day.DayOfWeek].Add(day);
            }

            return _dayTree;
        }

        #region DefaultWeek
        /// <summary>
        /// Gets the default BusinessWeek.
        /// </summary>
        public static BusinessWeek DefaultWeek
        {
            get { return Nested.Current; }
        }

        /// <summary>
        /// Nested class to lazy-load singleton.
        /// </summary>
        private class Nested
        {
            /// <summary>
            /// Initializes the Nested class.
            /// </summary>
            /// <remarks>
            /// Explicit static constructor to tell C# compiler not to mark type as beforefieldinit.
            /// </remarks>
            static Nested()
            {
                Current = new BusinessWeek();
                Current.BusinessDays.Add(new BusinessDay(DayOfWeek.Monday));
                Current.BusinessDays.Add(new BusinessDay(DayOfWeek.Tuesday));
                Current.BusinessDays.Add(new BusinessDay(DayOfWeek.Wednesday));
                Current.BusinessDays.Add(new BusinessDay(DayOfWeek.Thursday));
                Current.BusinessDays.Add(new BusinessDay(DayOfWeek.Friday));
            }

            /// <summary>
            /// Current singleton instance.
            /// </summary>
            internal readonly static BusinessWeek Current;
        }

        #endregion
    }
}