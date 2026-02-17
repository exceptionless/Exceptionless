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
 * Returns the session duration in milliseconds for a given event.
 * If the session has ended, uses the event value or the difference between sessionend and start.
 * If the session is active, returns the duration from start to now.
 */
export function getSessionStartDuration(event: PersistentEvent): number {
    if (event.data?.sessionend) {
        if (event.value) {
            return event.value * 1000;
        }
        if (event.date) {
            return new Date(event.data.sessionend).getTime() - new Date(event.date).getTime();
        }
        return 0;
    }
    // If session is active, duration is from start to now
    return Date.now() - new Date(event.date).getTime();
}
