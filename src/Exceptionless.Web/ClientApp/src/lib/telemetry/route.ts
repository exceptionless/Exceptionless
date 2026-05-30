const OBJECTID_SEGMENT_REGEX = /^[0-9a-f]{24}$/i;
const UUID_SEGMENT_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NUMERIC_SEGMENT_REGEX = /^\d+$/;

export function normalizePath(path: string, basePath = '/next'): string {
    let normalized = path;

    if (basePath && normalized.startsWith(basePath)) {
        normalized = normalized.slice(basePath.length);
    }

    if (!normalized.startsWith('/')) {
        normalized = '/' + normalized;
    }

    normalized = normalized
        .split('/')
        .map((segment) => (segment && isIdSegment(segment) ? ':id' : segment))
        .join('/');

    if (normalized.length > 1 && normalized.endsWith('/')) {
        normalized = normalized.slice(0, -1);
    }

    return normalized;
}

export function normalizeRouteId(routeId: null | string): string {
    if (!routeId) {
        return 'root';
    }

    return (
        routeId
            .replace(/\/\([^)]+\)/g, '')
            .replace(/\[\[([^=\]]+)(?:=[^\]]+)?\]\]/g, ':$1')
            .replace(/\[\.\.\.([^=\]]+)(?:=[^\]]+)?\]/g, ':$1')
            .replace(/\[([^=\]]+)(?:=[^\]]+)?\]/g, ':$1')
            .replace(/^\//, '') || 'root'
    );
}

function isIdSegment(segment: string): boolean {
    return OBJECTID_SEGMENT_REGEX.test(segment) || UUID_SEGMENT_REGEX.test(segment) || NUMERIC_SEGMENT_REGEX.test(segment);
}
