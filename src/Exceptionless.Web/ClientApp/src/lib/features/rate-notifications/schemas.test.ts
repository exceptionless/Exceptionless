import { RateNotificationSignal, RateNotificationSubject, type ViewRateNotificationRule } from '$generated/api';
import { describe, expect, it } from 'vitest';

import { getRateNotificationRuleFormData, RateNotificationRuleSchema, toRateNotificationRuleRequest } from './schemas';

describe('RateNotificationRuleSchema', () => {
    it('requires a stack for stack-scoped rules', () => {
        const result = RateNotificationRuleSchema.safeParse({
            cooldown: '00:30:00',
            is_enabled: true,
            name: 'Stack errors',
            signal: RateNotificationSignal.Errors,
            stack_id: '',
            subject: RateNotificationSubject.Stack,
            threshold: 10,
            window: '00:05:00'
        });

        expect(result.success).toBe(false);
        expect(result.error?.issues).toContainEqual(expect.objectContaining({ path: ['stack_id'] }));
    });

    it('rejects a cooldown shorter than the window', () => {
        const result = RateNotificationRuleSchema.safeParse({
            cooldown: '00:05:00',
            is_enabled: true,
            name: 'Slow cooldown',
            signal: RateNotificationSignal.Errors,
            stack_id: '',
            subject: RateNotificationSubject.Project,
            threshold: 10,
            window: '00:30:00'
        });

        expect(result.success).toBe(false);
        expect(result.error?.issues).toContainEqual(expect.objectContaining({ path: ['cooldown'] }));
    });
});

describe('rate notification form mapping', () => {
    it('resets all editable values from the selected rule', () => {
        const rule: ViewRateNotificationRule = {
            cooldown: '01:00:00',
            created_utc: '2026-07-10T12:00:00Z',
            id: 'rule-id',
            is_enabled: false,
            is_snoozed: false,
            name: 'Selected rule',
            organization_id: 'organization-id',
            project_id: 'project-id',
            signal: RateNotificationSignal.CriticalErrors,
            stack_id: 'stack-id',
            subject: RateNotificationSubject.Stack,
            threshold: 25,
            updated_utc: '2026-07-10T12:00:00Z',
            user_id: 'user-id',
            version: 2,
            window: '00:15:00'
        };

        expect(getRateNotificationRuleFormData(rule)).toEqual({
            cooldown: '01:00:00',
            is_enabled: false,
            name: 'Selected rule',
            signal: RateNotificationSignal.CriticalErrors,
            stack_id: 'stack-id',
            subject: RateNotificationSubject.Stack,
            threshold: 25,
            window: '00:15:00'
        });
    });

    it('clears stack scope and disables the request when unavailable', () => {
        const request = toRateNotificationRuleRequest(
            {
                cooldown: '00:30:00',
                is_enabled: true,
                name: '  Project errors  ',
                signal: RateNotificationSignal.Errors,
                stack_id: 'stale-stack-id',
                subject: RateNotificationSubject.Project,
                threshold: 10,
                window: '00:05:00'
            },
            false
        );

        expect(request.is_enabled).toBe(false);
        expect(request.name).toBe('Project errors');
        expect(request.stack_id).toBeNull();
    });
});
