import type { AdminAssistantOrganizationUsage } from './models';

export type UsageRisk = 'critical' | 'normal' | 'unlimited' | 'warning';

export function getBlockedCount(usage: AdminAssistantOrganizationUsage): number {
    return usage.blocked_by_concurrency + usage.blocked_by_cost_limit + usage.blocked_by_rate_limit + usage.blocked_by_token_limit;
}

export function getTotalTokens(usage: AdminAssistantOrganizationUsage): number {
    return usage.prompt_tokens + usage.completion_tokens;
}

export function getUsageRisk(utilization: null | number | undefined, blockedCount = 0): UsageRisk {
    if (blockedCount > 0 || (utilization ?? 0) >= 1) {
        return 'critical';
    }

    if (utilization === null || utilization === undefined) {
        return 'unlimited';
    }

    return utilization >= 0.75 ? 'warning' : 'normal';
}

export function getUtcMonthKey(date = new Date()): string {
    return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}`;
}
