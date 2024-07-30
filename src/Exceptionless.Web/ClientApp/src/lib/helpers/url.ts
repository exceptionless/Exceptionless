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
