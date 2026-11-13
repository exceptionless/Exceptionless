import type { BillingPlan } from '#lib/generated/api.ts';

export type PlanLimits = Pick<BillingPlan, 'has_premium_features' | 'max_events_per_month' | 'retention_days'>;

/**
 * Describes feature and usage reductions before confirming a plan change.
 * User and project capacity checks are enforced separately.
 */
export function getDowngradeLosses(current: PlanLimits, next: PlanLimits): string[] {
    const losses: string[] = [];

    if (next.retention_days < current.retention_days) {
        losses.push(`event history drops from ${formatDays(current.retention_days)} to ${formatDays(next.retention_days)}`);
    }

    if (next.max_events_per_month >= 0 && (current.max_events_per_month < 0 || next.max_events_per_month < current.max_events_per_month)) {
        losses.push(`limit drops to ${next.max_events_per_month.toLocaleString()} events a month`);
    }

    if (current.has_premium_features && !next.has_premium_features) {
        losses.push('premium features turn off');
    }

    return losses;
}

function formatDays(days: number): string {
    return `${days} day${days === 1 ? '' : 's'}`;
}
