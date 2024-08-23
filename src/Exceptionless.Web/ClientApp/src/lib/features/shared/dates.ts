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

export function getDifferenceInSeconds(value: Date | string): number {
    return (new Date().getTime() - new Date(value).getTime()) / 1000;
}
