import { Exceptionless } from '@exceptionless/browser';

/**
 * Sets the current user identity for Exceptionless error tracking and starts a session.
 * Call once the full user profile is available (e.g., from getMeQuery.onSuccess).
 */
export async function setUserIdentity(userId: string, userName?: string): Promise<void> {
    if (!userId) {
        return;
    }

    if (userName) {
        Exceptionless.config.setUserIdentity(userId, userName);
    } else {
        Exceptionless.config.setUserIdentity(userId);
    }

    await Exceptionless.submitSessionStart();
}

/**
 * Ends the current Exceptionless session and clears user identity.
 * Call on logout.
 */
export async function endSession(): Promise<void> {
    await Exceptionless.submitSessionEnd();
    Exceptionless.config.setUserIdentity('', '');
}

/**
 * Submits a feature usage event for telemetry tracking.
 * Mirrors the legacy Angular $ExceptionlessClient.submitFeatureUsage pattern.
 */
export async function submitFeatureUsage(feature: string): Promise<void> {
    await Exceptionless.submitFeatureUsage(feature);
}
