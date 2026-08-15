export function normalizeAssistantUrl(url: string, key: string): string {
    if (key !== 'href') {
        return url;
    }

    try {
        const parsedUrl = new URL(url);
        if (parsedUrl.pathname === '/next' || parsedUrl.pathname.startsWith('/next/')) {
            return `${parsedUrl.pathname}${parsedUrl.search}${parsedUrl.hash}`;
        }
    } catch {
        // Relative URLs are already same-origin and should remain unchanged.
    }

    return url;
}
