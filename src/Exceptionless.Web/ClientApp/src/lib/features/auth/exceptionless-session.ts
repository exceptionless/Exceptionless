import type { Configuration } from '@exceptionless/browser';

import { browser } from '$app/environment';

let _activeUserId: null | string = null;

/** Keep SDK startup and resume events from creating sessions before authentication resolves. */
export function configureSessions(config: Configuration): void {
    config.useSessions(true, 60_000, true);
    config.addPlugin('authenticated-sessions', 20, async (context) => {
        if ((context.event.type === 'session' || context.event.source?.startsWith('assistant.')) && !context.event.data?.['@user']?.identity) {
            context.cancelled = true;
        }
    });
}

/**
 * Ends the current Exceptionless session and clears user identity.
 * Call on logout. A delayed session end must not clear a newer user's identity.
 */
export async function endSession(): Promise<void> {
    const endingUserId = _activeUserId;
    const Exceptionless = await getExceptionless();
    if (!Exceptionless) {
        _activeUserId = null;
        return;
    }

    if (_activeUserId !== endingUserId) {
        return;
    }
    const endingSessionId = Exceptionless.config.currentSessionIdentifier;

    try {
        if (endingUserId && endingSessionId) {
            await Exceptionless.submitSessionEnd(endingSessionId);
        }
    } finally {
        if (_activeUserId === endingUserId && Exceptionless.config.currentSessionIdentifier === endingSessionId) {
            Exceptionless.config.setUserIdentity('', '');
            Exceptionless.config.currentSessionIdentifier = null;
            _activeUserId = null;
        }
    }
}

/**
 * Sets the current user identity for Exceptionless error tracking.
 * Starts a new session only when the identity changes, including across profile refetches.
 */
export async function setUserIdentity(userId: string, userName?: string): Promise<void> {
    if (!userId) {
        return;
    }

    const Exceptionless = await getExceptionless();
    if (!Exceptionless) {
        return;
    }

    Exceptionless.config.setUserIdentity(userId, userName ?? '');

    if (_activeUserId !== userId) {
        _activeUserId = userId;
        await Exceptionless.createSessionStart()
            .setUserIdentity(userId, userName ?? '')
            .submit();
    }
}

/**
 * Submits a feature usage event for telemetry tracking.
 * Mirrors the legacy Angular $ExceptionlessClient.submitFeatureUsage pattern.
 */
export async function submitFeatureUsage(feature: string, properties?: Record<string, unknown>): Promise<void> {
    const Exceptionless = await getExceptionless();
    if (!Exceptionless) {
        return;
    }

    const event = Exceptionless.createFeatureUsage(feature);
    for (const [name, value] of Object.entries(properties ?? {})) {
        event.setProperty(name, value);
    }
    await event.submit();
}

/** Submits a log entry through the existing session, identity, and client settings. */
export async function submitLog(source: string, message: string, properties?: Record<string, unknown>): Promise<void> {
    const Exceptionless = await getExceptionless();
    if (!Exceptionless) {
        return;
    }

    const event = Exceptionless.createLog(source, message);
    for (const [name, value] of Object.entries(properties ?? {})) {
        event.setProperty(name, value);
    }
    await event.submit();
}

async function getExceptionless() {
    if (!browser) {
        return;
    }

    return (await import('@exceptionless/browser')).Exceptionless;
}
