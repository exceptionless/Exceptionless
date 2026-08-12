<script lang="ts" module>
    import type { useIntercom as useIntercomHook } from 'svelte-intercom';

    export type IntercomContext = ReturnType<typeof useIntercomHook>;
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { BootOptions } from 'svelte-intercom';

    import { accessToken } from '$features/auth/index.svelte';
    import { untrack } from 'svelte';
    import { setContext } from 'svelte';
    import { useIntercom } from 'svelte-intercom';

    import { INTERCOM_CONTEXT_KEY } from './keys';

    interface Props {
        bootOptions?: BootOptions;
        children: Snippet;
        routeKey?: string;
    }

    let { bootOptions = undefined, children, routeKey = undefined }: Props = $props();

    const intercom = useIntercom();
    let hasBooted = false;

    setContext<IntercomContext>(INTERCOM_CONTEXT_KEY, intercom);

    // The provider boots with the initial options. Only update after boot when the route or
    // identity/company data changes; eager or periodic updates create duplicate impressions.
    $effect(() => {
        void routeKey;
        const options = bootOptions;
        if (!options) {
            hasBooted = false;
            return;
        }

        if (!hasBooted) {
            hasBooted = true;
            return;
        }

        if (typeof window.Intercom === 'function') {
            untrack(() => intercom.update(options));
        }
    });

    // Shutdown when the user logs out.
    $effect(() => {
        if (!accessToken.current) {
            untrack(() => intercom.shutdown());
        }
    });
</script>

{@render children()}
