<script lang="ts" module>
    import { useIntercom } from 'svelte-intercom';

    export type IntercomContext = ReturnType<typeof useIntercom>;
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { BootOptions } from 'svelte-intercom';

    import { accessToken } from '$features/auth/index.svelte';
    import { DocumentVisibility } from '$shared/document-visibility.svelte';
    import { useInterval } from 'runed';
    import { untrack } from 'svelte';
    import { setContext } from 'svelte';

    import { INTERCOM_CONTEXT_KEY } from './keys';

    interface Props {
        bootOptions?: BootOptions;
        children: Snippet;
        routeKey?: string;
        updateIntervalMs?: number;
    }

    let { bootOptions = undefined, children, routeKey = undefined, updateIntervalMs = 90_000 }: Props = $props();

    const intercom = useIntercom();
    const visibility = new DocumentVisibility();

    setContext<IntercomContext>(INTERCOM_CONTEXT_KEY, intercom);

    const interval = useInterval(() => updateIntervalMs, {
        callback: () => {
            if (bootOptions && visibility.visible) {
                intercom.update(bootOptions);
            }
        },
        immediate: false
    });

    const shouldUpdate = $derived(bootOptions && visibility.visible);

    // Sync identity/company data and manage interval when boot options or visibility changes.
    $effect(() => {
        if (!bootOptions) {
            interval.pause();
            return;
        }

        if (visibility.visible) {
            interval.resume();
        } else {
            interval.pause();
        }
    });

    // Sync on route transitions and visibility changes.
    $effect(() => {
        void routeKey;
        if (shouldUpdate) {
            untrack(() => intercom.update(bootOptions!));
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
