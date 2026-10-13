export function getHttpsRedirectUrl(url: URL, enableSsl: string | undefined): undefined | URL {
    if (enableSsl !== 'true' || url.protocol !== 'http:') {
        return;
    }

    const destination = new URL(url);
    destination.protocol = 'https:';
    return destination;
}
