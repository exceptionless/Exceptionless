import { describe, expect, it } from 'vitest';

import {
    applyBulkOperationUpdate,
    BulkOperationSelectionKind,
    BulkOperationStatus,
    FINISHED_OPERATION_VISIBLE_MS,
    getProgressPercent,
    selectVisibleOperations,
    type ViewBulkOperation
} from './models';

function createOperation(overrides: Partial<ViewBulkOperation> = {}): ViewBulkOperation {
    return {
        created_by_user_id: 'user-id',
        created_utc: '2026-01-01T10:00:00Z',
        description: 'Delete events matching filter',
        failed: 0,
        id: 'operation-1',
        organization_id: 'organization-id',
        processed: 0,
        selection: { kind: BulkOperationSelectionKind.Filter },
        status: BulkOperationStatus.Running,
        succeeded: 0,
        total: 100,
        type: 'events.delete',
        updated_utc: '2026-01-01T10:00:00Z',
        ...overrides
    };
}

function createUpdate(overrides: Record<string, unknown> = {}) {
    return {
        description: 'Delete events matching filter',
        failed: 0,
        id: 'operation-1',
        organization_id: 'organization-id',
        processed: 0,
        status: BulkOperationStatus.Running,
        succeeded: 0,
        total: 100,
        type: 'events.delete',
        updated_utc: '2026-01-01T10:00:05Z',
        ...overrides
    };
}

describe('applyBulkOperationUpdate', () => {
    it('patches the progress of a known operation', () => {
        // Arrange
        const operations = [createOperation(), createOperation({ id: 'operation-2' })];

        // Act
        const result = applyBulkOperationUpdate(operations, createUpdate({ failed: 1, processed: 40, succeeded: 39 }));

        // Assert
        expect(result?.[0]).toMatchObject({ failed: 1, processed: 40, succeeded: 39 });
        expect(result?.[1]).toBe(operations[1]);
    });

    it('ignores a message that is older than what is already known', () => {
        // Arrange
        const operations = [createOperation({ processed: 60, updated_utc: '2026-01-01T10:00:10Z' })];

        // Act
        const result = applyBulkOperationUpdate(operations, createUpdate({ processed: 20 }));

        // Assert
        expect(result).toBe(operations);
    });

    it('reports an unknown operation so the list can be reloaded', () => {
        // Arrange
        const operations = [createOperation()];

        // Act
        const result = applyBulkOperationUpdate(operations, createUpdate({ id: 'started-elsewhere', status: BulkOperationStatus.Queued, total: 5 }));

        // Assert
        expect(result).toBeUndefined();
    });
});

describe('getProgressPercent', () => {
    it.each([
        [{ processed: 0, status: BulkOperationStatus.Queued, total: 0 }, 0],
        [{ processed: 50, status: BulkOperationStatus.Running, total: 200 }, 25],
        [{ processed: 5, status: BulkOperationStatus.Running, total: 3 }, 100],
        [{ processed: 3, status: BulkOperationStatus.Completed, total: 0 }, 100]
    ])('maps %j to %i percent', (operation, expected) => {
        // Arrange & Act
        const percent = getProgressPercent(operation);

        // Assert
        expect(percent).toBe(expected);
    });
});

describe('selectVisibleOperations', () => {
    const now = new Date('2026-01-01T10:30:00Z').getTime();

    it('keeps active operations and recently finished ones, newest first', () => {
        // Arrange
        const active = createOperation({ created_utc: '2026-01-01T09:00:00Z', id: 'active', updated_utc: '2026-01-01T09:00:00Z' });
        const recent = createOperation({
            created_utc: '2026-01-01T10:20:00Z',
            id: 'recent',
            status: BulkOperationStatus.Completed,
            updated_utc: new Date(now - FINISHED_OPERATION_VISIBLE_MS + 1000).toISOString()
        });
        const old = createOperation({
            id: 'old',
            status: BulkOperationStatus.Completed,
            updated_utc: new Date(now - FINISHED_OPERATION_VISIBLE_MS - 1000).toISOString()
        });

        // Act
        const result = selectVisibleOperations([active, old, recent], new Set(), now);

        // Assert
        expect(result.map((operation) => operation.id)).toEqual(['recent', 'active']);
    });

    it('hides dismissed operations', () => {
        // Arrange
        const operation = createOperation({ id: 'dismissed' });

        // Act
        const result = selectVisibleOperations([operation], new Set(['dismissed']), now);

        // Assert
        expect(result).toEqual([]);
    });
});
