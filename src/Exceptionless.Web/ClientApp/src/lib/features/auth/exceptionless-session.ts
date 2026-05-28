import { Exceptionless } from '@exceptionless/browser';

let _activeUserId: null | string = null;

/**
 * Ends the current Exceptionless session and clears user identity.
 * Call on logout. Clears local state unconditionally even if submitSessionEnd fails.
 */
export async function endSession(): Promise<void> {
    try {
        await Exceptionless.submitSessionEnd();
    } finally {
        Exceptionless.config.setUserIdentity('', '');
        _activeUserId = null;
    }
}

/**
 * Sets the current user identity for Exceptionless error tracking.
 * Starts a new session only when the identity changes (guards against repeated onSuccess calls from query refetches).
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

    if (_activeUserId !== userId) {
        _activeUserId = userId;
        await Exceptionless.submitSessionStart();
    }
}

/**
 * Submits a feature usage event for telemetry tracking.
 * Mirrors the legacy Angular $ExceptionlessClient.submitFeatureUsage pattern.
 */
export async function submitFeatureUsage(feature: string): Promise<void> {
    await Exceptionless.submitFeatureUsage(feature);
}
