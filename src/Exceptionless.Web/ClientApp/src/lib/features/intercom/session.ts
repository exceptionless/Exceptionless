import { hide, shutdown } from '@intercom/messenger-js-sdk';

export function clearIntercomCookies() {
    if (typeof document === 'undefined') {
        return;
    }

    const cookieNames = new Set(
        document.cookie
            .split(';')
            .map((cookie) => cookie.trim().split('=', 1)[0] ?? '')
            .filter((name) => name.startsWith('intercom-'))
    );

    const hostname = typeof window === 'undefined' ? '' : window.location.hostname;
    const domainCandidates = getCookieDomainCandidates(hostname);

    for (const name of cookieNames) {
        expireCookie(name);
        for (const domain of domainCandidates) {
            expireCookie(name, domain);
        }
    }
}

export function shutdownIntercomSession() {
    if (typeof window !== 'undefined' && typeof window.Intercom === 'function') {
        hide();
        shutdown();
    }

    clearIntercomCookies();
}

function expireCookie(name: string, domain?: string) {
    const domainAttribute = domain ? `; Domain=${domain}` : '';
    document.cookie = `${name}=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Max-Age=0; Path=/${domainAttribute}; SameSite=Lax`;
}

function getCookieDomainCandidates(hostname: string) {
    if (!hostname.includes('.') || /^\d{1,3}(\.\d{1,3}){3}$/.test(hostname)) {
        return [];
    }

    const parts = hostname.split('.');
    return parts.slice(0, -1).map((_, index) => parts.slice(index).join('.'));
}
