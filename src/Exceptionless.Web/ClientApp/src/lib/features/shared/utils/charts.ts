/**
 * Fills date series with evenly spaced data points for chart visualization.
 *
 * @param startDate Start date for the series
 * @param endDate End date for the series
 * @param bucketCount Number of buckets to create between start and end (default: 20)
 * @param map Callback function that gets called for each date
 * @returns Array of whatever the callback returns
 */
export function fillDateSeries<T>(startDate: Date, endDate: Date, map: (date: Date) => T, bucketCount: number = 20): T[] {
    const series: T[] = [];
    const totalMs = endDate.getTime() - startDate.getTime();
    const intervalMs = totalMs / Math.max(1, bucketCount - 1);

    for (let i = 0; i < bucketCount; i++) {
        const date = new Date(startDate.getTime() + intervalMs * i);
        series.push(map(date));
    }

    return series;
}
