<script lang="ts">
    import Number from '$comp/formatters/number.svelte';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Tooltip from '$comp/ui/tooltip';
    import CalendarDays from '@lucide/svelte/icons/calendar-days';
    import Info from '@lucide/svelte/icons/info';
    import Layers from '@lucide/svelte/icons/layers';
    import Sparkles from '@lucide/svelte/icons/sparkles';
    import TrendingUp from '@lucide/svelte/icons/trending-up';

    interface Props {
        eventsPerHour?: number;
        isLoading?: boolean;
        newStacks?: number;
        totalEvents?: number;
        totalStacks?: number;
    }

    let { eventsPerHour = 0, isLoading = false, newStacks = 0, totalEvents = 0, totalStacks = 0 }: Props = $props();

    const metricCardClass =
        "relative h-[66px]! justify-between gap-1! overflow-hidden bg-card py-2! ring-[color-mix(in_oklab,var(--chart-1)_42%,transparent)] before:absolute before:inset-x-0 before:top-0 before:h-1 before:bg-[linear-gradient(90deg,var(--chart-1),var(--chart-2))] before:content-['']";
    const metricHeaderClass = 'flex flex-row items-center justify-between gap-1.5 px-3 pb-0';
    const metricTitleClass = 'min-w-0 truncate text-xs font-semibold text-[color-mix(in_oklab,var(--chart-2)_82%,var(--foreground))]';
    const metricIconClass = 'size-3.5 shrink-0 text-[var(--chart-2)]';
    const metricValueClass = 'truncate text-lg leading-none font-bold text-[var(--chart-2)] tabular-nums sm:text-xl';
</script>

<div class="grid grid-cols-2 gap-2 sm:grid-cols-4">
    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <CalendarDays aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass}>Events</Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="rounded-sm text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
                    aria-label="About events"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>Total event occurrences matching the current filters.</Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-5 w-16" />
            {:else}
                <div class={metricValueClass}>
                    <Number value={totalEvents} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <Layers aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass}>Stacks</Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="rounded-sm text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
                    aria-label="About stacks"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>Unique stacks matching the current filters.</Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-5 w-16" />
            {:else}
                <div class={metricValueClass}>
                    <Number value={totalStacks} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <Sparkles aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass}>New Stacks</Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="rounded-sm text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
                    aria-label="About new stacks"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>Stacks with their first occurrence in the selected time range.</Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-5 w-16" />
            {:else}
                <div class={metricValueClass}>
                    <Number value={newStacks} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <TrendingUp aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass}>Events/hr</Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="rounded-sm text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
                    aria-label="About events per hour"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>Average event occurrences per hour across the selected time range.</Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-5 w-16" />
            {:else}
                <div class={metricValueClass}>
                    <Number value={eventsPerHour} formatOptions={{ maximumFractionDigits: 1 }} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>
</div>
