import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { BackgroundTaskStatus, BulkOperationSelectionKind, type ViewBackgroundTask } from '../models';
import TaskItem from './task-item.svelte';

function createTask(overrides: Partial<ViewBackgroundTask> = {}): ViewBackgroundTask {
    return {
        can_cancel: true,
        created_by_user_id: 'user-id',
        created_utc: '2026-01-01T10:00:00Z',
        description: 'Delete 1,234 events matching filter',
        failed: 0,
        id: 'task-1',
        organization_id: 'organization-id',
        processed: 500,
        progress: 0,
        selection: { kind: BulkOperationSelectionKind.Filter },
        status: BackgroundTaskStatus.Running,
        succeeded: 500,
        total: 1234,
        type: 'events.delete',
        updated_utc: '2026-01-01T10:00:00Z',
        ...overrides
    };
}

function statusText(): string {
    return (document.querySelector('[data-slot="task-progress"]')?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('TaskItem', () => {
    afterEach(cleanup);

    it('shows the description, x of y progress and a Cancel button while running', async () => {
        // Arrange
        const onCancel = vi.fn();
        const task = createTask();
        render(TaskItem, { props: { canCancel: true, onCancel, onDismiss: vi.fn(), task } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Cancel / }));

        // Assert
        expect(screen.getByText('Delete 1,234 events matching filter')).toBeTruthy();
        expect(statusText()).toBe('500 of 1,234');
        expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('41');
        expect(onCancel).toHaveBeenCalledWith(task);
        expect(screen.queryByRole('button', { name: /^Dismiss / })).toBeNull();
    });

    it('names the task on its buttons and announces only state changes, not every progress update', async () => {
        // Arrange
        const { rerender } = render(TaskItem, { props: { onCancel: vi.fn(), onDismiss: vi.fn(), task: createTask({ processed: 100 }) } });
        const announcement = () => screen.getByRole('status').textContent;
        const initial = announcement();

        // Act
        await rerender({ task: createTask({ processed: 900 }) });
        const afterProgress = announcement();
        await rerender({ task: createTask({ processed: 1234, status: BackgroundTaskStatus.Completed }) });

        // Assert
        expect(initial).toBe('Delete 1,234 events matching filter: in progress');
        expect(afterProgress).toBe(initial);
        expect(announcement()).toBe('Delete 1,234 events matching filter: completed');
        expect(screen.getByRole('button', { name: 'Dismiss Delete 1,234 events matching filter' })).toBeTruthy();
    });

    it('labels Cancel with the task it cancels', () => {
        // Arrange & Act
        render(TaskItem, { props: { canCancel: true, onCancel: vi.fn(), onDismiss: vi.fn(), task: createTask() } });

        // Assert
        expect(screen.getByRole('button', { name: 'Cancel Delete 1,234 events matching filter' })).toBeTruthy();
    });

    it('shows the queued state without a count', () => {
        // Arrange & Act
        render(TaskItem, {
            props: { canCancel: true, onCancel: vi.fn(), onDismiss: vi.fn(), task: createTask({ processed: 0, status: BackgroundTaskStatus.Queued }) }
        });

        // Assert
        expect(statusText()).toBe('Waiting to start');
        expect(screen.getByRole('button', { name: /^Cancel / })).toBeTruthy();
    });

    it('disables Cancel once cancellation was requested', () => {
        // Arrange & Act
        render(TaskItem, { props: { canCancel: true, isCancelling: true, onCancel: vi.fn(), onDismiss: vi.fn(), task: createTask() } });

        // Assert
        expect((screen.getByRole('button', { name: /^Cancel / }) as HTMLButtonElement).disabled).toBe(true);
    });

    it('shows a completed task with its failures and a Dismiss button', async () => {
        // Arrange
        const onDismiss = vi.fn();
        const task = createTask({ failed: 2, processed: 1234, status: BackgroundTaskStatus.Completed, succeeded: 1232 });
        render(TaskItem, { props: { onCancel: vi.fn(), onDismiss, task } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Dismiss / }));

        // Assert
        expect(statusText()).toBe('Completed · 1,232 processed, 2 failed');
        expect(screen.queryByRole('button', { name: /^Cancel / })).toBeNull();
        expect(onDismiss).toHaveBeenCalledWith(task);
    });

    it('shows how far a cancelled task got', () => {
        // Arrange & Act
        render(TaskItem, { props: { onCancel: vi.fn(), onDismiss: vi.fn(), task: createTask({ status: BackgroundTaskStatus.Cancelled }) } });

        // Assert
        expect(statusText()).toBe('Cancelled after 500 of 1,234');
        expect(screen.queryByRole('progressbar')).toBeNull();
    });

    it('shows why a task failed', () => {
        // Arrange & Act
        render(TaskItem, {
            props: {
                onCancel: vi.fn(),
                onDismiss: vi.fn(),
                task: createTask({ message: 'The task stopped because of an unexpected error.', status: BackgroundTaskStatus.Failed })
            }
        });

        // Assert
        expect(statusText()).toBe('Failed after 500 of 1,234 · The task stopped because of an unexpected error.');
    });

    it('shows the percent and message of a task that has no item counts', () => {
        // Arrange & Act
        render(TaskItem, {
            props: {
                onCancel: vi.fn(),
                onDismiss: vi.fn(),
                task: createTask({ can_cancel: false, message: 'Events removed: 12', processed: 0, progress: 50, total: 0, type: 'projects.reset-data' })
            }
        });

        // Assert
        expect(statusText()).toBe('50% · Events removed: 12');
        expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('50');
    });

    it('does not offer Cancel for a running task when cancelling is not allowed', () => {
        // Arrange & Act
        render(TaskItem, { props: { canCancel: false, onCancel: vi.fn(), onDismiss: vi.fn(), task: createTask({ can_cancel: false }) } });

        // Assert
        expect(screen.queryByRole('button', { name: /^Cancel / })).toBeNull();
        expect(screen.queryByRole('button', { name: /^Dismiss / })).toBeNull();
    });
});
