<script lang="ts">
    import Duration from '$comp/formatters/duration.svelte';
    import Number from '$comp/formatters/number.svelte';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Tooltip from '$comp/ui/tooltip';
    import AreaChart from '@lucide/svelte/icons/area-chart';
    import Clock from '@lucide/svelte/icons/clock';
    import Info from '@lucide/svelte/icons/info';
    import LineChart from '@lucide/svelte/icons/trending-up';
    import Users from '@lucide/svelte/icons/users';
    import prettyMilliseconds from 'pretty-ms';

    interface Props {
        avgDuration?: number;
        avgPerHour?: number;
        isLoading?: boolean;
        totalSessions?: number;
        totalUsers?: number;
    }

    let { avgDuration = 0, avgPerHour = 0, isLoading = false, totalSessions = 0, totalUsers = 0 }: Props = $props();

    const metricCardClass =
        "relative h-[66px]! justify-between gap-1! overflow-hidden bg-card py-2! ring-[color-mix(in_oklab,var(--chart-1)_42%,transparent)] before:absolute before:inset-x-0 before:top-0 before:h-1 before:bg-[linear-gradient(90deg,var(--chart-1),var(--chart-2))] before:content-['']";
    const metricHeaderClass = 'flex flex-row items-center justify-between gap-1.5 px-3 pb-0';
    const metricTitleClass = 'min-w-0 truncate text-xs font-semibold text-[color-mix(in_oklab,var(--chart-2)_82%,var(--foreground))]';
    const metricIconClass = 'size-3.5 shrink-0 text-[var(--chart-2)]';
    const metricValueClass = 'truncate text-lg leading-none font-bold text-[var(--chart-2)] tabular-nums sm:text-xl';

    const compactAverageDuration = $derived(avgDuration > 0 ? prettyMilliseconds(avgDuration * 1000, { compact: true, secondsDecimalDigits: 0 }) : '—');
    const preciseAverageDuration = $derived(avgDuration > 0 ? prettyMilliseconds(avgDuration * 1000, { secondsDecimalDigits: 0, unitCount: 2 }) : '—');
</script>

<div class="grid grid-cols-2 gap-2 sm:grid-cols-4">
    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <AreaChart aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass}>Total</Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                    aria-label="About sessions"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>Total sessions matching the current filters.</Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-6 w-16" />
            {:else}
                <div class={metricValueClass}>
                    <Number value={totalSessions} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <LineChart aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass}>Avg/hr</Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                    aria-label="About average sessions per hour"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>Average sessions per hour across the selected time range.</Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-6 w-16" />
            {:else}
                <div class={metricValueClass}>
                    <Number value={avgPerHour} formatOptions={{ maximumFractionDigits: 1 }} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <Users aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass}>Users</Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                    aria-label="About users"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>Unique users seen in matching sessions.</Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-6 w-16" />
            {:else}
                <div class={metricValueClass}>
                    <Number value={totalUsers} />
                </div>
            {/if}
        </Card.Content>
    </Card.Root>

    <Card.Root size="sm" class={metricCardClass}>
        <Card.Header class={metricHeaderClass}>
            <div class="flex min-w-0 items-center gap-1.5">
                <Clock aria-hidden="true" class={metricIconClass} />
                <Card.Title class={metricTitleClass} aria-label="Duration">
                    <span class="duration-label-short">Dur.</span>
                    <span class="duration-label-full">Duration</span>
                </Card.Title>
            </div>
            <Tooltip.Root>
                <Tooltip.Trigger
                    class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                    aria-label="About average duration"
                >
                    <Info aria-hidden="true" class="size-3.5" />
                </Tooltip.Trigger>
                <Tooltip.Content sideOffset={6}>
                    Average session duration.
                    {#if avgDuration > 0}
                        Full value: <Duration value={avgDuration * 1000} />.
                    {/if}
                </Tooltip.Content>
            </Tooltip.Root>
        </Card.Header>
        <Card.Content class="px-3">
            {#if isLoading}
                <Skeleton class="h-6 w-16" />
            {:else}
                <div class={[metricValueClass, 'duration-value-container']} aria-label={preciseAverageDuration}>
                    <span class="duration-value-short">{compactAverageDuration}</span>
                    <span class="duration-value-full">{preciseAverageDuration}</span>
                </div>
            {/if}
        </Card.Content>
    </Card.Root>
</div>

<style>
    .duration-label-full {
        display: none;
    }

    .duration-label-short {
        display: inline;
    }

    .duration-value-container {
        container: duration-value / inline-size;
    }

    .duration-value-full {
        display: none;
    }

    .duration-value-short {
        display: inline;
    }

    @container card-header (min-width: 9rem) {
        .duration-label-full {
            display: inline;
        }

        .duration-label-short {
            display: none;
        }
    }

    @container duration-value (min-width: 6rem) {
        .duration-value-full {
            display: inline;
        }

        .duration-value-short {
            display: none;
        }
    }
</style>
