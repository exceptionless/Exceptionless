import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { BackgroundTaskStatus, BulkOperationSelectionKind, type ViewBackgroundTask } from './models';

const mocks = vi.hoisted(() => ({
    createMutation: vi.fn<(options: () => { onSuccess: (task: ViewBackgroundTask) => Promise<unknown> | void }) => void>(),
    useQueryClient: vi.fn()
}));

vi.mock('#features/auth/index.svelte.ts', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('#features/events/api.svelte.ts', () => ({ queryKeys: { type: ['PersistentEvent'] } }));
vi.mock('#features/stacks/api.svelte.ts', () => ({ queryKeys: { type: ['Stack'] } }));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: vi.fn() }));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createMutation: mocks.createMutation,
    createQuery: vi.fn(),
    useQueryClient: mocks.useQueryClient
}));

import { cancelTask, invalidateTaskAffectedQueries, postBulkOperation, queryKeys, updateTaskFromMessage } from './api.svelte';

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
    const queryClient = { cancelQueries: vi.fn().mockResolvedValue(undefined), getQueryData: vi.fn(), invalidateQueries: vi.fn(), setQueryData: vi.fn() };

    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('writes the progress of a known task into the cached list', async () => {
        // Arrange
        queryClient.getQueryData.mockReturnValue([createTask()]);

        // Act
        await updateTaskFromMessage(queryClient as never, createUpdate('task-1'));

        // Assert
        expect(queryClient.setQueryData).toHaveBeenCalledWith(queryKeys.organization('organization-id'), [
            expect.objectContaining({ id: 'task-1', processed: 75 })
        ]);
        expect(queryClient.invalidateQueries).not.toHaveBeenCalled();
    });

    it('reloads the list when the task was started somewhere else', async () => {
        // Arrange
        queryClient.getQueryData.mockReturnValue([createTask()]);

        // Act
        await updateTaskFromMessage(queryClient as never, createUpdate('started-elsewhere'));

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

describe('task cache lifecycle', () => {
    let client: QueryClient;
    const key = queryKeys.organization('organization-id');

    beforeEach(() => {
        vi.clearAllMocks();
        client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
        mocks.useQueryClient.mockReturnValue(client);
    });

    afterEach(() => client.clear());

    it('keeps a newly queued task when an earlier list request returns without it', async () => {
        // Arrange
        const pending = Promise.withResolvers<ViewBackgroundTask[]>();
        const read = client.fetchQuery({ queryFn: () => pending.promise, queryKey: key }).catch(() => undefined);
        const task = createTask({ status: BackgroundTaskStatus.Queued });
        postBulkOperation();
        const options = mocks.createMutation.mock.calls[0]![0]();

        // Act
        await options.onSuccess(task);
        pending.resolve([]);
        await read;

        // Assert
        expect(client.getQueryData(key)).toEqual([task]);
    });

    it('preserves a newer pushed completion when cancellation returns an older running snapshot', async () => {
        // Arrange
        const running = createTask();
        client.setQueryData(key, [running]);
        const pending = Promise.withResolvers<ViewBackgroundTask[]>();
        const read = client.fetchQuery({ queryFn: () => pending.promise, queryKey: key }).catch(() => undefined);
        const completed = createTask({ status: BackgroundTaskStatus.Completed, updated_utc: '2026-01-01T10:01:00Z' });
        client.setQueryData(key, [completed]);
        cancelTask();
        const options = mocks.createMutation.mock.calls[0]![0]();

        // Act
        await options.onSuccess(running);
        pending.resolve([running]);
        await read;

        // Assert
        expect(client.getQueryData(key)).toEqual([completed]);
    });

    it.each([
        [BackgroundTaskStatus.Failed, BackgroundTaskStatus.Running],
        [BackgroundTaskStatus.Running, BackgroundTaskStatus.Completed]
    ])('preserves a pushed %s -> %s transition when an earlier list request finishes', async (previousStatus, nextStatus) => {
        // Arrange
        const previous = createTask({ status: previousStatus });
        client.setQueryData(key, [previous]);
        const pending = Promise.withResolvers<ViewBackgroundTask[]>();
        const read = client.fetchQuery({ queryFn: () => pending.promise, queryKey: key }).catch(() => undefined);

        // Act
        await updateTaskFromMessage(client, { ...createUpdate(previous.id), status: nextStatus });
        pending.resolve([previous]);
        await read;

        // Assert
        expect(client.getQueryData<ViewBackgroundTask[]>(key)?.[0]).toMatchObject({ status: nextStatus, updated_utc: '2026-01-01T10:00:09Z' });
    });

    it('retains concurrent pushed updates for different tasks', async () => {
        // Arrange
        client.setQueryData(key, [createTask(), createTask({ id: 'task-2' })]);

        // Act
        await Promise.all([updateTaskFromMessage(client, createUpdate('task-1')), updateTaskFromMessage(client, createUpdate('task-2'))]);

        // Assert
        expect(client.getQueryData<ViewBackgroundTask[]>(key)?.map((task) => task.processed)).toEqual([75, 75]);
    });
});
