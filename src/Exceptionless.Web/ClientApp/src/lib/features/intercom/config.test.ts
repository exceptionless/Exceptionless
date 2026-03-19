import { describe, expect, it } from 'vitest';

import { intercomJwtLifetimeMs, intercomTokenRefreshIntervalMs, intercomTokenRefreshLeadTimeMs } from './config';

describe('intercom token refresh cadence', () => {
    it('refreshes five minutes before a one-hour token expires', () => {
        expect(intercomJwtLifetimeMs).toBe(60 * 60 * 1000);
        expect(intercomTokenRefreshLeadTimeMs).toBe(5 * 60 * 1000);
        expect(intercomTokenRefreshIntervalMs).toBe(55 * 60 * 1000);
        expect(intercomTokenRefreshIntervalMs).toBe(intercomJwtLifetimeMs - intercomTokenRefreshLeadTimeMs);
    });
});
