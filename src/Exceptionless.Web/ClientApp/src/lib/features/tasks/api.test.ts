import { beforeEach, describe, expect, it, vi } from 'vitest';

import { BackgroundTaskStatus, BulkOperationSelectionKind, type ViewBackgroundTask } from './models';

vi.mock('#features/auth/index.svelte.ts', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('#features/events/api.svelte.ts', () => ({ queryKeys: { type: ['PersistentEvent'] } }));
vi.mock('#features/stacks/api.svelte.ts', () => ({ queryKeys: { type: ['Stack'] } }));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: vi.fn() }));
vi.mock('@tanstack/svelte-query', () => ({ createMutation: vi.fn(), createQuery: vi.fn(), useQueryClient: vi.fn() }));

import { invalidateTaskAffectedQueries, queryKeys, updateTaskFromMessage } from './api.svelte';

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

function createUpdate(id: string) {
    return {
        can_cancel: true,
        description: 'Delete events matching filter',
        failed: 0,
        id,
        organization_id: 'organization-id',
        processed: 75,
        progress: 75,
        status: BackgroundTaskStatus.Running,
        succeeded: 75,
        total: 100,
        type: 'events.delete',
        updated_utc: '2026-01-01T10:00:09Z'
    };
}

describe('updateTaskFromMessage', () => {
    const queryClient = { getQueryData: vi.fn(), invalidateQueries: vi.fn(), setQueryData: vi.fn() };

    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('writes the progress of a known task into the cached list', () => {
        // Arrange
        queryClient.getQueryData.mockReturnValue([createTask()]);

        // Act
        updateTaskFromMessage(queryClient as never, createUpdate('task-1'));

        // Assert
        expect(queryClient.setQueryData).toHaveBeenCalledWith(queryKeys.organization('organization-id'), [
            expect.objectContaining({ id: 'task-1', processed: 75 })
        ]);
        expect(queryClient.invalidateQueries).not.toHaveBeenCalled();
    });

    it('reloads the list when the task was started somewhere else', () => {
        // Arrange
        queryClient.getQueryData.mockReturnValue([createTask()]);

        // Act
        updateTaskFromMessage(queryClient as never, createUpdate('started-elsewhere'));

        // Assert
        expect(queryClient.setQueryData).not.toHaveBeenCalled();
        expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: queryKeys.organization('organization-id') });
    });
});

describe('invalidateTaskAffectedQueries', () => {
    it('refreshes event and stack queries', async () => {
        // Arrange
        const queryClient = { invalidateQueries: vi.fn().mockResolvedValue(undefined) };

        // Act
        await invalidateTaskAffectedQueries(queryClient as never);

        // Assert
        expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ['PersistentEvent'] });
        expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ['Stack'] });
    });
});
