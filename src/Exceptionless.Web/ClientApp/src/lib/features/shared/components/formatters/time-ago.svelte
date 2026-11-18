<script lang="ts">
    import Time from 'svelte-time';

    interface Props {
        value: Date | string | undefined;
    }

    let { value }: Props = $props();

    const title = $derived(
        value
            ? new Date(value).toLocaleString(undefined, {
                  dateStyle: 'medium',
                  timeStyle: 'long'
              })
            : undefined
    );
</script>

<Time {title} live={true} relative={true} timestamp={value}>
    {#snippet children(relativeTime)}
        <!-- Keep these on one line: a line break here renders as a space before any following punctuation. -->
        {relativeTime}{#if title}<span class="sr-only"> ({title})</span>{/if}
    {/snippet}
</Time>
