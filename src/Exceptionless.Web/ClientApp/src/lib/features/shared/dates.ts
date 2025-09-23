/**
 * Configuration options for date and time formatting
 */
interface DateLabelFormatOptions {
    /** Use 12-hour format with AM/PM (default: true) */
    hour12?: boolean;
    /** Show relative labels like "Today" and "Yesterday" (default: true) */
    includeRelative?: boolean;
    /** Text to join date and time parts (default: ' at ') */
    joiner?: string;
    /** Month format - 'short' for "Dec" or 'long' for "December" (default: 'long') */
    month?: 'long' | 'short';
    /** Specific timezone to format in (optional) */
    timeZone?: string;
}

export function formatDate(value: Date): string {
    return value.toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'long',
        year: 'numeric'
    });
}

/**
 * Formats a date with intelligent time display - shows minimal time components
 * and uses relative labels for recent dates.
 *
 * @param date - The date to format
 * @param currentDate - Current date for relative comparisons (default: new Date())
 * @param options - Formatting options
 * @returns Formatted date string
 *
 * @example
 * // Midnight dates show date only
 * formatDateLabel(new Date('2025-09-10T00:00:00')) // "Today"
 * formatDateLabel(new Date('2025-09-09T00:00:00')) // "Yesterday"
 * formatDateLabel(new Date('2025-08-15T00:00:00')) // "August 15"
 *
 * // Time components are shown minimally
 * formatDateLabel(new Date('2025-09-10T14:00:00')) // "Today at 2 PM"
 * formatDateLabel(new Date('2025-09-10T14:30:00')) // "Today at 2:30 PM"
 * formatDateLabel(new Date('2025-09-10T14:30:45')) // "Today at 2:30:45 PM"
 *
 * // Customizable options
 * formatDateLabel(date, now, {
 *   hour12: false,           // "Today at 14:30"
 *   month: 'short',          // "Dec 25 at 2 PM"
 *   includeRelative: false,  // "September 10 at 2 PM"
 *   joiner: ' @ ',           // "Today @ 2 PM"
 *   timeZone: 'UTC'          // Format in UTC
 * })
 */
export function formatDateLabel(date: Date, currentDate: Date = new Date(), options: DateLabelFormatOptions = {}): string {
    const { hour12 = true, includeRelative = true, joiner = ' at ', month = 'long', timeZone } = options;

    const sameDay = date.toDateString() === currentDate.toDateString();
    const yesterday = new Date(currentDate);
    yesterday.setDate(currentDate.getDate() - 1);
    const isYesterday = date.toDateString() === yesterday.toDateString();
    const isSameYear = date.getFullYear() === currentDate.getFullYear();

    const isMidnight = date.getHours() === 0 && date.getMinutes() === 0 && date.getSeconds() === 0 && date.getMilliseconds() === 0;

    // Build the date formatter (omit year if same year)
    const dateFmt = new Intl.DateTimeFormat(undefined, {
        day: 'numeric',
        month,
        ...(isSameYear ? undefined : { year: 'numeric' }),
        ...(timeZone ? { timeZone } : undefined)
    });

    // If exactly midnight, return date-only with relative labels
    if (isMidnight) {
        if (includeRelative && (sameDay || isYesterday)) {
            return sameDay ? 'Today' : 'Yesterday';
        }

        return dateFmt.format(date);
    }

    // Minimal time components per your rules
    const ms = date.getMilliseconds();
    const sec = date.getSeconds();
    const min = date.getMinutes();

    const timeOpts: Intl.DateTimeFormatOptions = {
        hour: 'numeric',
        hour12,
        ...(min > 0 || sec > 0 || ms > 0 ? { minute: '2-digit' } : {}),
        ...(sec > 0 || ms > 0 ? { second: '2-digit' } : {}),
        ...(ms > 0 ? { fractionalSecondDigits: 3 } : {}),
        ...(timeZone ? { timeZone } : {})
    };

    const timeStr = new Intl.DateTimeFormat(undefined, timeOpts).format(date);

    const datePart = includeRelative && (sameDay || isYesterday) ? (sameDay ? 'Today' : 'Yesterday') : dateFmt.format(date);
    return `${datePart}${joiner}${timeStr}`;
}

export function formatLongDate(value: Date): string {
    return value.toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'long',
        year: 'numeric'
    });
}

export function getDifferenceInSeconds(value: Date | string): number {
    return (new Date().getTime() - new Date(value).getTime()) / 1000;
}

export function getRelativeTimeFormatUnit(differenceInSeconds: number): Intl.RelativeTimeFormatUnit {
    const minute = 60;
    const hour = minute * 60;
    const day = hour * 24;
    const week = day * 7;
    const month = day * 30.44; // Average length of a month (365.24/12)
    const year = day * 365.24; // Average length of a year (accounts for leap years)

    if (differenceInSeconds < minute) {
        return 'seconds';
    } else if (differenceInSeconds < hour) {
        return 'minutes';
    } else if (differenceInSeconds < day) {
        return 'hours';
    } else if (differenceInSeconds < week) {
        return 'days';
    } else if (differenceInSeconds < month) {
        return 'weeks';
    } else if (differenceInSeconds < year) {
        return 'months';
    } else {
        return 'years';
    }
}

export function getSetIntervalTime(value: Date | string): number {
    const minute = 60;
    const hour = minute * 60;
    const day = hour * 24;

    const unit = getRelativeTimeFormatUnit(getDifferenceInSeconds(value));
    switch (unit) {
        case 'hours':
            return hour * 1000; // update every hour
        case 'minutes':
            return minute * 1000; // update every minute
        case 'seconds':
            return 15 * 1000; // update every 15 seconds
        default:
            return day * 1000; // update every day
    }
}

export function isSameUtcMonth(date: Date, other: Date = new Date()): boolean {
    return date.getUTCFullYear() === other.getUTCFullYear() && date.getUTCMonth() === other.getUTCMonth();
}
