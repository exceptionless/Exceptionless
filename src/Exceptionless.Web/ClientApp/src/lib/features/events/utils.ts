import type { PersistentEvent } from '$features/events/models';

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
