export function buildUrl(isSecure?: boolean, host?: string, port?: number, path?: string, queryString?: Record<string, string>): null | string {
    if (!host) {
        return null;
    }

    const url = new URL((isSecure ? 'https://' : 'http://') + host);
    if (port && port !== 80 && port !== 443) {
        url.port = port.toString();
    }

    if (path) {
        url.pathname = path.startsWith('/') ? path : `/${path}`;
    }

    if (queryString) {
        url.search = new URLSearchParams(queryString).toString();
    }

    return url.toString();
}

/**
 * Validates and returns a safe redirect URL.
 * Only allows relative paths that start with '/' to prevent open redirect attacks.
 * Returns the fallback URL if the redirect is invalid or potentially malicious.
 *
 * @param redirect - The redirect URL from query parameters or other sources
 * @param fallback - The fallback URL to use if redirect is invalid (must be a relative path, or null)
 * @returns A safe relative path for redirection, or null if fallback is null and redirect is invalid
 */
export function getSafeRedirectUrl(redirect: null | string | undefined, fallback: string): string;
export function getSafeRedirectUrl(redirect: null | string | undefined, fallback: null): null | string;
export function getSafeRedirectUrl(redirect: null | string | undefined, fallback: null | string): null | string {
    // If no redirect provided, use fallback
    if (!redirect) {
        return fallback;
    }

    // Trim whitespace
    const trimmed = redirect.trim();

    // Must start with a single forward slash (relative path)
    // Reject: empty, protocol-relative (//), absolute URLs, or paths not starting with /
    if (!trimmed.startsWith('/') || trimmed.startsWith('//')) {
        return fallback;
    }

    // Check for any protocol patterns that could be exploited
    // This catches javascript:, data:, vbscript:, etc.
    if (/^[a-z][a-z0-9+.-]*:/i.test(trimmed)) {
        return fallback;
    }

    // Additional check: try parsing as URL to catch edge cases
    // If it parses as an absolute URL with a different origin, reject it
    try {
        const parsed = new URL(trimmed, 'http://localhost');
        // If the pathname changed or host was added, it might be malicious
        if (parsed.host !== 'localhost') {
            return fallback;
        }
    } catch {
        // If URL parsing fails, the path is invalid
        return fallback;
    }

    return trimmed;
}
