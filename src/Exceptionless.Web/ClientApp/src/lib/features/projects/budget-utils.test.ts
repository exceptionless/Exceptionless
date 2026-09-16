import { describe, expect, it } from 'vitest';

import { createProjectIngestLimit, getEffectiveProjectLimit } from './budget-utils';
import { ProjectBudgetCardSchema } from './schemas';

describe('project budget controls', () => {
    it('rejects truncated fixed decimals and out-of-range percentages', () => {
        expect(ProjectBudgetCardSchema.safeParse({ type: 'fixed', value: '1.5' }).success).toBe(false);
        expect(ProjectBudgetCardSchema.safeParse({ type: 'fixed', value: '10' }).success).toBe(true);
        expect(ProjectBudgetCardSchema.safeParse({ type: 'percent', value: '100.1' }).success).toBe(false);
        expect(ProjectBudgetCardSchema.safeParse({ type: 'percent', value: '0.0001' }).success).toBe(true);
        expect(ProjectBudgetCardSchema.safeParse({ type: 'percent', value: '0.00001' }).success).toBe(false);
    });

    it('maps modes to additive API payloads', () => {
        expect(createProjectIngestLimit('none', '')).toBeNull();
        expect(createProjectIngestLimit('fixed', 'not-a-number')).toBeNull();
        expect(createProjectIngestLimit('fixed', '20000')).toMatchObject({ fixed_limit: 20000, type: 0 });
        expect(createProjectIngestLimit('percent', '25.5')).toMatchObject({ percent_of_organization_limit: 25.5, type: 1 });
    });

    it('calculates percentage caps and clamps fixed caps to finite organization allowance', () => {
        expect(getEffectiveProjectLimit(1001, createProjectIngestLimit('percent', '50'))).toBe(501);
        expect(getEffectiveProjectLimit(3000, createProjectIngestLimit('percent', '1.1'))).toBe(33);
        expect(getEffectiveProjectLimit(1_000_000, createProjectIngestLimit('percent', '8.3'))).toBe(83_000);
        expect(getEffectiveProjectLimit(1_000_000, createProjectIngestLimit('percent', '0.0001'))).toBe(1);
        expect(getEffectiveProjectLimit(1000, createProjectIngestLimit('fixed', '2000'))).toBe(1000);
        expect(getEffectiveProjectLimit(-1, createProjectIngestLimit('percent', '50'))).toBeNull();
    });
});
