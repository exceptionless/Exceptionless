import { describe, expect, it } from 'vitest';

import { shouldResetActiveEventTab } from './events-overview-tab-state';

describe('event overview tab state', () => {
    it('waits for project metadata before deciding whether a promoted tab is unavailable', () => {
        const activeTab = 'Customer Context';

        expect(shouldResetActiveEventTab(true, true, ['Overview', 'Exception'], activeTab)).toBe(false);
        expect(shouldResetActiveEventTab(true, false, ['Overview', 'Exception', activeTab], activeTab)).toBe(false);
        expect(shouldResetActiveEventTab(true, false, ['Overview', 'Exception'], activeTab)).toBe(true);
    });
});
