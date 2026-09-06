import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import SaveViewDialog from './save-view-dialog.svelte';

describe('SaveViewDialog', () => {
    it('preserves the draft when the guide default changes while open', async () => {
        const { rerender } = render(SaveViewDialog, {
            defaultPrivate: true,
            onClose: vi.fn(),
            onLoadView: vi.fn(),
            onSave: vi.fn(),
            open: true,
            savedViews: [],
            saving: false
        });
        await fireEvent.input(screen.getByLabelText('Name'), { target: { value: 'My errors' } });

        await rerender({ defaultPrivate: false });

        expect(screen.getByLabelText('Name')).toHaveValue('My errors');
        expect(screen.getByLabelText('URL name')).toHaveValue('my-errors');
        expect(screen.getByRole('switch', { name: 'Private' })).toHaveAttribute('aria-checked', 'true');

        await rerender({ open: false });
        await rerender({ open: true });

        expect(screen.getByLabelText('Name')).toHaveValue('');
        expect(screen.getByRole('switch', { name: 'Private' })).toHaveAttribute('aria-checked', 'false');
    });
});
