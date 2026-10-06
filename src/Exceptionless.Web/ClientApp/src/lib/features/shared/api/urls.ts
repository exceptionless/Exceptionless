import { env } from '$env/dynamic/public';

export function getApiUrl(path = ''): string {
    return getServerUrl(`api/v2${path ? `/${path.replace(/^\/+/, '')}` : ''}`);
}

/** EX_ApiUrl may point to a separate API origin; an unset value uses the UI origin. */
export function getServerUrl(path: string): string {
    const base = (env.PUBLIC_BASE_URL ?? '').trim().replace(/\/+$/, '');
    return `${base}/${path.replace(/^\/+/, '')}`;
}
