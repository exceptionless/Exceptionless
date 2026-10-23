let historyTraversalCount = 0;
let isTrackingHistoryTraversals = false;

/** The number of Back and Forward traversals since tracking started. */
export function getHistoryTraversalCount(): number {
    return historyTraversalCount;
}

/**
 * Counts Back and Forward traversals. SvelteKit applies the destination's page state in its own
 * popstate listener, and browsers run effects that track `page.state` before later listeners, so
 * call this before the router starts (from the client `init` hook). Code that reacts to
 * `page.state` can then tell a traversal from a shallow history write.
 */
export function trackHistoryTraversals(): void {
    if (isTrackingHistoryTraversals) {
        return;
    }

    isTrackingHistoryTraversals = true;
    window.addEventListener('popstate', () => historyTraversalCount++);
}
