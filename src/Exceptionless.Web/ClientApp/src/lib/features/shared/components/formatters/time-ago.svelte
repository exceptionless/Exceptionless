<script lang="ts">
    import type { HTMLAnchorAttributes } from 'svelte/elements';

    import DateTime from '$comp/formatters/date-time.svelte';
    import * as Tooltip from '$comp/ui/tooltip';
    import Time from 'svelte-time';

    interface Props extends Pick<HTMLAnchorAttributes, 'aria-label' | 'class' | 'href'> {
        value: Date | string | undefined;
    }

    let { 'aria-label': ariaLabel, class: className, href, value }: Props = $props();

    const date = $derived.by(() => {
        if (!value) {
            return undefined;
        }

        const parsedDate = value instanceof Date ? value : new Date(value);
        return isNaN(parsedDate.getTime()) ? undefined : parsedDate;
    });
</script>

{#if date}
    <Tooltip.Root>
        <Tooltip.Trigger>
            {#snippet child({ props })}
                {#if href}
                    <a
                        {...props}
                        {href}
                        aria-label={ariaLabel}
                        class={['focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-offset-2', className]}
                    >
                        <Time live={true} relative={true} timestamp={date} title={undefined} />
                    </a>
                {:else}
                    <Time
                        {...props}
                        aria-label={ariaLabel}
                        class={[
                            'focus-visible:ring-ring inline cursor-help rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
                            className
                        ]}
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
