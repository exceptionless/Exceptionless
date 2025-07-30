import { FetchClient, FetchClientProvider, getCurrentProvider } from '@exceptionless/fetchclient';
import { SvelteDate } from 'svelte/reactivity';

export const DEFAULT_LIMIT = 20;

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

export function useFetchClientStatus(target?: FetchClient | FetchClientProvider) {
    return new FetchClientStatus(target);
}
