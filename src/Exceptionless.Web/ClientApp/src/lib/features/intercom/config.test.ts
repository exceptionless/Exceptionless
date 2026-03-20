import { describe, expect, it } from 'vitest';

import {
    getIntercomTokenSessionKey,
    intercomJwtLifetimeMs,
    intercomTokenRefreshIntervalMs,
    intercomTokenRefreshLeadTimeMs,
    shouldLoadIntercomOrganization
} from './config';

describe('intercom token refresh cadence', () => {
    it('refreshes five minutes before a one-hour token expires', () => {
        expect(intercomJwtLifetimeMs).toBe(60 * 60 * 1000);
        expect(intercomTokenRefreshLeadTimeMs).toBe(5 * 60 * 1000);
        expect(intercomTokenRefreshIntervalMs).toBe(55 * 60 * 1000);
        expect(intercomTokenRefreshIntervalMs).toBe(intercomJwtLifetimeMs - intercomTokenRefreshLeadTimeMs);
    });

    it('uses a stable non-secret session key for the current auth token', () => {
        expect(getIntercomTokenSessionKey(null)).toBeNull();
        expect(getIntercomTokenSessionKey('token-a')).toBe(getIntercomTokenSessionKey('token-a'));
        expect(getIntercomTokenSessionKey('token-a')).not.toBe('token-a');
        expect(getIntercomTokenSessionKey('token-a')).not.toBe(getIntercomTokenSessionKey('token-b'));
    });

    it('only loads organization details after Intercom is active for the session', () => {
        expect(shouldLoadIntercomOrganization(undefined, true)).toBe(false);
        expect(shouldLoadIntercomOrganization('app_123', false)).toBe(false);
        expect(shouldLoadIntercomOrganization('app_123', true)).toBe(true);
    });
});
