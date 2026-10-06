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
