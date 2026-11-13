import type { BackgroundTaskUpdated } from '#features/websockets/models.ts';

import { BackgroundTaskStatus, BulkOperationSelectionKind, type NewBulkOperation, type ViewBackgroundTask } from '#generated/api.ts';

export { BackgroundTaskStatus, BulkOperationSelectionKind, type NewBulkOperation, type ViewBackgroundTask };

export const EVENTS_DELETE_OPERATION = 'events.delete';

/** Finished tasks stay visible in the panel for this long, unless dismissed. */
export const FINISHED_TASK_VISIBLE_MS = 2 * 60 * 1000;

export function applyTaskUpdate(tasks: ViewBackgroundTask[], update: BackgroundTaskUpdated): undefined | ViewBackgroundTask[] {
    const index = tasks.findIndex((task) => task.id === update.id);
    if (index < 0) {
        return undefined;
    }

    const current = tasks[index]!;
    if (new Date(update.updated_utc).getTime() < new Date(current.updated_utc).getTime()) {
        // Messages can arrive out of order; never move an task backwards.
        return tasks;
    }

    const next = [...tasks];
    next[index] = {
        ...current,
        can_cancel: update.can_cancel,
        description: update.description,
        failed: update.failed,
        message: update.message ?? null,
        processed: update.processed,
        progress: update.progress,
        status: update.status,
        succeeded: update.succeeded,
        total: update.total,
        updated_utc: update.updated_utc
    };

    return next;
}

/** Tasks that report item counts show those; tasks that only report a percent (total of zero) show the percent. */
export function getProgressPercent(task: Pick<ViewBackgroundTask, 'processed' | 'progress' | 'status' | 'total'>): number {
    if (task.status === BackgroundTaskStatus.Completed) {
        return 100;
    }

    if (task.total <= 0) {
        return Math.min(100, Math.max(0, task.progress ?? 0));
    }

    return Math.min(100, Math.round((task.processed / task.total) * 100));
}

export function hasCounts(task: Pick<ViewBackgroundTask, 'total'>): boolean {
    return task.total > 0;
}

/** An active task that has not reported progress for this long is abandoned, and the server lets anyone clear it. */
export const STALE_TASK_MS = 2 * 60 * 60 * 1000;

/** Whether the panel offers Cancel: the task can be stopped, or it is abandoned and only needs clearing. */
export function canCancelTask(task: Pick<ViewBackgroundTask, 'can_cancel' | 'status' | 'updated_utc'>, now: number): boolean {
    if (!isTaskActive(task.status)) {
        return false;
    }

    return task.can_cancel || now - new Date(task.updated_utc).getTime() >= STALE_TASK_MS;
}

export function isTaskActive(status: BackgroundTaskStatus): boolean {
    return status === BackgroundTaskStatus.Queued || status === BackgroundTaskStatus.Running;
}

/** Active tasks plus recently finished ones the user has not dismissed, newest first. */
export function selectVisibleTasks(tasks: ViewBackgroundTask[], dismissedIds: ReadonlySet<string>, now: number): ViewBackgroundTask[] {
    return tasks
        .filter((task) => {
            if (isTaskActive(task.status)) {
                return true;
            }

            if (dismissedIds.has(task.id)) {
                return false;
            }

            return now - new Date(task.updated_utc).getTime() < FINISHED_TASK_VISIBLE_MS;
        })
        .sort((a, b) => new Date(b.created_utc).getTime() - new Date(a.created_utc).getTime());
}
