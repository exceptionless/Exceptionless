import type { ViewCurrentUser, ViewOrganization } from '$lib/generated/api';

import { getContext } from 'svelte';

import type { IntercomContext } from './intercom-initializer.svelte';

import { INTERCOM_CONTEXT_KEY } from './keys';

/**
 * Builds Intercom boot options from user and organization data.
 * Matches the data structure from the legacy Angular implementation.
 */
export function buildIntercomBootOptions(user: undefined | ViewCurrentUser, organization: undefined | ViewOrganization) {
    if (!user || !user.hash) {
        return undefined;
    }

    // Extract timestamp from MongoDB ObjectId (first 8 hex chars = unix timestamp)
    const userCreatedAt = user.id ? parseInt(user.id.substring(0, 8), 16) : undefined;
    const orgCreatedAt = organization?.id ? parseInt(organization.id.substring(0, 8), 16) : undefined;

    return {
        company: organization
            ? {
                  createdAt: orgCreatedAt?.toString(),
                  id: organization.id,
                  monthlySpend: organization.billing_price,
                  name: organization.name,
                  plan: organization.plan_id
              }
            : undefined,
        createdAt: userCreatedAt?.toString(),
        email: user.email_address,
        hideDefaultLauncher: true,
        name: user.full_name,
        userHash: user.hash,
        userId: user.id
    };
}

/**
 * Get the Intercom context from the nearest IntercomInitializer.
 * Must be called inside a component wrapped by IntercomInitializer.
 */
export function getIntercom(): IntercomContext | undefined {
    return getContext<IntercomContext>(INTERCOM_CONTEXT_KEY);
}
