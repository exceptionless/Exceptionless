export function isOAuthPopupCallback(url: URL, hasOpener: boolean): boolean {
    if (!hasOpener || url.pathname !== '/') {
        return false;
    }

    const hash = new URLSearchParams(url.hash.slice(1));
    return url.searchParams.has('code') || url.searchParams.has('error') || hash.has('code') || hash.has('error') || hash.has('access_token');
}
