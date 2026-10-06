import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { changePlanDialog, showChangePlanDialog } from '../change-plan.svelte';

const organizationQuery = vi.hoisted(() => ({
    data: undefined,
    error: undefined as Error | undefined,
    isFetching: false,
    refetch: vi.fn(() => Promise.resolve())
}));

vi.mock('$features/organizations/api.svelte', () => ({
    getOrganizationQuery: () => organizationQuery
}));
vi.mock('$env/dynamic/public', () => ({ env: {} }));

import ChangePlanDialogHost from './change-plan-dialog-host.svelte';

describe('ChangePlanDialogHost', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        organizationQuery.error = undefined;
        organizationQuery.isFetching = false;
        organizationQuery.refetch.mockClear();
    });

    afterEach(async () => {
        changePlanDialog.reset();
        cleanup();
        // Finish the dialog's delayed scroll-lock cleanup while the DOM still exists.
        await vi.runOnlyPendingTimersAsync();
        vi.useRealTimers();
    });

    it('restores body scrolling after the open dialog is unmounted', async () => {
        // Arrange
        const originalStyle = document.body.style.cssText;
        showChangePlanDialog('organization-id');
        const { unmount } = render(ChangePlanDialogHost);
        expect(document.body.style.overflow).toBe('hidden');

        // Act
        await unmount();
        expect(vi.getTimerCount()).toBeGreaterThan(0);
        await vi.runOnlyPendingTimersAsync();

        // Assert
        expect(document.body.style.cssText).toBe(originalStyle);
        expect(vi.getTimerCount()).toBe(0);
    });

    it('shows billing loading state as soon as the picker opens', () => {
        showChangePlanDialog('organization-id');
        render(ChangePlanDialogHost);

        expect(screen.getByText('Loading billing details…')).toBeTruthy();
        expect(screen.getByLabelText('Loading billing details')).toBeTruthy();
    });

    it('shows a retry action when organization loading fails', async () => {
        organizationQuery.error = new Error('offline');
        showChangePlanDialog('organization-id');
        render(ChangePlanDialogHost);

        await fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

        expect(organizationQuery.refetch).toHaveBeenCalledOnce();
    });
});
