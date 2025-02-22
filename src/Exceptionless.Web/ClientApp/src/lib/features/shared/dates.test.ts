import { describe, expect, it } from 'vitest';

import { getDifferenceInSeconds, getRelativeTimeFormatUnit, getSetIntervalTime } from './dates';

const Time = {
    days: (n: number) => n * 60 * 60 * 24,
    hours: (n: number) => n * 60 * 60,
    minutes: (n: number) => n * 60,
    months: (n: number) => n * 60 * 60 * 24 * 30.44,
    ms: (n: number) => n * 1000,
    seconds: (n: number) => n,
    weeks: (n: number) => n * 60 * 60 * 24 * 7,
    years: (n: number) => n * 60 * 60 * 24 * 365.24
};

describe('getDifferenceInSeconds', () => {
    it('should calculate difference in seconds correctly', () => {
        const now = new Date();
        const past = new Date(now.getTime() - 5000);
        expect(getDifferenceInSeconds(past)).toBeCloseTo(5, 0);
    });
});

describe('getRelativeTimeFormatUnit', () => {
    it('should return correct unit for given seconds', () => {
        expect(getRelativeTimeFormatUnit(Time.seconds(30))).toBe('seconds');
        expect(getRelativeTimeFormatUnit(Time.minutes(30))).toBe('minutes');
        expect(getRelativeTimeFormatUnit(Time.hours(2))).toBe('hours');
        expect(getRelativeTimeFormatUnit(Time.days(3))).toBe('days');
        expect(getRelativeTimeFormatUnit(Time.weeks(2))).toBe('weeks');
        expect(getRelativeTimeFormatUnit(Time.months(2))).toBe('months');
        expect(getRelativeTimeFormatUnit(Time.years(2))).toBe('years');
    });

    it('should handle boundary cases correctly', () => {
        expect(getRelativeTimeFormatUnit(Time.minutes(1) - 1)).toBe('seconds');
        expect(getRelativeTimeFormatUnit(Time.hours(1) - 1)).toBe('minutes');
        expect(getRelativeTimeFormatUnit(Time.days(1) - 1)).toBe('hours');
        expect(getRelativeTimeFormatUnit(Time.weeks(1) - 1)).toBe('days');
        expect(getRelativeTimeFormatUnit(Time.months(1) - 1)).toBe('weeks');
        expect(getRelativeTimeFormatUnit(Time.years(1) - 1)).toBe('months');
    });

    it('should return months for durations more than the average month length', () => {
        expect(getRelativeTimeFormatUnit(Time.days(31))).toBe('months');
    });

    it('should return years for durations more than the average length of a year', () => {
        expect(getRelativeTimeFormatUnit(Time.days(366))).toBe('years');
    });
});

describe('getSetIntervalTime', () => {
    it('should return correct interval for given age in seconds', () => {
        const now = new Date();
        expect(getSetIntervalTime(new Date(now.getTime() - Time.seconds(30) * 1000))).toBe(Time.ms(15));
        expect(getSetIntervalTime(new Date(now.getTime() - Time.seconds(3000) * 1000))).toBe(Time.ms(60));
        expect(getSetIntervalTime(new Date(now.getTime() - Time.seconds(7200) * 1000))).toBe(Time.ms(3600));
        expect(getSetIntervalTime(new Date(now.getTime() - Time.seconds(172800) * 1000))).toBe(Time.ms(86400));
    });
});
