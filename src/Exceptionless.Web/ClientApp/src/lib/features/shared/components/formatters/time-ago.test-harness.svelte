<script lang="ts">
    import * as Tooltip from '$comp/ui/tooltip';

    import TimeAgo from './time-ago.svelte';

    interface Props {
        linked?: boolean;
        value: Date | string | undefined;
    }

    let { linked = false, value }: Props = $props();
</script>

<Tooltip.Provider>
    {#if linked}
        <TimeAgo {value} href="/next/event/test" aria-label="Open event test" />
    {:else}
        <TimeAgo {value} />
    {/if}
</Tooltip.Provider>
