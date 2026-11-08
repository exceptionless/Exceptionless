import { cleanup, render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ViewOrganization } from '#features/organizations/models.ts';

import * as Sidebar from '#comp/ui/sidebar/index.ts';
import { TRIAL_PLAN_ID } from '#features/billing/constants.ts';

import SidebarTrialIndicator from './sidebar-trial-indicator.svelte';

function renderTrialIndicator() {
    const organization = {
        id: 'organization-id',
        plan_id: TRIAL_PLAN_ID,
        trial_ends_utc: '2026-11-11T12:00:00Z'
    } as ViewOrganization;

    return render(SidebarTrialIndicator, { organization }, { wrapper: Sidebar.Provider });
}

describe('SidebarTrialIndicator', () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });

    afterEach(() => {
        cleanup();
        vi.useRealTimers();
    });

    it('updates the remaining days in an open tab without an organization change', async () => {
        // Arrange
        vi.setSystemTime(new Date('2026-11-08T11:59:30Z'));
        renderTrialIndicator();
        expect(screen.getByRole('button', { name: '4 days left in trial' })).toBeTruthy();

        // Act
        await vi.advanceTimersByTimeAsync(60_000);
        await tick();

        // Assert
        expect(screen.getByRole('button', { name: '3 days left in trial' })).toBeTruthy();
        expect(screen.queryByRole('button', { name: '4 days left in trial' })).toBeNull();
    });

    it('hides the indicator when the trial expires in an open tab', async () => {
        // Arrange
        vi.setSystemTime(new Date('2026-11-11T11:59:30Z'));
        renderTrialIndicator();
        expect(screen.getByRole('button', { name: '1 day left in trial' })).toBeTruthy();

        // Act
        await vi.advanceTimersByTimeAsync(60_000);
        await tick();

        // Assert
        expect(screen.queryByRole('button', { name: /left in trial/ })).toBeNull();
    });
});
