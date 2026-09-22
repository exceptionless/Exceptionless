<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    import DateTime from '$comp/formatters/date-time.svelte';
    import * as Tooltip from '$comp/ui/tooltip';
    import Time from 'svelte-time';

    interface Props {
        child?: Snippet<[{ children: Snippet; props: HTMLAttributes<HTMLElement> }]>;
        value: Date | string | undefined;
    }

    let { child: renderChild, value }: Props = $props();

    const date = $derived.by(() => {
        if (!value) {
            return undefined;
        }

        const parsedDate = value instanceof Date ? value : new Date(value);
        return isNaN(parsedDate.getTime()) ? undefined : parsedDate;
    });
</script>

{#snippet relativeTime()}
    <Time live={true} relative={true} timestamp={date} title={undefined} />
{/snippet}

{#if date}
    <Tooltip.Root>
        <Tooltip.Trigger>
            {#snippet child({ props })}
                {#if renderChild}
                    {@render renderChild({
                        children: relativeTime,
                        props
                    })}
                {:else}
                    <Time
                        {...props}
                        class="focus-visible:ring-ring inline cursor-help rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
                        live={true}
                        relative={true}
                        timestamp={date}
                        title={undefined}
                    />
                {/if}
            {/snippet}
        </Tooltip.Trigger>
        <Tooltip.Content role="tooltip" sideOffset={6} collisionPadding={8}
            ><DateTime
                value={date}
                formatOptions={{
                    timeZoneName: 'short'
                }}
            /></Tooltip.Content
        >
    </Tooltip.Root>
{/if}
