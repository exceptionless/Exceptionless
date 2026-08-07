import { isSameUtcMonth } from '$features/shared/dates';

import type { ViewOrganization } from './models';

export function getEffectiveEventLimit(organization?: ViewOrganization): number {
    if (organization?.max_events_per_month == null || organization.max_events_per_month <= 0) {
        return -1;
    }

    const now = new Date();
    const currentUsage = organization.usage?.[organization.usage.length - 1];
    if (currentUsage && isSameUtcMonth(new Date(currentUsage.date), now)) {
        return currentUsage.limit;
    }

    const bonusEvents = organization.bonus_expiration && new Date(organization.bonus_expiration) > now ? organization.bonus_events_per_month : 0;
    return organization.max_events_per_month + bonusEvents;
}

export function getNextBillingDateUtc(organization?: ViewOrganization): Date {
    if (organization?.subscribe_date) {
        console.log('Organization subscribe date for next billing date:', organization.subscribe_date);
    }

    const now = new Date();
    return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
}

export function getRemainingEventLimit(organization?: ViewOrganization): number {
    const eventLimit = getEffectiveEventLimit(organization);
    if (eventLimit <= 0) {
        return 0;
    }

    const now = new Date();
    const usage = organization?.usage?.[organization.usage.length - 1];
    if (usage) {
        const usageDate = new Date(usage.date);
        if (isSameUtcMonth(usageDate, now)) {
            const remaining = eventLimit - usage.total;
            return remaining > 0 ? remaining : 0;
        }
    }

    return eventLimit;
}
