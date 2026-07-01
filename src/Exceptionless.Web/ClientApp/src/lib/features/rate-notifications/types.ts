// Types matching the RateNotificationRule API DTOs

export type RateNotificationSignal = 'AllEvents' | 'Errors' | 'CriticalErrors' | 'NewErrors' | 'Regressions';
export type RateNotificationSubject = 'Project' | 'Stack';

export interface ViewRateNotificationRule {
    id: string;
    organization_id: string;
    project_id: string;
    user_id: string;
    version: number;
    name: string;
    is_enabled: boolean;
    signal: RateNotificationSignal;
    subject: RateNotificationSubject;
    stack_id?: string;
    threshold: number;
    /** ISO 8601 duration string (e.g. "00:05:00") */
    window: string;
    /** ISO 8601 duration string */
    cooldown: string;
    snoozed_until_utc?: string;
    last_fired_utc?: string;
    created_utc: string;
    updated_utc: string;
    /** Computed: snoozed_until_utc is in the future */
    is_snoozed: boolean;
}

export interface NewRateNotificationRule {
    name: string;
    signal: RateNotificationSignal;
    subject: RateNotificationSubject;
    stack_id?: string;
    threshold: number;
    /** ISO 8601 duration string (e.g. "00:05:00") */
    window: string;
    /** ISO 8601 duration string */
    cooldown: string;
    is_enabled: boolean;
}

export interface UpdateRateNotificationRule {
    name?: string;
    signal?: RateNotificationSignal;
    subject?: RateNotificationSubject;
    stack_id?: string;
    threshold?: number;
    window?: string;
    cooldown?: string;
    is_enabled?: boolean;
}

export interface SnoozeRateNotificationRuleRequest {
    duration_seconds?: number;
    until_utc?: string;
}

/** Friendly labels for signal enum values */
export const SIGNAL_LABELS: Record<RateNotificationSignal, string> = {
    AllEvents: 'All Events',
    CriticalErrors: 'Critical Errors',
    Errors: 'Errors',
    NewErrors: 'New Errors',
    Regressions: 'Regressions'
};

/** Allowed window durations (as ISO 8601) mapped to friendly labels */
export const WINDOW_OPTIONS: { label: string; value: string }[] = [
    { label: '1 minute', value: '00:01:00' },
    { label: '5 minutes', value: '00:05:00' },
    { label: '10 minutes', value: '00:10:00' },
    { label: '15 minutes', value: '00:15:00' },
    { label: '30 minutes', value: '00:30:00' },
    { label: '1 hour', value: '01:00:00' }
];

export const MAX_RULES_PER_PROJECT = 20;
