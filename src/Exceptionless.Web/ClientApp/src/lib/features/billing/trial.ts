const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;

/** The trial banner only shows in the last few days of a trial; before that the sidebar shows a quiet indicator. */
export const TRIAL_ENDING_SOON_DAYS = 3;

/** Product tour record name that marks the one-time trial-ended notice as dismissed for an organization. */
export type TrialEndedNoticeKey = `trial-ended-${string}`;

/**
 * Whole days left in a trial, rounded up so a trial ending later today still shows one day left.
 * Returns 0 once the trial has ended.
 */
export function getTrialDaysRemaining(trialEndsUtc: null | string | undefined, now: Date = new Date()): number {
    if (!trialEndsUtc) {
        return 0;
    }

    const endsAt = new Date(trialEndsUtc).getTime();
    if (Number.isNaN(endsAt)) {
        return 0;
    }

    return Math.max(0, Math.ceil((endsAt - now.getTime()) / MILLISECONDS_PER_DAY));
}

export function getTrialEndedNoticeKey(organizationId: string): TrialEndedNoticeKey {
    return `trial-ended-${organizationId}`;
}

/**
 * Whether a Free organization got there because its trial ended (or was ended early) without ever subscribing.
 * Organizations that converted to a paid plan and later downgraded keep their trial date, but didn't just leave a trial.
 */
export function hasTrialEndedWithoutSubscribing(organization: { subscribe_date?: null | string; trial_ends_utc?: null | string }): boolean {
    if (!organization.trial_ends_utc) {
        return false;
    }

    return !organization.subscribe_date || organization.subscribe_date.startsWith('0001-01-01');
}

/**
 * Whether the trial is in its last few days, or has ended but the organization hasn't moved to the Free plan yet.
 */
export function isTrialEndingSoon(daysRemaining: number): boolean {
    return daysRemaining <= TRIAL_ENDING_SOON_DAYS;
}
