import type { BootOptions } from 'svelte-intercom';

import { describe, expect, it } from 'vitest';

import { buildIntercomDataUpdate, buildIntercomRouteUpdate, getIntercomRouteKey } from './updates';

describe('Intercom updates', () => {
    it('builds a minimal route update with identity and the current timestamp', () => {
        const bootOptions = {
            company: { id: 'organization_123', name: 'Acme' },
            email: 'user@example.com',
            intercomUserJwt: 'signed-token',
            userId: 'user_123'
        } as BootOptions;

        expect(buildIntercomRouteUpdate(bootOptions, 1_750_000_123_456)).toEqual({
            email: 'user@example.com',
            intercomUserJwt: 'signed-token',
            lastRequestAt: 1_750_000_123,
            userId: 'user_123'
        });
    });

    it('includes only identity and changed user data in a data update', () => {
        const previousBootOptions = {
            company: { id: 'organization_123', name: 'Acme' },
            email: 'user@example.com',
            intercomUserJwt: 'signed-token-1',
            userId: 'user_123'
        } as BootOptions;
        const bootOptions = {
            company: { id: 'organization_123', name: 'Acme' },
            email: 'user@example.com',
            intercomUserJwt: 'signed-token-2',
            userId: 'user_123'
        } as BootOptions;

        expect(buildIntercomDataUpdate(previousBootOptions, bootOptions)).toEqual({
            email: 'user@example.com',
            intercomUserJwt: 'signed-token-2',
            userId: 'user_123'
        });
    });

    it('includes changed company data without resending unchanged user fields', () => {
        const previousBootOptions = {
            company: { id: 'organization_123', name: 'Acme' },
            email: 'user@example.com',
            intercomUserJwt: 'signed-token',
            name: 'Example User',
            userId: 'user_123'
        } as BootOptions;
        const bootOptions = {
            company: { id: 'organization_123', name: 'Acme, Inc.' },
            email: 'user@example.com',
            intercomUserJwt: 'signed-token',
            name: 'Example User',
            userId: 'user_123'
        } as BootOptions;

        expect(buildIntercomDataUpdate(previousBootOptions, bootOptions)).toEqual({
            company: { id: 'organization_123', name: 'Acme, Inc.' },
            email: 'user@example.com',
            intercomUserJwt: 'signed-token',
            userId: 'user_123'
        });
    });

    it('uses the normalized route ID instead of resource identifiers in the pathname', () => {
        const routeId = '/(app)/project/[projectId]/event/[eventId]';

        expect(getIntercomRouteKey(routeId, '/next/project/project-a/event/event-a')).toBe(routeId);
        expect(getIntercomRouteKey(routeId, '/next/project/project-a/event/event-b')).toBe(routeId);
    });

    it('falls back to the pathname when SvelteKit has no route ID', () => {
        expect(getIntercomRouteKey(null, '/next/status')).toBe('/next/status');
    });
});
