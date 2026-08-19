import { afterEach, describe, expect, it, vi } from 'vitest';

import { changePlanDialog, showChangePlanDialog } from './change-plan.svelte';

describe('showChangePlanDialog', () => {
    afterEach(() => changePlanDialog.reset());

    it('opens the plan picker with an initial plan and success callback', () => {
        const onSuccess = vi.fn();

        showChangePlanDialog('organization-id', {
            initialPlanId: 'EX_MEDIUM_YEARLY',
            onSuccess
        });

        expect(changePlanDialog.open).toBe(true);
        expect(changePlanDialog.organizationId).toBe('organization-id');
        expect(changePlanDialog.initialPlanId).toBe('EX_MEDIUM_YEARLY');
        expect(changePlanDialog.onSuccess).toBe(onSuccess);
    });
});
