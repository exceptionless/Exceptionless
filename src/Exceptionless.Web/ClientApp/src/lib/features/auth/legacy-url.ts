// Translate saved bookmarks and already-sent notifications at the client boundary.
export function getCanonicalAppUrl(url: URL): URL {
    const result = new URL(url);
    if (result.hash.startsWith('#!/') || result.hash.startsWith('#/')) {
        const route = result.hash.replace(/^#!?/, '');
        if (!route.startsWith('//') && !route.includes('\\')) {
            const bookmarkedUrl = new URL(route, result.origin);
            result.pathname = bookmarkedUrl.pathname;
            bookmarkedUrl.searchParams.forEach((value, key) => result.searchParams.set(key, value));
            result.hash = bookmarkedUrl.hash;
        }
    }

    result.pathname = result.pathname.replace(/^\/next(?:\/|$)/i, '/').replace(/^\/+/, '/');
    const path = result.pathname;
    const stackAction = path.match(/^\/stack\/([^/]+)\/(mark-fixed|ignored|discarded)\/?$/);
    const projectReport = path.match(/^\/project\/([^/]+)\/(?:(error|log|usage|session)\/)?(dashboard|frequent|new|timeline)\/?$/);
    const list = path.match(/^\/(organization)\/([^/]+)\/(dashboard|frequent|new|timeline)\/?$/);
    if (stackAction) {
        result.pathname = `/stack/${stackAction[1]}`;
        result.searchParams.set('action', stackAction[2] === 'mark-fixed' ? 'fixed' : stackAction[2]!);
    } else if (projectReport) {
        // Resolve the authorized project owner before list filters initialize in another organization.
        result.pathname = `/project/${projectReport[1]}/dashboard`;
        if (projectReport[2] || projectReport[3] !== 'dashboard') {
            result.searchParams.set('type', projectReport[2] ?? 'error');
        }
        if (projectReport[3] !== 'dashboard' || !result.searchParams.has('view')) {
            result.searchParams.set('view', projectReport[3] === 'new' || projectReport[3] === 'frequent' ? 'stacks' : 'events');
        }
        if (projectReport[3] === 'new') {
            result.searchParams.set('mode', 'stack_new');
        }
    } else if (list) {
        result.pathname = list[3] === 'dashboard' || list[3] === 'timeline' ? '/event' : '/stack';
        result.searchParams.set(list[1]!, list[2]!);
        if (list[3] !== 'dashboard') {
            result.searchParams.set('type', 'error');
        }
        if (list[3] === 'new') {
            result.searchParams.set('mode', 'stack_new');
        }
    } else if (path === '/account/manage' && result.searchParams.get('tab') === 'notifications') {
        result.pathname = '/account/notifications';
        const projectId = result.searchParams.get('projectId');
        if (projectId) {
            result.searchParams.set('project', projectId);
            result.searchParams.delete('projectId');
        }
        result.searchParams.delete('tab');
    } else if (/^\/organization\/[^/]+\/upgrade\/?$/.test(path)) {
        result.pathname = path.replace(/\/upgrade\/?$/, '/billing');
        result.searchParams.set('changePlan', 'true');
    } else if (/^\/organization\/[^/]+\/manage\/?$/.test(path) && result.searchParams.get('tab') === 'billing') {
        result.pathname = path.replace(/\/manage\/?$/, '/billing');
        result.searchParams.delete('tab');
    } else if (/^\/project\/[^/]+\/manage\/?$/.test(path) && result.searchParams.get('tab') === 'integrations') {
        result.pathname = path.replace(/\/manage\/?$/, '/integrations');
        result.searchParams.delete('tab');
    }

    return result;
}
