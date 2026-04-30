import type { EventSessionSummaryData } from '../components/summary/index';
import type { PersistentEvent } from '../models';
import type { LogLevel } from '../models/event-data';

import { logLevels } from '../options';

export function getLogLevel(level?: LogLevel | null): LogLevel | null {
    switch (level?.toLowerCase().trim()) {
        case '0':
        case 'false':
        case 'no':
        case 'off':
            return 'off';
        case '1':
        case 'trace':
        case 'true':
        case 'yes':
            return 'trace';
        case 'debug':
            return 'debug';
        case 'error':
            return 'error';
        case 'fatal':
            return 'fatal';
        case 'info':
            return 'info';
        case 'warn':
            return 'warn';
        default:
            return level ?? null;
    }
}

export function getLogLevelDisplayName(level?: LogLevel | null): LogLevel | null {
    const resolvedLevel = getLogLevel(level);
    return logLevels.find((l) => l.value === resolvedLevel)?.label ?? level ?? null;
}

/**
 * Determine session ID from event.
 * For session start events, use reference_id
 * For other events, check @ref:session in data
 * @param event
 * @returns Session ID or undefined if not found
 */
export function getSessionId(event?: null | PersistentEvent): string | undefined {
    if (!event) {
        return undefined;
    }

    // For session start events, use reference_id
    if (event.type === 'session') {
        return event.reference_id ?? undefined;
    }

    // For other events, check @ref:session in data
    return event.data?.['@ref:session'] as string | undefined;
}

/**
 * Returns the session duration for a given event.
 * If the session has ended, returns a numeric duration in milliseconds.
 * If the session is active, returns the session start date so Duration component live-updates.
 */
export function getSessionStartDuration(event: PersistentEvent): Date | number {
    if (event.data?.sessionend) {
        if (event.value != null) {
            return event.value * 1000;
        }

        if (event.date) {
            return new Date(event.data.sessionend).getTime() - new Date(event.date).getTime();
        }

        return 0;
    }

    // Return start date so Duration component live-updates via interval
    return new Date(event.date);
}

/**
 * Returns session duration from summary data (used in table cells).
 * For active sessions, returns the event date so Duration component live-updates.
 * For ended sessions, returns a numeric duration in milliseconds.
 */
export function getSessionSummaryDuration(data: EventSessionSummaryData | undefined, eventDate?: string): Date | number | undefined {
    if (!data) {
        return undefined;
    }

    const isActive = !data.SessionEnd;
    if (isActive) {
        return eventDate ? new Date(eventDate) : undefined;
    }

    if (data.Value) {
        return parseFloat(data.Value) * 1000;
    }

    if (data.SessionEnd && eventDate) {
        return new Date(data.SessionEnd).getTime() - new Date(eventDate).getTime();
    }

    return 0;
}
