using System;
using System.Diagnostics;
using System.Text;

namespace CodeSmith.Core
{
    /// <summary>
    /// A class defining a business day.
    /// </summary>
    [DebuggerDisplay("DayOfWeek={DayOfWeek}, StartTime={StartTime}, EndTime={EndTime}")]
    public class BusinessDay
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessDay"/> class.
        /// </summary>
        /// <param name="dayOfWeek">The day of week this business day represents.</param>
        public BusinessDay(DayOfWeek dayOfWeek)
        {
            StartTime = TimeSpan.FromHours(9); // 9am
            EndTime = TimeSpan.FromHours(17);  // 5pm
            DayOfWeek = dayOfWeek;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessDay"/> class.
        /// </summary>
        /// <param name="dayOfWeek">The day of week this business day represents.</param>
        /// <param name="startTime">The start time of the business day.</param>
        /// <param name="endTime">The end time of the business day.</param>
        public BusinessDay(DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime)
        {
            if (startTime.TotalDays >= 1)
#if SILVERLIGHT
                throw new ArgumentOutOfRangeException("startTime", "The startTime argument must be less then one day.");
#else
                throw new ArgumentOutOfRangeException("startTime", startTime, "The startTime argument must be less then one day.");
#endif

            if (endTime.TotalDays > 1)
#if SILVERLIGHT
                throw new ArgumentOutOfRangeException("endTime", "The endTime argument must be less then one day.");
#else
                throw new ArgumentOutOfRangeException("endTime", endTime, "The endTime argument must be less then one day.");
#endif

            if (endTime <= startTime)
#if SILVERLIGHT
                throw new ArgumentOutOfRangeException("endTime", "The endTime argument must be greater then startTime.");
#else
                throw new ArgumentOutOfRangeException("endTime", endTime, "The endTime argument must be greater then startTime.");
#endif

            DayOfWeek = dayOfWeek;
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>
        /// Gets the day of week this business day represents..
        /// </summary>
        /// <value>The day of week.</value>
        public DayOfWeek DayOfWeek { get; private set; }

        /// <summary>
        /// Gets the start time of the business day.
        /// </summary>
        /// <value>The start time of the business day.</value>
        public TimeSpan StartTime { get; private set; }

        /// <summary>
        /// Gets the end time of the business day.
        /// </summary>
        /// <value>The end time of the business day.</value>
        public TimeSpan EndTime { get; private set; }

        /// <summary>
        /// Determines whether the specified date falls in the business day.
        /// </summary>
        /// <param name="date">The date to check.</param>
        /// <returns>
        /// 	<c>true</c> if the specified date falls in the business day; otherwise, <c>false</c>.
        /// </returns>
        public bool IsBusinessDay(DateTime date)
        {
            if (date.DayOfWeek != DayOfWeek)
                return false;

            if (date.TimeOfDay < StartTime)
                return false;

            if (date.TimeOfDay > EndTime)
                return false;

            return true;
        }
    }
}
