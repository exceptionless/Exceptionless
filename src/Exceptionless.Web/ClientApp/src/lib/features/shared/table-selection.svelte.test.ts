import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

import TableSelectionTestHarness from './table-selection.test-harness.svelte';

afterEach(cleanup);

describe('shared table selection scope', () => {
    it('clears selection when moving to another page', async () => {
        render(TableSelectionTestHarness);

        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));
        expect(screen.getByLabelText('Selected rows').textContent).toBe('1');

        await fireEvent.click(screen.getByRole('button', { name: 'Next page' }));
        expect(screen.getByLabelText('Selected rows').textContent).toBe('0');
    });

    it('clears selection when the result sort changes', async () => {
        render(TableSelectionTestHarness);

        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));
        expect(screen.getByLabelText('Selected rows').textContent).toBe('1');

        await fireEvent.click(screen.getByRole('button', { name: 'Sort descending' }));
        expect(screen.getByLabelText('Selected rows').textContent).toBe('0');
    });

    it('clears selection when browser history restores another page or sort', async () => {
        render(TableSelectionTestHarness);

        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Restore page from URL' }));
        expect(screen.getByLabelText('Selected rows').textContent).toBe('0');

        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Restore sort from URL' }));
        expect(screen.getByLabelText('Selected rows').textContent).toBe('0');
    });
});

describe('shared memory pagination', () => {
    it('MemoryTable_WithoutSortingHook_PagesInReceivedOrderAndClearsSelection', async () => {
        // Arrange
        render(TableSelectionTestHarness, { paginationStrategy: 'memory' });
        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Next page' }));

        // Assert
        expect(screen.getByLabelText('Visible rows')).toHaveTextContent('row-2');
        expect(screen.getByLabelText('Page index')).toHaveTextContent('1');
        expect(screen.getByLabelText('Selected rows')).toHaveTextContent('0');
    });

    it('MemoryTable_WhenExternalPageAndLimitChange_ReslicesWithoutStaleSelection', async () => {
        // Arrange
        render(TableSelectionTestHarness, { paginationStrategy: 'memory' });
        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Restore page from URL' }));

        // Assert
        expect(screen.getByLabelText('Visible rows')).toHaveTextContent('row-2');
        expect(screen.getByLabelText('Selected rows')).toHaveTextContent('0');

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Restore limit from URL' }));

        // Assert
        expect(screen.getByLabelText('Visible rows')).toHaveTextContent('row-3');
        expect(screen.getByLabelText('Selected rows')).toHaveTextContent('0');
    });
});
