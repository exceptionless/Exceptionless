import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { BulkOperationSelectionKind, BulkOperationStatus, type ViewBulkOperation } from '../models';

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
    const state = new SvelteMap<string, ViewBulkOperation[]>();

    return {
        __state: state,
        cancelBulkOperation: () => ({ mutateAsync: mocks.cancelMutateAsync }),
        getBulkOperationsQuery: () => ({
            get data() {
                return state.get('operations');
            }
        }),
        invalidateBulkOperationAffectedQueries: mocks.invalidateAffected,
        updateBulkOperationFromMessage: mocks.updateFromMessage
    };
});

import * as api from '../api.svelte';
import BulkOperationPanel from './bulk-operation-panel.svelte';

const state = (api as unknown as { __state: Map<string, ViewBulkOperation[]> }).__state;

function createOperation(overrides: Partial<ViewBulkOperation> = {}): ViewBulkOperation {
    const now = new Date().toISOString();
    return {
        created_by_user_id: 'user-id',
        created_utc: now,
        description: 'Delete 50 events matching filter',
        failed: 0,
        id: 'operation-1',
        organization_id: 'organization-id',
        processed: 20,
        selection: { kind: BulkOperationSelectionKind.Filter },
        status: BulkOperationStatus.Running,
        succeeded: 20,
        total: 50,
        type: 'events.delete',
        updated_utc: now,
        ...overrides
    };
}

describe('BulkOperationPanel', () => {
    beforeEach(() => {
        state.clear();
        mocks.cancelMutateAsync.mockResolvedValue(undefined);
    });

    afterEach(cleanup);

    it('hydrates running operations from the list after a reload and ignores stale finished ones', async () => {
        // Arrange
        state.set('operations', [
            createOperation(),
            createOperation({ id: 'long-finished', status: BulkOperationStatus.Completed, updated_utc: '2020-01-01T00:00:00Z' })
        ]);

        // Act
        render(BulkOperationPanel);
        await tick();

        // Assert
        expect(screen.getAllByText('Delete 50 events matching filter')).toHaveLength(1);
        expect(document.querySelector('[data-slot="bulk-operation-progress"]')?.textContent?.replace(/\s+/g, ' ').trim()).toBe('20 of 50');
        expect(mocks.invalidateAffected).not.toHaveBeenCalled();
    });

    it('applies pushed updates for its organization and ignores other organizations', async () => {
        // Arrange
        state.set('operations', [createOperation()]);
        render(BulkOperationPanel);
        const update = { id: 'operation-1', organization_id: 'organization-id', processed: 30, status: BulkOperationStatus.Running };

        // Act
        document.dispatchEvent(new CustomEvent('BulkOperationUpdated', { detail: update }));
        document.dispatchEvent(new CustomEvent('BulkOperationUpdated', { detail: { ...update, organization_id: 'another-organization' } }));

        // Assert
        expect(mocks.updateFromMessage).toHaveBeenCalledTimes(1);
        expect(mocks.updateFromMessage).toHaveBeenCalledWith(mocks.queryClient, update);
    });

    it('refreshes events and stacks once when a running operation finishes', async () => {
        // Arrange
        state.set('operations', [createOperation()]);
        render(BulkOperationPanel);
        await tick();

        // Act
        state.set('operations', [createOperation({ processed: 50, status: BulkOperationStatus.Completed, succeeded: 50 })]);
        await tick();
        state.set('operations', [
            createOperation({ processed: 50, status: BulkOperationStatus.Completed, succeeded: 50, updated_utc: new Date().toISOString() })
        ]);
        await tick();

        // Assert
        expect(mocks.invalidateAffected).toHaveBeenCalledTimes(1);
        expect(mocks.invalidateAffected).toHaveBeenCalledWith(mocks.queryClient);
        expect(document.querySelector('[data-slot="bulk-operation-progress"]')?.textContent).toContain('Completed');
    });

    it('requests cancellation of a running operation', async () => {
        // Arrange
        state.set('operations', [createOperation()]);
        render(BulkOperationPanel);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Cancel / }));

        // Assert
        await waitFor(() => expect(mocks.cancelMutateAsync).toHaveBeenCalledWith('operation-1'));
    });

    it('removes a finished operation when it is dismissed', async () => {
        // Arrange
        state.set('operations', [createOperation({ status: BulkOperationStatus.Completed })]);
        render(BulkOperationPanel);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Dismiss / }));

        // Assert
        expect(screen.queryByText('Delete 50 events matching filter')).toBeNull();
    });

    it('renders nothing when there is nothing to show', () => {
        // Arrange & Act
        render(BulkOperationPanel);

        // Assert
        expect(screen.queryByRole('region', { name: 'Bulk operations' })).toBeNull();
    });
});
