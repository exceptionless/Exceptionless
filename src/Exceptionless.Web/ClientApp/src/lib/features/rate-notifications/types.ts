import { RateNotificationSignal, RateNotificationSubject } from '$generated/api';

export type { NewRateNotificationRule, SnoozeRateNotificationRuleRequest, UpdateRateNotificationRule, ViewRateNotificationRule } from '$generated/api';
export { RateNotificationSignal, RateNotificationSubject } from '$generated/api';

export const MAX_RULES_PER_PROJECT = 20;

export const SIGNAL_LABELS: Record<RateNotificationSignal, string> = {
    [RateNotificationSignal.AllEvents]: 'All Events',
    [RateNotificationSignal.CriticalErrors]: 'Critical Errors',
    [RateNotificationSignal.Errors]: 'Errors',
    [RateNotificationSignal.NewErrors]: 'New Errors',
    [RateNotificationSignal.Regressions]: 'Regressions'
};

export const SUBJECT_LABELS: Record<RateNotificationSubject, string> = {
    [RateNotificationSubject.Project]: 'Project',
    [RateNotificationSubject.Stack]: 'Stack'
};

export const WINDOW_OPTIONS = [
    { label: '1 minute', value: '00:01:00' },
    { label: '5 minutes', value: '00:05:00' },
    { label: '10 minutes', value: '00:10:00' },
    { label: '15 minutes', value: '00:15:00' },
    { label: '30 minutes', value: '00:30:00' },
    { label: '1 hour', value: '01:00:00' }
] as const;

export const COOLDOWN_OPTIONS = [
    ...WINDOW_OPTIONS,
    { label: '2 hours', value: '02:00:00' },
    { label: '4 hours', value: '04:00:00' },
    { label: '8 hours', value: '08:00:00' },
    { label: '24 hours', value: '1.00:00:00' }
] as const;
