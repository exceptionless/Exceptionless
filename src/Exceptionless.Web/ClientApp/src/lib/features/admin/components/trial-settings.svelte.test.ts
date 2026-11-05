import { ProblemDetails } from '@foundatiofx/fetchclient';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const state = vi.hoisted(() => ({
    domains: ['mailinator.com', 'yopmail.com'],
    error: vi.fn(),
    isDefault: true,
    success: vi.fn(),
    update: vi.fn()
}));
vi.mock('svelte-sonner', () => ({ toast: { error: state.error, success: state.success } }));
vi.mock('#features/admin/api.svelte.ts', () => ({
    getTrialSettingsQuery: () => ({
        data: {
            blocked_email_domains: state.domains,
            default_blocked_email_domains: ['mailinator.com', 'yopmail.com'],
            is_default: state.isDefault
        },
        isError: false,
        isPending: false
    }),
    putTrialSettingsMutation: () => ({ isPending: false, mutateAsync: state.update })
}));

import TrialSettings from './trial-settings.svelte';

describe('Free trial settings', () => {
    beforeEach(() => {
        state.domains = ['mailinator.com', 'yopmail.com'];
        state.isDefault = true;
        state.update.mockReset();
        state.success.mockClear();
        state.update.mockImplementation(async ({ blocked_email_domains }: { blocked_email_domains: null | string[] }) => ({
            blocked_email_domains: blocked_email_domains ?? ['mailinator.com', 'yopmail.com'],
            default_blocked_email_domains: ['mailinator.com', 'yopmail.com'],
            is_default: blocked_email_domains === null
        }));
    });

    it('saves the entered domains normalized, deduplicated, and sorted', async () => {
        render(TrialSettings);
        const domains = screen.getByRole('textbox', { name: 'Blocked trial email domains' });
        await waitFor(() => expect(domains).toHaveValue('mailinator.com\nyopmail.com'));
        expect(screen.getByRole('button', { name: 'Save blocked trial domains' })).toBeDisabled();

        await fireEvent.input(domains, { target: { value: 'Spam.Test\n@spam.test, other.test\n\n' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Save blocked trial domains' }));

        await waitFor(() => expect(state.update).toHaveBeenCalledWith({ blocked_email_domains: ['other.test', 'spam.test'] }));
        expect(state.success).toHaveBeenCalledWith('Blocked trial domains saved.');
    });

    it('resets a custom list to the built-in list', async () => {
        state.domains = ['spam.test'];
        state.isDefault = false;
        render(TrialSettings);

        await fireEvent.click(screen.getByRole('button', { name: 'Reset blocked trial domains to the built-in list' }));

        await waitFor(() => expect(state.update).toHaveBeenCalledWith({ blocked_email_domains: null }));
        expect(screen.getByRole('textbox', { name: 'Blocked trial email domains' })).toHaveValue('mailinator.com\nyopmail.com');
    });

    it('shows validation errors from the API', async () => {
        state.update.mockImplementation(async () => {
            const problem = new ProblemDetails();
            problem.title = 'Validation failed';
            problem.errors = { blocked_email_domains: ['Every blocked email domain must be a domain name, such as example.com.'] };
            throw problem;
        });
        render(TrialSettings);
        const domains = screen.getByRole('textbox', { name: 'Blocked trial email domains' });

        await fireEvent.input(domains, { target: { value: 'not_a_domain' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Save blocked trial domains' }));

        expect(await screen.findByText('Every blocked email domain must be a domain name, such as example.com.')).toBeTruthy();
    });
});
