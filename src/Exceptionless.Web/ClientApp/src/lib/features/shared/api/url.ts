import { env } from '$env/dynamic/public';

export function getApiUrl(path: string): string {
    const baseUrl = (env.PUBLIC_BASE_URL ?? '').trim().replace(/\/+$/, '');
    return `${baseUrl}/${path.replace(/^\/+/, '')}`;
}
