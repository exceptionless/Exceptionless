import type { ProblemDetails } from '@foundatiofx/fetchclient';

import { QueryClient, QueryObserver, type QueryObserverOptions } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { BackgroundTaskStatus, BulkOperationSelectionKind, type ViewBackgroundTask } from './models';

const mocks = vi.hoisted(() => ({
    createMutation: vi.fn<(options: () => { onSuccess: (task: ViewBackgroundTask) => Promise<unknown> | void }) => void>(),
    createQuery: vi.fn<(options: () => QueryObserverOptions<ViewBackgroundTask[], ProblemDetails>) => void>(),
    getJSON: vi.fn(),
    useQueryClient: vi.fn()
}));

vi.mock('#features/auth/index.svelte.ts', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('#features/events/api.svelte.ts', () => ({ queryKeys: { type: ['PersistentEvent'] } }));
vi.mock('#features/stacks/api.svelte.ts', () => ({ queryKeys: { type: ['Stack'] } }));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: () => ({ getJSON: mocks.getJSON }) }));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createMutation: mocks.createMutation,
    createQuery: mocks.createQuery,
    useQueryClient: mocks.useQueryClient
}));

import {
    ACTIVE_TASK_POLL_INTERVAL_MS,
    cancelTask,
    getTasksQuery,
    invalidateTaskAffectedQueries,
    postBulkOperation,
    queryKeys,
    updateTaskFromMessage
} from './api.svelte';

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

describe('getTasksQuery', () => {
    it.each([
        ['empty', BackgroundTaskStatus.Running],
        ['empty', BackgroundTaskStatus.Completed],
        ['finished', BackgroundTaskStatus.Running],
        ['finished', BackgroundTaskStatus.Completed]
    ] as const)('refreshes a cached %s list after returning to an organization with a new %s task', async (cachedList, newStatus) => {
        // Arrange
        vi.clearAllMocks();
        const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 5 * 60 * 1000 } } });
        let organizationId = 'organization-id';
        const initialTasks = cachedList === 'finished' ? [createTask({ status: BackgroundTaskStatus.Completed })] : [];
        const missedTask = createTask({ id: 'started-elsewhere', status: newStatus });
        mocks.getJSON
            .mockReset()
            .mockResolvedValueOnce({ data: initialTasks })
            .mockResolvedValueOnce({ data: [] })
            .mockResolvedValueOnce({ data: [...initialTasks, missedTask] });
        getTasksQuery({
            route: {
                get organizationId() {
                    return organizationId;
                }
            }
        });
        const options = mocks.createQuery.mock.calls[0]![0];
        const observer = new QueryObserver(client, options());
        const unsubscribe = observer.subscribe(() => {});

        try {
            await observer.getCurrentQuery().promise;

            // Act
            organizationId = 'another-organization';
            observer.setOptions(options());
            await observer.getCurrentQuery().promise;
            organizationId = 'organization-id';
            observer.setOptions(options());
            await observer.getCurrentQuery().promise;

            // Assert
            expect(mocks.getJSON.mock.calls.map(([url]) => url)).toEqual([
                'organizations/organization-id/tasks',
                'organizations/another-organization/tasks',
                'organizations/organization-id/tasks'
            ]);
            expect(observer.getCurrentResult().data).toEqual([...initialTasks, missedTask]);
            const refetchInterval = options().refetchInterval;
            expect(typeof refetchInterval === 'function' ? refetchInterval(observer.getCurrentQuery()) : refetchInterval).toBe(
                newStatus === BackgroundTaskStatus.Running ? ACTIVE_TASK_POLL_INTERVAL_MS : false
            );
        } finally {
            unsubscribe();
            client.clear();
        }
    });
});

describe('updateTaskFromMessage', () => {
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.clearAllMocks();
        queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
        vi.spyOn(queryClient, 'invalidateQueries');
    });

    afterEach(() => queryClient.clear());

    it('writes the progress of a known task into the cached list', async () => {
        // Arrange
        queryClient.setQueryData(queryKeys.organization('organization-id'), [createTask()]);

        // Act
        await updateTaskFromMessage(queryClient, createUpdate('task-1'));

        // Assert
        expect(queryClient.getQueryData(queryKeys.organization('organization-id'))).toMatchObject([{ id: 'task-1', processed: 75 }]);
        expect(queryClient.invalidateQueries).not.toHaveBeenCalled();
    });

    it('reloads the list when the task was started somewhere else', async () => {
        // Arrange
        const tasks = [createTask()];
        queryClient.setQueryData(queryKeys.organization('organization-id'), tasks);

        // Act
        await updateTaskFromMessage(queryClient, createUpdate('started-elsewhere'));

        // Assert
        expect(queryClient.getQueryData(queryKeys.organization('organization-id'))).toEqual(tasks);
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

    it.each([
        ['2026-01-01T10:00:00Z', '2026-01-01T10:01:00Z'],
        ['2026-01-01T10:00:00.1234566Z', '2026-01-01T10:00:00.1234567Z']
    ])('ignores a cancellation snapshot at %s after a pushed completion at %s', async (older, newer) => {
        // Arrange
        const running = createTask({ updated_utc: older });
        client.setQueryData(key, [running]);
        const pending = Promise.withResolvers<ViewBackgroundTask[]>();
        const read = client.fetchQuery({ queryFn: () => pending.promise, queryKey: key }).catch(() => undefined);
        const completed = createTask({ status: BackgroundTaskStatus.Completed, updated_utc: newer });
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

    it.each(['pushed completion', 'cancellation response'] as const)(
        'finishes discovering an unknown task after a %s cancels the list request',
        async (source) => {
            // Arrange
            const running = createTask();
            const completed = createTask({ status: BackgroundTaskStatus.Completed, updated_utc: '2026-01-01T10:00:09Z' });
            const unknown = createTask({ id: 'started-elsewhere' });
            client.setQueryData(key, [running]);
            const previousRead = Promise.withResolvers<ViewBackgroundTask[]>();
            const nextRead = Promise.withResolvers<ViewBackgroundTask[]>();
            const queryFn = vi.fn().mockReturnValueOnce(previousRead.promise).mockReturnValueOnce(nextRead.promise);
            const observer = new QueryObserver(client, { queryFn, queryKey: key, staleTime: Infinity });
            const unsubscribe = observer.subscribe(() => {});

            // Act
            await updateTaskFromMessage(client, createUpdate(unknown.id));
            if (source === 'pushed completion') {
                await updateTaskFromMessage(client, { ...completed, message: undefined, project_id: undefined });
            } else {
                cancelTask();
                await mocks.createMutation.mock.calls[0]![0]().onSuccess(completed);
            }

            // Assert
            expect(client.getQueryData(key)).toMatchObject([completed]);
            expect(queryFn).toHaveBeenCalledTimes(2);

            // Act
            previousRead.resolve([running, unknown]);
            nextRead.resolve([completed, unknown]);
            await client.getQueryCache().find({ queryKey: key })?.promise;
            unsubscribe();

            // Assert
            expect(client.getQueryData(key)).toEqual([completed, unknown]);
        }
    );

    it('keeps unknown-task reconciliation pending when the panel unmounts before the list is merged', async () => {
        // Arrange
        const running = createTask();
        const completed = createTask({ status: BackgroundTaskStatus.Completed, updated_utc: '2026-01-01T10:00:09Z' });
        client.setQueryData(key, [running]);
        const pending = Promise.withResolvers<ViewBackgroundTask[]>();
        const queryFn = vi.fn(({ signal }: { signal: AbortSignal }) => {
            void signal;
            return pending.promise;
        });
        const observer = new QueryObserver(client, { queryFn, queryKey: key, staleTime: Infinity });
        const unsubscribe = observer.subscribe(() => {});

        // Act
        await updateTaskFromMessage(client, createUpdate('started-elsewhere'));
        unsubscribe();
        await updateTaskFromMessage(client, { ...completed, message: undefined, project_id: undefined });

        // Assert
        expect(client.getQueryData(key)).toMatchObject([completed]);
        expect(client.getQueryState(key)?.isInvalidated).toBe(true);
        expect(queryFn).toHaveBeenCalledOnce();
    });
});
