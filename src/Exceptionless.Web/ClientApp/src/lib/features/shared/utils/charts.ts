/**
 * Fills date series with evenly spaced data points for chart visualization.
 *
 * @param startDate Start date for the series
 * @param endDate End date for the series
 * @param map Callback function that gets called for each date
 * @param bucketCount Number of buckets to create between start and end (default: 20)
 * @returns Array of whatever the callback returns
 */
export function fillDateSeries<T>(startDate: Date, endDate: Date, map: (date: Date) => T, bucketCount: number = 20): T[] {
    const series: T[] = [];
    const totalMs = endDate.getTime() - startDate.getTime();
    const intervalMs = totalMs / Math.max(1, bucketCount - 1);

    // Determine rounding granularity based on time span
    const getRoundedDate = (timestamp: number): Date => {
        const date = new Date(timestamp);

        // For very small windows (< 60 seconds), keep full precision
        if (totalMs < 60 * 1000) {
            return date;
        }

        // For windows < 1 hour, round to nearest second
        if (totalMs < 60 * 60 * 1000) {
            date.setMilliseconds(0);
            return date;
        }

        // For windows < 1 day, round to nearest minute
        if (totalMs < 24 * 60 * 60 * 1000) {
            date.setSeconds(0, 0);
            return date;
        }

        // For windows < 30 days, round to nearest hour
        if (totalMs < 30 * 24 * 60 * 60 * 1000) {
            date.setMinutes(0, 0, 0);
            return date;
        }

        // For larger windows, round to nearest day
        date.setHours(0, 0, 0, 0);
        return date;
    };

    for (let i = 0; i < bucketCount; i++) {
        const timestamp = startDate.getTime() + intervalMs * i;
        const roundedDate = getRoundedDate(timestamp);
        series.push(map(roundedDate));
    }

    return series;
}
