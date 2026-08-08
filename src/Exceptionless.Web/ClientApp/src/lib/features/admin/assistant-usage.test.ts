import { describe, expect, it } from 'vitest';

import type { AdminAssistantOrganizationUsage } from './models';

import { getBlockedCount, getTotalTokens, getUsageRisk } from './assistant-usage';

function usage(overrides: Partial<AdminAssistantOrganizationUsage> = {}): AdminAssistantOrganizationUsage {
    return {
        blocked_by_concurrency: 0,
        blocked_by_cost_limit: 0,
        blocked_by_rate_limit: 0,
        blocked_by_token_limit: 0,
        cancelled: 0,
        completed: 8,
        completion_tokens: 250,
        cost_usd: 0.5,
        failed: 1,
        last_used_utc: '2026-08-03T12:00:00Z',
        monthly_cost_limit_usd: 5,
        monthly_token_limit: 10000,
        organization_id: 'organization-id',
        organization_name: 'Test Organization',
        plan_id: 'medium',
        prompt_tokens: 750,
        provider_requests: 10,
        token_utilization: 0.1,
        tool_calls: 3,
        turns: 10,
        ...overrides
    };
}

describe('assistant usage helpers', () => {
    it('classifies utilization and blocked usage', () => {
        expect(getUsageRisk(undefined)).toBe('unlimited');
        expect(getUsageRisk(0.5)).toBe('normal');
        expect(getUsageRisk(0.75)).toBe('warning');
        expect(getUsageRisk(1)).toBe('critical');
        expect(getUsageRisk(0.1, 1)).toBe('critical');
    });

    it('totals tokens and blocked attempts', () => {
        const value = usage({
            blocked_by_concurrency: 1,
            blocked_by_cost_limit: 2,
            blocked_by_rate_limit: 3,
            blocked_by_token_limit: 4
        });

        expect(getTotalTokens(value)).toBe(1000);
        expect(getBlockedCount(value)).toBe(10);
    });
});
