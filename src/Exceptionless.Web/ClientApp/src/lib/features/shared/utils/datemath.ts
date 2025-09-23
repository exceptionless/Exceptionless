/**
 * Elasticsearch date math utilities - unified API for time expressions and ranges
 * Handles both single expressions (now-5m) and range expressions ([now-5m TO now])
 * Based on Elasticsearch date math syntax: https://www.elastic.co/guide/en/elasticsearch/reference/current/common-options.html#date-math
 */

import type { CustomDateRange } from "../models";

/** Time units supported by Elasticsearch date math */
const TIME_UNITS = ['s', 'm', 'h', 'd', 'w', 'M', 'Q', 'y'] as const;

type TimeUnit = (typeof TIME_UNITS)[number];

/** Mapping of time units to their human-readable names */
export const TIME_UNIT_NAMES: Record<TimeUnit, string> = {
    d: 'days',
    h: 'hours',
    m: 'minutes',
    M: 'months',
    Q: 'quarters',
    s: 'seconds',
    w: 'weeks',
    y: 'years'
} as const;

/** Absolute time like "2025-09-20T14:30:00" */
export interface AbsoluteTimeExpression {
    expression: string;
    type: 'absolute';
    value: Date;
}

/** Parse error result */
export interface DateMathError {
    error: string;
}

/** Unified date math range result */
export interface DateMathRange {
    end: ParsedTimeExpression;
    endBoundary: RangeBoundaryType;
    isPointInTime: boolean; // true if start === end (single expression)
    start: ParsedTimeExpression;
    startBoundary: RangeBoundaryType;
}

/** Union type for all parse results */
export type DateMathResult = DateMathError | DateMathRange;

/** Parsed time expression */
export type ParsedTimeExpression = AbsoluteTimeExpression | RelativeTimeExpression;

/** Range boundary types for Elasticsearch compatibility */
export type RangeBoundaryType = 'exclusive' | 'inclusive';

/** Relative time like "now-5m" or "now/d" */
export interface RelativeTimeExpression {
    expression: string;
    offset: number;
    round: null | TimeUnit;
    type: 'relative';
    unit: TimeUnit;
}

/** Simple time range interface for parseTimeParameter */
export interface DateRange {
    end: Date;
    start: Date;
}

/**
 * Extract the original start/end expressions from a date math range string.
 * Returns null when the input is not a range, resolves to a single point in time,
 * or uses unsupported wildcards.
 */
export function extractRangeExpressions(input: Date | string): null | CustomDateRange {
    if (!input) {
        return null;
    }

    if (input instanceof Date) {
        return {
            end: input.toISOString(),
            start: input.toISOString()
        };
    }

    if (!input.trim() || input.includes('*')) {
        return null;
    }

    const result = parseDateMath(input);
    if (!isDateMathRange(result) || result.isPointInTime) {
        return null;
    }

    return {
        end: result.end.expression,
        start: result.start.expression
    };
}

/** Get validation error message (for form validation) */
export function getDateMathValidationError(input: string): null | string {
    const result = parseDateMath(input);
    return isDateMathError(result) ? result.error : null;
}

/** Type guard for parse errors */
export function isDateMathError(result: DateMathResult): result is DateMathError {
    return 'error' in result;
}

/** Type guard for successful parse results */
export function isDateMathRange(result: DateMathResult): result is DateMathRange {
    return !isDateMathError(result);
}

/** Check if result represents a single point in time */
export function isPointInTime(result: DateMathResult): boolean {
    return isDateMathRange(result) && result.isPointInTime;
}

/**
 * Universal Elasticsearch date math parser - handles both single expressions and ranges
 *
 * Single expressions (become point-in-time ranges):
 * - "now-5m" → Range from now-5m to now-5m
 * - "2025-09-20T14:30:00" → Range from 2025-09-20T14:30:00 to 2025-09-20T14:30:00
 *
 * Range expressions:
 * - "now-5m TO now" → Range from now-5m to now
 * - "[2025-09-20T00:00:00 TO 2025-09-21T00:00:00]" → Elasticsearch inclusive range
 * - "{now-1w TO now}" → Elasticsearch exclusive range
 */
export function parseDateMath(input: string): DateMathResult {
    if (!input || !input.trim()) {
        const today = new Date().toISOString().split('T')[0];
        return { error: `Please enter a time. Try "now-5m" for 5 minutes ago or "${today}T14:30:00" for a specific time.` };
    }

    const trimmed = input.trim();

    // Check if it's a range expression (contains TO or has brackets)
    if (isRangeExpression(trimmed)) {
        return parseRangeExpression(trimmed);
    } else {
        return parseSingleExpression(trimmed);
    }
}

/**
 * Validate and resolve a time expression to an actual date
 * Returns null if the expression is invalid, otherwise returns the resolved Date
 */
export function validateAndResolveTime(expression: string, referenceTime?: Date): Date | null {
    const parsed = parseTimeExpression(expression);
    if ('error' in parsed) {
        return null;
    }

    return resolveTimeExpression(parsed, referenceTime);
}

/** Validate any date math input (single or range) */
export function validateDateMath(input: string): { error?: string; valid: boolean } {
    const error = getDateMathValidationError(input);
    return error ? { error, valid: false } : { valid: true };
}

/**
 * Convert two Date objects to a date math range string
 */
export function toDateMathRange(start: Date, end: Date): string {
    const startISO = start.toISOString();
    const endISO = end.toISOString();
    return `[${startISO} TO ${endISO}]`;
}

/**
 * Detect if input is a range expression
 */
function isRangeExpression(input: string): boolean {
    // Has TO keyword or is wrapped in brackets
    return /\s+TO\s+/i.test(input) || /^[[{].*[\]}]$/.test(input);
}

function isValidTimeUnit(unit: string): unit is TimeUnit {
    return TIME_UNITS.includes(unit as TimeUnit);
}

/**
 * Parse absolute time strings like "2025-09-20T14:30:00" or "2025-09-20T14:30"
 */
function parseAbsoluteTimeExpression(expression: string): DateMathError | ParsedTimeExpression {
    // Parse ISO 8601 datetime format used by Elasticsearch
    const iso8601Pattern = /^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})T(?<hour>\d{2}):(?<minute>\d{2})(?::(?<second>\d{2}))?$/;
    const match = expression.match(iso8601Pattern);

    if (!match?.groups) {
        const today = new Date().toISOString().split('T')[0];
        return {
            error: `Invalid date format: "${expression}". Please use formats like "${today}T14:30:00" or "${today}T09:15".`
        };
    }

    const { day, hour, minute, month, second = '00', year } = match.groups;

    // Ensure all required parts are present (TypeScript safety)
    if (!year || !month || !day || !hour || !minute) {
        const today = new Date().toISOString().split('T')[0];
        return {
            error: `Invalid date format: "${expression}". Please use formats like "${today}T14:30:00" or "${today}T09:15".`
        };
    }

    try {
        const dateComponents = {
            day: parseInt(day, 10),
            hour: parseInt(hour, 10),
            minute: parseInt(minute, 10),
            month: parseInt(month, 10),
            second: parseInt(second, 10),
            year: parseInt(year, 10)
        };

        // Validate each component with descriptive error messages
        if (dateComponents.month < 1 || dateComponents.month > 12) {
            return { error: `Month "${dateComponents.month}" is not valid. Please use 01-12.` };
        }
        if (dateComponents.day < 1 || dateComponents.day > 31) {
            return { error: `Day "${dateComponents.day}" is not valid. Please use 01-31.` };
        }
        if (dateComponents.hour < 0 || dateComponents.hour > 23) {
            return { error: `Hour "${dateComponents.hour}" is not valid. Please use 00-23 (24-hour format).` };
        }
        if (dateComponents.minute < 0 || dateComponents.minute > 59) {
            return { error: `Minute "${dateComponents.minute}" is not valid. Please use 00-59.` };
        }
        if (dateComponents.second < 0 || dateComponents.second > 59) {
            return { error: `Second "${dateComponents.second}" is not valid. Please use 00-59.` };
        }

        const date = new Date(
            dateComponents.year,
            dateComponents.month - 1, // JavaScript months are 0-indexed
            dateComponents.day,
            dateComponents.hour,
            dateComponents.minute,
            dateComponents.second
        );

        // Verify the date is valid and matches our components
        if (isNaN(date.getTime())) {
            return { error: `"${expression}" is not a valid date. Please check your date and time.` };
        }

        // Check if the date components match what we parsed (catches invalid dates like Feb 30)
        if (
            date.getFullYear() !== dateComponents.year ||
            date.getMonth() !== dateComponents.month - 1 ||
            date.getDate() !== dateComponents.day ||
            date.getHours() !== dateComponents.hour ||
            date.getMinutes() !== dateComponents.minute ||
            date.getSeconds() !== dateComponents.second
        ) {
            return { error: `"${expression}" is not a valid date. For example, February 30th doesn't exist.` };
        }

        return {
            expression,
            type: 'absolute',
            value: date
        };
    } catch {
        const today = new Date().toISOString().split('T')[0];
        return {
            error: `Invalid date format: "${expression}". Please use formats like "${today}T14:30:00" or "${today}T09:15".`
        };
    }
}

/**
 * Parse Elasticsearch-style range with boundaries
 */
function parseElasticsearchRange(startExpr: string, endExpr: string, startBoundary: RangeBoundaryType, endBoundary: RangeBoundaryType): DateMathResult {
    // Handle wildcard cases
    if (startExpr === '*' && endExpr === '*') {
        return { error: 'Cannot use wildcards (*) for both start and end times.' };
    }

    let start: DateMathError | ParsedTimeExpression;
    let end: DateMathError | ParsedTimeExpression;

    if (startExpr === '*') {
        // Open-ended start range: {* TO 2012-01-01}
        start = { expression: 'now-1000y', offset: -1000, round: null, type: 'relative', unit: 'y' };
    } else {
        start = parseTimeExpression(startExpr);
    }

    if (endExpr === '*') {
        // Open-ended end range: [2012-01-01 TO *]
        end = { expression: 'now+1000y', offset: 1000, round: null, type: 'relative', unit: 'y' };
    } else {
        end = parseTimeExpression(endExpr);
    }

    if ('error' in start) {
        return { error: `Problem with start time: ${start.error}` };
    }

    if ('error' in end) {
        return { error: `Problem with end time: ${end.error}` };
    }

    return {
        end,
        endBoundary,
        isPointInTime: false,
        start,
        startBoundary
    };
}

/**
 * Parse range expression (e.g., "now-5m TO now", "[start TO end]")
 */
function parseRangeExpression(expression: string): DateMathResult {
    // Try Elasticsearch range format first: [start TO end] or {start TO end}
    const elasticsearchMatch = expression.match(/^([[{])\s*(.+?)\s+TO\s+(.+?)\s*([\]}])$/i);
    if (elasticsearchMatch) {
        const [, startBracket, startExpr, endExpr, endBracket] = elasticsearchMatch;

        const startBoundary: RangeBoundaryType = startBracket === '[' ? 'inclusive' : 'exclusive';
        const endBoundary: RangeBoundaryType = endBracket === ']' ? 'inclusive' : 'exclusive';

        return parseElasticsearchRange(startExpr!.trim(), endExpr!.trim(), startBoundary, endBoundary);
    }

    // Try simple "start TO end" or "start to end" format (support both for compatibility)
    const simpleMatch = expression.match(/^(.+?)\s+(to|TO)\s+(.+)$/);
    if (simpleMatch) {
        const [, startExpr, , endExpr] = simpleMatch;
        return parseSimpleRange(startExpr!.trim(), endExpr!.trim());
    }

    const today = new Date().toISOString().split('T')[0];
    return { error: `Invalid range format. Try "now-1h TO now" for the last hour, or "[${today}T00:00:00 TO ${today}T23:59:59]" for a specific range.` };
}

/**
 * Parse relative time expressions like "now", "now-5m", "now+2h", "now/d", "now-1w/w"
 */
function parseRelativeTimeExpression(expression: string): DateMathError | ParsedTimeExpression {
    // Parse Elasticsearch date math expressions like "now-5m", "now/d", "now-1w/w"
    const dateMatchPattern = /^now(?<offset>[+-]?\d+)?(?<unit>[a-zA-Z]{1,2})?(?:\/(?<roundUnit>[a-zA-Z]{1,2}))?$/;
    const match = expression.match(dateMatchPattern);

    if (!match?.groups) {
        return {
            error: `Invalid time expression: "${expression}". Try examples like "now-5m" (5 minutes ago), "now/d" (start of today), or "now+2h" (2 hours from now).`
        };
    }

    const { offset: offsetStr = '0', roundUnit = null, unit = 's' } = match.groups;
    const offset = parseInt(offsetStr, 10);
    if (isNaN(offset)) {
        return {
            error: `Invalid number: "${offsetStr}". Please use a whole number like "5" in "now-5m".`
        };
    }

    if (!isValidTimeUnit(unit)) {
        return {
            error: `Unknown time unit: "${unit}". Use ${Object.entries(TIME_UNIT_NAMES)
                .map(([u, name]) => `${u} (${name})`)
                .join(', ')}.`
        };
    }

    if (roundUnit && !isValidTimeUnit(roundUnit)) {
        return {
            error: `Unknown rounding unit: "${roundUnit}". Use ${Object.entries(TIME_UNIT_NAMES)
                .map(([u, name]) => `${u} (${name})`)
                .join(', ')}.`
        };
    }

    return {
        expression,
        offset,
        round: roundUnit as null | TimeUnit,
        type: 'relative',
        unit: unit as TimeUnit
    };
}

/**
 * Parse simple "start TO end" format (always inclusive)
 */
function parseSimpleRange(startExpr: string, endExpr: string): DateMathResult {
    const start = parseTimeExpression(startExpr);
    const end = parseTimeExpression(endExpr);

    if ('error' in start) {
        return { error: `Problem with start time: ${start.error}` };
    }

    if ('error' in end) {
        return { error: `Problem with end time: ${end.error}` };
    }

    return {
        end,
        endBoundary: 'inclusive',
        isPointInTime: false,
        start,
        startBoundary: 'inclusive'
    };
}

/**
 * Parse single expression as a point-in-time range
 */
function parseSingleExpression(expression: string): DateMathResult {
    const parsed = parseTimeExpression(expression);
    if ('error' in parsed) {
        return parsed;
    }

    return {
        end: parsed,
        endBoundary: 'inclusive',
        isPointInTime: true,
        start: parsed,
        startBoundary: 'inclusive'
    };
}

/**
 * Core time expression parser (handles individual expressions like "now-5m", "2025-09-20T14:30:00")
 */
function parseTimeExpression(expression: string): DateMathError | ParsedTimeExpression {
    if (!expression || !expression.trim()) {
        const today = new Date().toISOString().split('T')[0];
        return { error: `Please enter a time. Try "now-5m" for 5 minutes ago or "${today}T14:30:00" for a specific time.` };
    }

    const trimmed = expression.trim();
    if (trimmed.startsWith('now')) {
        return parseRelativeTimeExpression(trimmed);
    } else {
        return parseAbsoluteTimeExpression(trimmed);
    }
}

/**
 * Resolve a parsed time expression to actual Date
 */
function resolveTimeExpression(parsed: ParsedTimeExpression, referenceTime?: Date): Date {
    const now = referenceTime || new Date();

    if (parsed.type === 'absolute') {
        // For absolute times, we already have the date
        return parsed.value;
    } else {
        let result = new Date(now);

        // Apply the offset
        switch (parsed.unit) {
            case 'd':
                result.setDate(result.getDate() + parsed.offset);
                break;
            case 'h':
                result.setHours(result.getHours() + parsed.offset);
                break;
            case 'm':
                result.setMinutes(result.getMinutes() + parsed.offset);
                break;
            case 'M':
                result.setMonth(result.getMonth() + parsed.offset);
                break;
            case 'Q':
                result.setMonth(result.getMonth() + parsed.offset * 3);
                break;
            case 's':
                result.setSeconds(result.getSeconds() + parsed.offset);
                break;
            case 'w':
                result.setDate(result.getDate() + parsed.offset * 7);
                break;
            case 'y':
                result.setFullYear(result.getFullYear() + parsed.offset);
                break;
        }

        if (parsed.round) {
            result = roundDate(result, parsed.round);
        }

        return result;
    }
}

// ===== HUMAN-READABLE TIME PARAMETER PARSING =====
// This functionality was moved from date-filter-utils.ts for consolidation

/**
 * Round a date to the specified unit
 */
function roundDate(date: Date, unit: TimeUnit): Date {
    const result = new Date(date);

    switch (unit) {
        case 'd':
            result.setHours(0, 0, 0, 0);
            break;
        case 'h':
            result.setMinutes(0, 0, 0);
            break;
        case 'm':
            result.setSeconds(0, 0);
            break;
        case 'M':
            result.setDate(1);
            result.setHours(0, 0, 0, 0);
            break;
        case 'Q': {
            // Round to start of quarter
            const quarter = Math.floor(result.getMonth() / 3);
            result.setMonth(quarter * 3, 1);
            result.setHours(0, 0, 0, 0);
            break;
        }
        case 's':
            result.setMilliseconds(0);
            break;
        case 'w': {
            // Round to start of week (Sunday)
            const dayOfWeek = result.getDay();
            result.setDate(result.getDate() - dayOfWeek);
            result.setHours(0, 0, 0, 0);
            break;
        }
        case 'y':
            result.setMonth(0, 1);
            result.setHours(0, 0, 0, 0);
            break;
    }

    return result;
}

/** Regular expression to match custom date ranges in format: YYYY-MM-DDTHH:mm:ss-YYYY-MM-DDTHH:mm:ss */
const DATE_RANGE_REGEX = /(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}(?::\d{2})?(?:[+-]\d{2}:?\d{2}|Z)?)?)/g;

/**
 * Parse a human-readable time string and return a time range with start and end dates.
 * Supports natural language time expressions like "last week", "today so far", and custom date ranges.
 * This is a legacy function for compatibility - prefer using parseDateMath() for Elasticsearch date math expressions.
 *
 * @param time The human-readable time string (e.g., "last week", "last 30 days", "today so far")
 * @returns A DateRange object with start and end dates
 */
export function parseDateMathRange(time: string): DateRange {
    const trimmedTime = time?.trim() ?? '';
    const normalizedTime = trimmedTime.toLowerCase();

    if (trimmedTime === '' || normalizedTime === 'all' || normalizedTime === 'all time') {
        return {
            end: new Date(),
            start: new Date('1900-01-01')
        };
    }

    const now = new Date();
    const dayInMs = 24 * 60 * 60 * 1000;

    const lastMatch = normalizedTime.match(/^last\s+(?:(\d+)\s+)?(minute|minutes|hour|hours|day|days|week|weeks|month|months|quarter|quarters|year|years)$/);
    if (lastMatch) {
        const amount = Number.parseInt(lastMatch[1] ?? '1', 10);
        const unit = lastMatch[2];

        switch (unit) {
            case 'day':
            case 'days': {
                const start = new Date(now.getTime() - amount * dayInMs);
                return { end: now, start };
            }
            case 'hour':
            case 'hours': {
                const start = new Date(now.getTime() - amount * 60 * 60 * 1000);
                return { end: now, start };
            }
            case 'minute':
            case 'minutes': {
                const start = new Date(now.getTime() - amount * 60 * 1000);
                return { end: now, start };
            }
            case 'month':
            case 'months': {
                const start = subtractMonths(now, amount);
                return { end: now, start };
            }
            case 'quarter':
            case 'quarters': {
                const start = subtractMonths(now, amount * 3);
                return { end: now, start };
            }
            case 'week':
            case 'weeks': {
                const start = new Date(now.getTime() - amount * 7 * dayInMs);
                return { end: now, start };
            }
            case 'year':
            case 'years': {
                const start = subtractYears(now, amount);
                return { end: now, start };
            }
        }
    }

    if (normalizedTime === 'today so far' || normalizedTime === 'this day so far') {
        const start = startOfDay(now);
        return { end: now, start };
    }

    const thisSoFarMatch = normalizedTime.match(/^this\s+(week|month|quarter|year)\s+so\s+far$/);
    if (thisSoFarMatch) {
        const period = thisSoFarMatch[1];
        switch (period) {
            case 'month':
                return { end: now, start: startOfMonth(now) };
            case 'quarter':
                return { end: now, start: startOfQuarter(now) };
            case 'week':
                return { end: now, start: startOfWeek(now) };
            case 'year':
                return { end: now, start: startOfYear(now) };
        }
    }

    const previousMatch = normalizedTime.match(/^previous\s+(day|week|month|quarter|year)$/);
    if (previousMatch) {
        const period = previousMatch[1];
        switch (period) {
            case 'day': {
                const start = startOfDay(new Date(startOfDay(now).getTime() - dayInMs));
                return { end: endOfDay(start), start };
            }
            case 'month': {
                const currentMonthStart = startOfMonth(now);
                const previousMonthEnd = new Date(currentMonthStart.getTime() - 1);
                const start = startOfMonth(previousMonthEnd);
                return { end: endOfMonth(previousMonthEnd), start };
            }
            case 'quarter': {
                const currentQuarterStart = startOfQuarter(now);
                const previousQuarterEnd = new Date(currentQuarterStart.getTime() - 1);
                const start = startOfQuarter(previousQuarterEnd);
                return { end: endOfQuarter(previousQuarterEnd), start };
            }
            case 'week': {
                const currentWeekStart = startOfWeek(now);
                const previousWeekEnd = new Date(currentWeekStart.getTime() - 1);
                const start = startOfWeek(previousWeekEnd);
                return { end: previousWeekEnd, start };
            }
            case 'year': {
                const start = startOfYear(new Date(now.getFullYear() - 1, 0, 1));
                return { end: endOfYear(start), start };
            }
        }
    }

    if (normalizedTime === 'yesterday') {
        const start = startOfDay(new Date(now.getTime() - dayInMs));
        return { end: endOfDay(start), start };
    }

    const explicitRange = parseExplicitDelimiterRange(trimmedTime);
    if (explicitRange) {
        return explicitRange;
    }

    // Try to parse as a custom date range using regex (e.g., "2024-01-01T00:00:00 TO 2024-01-31T23:59:59")
    const customRange = parseCustomDateRange(trimmedTime);
    if (customRange) {
        const start = new Date(customRange.start);
        const end = new Date(customRange.end);
        if (!isNaN(start.getTime()) && !isNaN(end.getTime())) {
            return { end, start };
        }
    }

    // Fallback to 'last week' for unknown formats
    const fallbackStart = new Date(now.getTime() - 7 * dayInMs);
    return { end: now, start: fallbackStart };
}

// === Helper functions for parseTimeParameter ===

function endOfDay(date: Date) {
    const result = startOfDay(date);
    result.setHours(23, 59, 59, 999);
    return result;
}

function endOfMonth(date: Date) {
    const start = startOfMonth(date);
    const nextMonth = new Date(start.getFullYear(), start.getMonth() + 1, 1);
    return new Date(nextMonth.getTime() - 1);
}

function endOfQuarter(date: Date) {
    const start = startOfQuarter(date);
    const nextQuarter = new Date(start.getFullYear(), start.getMonth() + 3, 1);
    return new Date(nextQuarter.getTime() - 1);
}

function endOfYear(date: Date) {
    const start = startOfYear(date);
    const nextYear = new Date(start.getFullYear() + 1, 0, 1);
    return new Date(nextYear.getTime() - 1);
}

function hasIncompleteTimestamp(value: string): boolean {
    if (!value.includes('T')) {
        return false; // Date-only is valid
    }

    const timePart = value.split('T')[1]?.split('|')[0]; // Remove timezone metadata
    if (!timePart) {
        return true; // T present but no time part
    }

    // Must have at least hour:minute format
    const timeComponents = timePart.split(':');
    return timeComponents.length < 2;
}

function parseCustomDateRange(input: string): null | { end: string; start: string } {
    if (!input) {
        return null;
    }

    // Quick rejection: if pattern has a single hours-only timestamp token (YYYY-MM-DDTHH) treat as invalid to trigger fallback
    const hoursOnlyToken = /\d{4}-\d{2}-\d{2}T\d{2}(?:$|[^:\d])/; // no colon following the hour
    if (hoursOnlyToken.test(input)) {
        return null;
    }

    // Fall back to the original regex-based approach for other formats
    const matches: string[] = [];
    let found: null | RegExpExecArray;

    // Reset regex lastIndex to ensure consistent parsing
    DATE_RANGE_REGEX.lastIndex = 0;

    while ((found = DATE_RANGE_REGEX.exec(input))) {
        matches.push(found[0]);
    }

    if (matches.length === 2 && matches[0] && matches[1]) {
        // Reject if either match ends with hours-only timestamp (YYYY-MM-DDTHH)
        const hoursOnly = /T\d{2}$/;
        if (hoursOnly.test(matches[0]) || hoursOnly.test(matches[1])) {
            return null;
        }
        return {
            end: matches[1],
            start: matches[0]
        };
    }

    return null;
}

function parseDateString(value: string) {
    if (!value) {
        return new Date(NaN);
    }

    // Validate that the date string is in a complete format
    // Reject incomplete time formats like "2024-01-01T10" (missing minutes/seconds)
    if (value.includes('T')) {
        const timePart = value.split('T')[1];
        // Explicitly reject hours-only (e.g., 2024-01-01T10)
        if (timePart && /^\d{2}$/.test(timePart)) {
            return new Date(NaN);
        }
        if (timePart && !timePart.includes(':')) {
            // Time part exists but has no colons (incomplete format)
            return new Date(NaN);
        }
        if (timePart && timePart.split(':').length < 2) {
            // Time part has fewer than 2 components (hour:minute minimum)
            return new Date(NaN);
        }
    }

    const parsed = new Date(value);
    if (!Number.isNaN(parsed.getTime())) {
        return parsed;
    }

    return new Date(`${value}Z`);
}

function parseExplicitDelimiterRange(value: string): null | DateRange {
    if (!value.includes('..')) {
        return null;
    }

    const [startPart, endPart] = value.split('..');
    if (!startPart || !endPart) {
        return null;
    }

    // Check for incomplete timestamp formats before parsing
    // If the input contains T but incomplete time parts, reject it
    if (hasIncompleteTimestamp(startPart) || hasIncompleteTimestamp(endPart)) {
        return null;
    }

    const startIso = sanitizeIsoSegment(startPart);
    const endIso = sanitizeIsoSegment(endPart);

    if (!startIso || !endIso) {
        return null;
    }

    const start = parseDateString(startIso);
    const end = parseDateString(endIso);

    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
        return null;
    }

    return { end, start };
}

function sanitizeIsoSegment(segment: string) {
    return segment.split('|')[0]?.trim() ?? '';
}

function startOfDay(date: Date) {
    const result = new Date(date);
    result.setHours(0, 0, 0, 0);
    return result;
}

function startOfMonth(date: Date) {
    return new Date(date.getFullYear(), date.getMonth(), 1);
}

function startOfQuarter(date: Date) {
    const quarter = Math.floor(date.getMonth() / 3);
    const startMonth = quarter * 3;
    return new Date(date.getFullYear(), startMonth, 1);
}

function startOfWeek(date: Date) {
    const result = startOfDay(date);
    const diffToMonday = (result.getDay() + 6) % 7;
    result.setDate(result.getDate() - diffToMonday);
    return result;
}

function startOfYear(date: Date) {
    return new Date(date.getFullYear(), 0, 1);
}

function subtractMonths(date: Date, months: number) {
    const result = new Date(date);
    result.setMonth(result.getMonth() - months);
    return result;
}

function subtractYears(date: Date, years: number) {
    const result = new Date(date);
    result.setFullYear(result.getFullYear() - years);
    return result;
}
