import type { RowSelectionState } from '@tanstack/svelte-table';

export interface AllMatchingSelection {
    /** How many events matched when the user chose "select all matching". */
    count: number;
    /** The query the selection was made for. */
    identity: string;
}

export interface AllMatchingSelectionSnapshot {
    identity: string;
    rowSelection: RowSelectionState | undefined;
}

/** Everything matching the current filter, as passed to an action menu. */
export interface AllMatchingTarget {
    count: number;
    filter?: string;
    offset?: string;
    organizationId: string;
    time?: string;
}

/**
 * Tracks the Gmail-style "everything matching this filter" selection. The selection belongs to the query it was made
 * for: as soon as the query identity (organization, filter, time range, saved view) changes it is no longer active, so
 * an action can never run against a different set of events than the user confirmed.
 */
export function createAllMatchingSelection(getIdentity: () => string, getRowSelection?: () => RowSelectionState) {
    let selection = $state<AllMatchingSelection>();

    return {
        get active(): AllMatchingSelection | undefined {
            return selection && selection.identity === getIdentity() ? selection : undefined;
        },
        clear() {
            selection = undefined;
        },
        /**
         * Selects everything matching the current query. A pending count must not replace a newer filter or page
         * selection, including clearing and then selecting the same rows again.
         */
        select(count: number, expected?: AllMatchingSelectionSnapshot): boolean {
            const identity = getIdentity();
            if (expected && (expected.identity !== identity || expected.rowSelection !== getRowSelection?.())) {
                return false;
            }

            selection = {
                count,
                identity
            };
            return true;
        },
        /** Capture the query and page selection before awaiting a count, then pass the snapshot to select. */
        get snapshot(): AllMatchingSelectionSnapshot {
            return {
                identity: getIdentity(),
                rowSelection: getRowSelection?.()
            };
        }
    };
}

export function getEventsQueryIdentity(parts: { filter?: null | string; organizationId?: string; savedViewId?: string; time?: null | string }): string {
    return JSON.stringify([parts.organizationId ?? null, parts.filter ?? null, parts.time ?? null, parts.savedViewId ?? null]);
}
