/** Primary public bookmarks from Angular and the /next preview. Unknown routes remain unknown. */
export function canonicalAppUrl(source: URL): URL {
    const url = new URL(source);
    if (url.hash.startsWith('#!/') || url.hash.startsWith('#/')) {
        const route = url.hash.replace(/^#!?/, '');
        // Never interpret a protocol-relative hash as another origin.
        if (!route.startsWith('//')) {
            const legacy = new URL(route, url.origin);
            legacy.searchParams.forEach((value, key) => url.searchParams.set(key, value));
            url.pathname = legacy.pathname;
            url.hash = legacy.hash;
        }
    }

    url.pathname = url.pathname.replace(/^\/next(?=\/|$)/, '') || '/';
    const path = url.pathname.replace(/\/$/, '') || '/';
    const tab = url.searchParams.get('tab');

    if (path === '/account/manage' && tab === 'notifications') {
        url.pathname = '/account/notifications';
        url.searchParams.delete('tab');
        const projectId = url.searchParams.get('projectId');
        if (projectId) {
            url.searchParams.set('project', projectId);
            url.searchParams.delete('projectId');
        }
    }

    const organization = /^\/organization\/([^/]+)\/(upgrade|manage|frequent)$/.exec(path);
    if (organization) {
        const [, id, action] = organization;
        if (action === 'upgrade' || (action === 'manage' && tab === 'billing')) {
            url.pathname = `/organization/${id}/billing`;
            url.searchParams.delete('tab');
            if (action === 'upgrade') url.searchParams.set('changePlan', 'true');
        } else if (action === 'frequent') {
            url.pathname = `/organization/${id}/dashboard`;
            url.searchParams.set('view', 'stacks');
        }
    }

    const project = /^\/project\/([^/]+)\/(?:(error|log|usage|session)\/)?(timeline|frequent|new|dashboard)$/.exec(path);
    if (project) {
        const [, id, type, view] = project;
        url.pathname = `/project/${id}/dashboard`;
        if (type) url.searchParams.set('type', type);
        if (view === 'frequent' || view === 'new') url.searchParams.set('view', 'stacks');
    }

    if (/^\/project\/[^/]+\/manage$/.test(path) && tab === 'integrations') {
        url.pathname = path.replace(/\/manage$/, '/integrations');
        url.searchParams.delete('tab');
    }

    // Historical action links only open details. Navigating must never change stack state.
    url.pathname = url.pathname.replace(/^(\/stack\/[^/]+)\/(mark-fixed|ignored|discarded)\/?$/, '$1');
    return url;
}

/** Social sign-in popups return to the origin. Leave the response available to the opener. */
export function isSignInCallback(url: URL): boolean {
    if (url.pathname !== '/') return false;
    const hash = new URLSearchParams(url.hash.slice(1));
    return ['code', 'error', 'access_token'].some((key) => url.searchParams.has(key) || hash.has(key));
}
