import { FetchClient, FetchClientProvider, getCurrentProvider } from '@exceptionless/fetchclient';

export const DEFAULT_LIMIT = 10;

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
