export function mediaQuery(query: string) {
    const isSupported = globalThis && 'matchMedia' in globalThis && typeof globalThis.matchMedia === 'function';

    let mediaQuery: MediaQueryList | undefined;
    let state = $state(false);

    $effect.root(() => {
        function removeEventHandler() {
            mediaQuery?.removeEventListener('change', updateState);
        }

        function updateState() {
            if (!isSupported) {
                return;
            }

            removeEventHandler();

            mediaQuery = globalThis.matchMedia(query);
            state = mediaQuery.matches;

            mediaQuery.addEventListener('change', updateState);
        }

        updateState();

        return removeEventHandler;
    });

    return state;
}
