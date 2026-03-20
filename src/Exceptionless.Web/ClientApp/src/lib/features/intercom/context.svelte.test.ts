import type { ViewCurrentUser, ViewOrganization } from '$lib/generated/api';

import { describe, expect, it } from 'vitest';

import { buildIntercomBootOptions } from './context.svelte';

describe('buildIntercomBootOptions', () => {
    it('returns undefined when the intercom token is missing', () => {
        // Arrange
        const user = { id: '67620d1818f5a40d98f3e812' } as ViewCurrentUser;

        // Act
        const result = buildIntercomBootOptions(user, undefined, undefined);

        // Assert
        expect(result).toBeUndefined();
    });

    it('builds boot options from the current user organization and token', () => {
        // Arrange
        const organizationCreatedUtc = '2024-12-21T01:23:45.000Z';
        const user = {
            email_address: 'test-user@example.com',
            full_name: 'Test User',
            id: '67620d1818f5a40d98f3e812'
        } as ViewCurrentUser;
        const organization = {
            billing_price: 29,
            created_utc: organizationCreatedUtc,
            id: '67620d1818f5a40d98f3e999',
            name: 'Acme Corp',
            plan_id: 'unlimited'
        } as ViewOrganization;

        // Act
        const result = buildIntercomBootOptions(user, organization, 'signed-intercom-token');

        // Assert
        expect(result).toEqual({
            company: {
                createdAt: String(Math.floor(Date.parse(organizationCreatedUtc) / 1000)),
                id: organization.id,
                monthlySpend: 29,
                name: 'Acme Corp',
                plan: 'unlimited'
            },
            createdAt: String(parseInt('67620d18', 16)),
            email: 'test-user@example.com',
            hideDefaultLauncher: true,
            intercomUserJwt: 'signed-intercom-token',
            name: 'Test User',
            userId: user.id
        });
    });
});
