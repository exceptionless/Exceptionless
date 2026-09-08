import { describe, expect, it } from 'vitest';

import { formatDateLabel, getDifferenceInSeconds, getRelativeTimeFormatUnit, getSetIntervalTime } from './dates';

describe('formatDateLabel', () => {
    it('preserves local date and relative-label behavior without a timezone override', () => {
        // Arrange
        const current = new Date(2026, 0, 2, 12);

        // Act / Assert
        expect(formatDateLabel(new Date(2026, 0, 2), current)).toBe('Today');
        expect(formatDateLabel(new Date(2026, 0, 1), current)).toBe('Yesterday');
        expect(formatDateLabel(new Date(2026, 0, 2, 14, 30), current)).toContain('Today at ');
    });

    it('uses the requested timezone for relative days and year boundaries', () => {
        // Arrange
        const date = new Date('2025-12-31T23:00:00Z');
        const current = new Date('2026-01-01T12:00:00Z');

        // Act
        const label = formatDateLabel(date, current, { timeZone: 'Pacific/Auckland' });

        // Assert
        expect(label).toContain('Yesterday at ');
        const absoluteLabel = formatDateLabel(date, current, { includeRelative: false, timeZone: 'Pacific/Auckland' });
        expect(absoluteLabel).not.toContain('2025');
        expect(absoluteLabel).not.toContain('2026');
    });

    it('uses the requested timezone to determine midnight', () => {
        // Arrange
        const date = new Date('2026-01-01T00:00:00Z');

        // Act
        const label = formatDateLabel(date, new Date('2026-01-02T12:00:00Z'), { includeRelative: false, month: 'short', timeZone: 'UTC' });

        // Assert
        expect(label).toBe(new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', timeZone: 'UTC' }).format(date));
    });

    it('preserves the time and minutes in a timezone with a half-hour offset', () => {
        // Arrange
        const date = new Date('2026-01-01T00:00:00Z');
        const timeZone = 'Asia/Kolkata';

        // Act
        const label = formatDateLabel(date, new Date(2026, 0, 2), { includeRelative: false, month: 'short', timeZone });

        // Assert
        expect(label).toContain(' at ');
        expect(label).toContain(new Intl.DateTimeFormat(undefined, { hour: 'numeric', hour12: true, minute: '2-digit', timeZone }).format(date));
    });
});

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
