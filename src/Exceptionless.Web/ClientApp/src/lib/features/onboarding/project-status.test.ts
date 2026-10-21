import { describe, expect, it } from 'vitest';

import { getContinueSetupHref, isProjectAwaitingFirstEvent } from './project-status';

describe('project onboarding status', () => {
    it('treats only an explicit false flag as awaiting the first event', () => {
        expect(isProjectAwaitingFirstEvent({ is_configured: false })).toBe(true);
        expect(isProjectAwaitingFirstEvent({ is_configured: true })).toBe(false);
        expect(isProjectAwaitingFirstEvent({ is_configured: null })).toBe(false);
    });

    it('continues setup with the first quiet project, or adds one', () => {
        expect(
            getContinueSetupHref([
                { id: 'configured', is_configured: true },
                { id: '5f0000000000000000000001', is_configured: false }
            ])
        ).toBe('/project/5f0000000000000000000001/configure');
        expect(getContinueSetupHref([{ id: 'configured', is_configured: true }])).toBe('/project/add');
        expect(getContinueSetupHref([])).toBe('/project/add');
    });
});
