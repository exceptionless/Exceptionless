import type { BulkOperationUpdated } from '#features/websockets/models.ts';

import { BulkOperationSelectionKind, BulkOperationStatus, type NewBulkOperation, type ViewBulkOperation } from '#generated/api.ts';

export { BulkOperationSelectionKind, BulkOperationStatus, type NewBulkOperation, type ViewBulkOperation };

export const EVENTS_DELETE_OPERATION = 'events.delete';

/** Finished operations stay visible in the panel for this long, unless dismissed. */
export const FINISHED_OPERATION_VISIBLE_MS = 2 * 60 * 1000;

export function applyBulkOperationUpdate(operations: ViewBulkOperation[], update: BulkOperationUpdated): undefined | ViewBulkOperation[] {
    const index = operations.findIndex((operation) => operation.id === update.id);
    if (index < 0) {
        return undefined;
    }

    const current = operations[index]!;
    if (new Date(update.updated_utc).getTime() < new Date(current.updated_utc).getTime()) {
        // Messages can arrive out of order; never move an operation backwards.
        return operations;
    }

    const next = [...operations];
    next[index] = {
        ...current,
        description: update.description,
        failed: update.failed,
        message: update.message ?? null,
        processed: update.processed,
        status: update.status,
        succeeded: update.succeeded,
        total: update.total,
        updated_utc: update.updated_utc
    };

    return next;
}

export function getProgressPercent(operation: Pick<ViewBulkOperation, 'processed' | 'status' | 'total'>): number {
    if (operation.status === BulkOperationStatus.Completed) {
        return 100;
    }

    if (operation.total <= 0) {
        return 0;
    }

    return Math.min(100, Math.round((operation.processed / operation.total) * 100));
}

export function isBulkOperationActive(status: BulkOperationStatus): boolean {
    return status === BulkOperationStatus.Queued || status === BulkOperationStatus.Running;
}

/** Active operations plus recently finished ones the user has not dismissed, newest first. */
export function selectVisibleOperations(operations: ViewBulkOperation[], dismissedIds: ReadonlySet<string>, now: number): ViewBulkOperation[] {
    return operations
        .filter((operation) => {
            if (dismissedIds.has(operation.id)) {
                return false;
            }

            return isBulkOperationActive(operation.status) || now - new Date(operation.updated_utc).getTime() < FINISHED_OPERATION_VISIBLE_MS;
        })
        .sort((a, b) => new Date(b.created_utc).getTime() - new Date(a.created_utc).getTime());
}
