/**
 * Utility functions for parsing time parameters in event filters.
 * Based on the legacy Angular filter service functionality.
 */

/**
 * Represents a parsed time range with start and end dates.
 */
export interface TimeRange {
    end: Date;
    start: Date;
}

/**
 * Regular expression to match custom date ranges in format: YYYY-MM-DDTHH:mm:ss-YYYY-MM-DDTHH:mm:ss
 * Based on the legacy dateRangeParserService.
 */
const DATE_RANGE_REGEX = /(\d{4}-\d{2}-\d{2}(?:T(?:\d{2}:\d{2}:\d{2}|\d{2}:\d{2}|\d{2}))?)/g;

/**
 * Parse a time parameter string and return a time range with start and end dates.
 * Supports relative time strings like "last week" and custom date ranges.
 *
 * @param timeParam The time parameter string (e.g., "last week", "last 30 days", "2024-01-01T00:00:00-2024-01-31T23:59:59")
 * @returns A TimeRange object with start and end dates
 */
export function parseTimeParameter(time: string): TimeRange {
    const trimmedTime = time?.trim() ?? '';

    if (trimmedTime === '' || trimmedTime === 'all' || trimmedTime === 'all time') {
        return {
            end: new Date(),
            start: new Date('1900-01-01')
        };
    }

    const now = new Date();
    switch (trimmedTime) {
        case 'last 2 hours': {
            const start = new Date(now.getTime() - 2 * 60 * 60 * 1000);
            return { end: now, start };
        }
        case 'last 3 months': {
            const start = new Date(now.getFullYear(), now.getMonth() - 3, now.getDate());
            return { end: now, start };
        }
        case 'last 6 months': {
            const start = new Date(now.getFullYear(), now.getMonth() - 6, now.getDate());
            return { end: now, start };
        }
        case 'last 12 hours': {
            const start = new Date(now.getTime() - 12 * 60 * 60 * 1000);
            return { end: now, start };
        }
        case 'last 24 hours': {
            const start = new Date(now.getTime() - 24 * 60 * 60 * 1000);
            return { end: now, start };
        }
        case 'last 30 days': {
            const start = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
            return { end: now, start };
        }
        case 'last hour': {
            const start = new Date(now.getTime() - 60 * 60 * 1000);
            return { end: now, start };
        }
        case 'last month': {
            const start = new Date(now.getFullYear(), now.getMonth() - 1, now.getDate());
            return { end: now, start };
        }
        case 'last week': {
            const start = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
            return { end: now, start };
        }
        case 'last year': {
            const start = new Date(now.getFullYear() - 1, now.getMonth(), now.getDate());
            return { end: now, start };
        }
        case 'yesterday': {
            const yesterday = new Date(now);
            yesterday.setDate(now.getDate() - 1);
            yesterday.setHours(0, 0, 0, 0);
            const end = new Date(yesterday);
            end.setHours(23, 59, 59, 999);
            return { end, start: yesterday };
        }
        default: {
            // Try to parse as a custom date range using regex (e.g., "2024-01-01T00:00:00-2024-01-31T23:59:59")
            const customRange = parseCustomDateRange(trimmedTime);
            if (customRange) {
                const start = new Date(customRange.start);
                const end = new Date(customRange.end);
                if (!isNaN(start.getTime()) && !isNaN(end.getTime())) {
                    return { end, start };
                }
            }

            // Try to parse as a custom date range (e.g., "2024-01-01..2024-01-31")
            if (trimmedTime.includes('..')) {
                const parts = trimmedTime.split('..');
                if (parts.length === 2 && parts[0] && parts[1]) {
                    const start = new Date(parts[0]);
                    const end = new Date(parts[1]);
                    if (!isNaN(start.getTime()) && !isNaN(end.getTime())) {
                        return { end, start };
                    }
                }
            }

            // Fallback to 'last week' for unknown formats
            const fallbackStart = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
            return { end: now, start: fallbackStart };
        }
    }
}

/**
 * Parse a custom date range string in the format: "start-end".
 * @param input The date range string to parse
 * @returns An object with start and end date strings, or null if parsing fails
 */
function parseCustomDateRange(input: string): null | { end: string; start: string } {
    if (!input) {
        return null;
    }

    const matches: string[] = [];
    let found: null | RegExpExecArray;

    // Reset regex lastIndex to ensure consistent parsing
    DATE_RANGE_REGEX.lastIndex = 0;

    while ((found = DATE_RANGE_REGEX.exec(input))) {
        matches.push(found[0]);
    }

    if (matches.length === 2 && matches[0] && matches[1]) {
        return {
            end: matches[1],
            start: matches[0]
        };
    }

    return null;
}
