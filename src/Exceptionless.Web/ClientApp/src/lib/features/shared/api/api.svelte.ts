import { FetchClient, FetchClientProvider, getCurrentProvider } from '@exceptionless/fetchclient';

export const DEFAULT_LIMIT = 10;

/**
 * Represents the default timezone offset based on the user's local time.
 * If the user's timezone offset is not 0 (UTC), returns the offset in minutes with a 'm' suffix.
 * If the user's timezone offset is 0, returns undefined.
 */
export const DEFAULT_OFFSET = new Date().getTimezoneOffset() !== 0 ? new Date().getTimezoneOffset() * -1 + 'm' : undefined;

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
