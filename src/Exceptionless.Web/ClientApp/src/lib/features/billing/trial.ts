const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;

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
