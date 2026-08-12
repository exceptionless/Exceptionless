<script lang="ts" module>
    import type { useIntercom as useIntercomHook } from 'svelte-intercom';

    export type IntercomContext = ReturnType<typeof useIntercomHook>;
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { BootOptions } from 'svelte-intercom';

    import { untrack } from 'svelte';
    import { setContext } from 'svelte';
    import { useIntercom } from 'svelte-intercom';

    import { INTERCOM_CONTEXT_KEY } from './keys';
    import { buildIntercomDataUpdate, buildIntercomRouteUpdate } from './updates';

    interface Props {
        bootOptions?: BootOptions;
        children: Snippet;
        routeKey?: string;
        /** @deprecated Intercom updates are event-driven; this value is retained as a no-op for compatibility. */
        updateIntervalMs?: number;
    }

    let { bootOptions = undefined, children, routeKey = undefined, updateIntervalMs = undefined }: Props = $props();

    const intercom = useIntercom();
    let hasBooted = false;
    let previousBootOptions: BootOptions | undefined;
    let previousRouteKey: string | undefined;

    setContext<IntercomContext>(INTERCOM_CONTEXT_KEY, intercom);

    // Retain the deprecated prop reactively without restoring periodic updates.
    $effect(() => {
        void updateIntervalMs;
    });

    // The provider boots with the initial options. Only update after boot when the route or
    // identity/company data changes; eager or periodic updates create duplicate impressions.
    $effect(() => {
        const options = bootOptions;
        const currentRouteKey = routeKey;
        if (!options) {
            hasBooted = false;
            previousBootOptions = undefined;
            previousRouteKey = undefined;
            return;
        }

        if (!hasBooted) {
            hasBooted = true;
            previousBootOptions = options;
            previousRouteKey = currentRouteKey;
            return;
        }

        if (typeof window.Intercom !== 'function') {
            return;
        }

        const priorBootOptions = previousBootOptions!;
        const bootOptionsChanged = options !== priorBootOptions;
        const routeChanged = currentRouteKey !== previousRouteKey;
        previousBootOptions = options;
        previousRouteKey = currentRouteKey;

        if (bootOptionsChanged) {
            untrack(() => intercom.update(buildIntercomDataUpdate(priorBootOptions, options)));
        } else if (routeChanged) {
            untrack(() => intercom.update(buildIntercomRouteUpdate(options)));
        }
    });
</script>

{@render children()}
