export { getSessionStartDuration } from '../utils';
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
