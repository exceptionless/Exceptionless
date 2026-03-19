<script lang="ts">
    import Duration from '$comp/formatters/duration.svelte';
    import { Muted } from '$comp/typography';

    interface Props {
        completedUtc: null | string | undefined;
        startedUtc: null | string | undefined;
    }

    let { completedUtc, startedUtc }: Props = $props();

    const durationMs = $derived(startedUtc && completedUtc ? new Date(completedUtc).getTime() - new Date(startedUtc).getTime() : null);
</script>

{#if durationMs !== null}
    <Duration value={durationMs} />
{:else}
    <Muted>—</Muted>
{/if}
