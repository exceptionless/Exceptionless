import type { NewRateNotificationRule, ViewRateNotificationRule } from '$generated/api';

import { WINDOW_OPTIONS } from '$features/rate-notifications/types';
import { RateNotificationSignal, RateNotificationSubject } from '$generated/api';
import { NewRateNotificationRuleSchema } from '$generated/schemas';
import { type infer as Infer, string } from 'zod';

const MAXIMUM_COOLDOWN_SECONDS = 24 * 60 * 60;
const SUPPORTED_WINDOWS = new Set<string>(WINDOW_OPTIONS.map((option) => option.value));

function durationSeconds(value: string): number {
    const daySeparator = value.indexOf('.');
    const days = daySeparator >= 0 ? Number(value.slice(0, daySeparator)) : 0;
    const time = daySeparator >= 0 ? value.slice(daySeparator + 1) : value;
    const [hours = '0', minutes = '0', seconds = '0'] = time.split(':');
    return days * 86400 + Number(hours) * 3600 + Number(minutes) * 60 + Number(seconds);
}

export const RateNotificationRuleSchema = NewRateNotificationRuleSchema.extend({
    name: string().trim().min(1, 'Enter a name.').max(100),
    stack_id: string().optional()
}).superRefine((value, context) => {
    if (value.subject === RateNotificationSubject.Stack && !value.stack_id) {
        context.addIssue({ code: 'custom', message: 'Select a stack.', path: ['stack_id'] });
    }

    if (!SUPPORTED_WINDOWS.has(value.window)) {
        context.addIssue({ code: 'custom', message: 'Select a supported window.', path: ['window'] });
    }

    if (durationSeconds(value.cooldown) < durationSeconds(value.window)) {
        context.addIssue({ code: 'custom', message: 'Cooldown must be at least as long as the window.', path: ['cooldown'] });
    }

    if (durationSeconds(value.cooldown) > MAXIMUM_COOLDOWN_SECONDS) {
        context.addIssue({ code: 'custom', message: 'Cooldown must not exceed 24 hours.', path: ['cooldown'] });
    }
});

export type RateNotificationRuleFormData = Infer<typeof RateNotificationRuleSchema>;

export const DEFAULT_RATE_NOTIFICATION_RULE: RateNotificationRuleFormData = {
    cooldown: '00:30:00',
    is_enabled: true,
    name: '',
    signal: RateNotificationSignal.Errors,
    stack_id: '',
    subject: RateNotificationSubject.Project,
    threshold: 10,
    window: '00:05:00'
};

export function getRateNotificationRuleFormData(value: undefined | ViewRateNotificationRule): RateNotificationRuleFormData {
    return value
        ? {
              cooldown: value.cooldown,
              is_enabled: value.is_enabled,
              name: value.name,
              signal: value.signal,
              stack_id: value.stack_id ?? '',
              subject: value.subject,
              threshold: value.threshold,
              window: value.window
          }
        : { ...DEFAULT_RATE_NOTIFICATION_RULE };
}

export function toRateNotificationRuleRequest(value: RateNotificationRuleFormData, enabled: boolean): NewRateNotificationRule {
    return {
        cooldown: value.cooldown,
        is_enabled: enabled && value.is_enabled,
        name: value.name.trim(),
        signal: value.signal as RateNotificationSignal,
        stack_id: value.subject === RateNotificationSubject.Stack ? value.stack_id || null : null,
        subject: value.subject as RateNotificationSubject,
        threshold: value.threshold,
        window: value.window
    };
}
