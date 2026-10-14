import { PUBLIC_BASE_URL } from '$features/shared/environment';

export function getApiUrl(path = ''): string {
    return getServerUrl(`api/v2${path ? `/${path.replace(/^\/+/, '')}` : ''}`);
}

/** EX_ApiUrl may point to a separate API origin; an unset value uses the UI origin. */
export function getServerUrl(path: string): string {
    const base = (PUBLIC_BASE_URL ?? '').trim().replace(/\/+$/, '');
    return `${base}/${path.replace(/^\/+/, '')}`;
}
