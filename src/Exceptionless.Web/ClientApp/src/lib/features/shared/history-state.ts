import { goto } from '$app/navigation';
import { navigating, page } from '$app/state';

export const detailSheetHistoryStateKey = '__exceptionlessDetailSheet';

export interface DetailSheetHistoryEntry {
    key: string;
    value: string;
}

export type DetailSheetPageState = App.PageState & { [detailSheetHistoryStateKey]?: DetailSheetHistoryEntry };

export interface ShallowHistoryUpdate {
    replace?: boolean;
    /** Derives the entry's page state from the state current when the write runs. */
    state?: (state: App.PageState) => App.PageState;
    /** Derives the URL from the address bar when the write runs; `undefined` skips the write. */
    url?: (url: URL) => string | undefined | URL;
}

let pendingWrites: Promise<void> = Promise.resolve();

export function getDetailSheetHistoryEntry(state: App.PageState, key: string): DetailSheetHistoryEntry | undefined {
    const entry = (state as DetailSheetPageState)[detailSheetHistoryStateKey];
    return entry?.key === key && typeof entry.value === 'string' ? entry : undefined;
}

export function hasDetailSheetHistoryEntry(state: App.PageState): boolean {
    const entry = (state as DetailSheetPageState)[detailSheetHistoryStateKey];
    return typeof entry?.key === 'string' && typeof entry.value === 'string';
}

/**
 * Updates the URL and page state without navigating (shallow routing).
 *
 * A shallow `goto` is itself a navigation and aborts one already in progress, so writes run
 * one at a time once the router is idle. Each write derives its URL and state when it runs,
 * so concurrent writers keep each other's parameters and markers, and a write is dropped if
 * the page that queued it has been navigated away from. State persists across reloads, like
 * the history entry it belongs to.
 */
export function updateShallowHistory({ replace = false, state = (current) => current, url = (current) => current }: ShallowHistoryUpdate): Promise<void> {
    const queuedPageUrl = page.url.href;
    const write = pendingWrites.then(async () => {
        while (navigating.complete) {
            await navigating.complete.catch(() => undefined);
        }

        const destination = page.url.href === queuedPageUrl ? url(new URL(window.location.href)) : undefined;
        if (destination !== undefined) {
            await goto(destination, { persistState: true, replace, shallow: true, state: state(page.state) });
        }
    });

    pendingWrites = write.catch(() => undefined);
    return write;
}

/** Resolves once no shallow history writes are waiting, including writes queued while waiting. */
export async function waitForShallowHistory(): Promise<void> {
    let writes: Promise<void>;
    do {
        writes = pendingWrites;
        await writes;
    } while (writes !== pendingWrites);
}

export function withoutDetailSheetHistoryEntry(state: App.PageState): App.PageState {
    const nextState = { ...state } as DetailSheetPageState;
    delete nextState[detailSheetHistoryStateKey];
    return nextState;
}
