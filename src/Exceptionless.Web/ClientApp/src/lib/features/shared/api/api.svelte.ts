import { accessToken } from '$features/auth/index.svelte';
import { type FetchClient, type FetchClientProvider, getCurrentProvider, ProblemDetails } from '@exceptionless/fetchclient';
import { SvelteDate } from 'svelte/reactivity';

export { DEFAULT_LIMIT } from './constants';

/**
 * Represents the default timezone offset based on the user's local time.
 * If the user's timezone offset is not 0 (UTC), returns the offset in minutes with a 'm' suffix.
 * If the user's timezone offset is 0, returns undefined.
 */
export const DEFAULT_OFFSET = new SvelteDate().getTimezoneOffset() !== 0 ? new SvelteDate().getTimezoneOffset() * -1 + 'm' : undefined;

export class FetchClientStatus {
    isLoading = $state(false);

    constructor(target?: FetchClient | FetchClientProvider) {
        if (!target) {
            target = getCurrentProvider();
        }

        target.loading.on((loading) => {
            this.isLoading = loading!;
        });
    }
}

export async function fetchApiJson<T>(path: string, init: RequestInit): Promise<T> {
    const headers = new Headers(init.headers);
    if (accessToken.current) {
        headers.set('Authorization', `Bearer ${accessToken.current}`);
    }

    const response = await fetch(`/api/v2/${path.replace(/^\/+/, '')}`, {
        ...init,
        headers
    });

    if (!response.ok) {
        throw await toProblemDetails(response);
    }

    return (await response.json()) as T;
}

export function useFetchClientStatus(target?: FetchClient | FetchClientProvider) {
    return new FetchClientStatus(target);
}

async function toProblemDetails(response: Response): Promise<ProblemDetails> {
    const contentType = response.headers.get('Content-Type') ?? '';
    if (contentType.startsWith('application/problem+json') || contentType.startsWith('application/json')) {
        try {
            const problem = Object.assign(new ProblemDetails(), (await response.json()) as Partial<ProblemDetails>);
            problem.status ??= response.status;
            return problem;
        } catch {
            // Fall through to a generic error for non-problem JSON payloads.
        }
    }

    return new ProblemDetails().setErrorMessage(`Unexpected status code: ${response.status}`);
}
