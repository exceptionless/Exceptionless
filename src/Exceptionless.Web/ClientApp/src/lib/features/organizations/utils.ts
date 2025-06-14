import { isSameUtcMonth } from '$features/shared/dates';

import type { ViewOrganization } from './models';

export function getNextBillingDateUtc(organization?: ViewOrganization): Date {
    if (organization?.subscribe_date) {
        console.log('Organization subscribe date for next billing date:', organization.subscribe_date);
    }

    const now = new Date();
    return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
}

export function getRemainingEventLimit(organization?: ViewOrganization): number {
    if (!organization?.max_events_per_month) {
        return 0;
    }

    const now = new Date();
    const bonusEvents = organization.bonus_expiration && new Date(organization.bonus_expiration) > now ? organization.bonus_events_per_month : 0;

    const usage = organization.usage && organization.usage[organization.usage.length - 1];
    if (usage) {
        const usageDate = new Date(usage.date);
        if (isSameUtcMonth(usageDate, now)) {
            const remaining = usage.limit - usage.total;
            return remaining > 0 ? remaining : 0;
        }
    }

    return organization.max_events_per_month + bonusEvents;
}
