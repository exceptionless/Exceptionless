<script lang="ts">
    import type { PersistentEvent } from '$features/events/models';

    import Duration from '$comp/formatters/duration.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import Live from '$comp/live.svelte';
    import { getSessionStartDuration } from '$features/events/utils';

    interface Props {
        event: PersistentEvent;
    }

    let { event }: Props = $props();

    const isActive = $derived(!event.data?.sessionend);
    const durationValue = $derived(getSessionStartDuration(event));
</script>

<Live live={isActive} liveTitle="Online" notLiveTitle="Ended" /><Duration value={durationValue} />{#if !isActive}
    <span class="text-muted-foreground"> (ended <TimeAgo value={event.data!.sessionend} />)</span>
{/if}
