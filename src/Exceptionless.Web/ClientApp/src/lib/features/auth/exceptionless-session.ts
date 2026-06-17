import { browser } from '$app/environment';

type ExceptionlessClient = typeof import('@exceptionless/browser').Exceptionless;

let _activeUserId: null | string = null;
let _exceptionlessClient: null | Promise<ExceptionlessClient> = null;

/**
 * Ends the current Exceptionless session and clears user identity.
 * Call on logout. Clears local state unconditionally even if submitSessionEnd fails.
 */
export async function endSession(): Promise<void> {
    const exceptionless = await getExceptionlessClient();

    try {
        await exceptionless?.submitSessionEnd();
    } finally {
        exceptionless?.config.setUserIdentity('', '');
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

    const exceptionless = await getExceptionlessClient();
    if (!exceptionless) {
        return;
    }

    if (userName) {
        exceptionless.config.setUserIdentity(userId, userName);
    } else {
        exceptionless.config.setUserIdentity(userId);
    }

    if (_activeUserId !== userId) {
        _activeUserId = userId;
        await exceptionless.submitSessionStart();
    }
}

/**
 * Submits a feature usage event for telemetry tracking.
 * Mirrors the legacy Angular $ExceptionlessClient.submitFeatureUsage pattern.
 */
export async function submitFeatureUsage(feature: string): Promise<void> {
    const exceptionless = await getExceptionlessClient();
    await exceptionless?.submitFeatureUsage(feature);
}

function getExceptionlessClient(): null | Promise<ExceptionlessClient> {
    if (!browser) {
        return null;
    }

    _exceptionlessClient ??= import('@exceptionless/browser').then(({ Exceptionless }) => Exceptionless);
    return _exceptionlessClient;
}
