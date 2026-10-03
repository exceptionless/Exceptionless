/** Primary public bookmarks from the old UI. Unknown routes remain unknown. */
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

    const organization = /^\/organization\/([^/]+)\/(manage|frequent)$/.exec(path);
    if (organization) {
        const [, id, action] = organization;
        if (action === 'manage' && tab === 'billing') {
            url.pathname = `/organization/${id}/billing`;
            url.searchParams.delete('tab');
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

    // Action routes show a confirmation before changing stack state.
    url.pathname = url.pathname.replace(/^(\/stack\/[^/]+)\/stop-notifications\/?$/, '$1/ignored');
    return url;
}

/** Social sign-in popups return to the origin. Leave the response available to the opener. */
export function isSignInCallback(url: URL): boolean {
    if (url.pathname !== '/') return false;
    const hash = new URLSearchParams(url.hash.slice(1));
    return ['code', 'error', 'access_token'].some((key) => url.searchParams.has(key) || hash.has(key));
}
