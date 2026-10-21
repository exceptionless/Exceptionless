export interface OAuthErrorMessage {
    description?: string;
    title: string;
}

/** Turns an OAuth popup or token exchange failure into something a person can act on. */
export function getOAuthErrorMessage(error: unknown, providerLabel: string, problem?: { detail?: null | string; title?: null | string }): OAuthErrorMessage {
    const message = error instanceof Error ? error.message : '';

    if (message === 'Failed to open popup window') {
        return {
            description: 'Allow pop-ups for this site, then try again.',
            title: `Your browser blocked the ${providerLabel} sign-in window`
        };
    }

    if (message === 'The popup window was closed') {
        return {
            description: 'The sign-in window closed before it finished. Try again when you are ready.',
            title: `${providerLabel} sign-in was cancelled`
        };
    }

    if (message === 'access_denied') {
        return {
            description: `${providerLabel} did not share your account. Try again and approve access.`,
            title: `${providerLabel} sign-in was cancelled`
        };
    }

    return {
        description: problem?.detail || problem?.title || 'Please try again, or use your email and password.',
        title: `We couldn't sign you in with ${providerLabel}`
    };
}
