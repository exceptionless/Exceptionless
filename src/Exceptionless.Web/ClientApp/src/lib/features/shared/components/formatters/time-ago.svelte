<script lang="ts">
    import * as Tooltip from '$comp/ui/tooltip';
    import { formatDateTime } from '$shared/dates';
    import Time from 'svelte-time';

    interface Props {
        value: Date | string | undefined;
    }

    let { value }: Props = $props();

    const date = $derived.by(() => {
        if (!value) {
            return undefined;
        }

        const parsedDate = value instanceof Date ? value : new Date(value);
        return isNaN(parsedDate.getTime()) ? undefined : parsedDate;
    });

    const fullTimestamp = $derived(date ? formatDateTime(date) : '');
</script>

{#if date}
    <Tooltip.Provider>
        <Tooltip.Root>
            <Tooltip.Trigger>
                {#snippet child({ props })}
                    <span
                        {...props}
                        class="focus-visible:ring-ring inline cursor-help rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
                    >
                        <Time live={true} relative={true} timestamp={date}></Time>
                    </span>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content role="tooltip">{fullTimestamp}</Tooltip.Content>
        </Tooltip.Root>
    </Tooltip.Provider>
{/if}
