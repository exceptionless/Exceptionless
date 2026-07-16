import { RateNotificationSignal, RateNotificationSubject, type ViewRateNotificationRule } from '$generated/api';
import { describe, expect, it } from 'vitest';

import { getRateNotificationRuleFormData, RateNotificationRuleSchema, toRateNotificationRuleRequest } from './schemas';

describe('RateNotificationRuleSchema', () => {
    it('rejects a whitespace-only name', () => {
        const result = RateNotificationRuleSchema.safeParse({
            cooldown: '00:30:00',
            is_enabled: true,
            name: '   ',
            signal: RateNotificationSignal.Errors,
            stack_id: '',
            subject: RateNotificationSubject.Project,
            threshold: 10,
            window: '00:05:00'
        });

        expect(result.success).toBe(false);
        expect(result.error?.issues).toContainEqual(expect.objectContaining({ path: ['name'] }));
    });

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

    it('rejects an unsupported window', () => {
        const result = RateNotificationRuleSchema.safeParse({
            cooldown: '00:30:00',
            is_enabled: true,
            name: 'Unsupported window',
            signal: RateNotificationSignal.Errors,
            stack_id: '',
            subject: RateNotificationSubject.Project,
            threshold: 10,
            window: '00:07:00'
        });

        expect(result.success).toBe(false);
        expect(result.error?.issues).toContainEqual(expect.objectContaining({ path: ['window'] }));
    });

    it('rejects a cooldown longer than 24 hours', () => {
        const result = RateNotificationRuleSchema.safeParse({
            cooldown: '1.01:00:00',
            is_enabled: true,
            name: 'Excessive cooldown',
            signal: RateNotificationSignal.Errors,
            stack_id: '',
            subject: RateNotificationSubject.Project,
            threshold: 10,
            window: '00:30:00'
        });

        expect(result.success).toBe(false);
        expect(result.error?.issues).toContainEqual(expect.objectContaining({ path: ['cooldown'] }));
    });

    it('accepts the .NET one-day TimeSpan wire format', () => {
        const result = RateNotificationRuleSchema.safeParse({
            cooldown: '1.00:00:00',
            is_enabled: true,
            name: 'Daily cooldown',
            signal: RateNotificationSignal.Errors,
            stack_id: '',
            subject: RateNotificationSubject.Project,
            threshold: 10,
            window: '01:00:00'
        });

        expect(result.success).toBe(true);
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
