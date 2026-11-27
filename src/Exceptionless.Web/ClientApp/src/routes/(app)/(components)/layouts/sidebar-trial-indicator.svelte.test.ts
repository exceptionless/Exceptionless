import { cleanup, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ViewOrganization } from '#features/organizations/models.ts';

import * as Sidebar from '#comp/ui/sidebar/index.ts';
import { FREE_PLAN_ID, TRIAL_PLAN_ID } from '#features/billing/constants.ts';

import SidebarTrialIndicator from './sidebar-trial-indicator.svelte';

function renderTrialIndicator(planId: string) {
    const organization = {
        id: 'organization-id',
        plan_id: planId,
        trial_ends_utc: '2026-11-11T12:00:00Z'
    } as ViewOrganization;

    return render(SidebarTrialIndicator, { organization }, { wrapper: Sidebar.Provider });
}

describe('SidebarTrialIndicator', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-11-08T12:00:00Z'));
    });

    afterEach(() => {
        cleanup();
        vi.useRealTimers();
    });

    it('shows when an active trial ends', () => {
        // Act
        renderTrialIndicator(TRIAL_PLAN_ID);

        // Assert
        expect(screen.getByRole('button').textContent).toContain('Trial ends in 3 days');
    });

    it('says the trial ended while the expiration job has not moved the organization to Free yet', () => {
        // Arrange
        vi.setSystemTime(new Date('2026-11-11T13:00:00Z'));

        // Act
        renderTrialIndicator(TRIAL_PLAN_ID);

        // Assert
        expect(screen.getByRole('button').textContent).toContain('Trial ended an hour ago');
    });

    it('hides once the organization is no longer on the trial plan', () => {
        // Act
        renderTrialIndicator(FREE_PLAN_ID);

        // Assert
        expect(screen.queryByRole('button')).toBeNull();
    });
});
