import { afterEach, describe, expect, it } from 'vitest';

import { consumeSelfInitiatedFlag, flagSelfInitiatedForceRefresh } from './force-refresh-coordinator';

// Reset module state between tests
afterEach(() => {
    // Consume any leftover flag to prevent leakage
    consumeSelfInitiatedFlag();
});

describe('force-refresh-coordinator', () => {
    it('returns false when no flag has been set', () => {
        expect(consumeSelfInitiatedFlag()).toBe(false);
    });

    it('returns true after flagging and clears the flag', () => {
        flagSelfInitiatedForceRefresh();

        expect(consumeSelfInitiatedFlag()).toBe(true);
        // Flag is consumed — subsequent call returns false
        expect(consumeSelfInitiatedFlag()).toBe(false);
    });

    it('is consumed exactly once per flag', () => {
        flagSelfInitiatedForceRefresh();

        consumeSelfInitiatedFlag(); // consume
        expect(consumeSelfInitiatedFlag()).toBe(false); // already cleared
    });

    it('multiple flags still consume as one', () => {
        flagSelfInitiatedForceRefresh();
        flagSelfInitiatedForceRefresh();

        expect(consumeSelfInitiatedFlag()).toBe(true);
        expect(consumeSelfInitiatedFlag()).toBe(false);
    });
});
