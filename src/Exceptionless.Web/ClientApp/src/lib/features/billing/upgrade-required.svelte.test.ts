import { afterEach, describe, expect, it, vi } from 'vitest';

import { showUpgradeDialog, upgradeRequiredDialog } from './upgrade-required.svelte';

describe('showUpgradeDialog', () => {
    afterEach(() => upgradeRequiredDialog.reset());

    it('stores direct picker options without changing generic defaults', () => {
        const onSuccess = vi.fn();

        showUpgradeDialog('organization-id', 'Upgrade for Exie.', {
            directToPlanPicker: true,
            initialTierId: 'EX_MEDIUM',
            onSuccess
        });

        expect(upgradeRequiredDialog.open).toBe(true);
        expect(upgradeRequiredDialog.step).toBe('plan-picker');
        expect(upgradeRequiredDialog.initialTierId).toBe('EX_MEDIUM');
        expect(upgradeRequiredDialog.onSuccess).toBe(onSuccess);
    });

    it('keeps the confirmation flow for existing callers', () => {
        showUpgradeDialog('organization-id');

        expect(upgradeRequiredDialog.step).toBe('confirmation');
        expect(upgradeRequiredDialog.initialTierId).toBeUndefined();
    });
});
