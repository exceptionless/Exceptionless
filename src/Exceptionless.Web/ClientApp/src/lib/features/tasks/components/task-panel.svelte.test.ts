import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { BackgroundTaskStatus, BulkOperationSelectionKind, type ViewBackgroundTask } from '../models';

const mocks = vi.hoisted(() => ({
    cancelMutateAsync: vi.fn(),
    invalidateAffected: vi.fn(),
    queryClient: { id: 'query-client' },
    updateFromMessage: vi.fn()
}));

vi.mock('@tanstack/svelte-query', () => ({ useQueryClient: () => mocks.queryClient }));
vi.mock('svelte-sonner', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));
vi.mock('#features/organizations/context.svelte.ts', () => ({ organization: { current: 'organization-id' } }));
vi.mock('../api.svelte', async () => {
    const { SvelteMap } = await import('svelte/reactivity');
    const state = new SvelteMap<string, ViewBackgroundTask[]>();

    return {
        __state: state,
        cancelTask: () => ({ mutateAsync: mocks.cancelMutateAsync }),
        getTasksQuery: () => ({
            get data() {
                return state.get('tasks');
            }
        }),
        invalidateTaskAffectedQueries: mocks.invalidateAffected,
        updateTaskFromMessage: mocks.updateFromMessage
    };
});

import * as api from '../api.svelte';
import TaskPanel from './task-panel.svelte';

const state = (api as unknown as { __state: Map<string, ViewBackgroundTask[]> }).__state;

function createTask(overrides: Partial<ViewBackgroundTask> = {}): ViewBackgroundTask {
    const now = new Date().toISOString();
    return {
        can_cancel: true,
        created_by_user_id: 'user-id',
        created_utc: now,
        description: 'Delete 50 events matching filter',
        failed: 0,
        id: 'task-1',
        organization_id: 'organization-id',
        processed: 20,
        progress: 0,
        selection: { kind: BulkOperationSelectionKind.Filter },
        status: BackgroundTaskStatus.Running,
        succeeded: 20,
        total: 50,
        type: 'events.delete',
        updated_utc: now,
        ...overrides
    };
}

describe('TaskPanel', () => {
    beforeEach(() => {
        state.clear();
        mocks.cancelMutateAsync.mockResolvedValue(undefined);
    });

    afterEach(() => {
        cleanup();
        vi.useRealTimers();
    });

    it('hydrates running tasks from the list after a reload and ignores stale finished ones', async () => {
        // Arrange
        state.set('tasks', [createTask(), createTask({ id: 'long-finished', status: BackgroundTaskStatus.Completed, updated_utc: '2020-01-01T00:00:00Z' })]);

        // Act
        render(TaskPanel);
        await tick();

        // Assert
        expect(screen.getAllByText('Delete 50 events matching filter')).toHaveLength(1);
        expect(document.querySelector('[data-slot="task-progress"]')?.textContent?.replace(/\s+/g, ' ').trim()).toBe('20 of 50');
        expect(mocks.invalidateAffected).not.toHaveBeenCalled();
    });

    it('applies pushed updates for its organization and ignores other organizations', async () => {
        // Arrange
        state.set('tasks', [createTask()]);
        render(TaskPanel);
        const update = { id: 'task-1', organization_id: 'organization-id', processed: 30, status: BackgroundTaskStatus.Running };

        // Act
        document.dispatchEvent(new CustomEvent('BackgroundTaskUpdated', { detail: update }));
        document.dispatchEvent(new CustomEvent('BackgroundTaskUpdated', { detail: { ...update, organization_id: 'another-organization' } }));

        // Assert
        expect(mocks.updateFromMessage).toHaveBeenCalledTimes(1);
        expect(mocks.updateFromMessage).toHaveBeenCalledWith(mocks.queryClient, update);
    });

    it('refreshes events and stacks once when a running task finishes', async () => {
        // Arrange
        state.set('tasks', [createTask()]);
        render(TaskPanel);
        await tick();

        // Act
        state.set('tasks', [createTask({ processed: 50, status: BackgroundTaskStatus.Completed, succeeded: 50 })]);
        await tick();
        state.set('tasks', [createTask({ processed: 50, status: BackgroundTaskStatus.Completed, succeeded: 50, updated_utc: new Date().toISOString() })]);
        await tick();

        // Assert
        expect(mocks.invalidateAffected).toHaveBeenCalledTimes(1);
        expect(mocks.invalidateAffected).toHaveBeenCalledWith(mocks.queryClient);
        expect(document.querySelector('[data-slot="task-progress"]')?.textContent).toContain('Completed');
    });

    it('requests cancellation of a running task', async () => {
        // Arrange
        state.set('tasks', [createTask()]);
        render(TaskPanel);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Cancel / }));

        // Assert
        await waitFor(() => expect(mocks.cancelMutateAsync).toHaveBeenCalledWith('task-1'));
    });

    it('allows cancellation to be retried after the request finishes while the worker is still running', async () => {
        // Arrange
        const request = Promise.withResolvers<ViewBackgroundTask>();
        mocks.cancelMutateAsync.mockReturnValueOnce(request.promise);
        state.set('tasks', [createTask()]);
        render(TaskPanel);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Cancel / }));

        // Assert
        expect(screen.getByRole('button', { name: /^Cancel / })).toBeDisabled();

        // Act
        request.resolve(createTask());
        await tick();

        // Assert
        await waitFor(() => expect(screen.getByRole('button', { name: /^Cancel / })).toBeEnabled());

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Cancel / }));

        // Assert
        expect(mocks.cancelMutateAsync).toHaveBeenCalledTimes(2);
    });

    it('removes a finished task when it is dismissed', async () => {
        // Arrange
        state.set('tasks', [createTask({ status: BackgroundTaskStatus.Completed })]);
        render(TaskPanel);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Dismiss / }));

        // Assert
        expect(screen.queryByText('Delete 50 events matching filter')).toBeNull();
    });

    it('shows a dismissed failed task again during and after an automatic retry', async () => {
        // Arrange
        state.set('tasks', [createTask({ status: BackgroundTaskStatus.Failed })]);
        render(TaskPanel);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Dismiss / }));

        // Assert
        expect(screen.queryByText('Delete 50 events matching filter')).toBeNull();

        // Act
        state.set('tasks', [createTask()]);
        await tick();

        // Assert
        expect(screen.getByRole('button', { name: /^Cancel / })).toBeEnabled();

        // Act
        state.set('tasks', [createTask({ processed: 50, status: BackgroundTaskStatus.Completed, succeeded: 50 })]);
        await tick();

        // Assert
        expect(document.querySelector('[data-slot="task-progress"]')?.textContent).toContain('Completed');
        expect(mocks.invalidateAffected).toHaveBeenCalledOnce();
    });

    it('renders nothing when there is nothing to show', () => {
        // Arrange & Act
        render(TaskPanel);

        // Assert
        expect(screen.queryByRole('region', { name: 'Background tasks' })).toBeNull();
    });

    it('expires a finished task while another task keeps reporting progress', async () => {
        // Arrange
        vi.useFakeTimers();
        vi.setSystemTime('2026-01-01T10:00:00Z');
        const finished = createTask({ description: 'Finished task', id: 'finished', status: BackgroundTaskStatus.Completed });
        state.set('tasks', [createTask(), finished]);
        render(TaskPanel);
        await tick();

        // Act
        for (let progress = 1; progress <= 25; progress++) {
            await vi.advanceTimersByTimeAsync(5000);
            state.set('tasks', [createTask({ processed: progress }), finished]);
            await tick();
        }

        // Assert
        expect(screen.queryByText('Finished task')).toBeNull();
        expect(screen.getByText('Delete 50 events matching filter')).toBeVisible();
    });
});
