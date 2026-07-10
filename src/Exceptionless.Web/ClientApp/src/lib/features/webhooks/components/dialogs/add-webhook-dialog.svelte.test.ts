import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import AddWebhookDialog from './add-webhook-dialog.svelte';

describe('AddWebhookDialog', () => {
    it('submits selected event types through the dialog action', async () => {
        const save = vi.fn().mockResolvedValue(undefined);
        render(AddWebhookDialog, {
            open: true,
            organizationId: '537650f3b77efe23a47914f3',
            projectId: '537650f3b77efe23a47914f4',
            save
        });

        await fireEvent.input(screen.getByRole('textbox', { name: 'URL' }), {
            target: { value: 'https://example.com/exceptionless' }
        });
        await fireEvent.click(screen.getByRole('checkbox', { name: 'New Error' }));
        const submitButton = screen.getByRole('button', { name: 'Add Webhook' });
        expect(submitButton.getAttribute('type')).toBe('submit');
        await fireEvent.click(submitButton);

        await waitFor(() =>
            expect(save).toHaveBeenCalledWith({
                event_types: ['NewError'],
                organization_id: '537650f3b77efe23a47914f3',
                project_id: '537650f3b77efe23a47914f4',
                url: 'https://example.com/exceptionless'
            })
        );
    });

    it('keeps the dialog open when submission fails', async () => {
        const save = vi.fn().mockRejectedValue(new Error('Save failed'));
        render(AddWebhookDialog, {
            open: true,
            organizationId: '537650f3b77efe23a47914f3',
            projectId: '537650f3b77efe23a47914f4',
            save
        });

        await fireEvent.input(screen.getByRole('textbox', { name: 'URL' }), {
            target: { value: 'https://example.com/exceptionless' }
        });
        await fireEvent.click(screen.getByRole('checkbox', { name: 'New Error' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Add Webhook' }));

        await screen.findByText('An unexpected error occurred, please try again.');
        expect(screen.getByRole('heading', { name: 'Add New Webhook' })).toBeTruthy();
    });
});
