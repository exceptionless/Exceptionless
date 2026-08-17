import { afterEach, describe, expect, it } from 'vitest';

import { showUpgradeDialog, upgradeRequiredDialog } from './upgrade-required.svelte';

describe('showUpgradeDialog', () => {
    afterEach(() => upgradeRequiredDialog.reset());

    it('opens the generic confirmation flow', () => {
        showUpgradeDialog('organization-id', 'Upgrade this feature.');

        expect(upgradeRequiredDialog.open).toBe(true);
        expect(upgradeRequiredDialog.message).toBe('Upgrade this feature.');
        expect(upgradeRequiredDialog.organizationId).toBe('organization-id');
    });
});
