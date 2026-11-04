import { describe, expect, it } from 'vitest';

import {
    applyTaskUpdate,
    BackgroundTaskStatus,
    BulkOperationSelectionKind,
    FINISHED_TASK_VISIBLE_MS,
    getProgressPercent,
    selectVisibleTasks,
    type ViewBackgroundTask
} from './models';

function createTask(overrides: Partial<ViewBackgroundTask> = {}): ViewBackgroundTask {
    return {
        can_cancel: true,
        created_by_user_id: 'user-id',
        created_utc: '2026-01-01T10:00:00Z',
        description: 'Delete events matching filter',
        failed: 0,
        id: 'task-1',
        organization_id: 'organization-id',
        processed: 0,
        progress: 0,
        selection: { kind: BulkOperationSelectionKind.Filter },
        status: BackgroundTaskStatus.Running,
        succeeded: 0,
        total: 100,
        type: 'events.delete',
        updated_utc: '2026-01-01T10:00:00Z',
        ...overrides
    };
}

function createUpdate(overrides: Record<string, unknown> = {}) {
    return {
        can_cancel: true,
        description: 'Delete events matching filter',
        failed: 0,
        id: 'task-1',
        organization_id: 'organization-id',
        processed: 0,
        progress: 0,
        status: BackgroundTaskStatus.Running,
        succeeded: 0,
        total: 100,
        type: 'events.delete',
        updated_utc: '2026-01-01T10:00:05Z',
        ...overrides
    };
}

describe('applyTaskUpdate', () => {
    it('patches the progress of a known task', () => {
        // Arrange
        const tasks = [createTask(), createTask({ id: 'task-2' })];

        // Act
        const result = applyTaskUpdate(tasks, createUpdate({ failed: 1, processed: 40, succeeded: 39 }));

        // Assert
        expect(result?.[0]).toMatchObject({ failed: 1, processed: 40, succeeded: 39 });
        expect(result?.[1]).toBe(tasks[1]);
    });

    it('ignores a message that is older than what is already known', () => {
        // Arrange
        const tasks = [createTask({ processed: 60, updated_utc: '2026-01-01T10:00:10Z' })];

        // Act
        const result = applyTaskUpdate(tasks, createUpdate({ processed: 20 }));

        // Assert
        expect(result).toBe(tasks);
    });

    it('reports an unknown task so the list can be reloaded', () => {
        // Arrange
        const tasks = [createTask()];

        // Act
        const result = applyTaskUpdate(tasks, createUpdate({ id: 'started-elsewhere', status: BackgroundTaskStatus.Queued, total: 5 }));

        // Assert
        expect(result).toBeUndefined();
    });
});

describe('getProgressPercent', () => {
    it.each([
        [{ processed: 0, progress: 0, status: BackgroundTaskStatus.Queued, total: 0 }, 0],
        [{ processed: 50, progress: 0, status: BackgroundTaskStatus.Running, total: 200 }, 25],
        [{ processed: 5, progress: 0, status: BackgroundTaskStatus.Running, total: 3 }, 100],
        [{ processed: 3, progress: 0, status: BackgroundTaskStatus.Completed, total: 0 }, 100],
        [{ processed: 0, progress: 42, status: BackgroundTaskStatus.Running, total: 0 }, 42]
    ])('maps %j to %i percent', (task, expected) => {
        // Arrange & Act
        const percent = getProgressPercent(task);

        // Assert
        expect(percent).toBe(expected);
    });
});

describe('selectVisibleTasks', () => {
    const now = new Date('2026-01-01T10:30:00Z').getTime();

    it('keeps active tasks and recently finished ones, newest first', () => {
        // Arrange
        const active = createTask({ created_utc: '2026-01-01T09:00:00Z', id: 'active', updated_utc: '2026-01-01T09:00:00Z' });
        const recent = createTask({
            created_utc: '2026-01-01T10:20:00Z',
            id: 'recent',
            status: BackgroundTaskStatus.Completed,
            updated_utc: new Date(now - FINISHED_TASK_VISIBLE_MS + 1000).toISOString()
        });
        const old = createTask({
            id: 'old',
            status: BackgroundTaskStatus.Completed,
            updated_utc: new Date(now - FINISHED_TASK_VISIBLE_MS - 1000).toISOString()
        });

        // Act
        const result = selectVisibleTasks([active, old, recent], new Set(), now);

        // Assert
        expect(result.map((task) => task.id)).toEqual(['recent', 'active']);
    });

    it('hides dismissed tasks', () => {
        // Arrange
        const task = createTask({ id: 'dismissed' });

        // Act
        const result = selectVisibleTasks([task], new Set(['dismissed']), now);

        // Assert
        expect(result).toEqual([]);
    });
});
