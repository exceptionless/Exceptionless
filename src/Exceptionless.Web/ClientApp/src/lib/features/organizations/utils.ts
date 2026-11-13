import { isSameUtcMonth } from '#features/shared/dates.ts';

import type { ViewOrganization } from './models';

export const ORGANIZATION_USAGE_REFETCH_INTERVAL_MS = 5 * 60 * 1000;
export const ORGANIZATION_USAGE_ROLLOVER_CHECK_INTERVAL_MS = 60 * 1000;

/** Share of the monthly event limit at which the usage warning shows. */
export const MONTHLY_USAGE_WARNING_THRESHOLD = 0.8;

export interface MonthlyEventUsage {
    limit: number;
    percent: number;
    total: number;
}

/**
 * Current UTC month event usage against the organization's limit, including bonus events.
 * Returns undefined for unlimited plans or when there's no usage recorded for this month.
 */
export function getMonthlyEventUsage(organization?: ViewOrganization, now: Date = new Date()): MonthlyEventUsage | undefined {
    const usage = organization?.usage?.[organization.usage.length - 1];
    if (!usage || usage.limit <= 0 || !isSameUtcMonth(new Date(usage.date), now)) {
        return undefined;
    }

    return { limit: usage.limit, percent: usage.total / usage.limit, total: usage.total };
}

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

export function getUtcMonthKey(date = new Date()): number {
    return date.getUTCFullYear() * 12 + date.getUTCMonth();
}

export function isApproachingMonthlyEventLimit(organization?: ViewOrganization, now: Date = new Date()): boolean {
    const usage = getMonthlyEventUsage(organization, now);
    return !!usage && usage.percent >= MONTHLY_USAGE_WARNING_THRESHOLD;
}
