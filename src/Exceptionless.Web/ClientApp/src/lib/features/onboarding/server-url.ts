export interface ClientSetupServerUrlOptions {
    /** PUBLIC_BASE_URL: the API origin when it differs from the UI origin. */
    baseUrl?: null | string;
    /** PUBLIC_EXCEPTIONLESS_SERVER_URL. */
    configuredServerUrl?: null | string;
    /** The UI origin, used when no API origin is configured. Self-hosted installs serve the API there. */
    origin: string;
    /** PUBLIC_EXCEPTIONLESS_CLIENT_SETUP_SHOW_SERVER_URL. Only the exact value `false` hides the server URL. */
    showServerUrl?: null | string;
}

export interface ClientSetupServerUrls {
    /** Shown in SDK snippets; undefined when the clients' default (hosted) server applies. */
    sdkServerUrl?: string;
    /** Used by raw HTTP examples, which always need a URL. */
    submissionServerUrl: string;
}

/**
 * Resolves the server URL for client setup instructions.
 *
 * Self-hosted installs often leave PUBLIC_EXCEPTIONLESS_SERVER_URL empty because it also
 * controls the UI's own telemetry. Falling back to the API origin keeps their snippets
 * pointed at their server instead of a placeholder or the hosted collector.
 */
export function resolveClientSetupServerUrls({ baseUrl, configuredServerUrl, origin, showServerUrl }: ClientSetupServerUrlOptions): ClientSetupServerUrls {
    const configured = normalizeAbsoluteUrl(configuredServerUrl);
    const submissionServerUrl = configured ?? normalizeAbsoluteUrl(baseUrl) ?? normalizeAbsoluteUrl(origin) ?? origin;
    const shouldShow = (showServerUrl ?? '').trim().toLowerCase() !== 'false';

    return {
        sdkServerUrl: shouldShow ? submissionServerUrl : undefined,
        submissionServerUrl
    };
}

function normalizeAbsoluteUrl(value: null | string | undefined): string | undefined {
    const trimmed = value?.trim();
    if (!trimmed || !/^https?:\/\//i.test(trimmed)) {
        return undefined;
    }

    try {
        const url = new URL(trimmed);
        return `${url.origin}${url.pathname}`.replace(/\/+$/, '');
    } catch {
        return undefined;
    }
}
