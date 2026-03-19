import type { ViewCurrentUser, ViewOrganization } from '$lib/generated/api';

import { getContext } from 'svelte';

import type { IntercomContext } from './intercom-initializer.svelte';

import { INTERCOM_CONTEXT_KEY } from './keys';

/**
 * Builds Intercom boot options from user and organization data.
 * Uses the auth feature's current Intercom token for authentication.
 * Uses the camelCase shape expected by the Svelte SDK, which converts keys to Intercom's payload format.
 */
export function buildIntercomBootOptions(user: undefined | ViewCurrentUser, organization: undefined | ViewOrganization, token?: string) {
    if (!user || !token) {
        return undefined;
    }

    const userCreatedAt = user.id ? parseInt(user.id.substring(0, 8), 16).toString() : undefined;
    const organizationCreatedAt = getUnixTimestampSeconds(organization?.created_utc);

    return {
        company: organization
            ? {
                  createdAt: organizationCreatedAt?.toString(),
                  id: organization.id,
                  monthlySpend: organization.billing_price,
                  name: organization.name,
                  plan: organization.plan_id
              }
            : undefined,
        createdAt: userCreatedAt?.toString(),
        email: user.email_address,
        hideDefaultLauncher: true,
        intercomUserJwt: token,
        name: user.full_name,
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

function getUnixTimestampSeconds(dateTime?: string): string | undefined {
    if (!dateTime) {
        return undefined;
    }

    const milliseconds = Date.parse(dateTime);
    if (Number.isNaN(milliseconds)) {
        return undefined;
    }

    return Math.floor(milliseconds / 1000).toString();
}
