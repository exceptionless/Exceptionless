import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { BulkOperationSelectionKind, BulkOperationStatus, type ViewBulkOperation } from '../models';
import BulkOperationItem from './bulk-operation-item.svelte';

function createOperation(overrides: Partial<ViewBulkOperation> = {}): ViewBulkOperation {
    return {
        created_by_user_id: 'user-id',
        created_utc: '2026-01-01T10:00:00Z',
        description: 'Delete 1,234 events matching filter',
        failed: 0,
        id: 'operation-1',
        organization_id: 'organization-id',
        processed: 500,
        selection: { kind: BulkOperationSelectionKind.Filter },
        status: BulkOperationStatus.Running,
        succeeded: 500,
        total: 1234,
        type: 'events.delete',
        updated_utc: '2026-01-01T10:00:00Z',
        ...overrides
    };
}

function statusText(): string {
    return (document.querySelector('[data-slot="bulk-operation-progress"]')?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('BulkOperationItem', () => {
    afterEach(cleanup);

    it('shows the description, x of y progress and a Cancel button while running', async () => {
        // Arrange
        const onCancel = vi.fn();
        const operation = createOperation();
        render(BulkOperationItem, { props: { onCancel, onDismiss: vi.fn(), operation } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Cancel / }));

        // Assert
        expect(screen.getByText('Delete 1,234 events matching filter')).toBeTruthy();
        expect(statusText()).toBe('500 of 1,234');
        expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('41');
        expect(onCancel).toHaveBeenCalledWith(operation);
        expect(screen.queryByRole('button', { name: /^Dismiss / })).toBeNull();
    });

    it('names the operation on its buttons and announces only state changes, not every progress update', async () => {
        // Arrange
        const { rerender } = render(BulkOperationItem, { props: { onCancel: vi.fn(), onDismiss: vi.fn(), operation: createOperation({ processed: 100 }) } });
        const announcement = () => screen.getByRole('status').textContent;
        const initial = announcement();

        // Act
        await rerender({ operation: createOperation({ processed: 900 }) });
        const afterProgress = announcement();
        await rerender({ operation: createOperation({ processed: 1234, status: BulkOperationStatus.Completed }) });

        // Assert
        expect(initial).toBe('Delete 1,234 events matching filter: in progress');
        expect(afterProgress).toBe(initial);
        expect(announcement()).toBe('Delete 1,234 events matching filter: completed');
        expect(screen.getByRole('button', { name: 'Dismiss Delete 1,234 events matching filter' })).toBeTruthy();
    });

    it('labels Cancel with the operation it cancels', () => {
        // Arrange & Act
        render(BulkOperationItem, { props: { onCancel: vi.fn(), onDismiss: vi.fn(), operation: createOperation() } });

        // Assert
        expect(screen.getByRole('button', { name: 'Cancel Delete 1,234 events matching filter' })).toBeTruthy();
    });

    it('shows the queued state without a count', () => {
        // Arrange & Act
        render(BulkOperationItem, {
            props: { onCancel: vi.fn(), onDismiss: vi.fn(), operation: createOperation({ processed: 0, status: BulkOperationStatus.Queued }) }
        });

        // Assert
        expect(statusText()).toBe('Waiting to start');
        expect(screen.getByRole('button', { name: /^Cancel / })).toBeTruthy();
    });

    it('disables Cancel once cancellation was requested', () => {
        // Arrange & Act
        render(BulkOperationItem, { props: { isCancelling: true, onCancel: vi.fn(), onDismiss: vi.fn(), operation: createOperation() } });

        // Assert
        expect((screen.getByRole('button', { name: /^Cancel / }) as HTMLButtonElement).disabled).toBe(true);
    });

    it('shows a completed operation with its failures and a Dismiss button', async () => {
        // Arrange
        const onDismiss = vi.fn();
        const operation = createOperation({ failed: 2, processed: 1234, status: BulkOperationStatus.Completed, succeeded: 1232 });
        render(BulkOperationItem, { props: { onCancel: vi.fn(), onDismiss, operation } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Dismiss / }));

        // Assert
        expect(statusText()).toBe('Completed · 1,232 processed, 2 failed');
        expect(screen.queryByRole('button', { name: /^Cancel / })).toBeNull();
        expect(onDismiss).toHaveBeenCalledWith(operation);
    });

    it('shows how far a cancelled operation got', () => {
        // Arrange & Act
        render(BulkOperationItem, { props: { onCancel: vi.fn(), onDismiss: vi.fn(), operation: createOperation({ status: BulkOperationStatus.Cancelled }) } });

        // Assert
        expect(statusText()).toBe('Cancelled after 500 of 1,234');
        expect(screen.queryByRole('progressbar')).toBeNull();
    });

    it('shows why an operation failed', () => {
        // Arrange & Act
        render(BulkOperationItem, {
            props: {
                onCancel: vi.fn(),
                onDismiss: vi.fn(),
                operation: createOperation({ message: 'The operation stopped because of an unexpected error.', status: BulkOperationStatus.Failed })
            }
        });

        // Assert
        expect(statusText()).toBe('Failed after 500 of 1,234 · The operation stopped because of an unexpected error.');
    });
});
