const SYNTHETIC_USER_AGENT = 'Exceptionless Playwright E2E';

interface RepresentativeEventOptions {
    appUrl: string;
    message: string;
    referenceId: string;
    runId: string;
}

interface SessionEventOptions {
    identity: string;
    name: string;
    sessionId: string;
}

export function createRepresentativeEvent({ appUrl, message, referenceId, runId }: RepresentativeEventOptions): Record<string, unknown> {
    return {
        data: {
            '@environment': {
                machine_name: 'playwright-runner',
                process_name: 'e2e-tests'
            },
            '@request': createSyntheticRequest(appUrl, referenceId),
            '@simple_error': {
                message,
                stack_trace: `Error: ${message}\n    at exceptionless-journey.ts:42:13`,
                type: 'PlaywrightOnboardingException'
            },
            e2e_reference: referenceId,
            run_id: runId
        },
        message,
        reference_id: referenceId,
        source: 'playwright-e2e',
        type: 'error'
    };
}

export function createSessionEvent({ identity, name, sessionId }: SessionEventOptions): Record<string, unknown> {
    return {
        data: {
            '@user': {
                identity,
                name
            }
        },
        message: `Session for ${name}`,
        reference_id: sessionId,
        source: 'playwright-e2e',
        type: 'session',
        value: 0
    };
}

function createSyntheticRequest(appUrl: string, referenceId: string): Record<string, unknown> {
    const requestUrl = new URL('/e2e/onboarding', `${appUrl}/`);

    return {
        headers: {
            'User-Agent': [SYNTHETIC_USER_AGENT]
        },
        host: requestUrl.hostname,
        http_method: 'GET',
        is_secure: requestUrl.protocol === 'https:',
        path: requestUrl.pathname,
        port: getPort(requestUrl),
        query_string: {
            reference: referenceId
        },
        user_agent: SYNTHETIC_USER_AGENT
    };
}

function getPort(url: URL): number {
    if (url.port) {
        return Number(url.port);
    }

    return url.protocol === 'https:' ? 443 : 80;
}
